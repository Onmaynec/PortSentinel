using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.Diagnostics.Tracing.Session;
using PortSentinel.Models;

namespace PortSentinel.Services;

internal sealed class EtwSessionGuardService
{
    public const string OwnedSessionPrefix = "PortSentinel-";

    private readonly string _reportsDirectory;

    public EtwSessionGuardService(string reportsDirectory)
    {
        _reportsDirectory = reportsDirectory;
        Directory.CreateDirectory(_reportsDirectory);
    }

    public EtwSessionInventory Inspect()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new EtwSessionInventory(
                DateTimeOffset.Now,
                false,
                Array.Empty<string>(),
                Array.Empty<string>(),
                "ETW session inventory доступен только на Windows.");
        }

        try
        {
            string[] active = TraceEventSession.GetActiveSessionNames()
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            string[] owned = active
                .Where(IsOwnedSession)
                .ToArray();

            return new EtwSessionInventory(
                DateTimeOffset.Now,
                true,
                active,
                owned,
                null);
        }
        catch (Exception ex)
        {
            return new EtwSessionInventory(
                DateTimeOffset.Now,
                false,
                Array.Empty<string>(),
                Array.Empty<string>(),
                ex.Message);
        }
    }

    public async Task<EtwOwnedSessionCleanupResult> CleanupOwnedAsync(
        IReadOnlyList<string> sessionNames,
        CancellationToken cancellationToken)
    {
        var result = new List<EtwOwnedSessionCleanupItem>();

        foreach (string sessionName in sessionNames
                     .Where(IsOwnedSession)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using TraceEventSession? session = TraceEventSession.GetActiveSession(sessionName);
                if (session is null)
                {
                    result.Add(new EtwOwnedSessionCleanupItem(
                        sessionName,
                        true,
                        "Сессия уже отсутствует."));
                    continue;
                }

                session.StopOnDispose = false;
                bool stopped = session.Stop(noThrow: true);
                result.Add(new EtwOwnedSessionCleanupItem(
                    sessionName,
                    stopped,
                    stopped
                        ? "Собственная ETW-сессия остановлена."
                        : "TraceEvent не подтвердил остановку сессии."));
            }
            catch (Exception ex)
            {
                (EtwSessionFailureKind kind, int? nativeCode) = Classify(ex);
                result.Add(new EtwOwnedSessionCleanupItem(
                    sessionName,
                    false,
                    $"{kind}: {FormatError(ex, nativeCode)}"));
            }

            await Task.Yield();
        }

        return new EtwOwnedSessionCleanupResult(DateTimeOffset.Now, result);
    }

    public async Task<string> ExportInventoryAsync(
        EtwSessionInventory inventory,
        string format,
        CancellationToken cancellationToken)
    {
        string extension = format.Equals("json", StringComparison.OrdinalIgnoreCase) ? "json" : "md";
        string path = Path.Combine(
            _reportsDirectory,
            $"etw-session-inventory-{inventory.CollectedAt:yyyyMMdd-HHmmss}.{extension}");

        if (extension == "json")
        {
            var payload = new
            {
                schemaVersion = 1,
                inventory,
                ownershipRule = $"Only session names beginning with '{OwnedSessionPrefix}' are eligible for cleanup.",
                foreignSessionPolicy = "Foreign ETW sessions are never stopped, restarted, or modified.",
                privacy = "Session inventory contains ETW session names only; packet payload is not captured."
            };
            await File.WriteAllTextAsync(
                path,
                JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);
            return path;
        }

        var text = new StringBuilder()
            .AppendLine("# PortSentinel ETW Session Inventory")
            .AppendLine()
            .AppendLine($"- **Collected:** {inventory.CollectedAt:O}")
            .AppendLine($"- **Inventory success:** {inventory.Success}")
            .AppendLine($"- **Active sessions:** {inventory.ActiveCount}")
            .AppendLine($"- **PortSentinel-owned sessions:** {inventory.PortSentinelCount}")
            .AppendLine($"- **Foreign sessions:** {inventory.ForeignCount}")
            .AppendLine($"- **Error:** {Escape(inventory.Error ?? "none")}")
            .AppendLine()
            .AppendLine("> PortSentinel never stops, restarts, or modifies foreign ETW sessions.")
            .AppendLine($"> Cleanup is limited to names beginning with `{OwnedSessionPrefix}`.")
            .AppendLine()
            .AppendLine("## Active sessions")
            .AppendLine();

        if (inventory.ActiveSessions.Count == 0)
            text.AppendLine("- none");
        else
        {
            foreach (string sessionName in inventory.ActiveSessions)
            {
                string owner = IsOwnedSession(sessionName) ? "PortSentinel" : "foreign";
                text.AppendLine($"- `{EscapeCode(sessionName)}` — {owner}");
            }
        }

        await File.WriteAllTextAsync(path, text.ToString(), cancellationToken);
        return path;
    }

    public async Task<string> ExportDiagnosticsAsync(
        EtwGuardedCaptureResult result,
        string format,
        CancellationToken cancellationToken)
    {
        string extension = format.Equals("json", StringComparison.OrdinalIgnoreCase) ? "json" : "md";
        string path = Path.Combine(
            _reportsDirectory,
            $"etw-session-guard-{result.Capture.StartedAt:yyyyMMdd-HHmmss}.{extension}");

        if (extension == "json")
        {
            var payload = new
            {
                schemaVersion = 1,
                result.Diagnostics,
                capture = new
                {
                    backend = result.Capture.Mode.ToString(),
                    result.Capture.StartedAt,
                    result.Capture.EndedAt,
                    result.Capture.Status,
                    result.Capture.Failure,
                    eventCount = result.Capture.Events.Count
                },
                foreignSessionPolicy = "Foreign ETW sessions are never stopped, restarted, or modified.",
                privacy = "No packet payload, HTTP body, credentials, tokens, or decrypted TLS content is captured."
            };
            await File.WriteAllTextAsync(
                path,
                JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);
            return path;
        }

        var inventory = result.Diagnostics.InventoryBefore;
        var text = new StringBuilder()
            .AppendLine("# PortSentinel ETW Session Guard")
            .AppendLine()
            .AppendLine($"- **Backend:** {result.Capture.Mode}")
            .AppendLine($"- **Started:** {result.Capture.StartedAt:O}")
            .AppendLine($"- **Ended:** {result.Capture.EndedAt:O}")
            .AppendLine($"- **Events:** {result.Capture.Events.Count}")
            .AppendLine($"- **Snapshot fallback:** {result.Diagnostics.UsedSnapshotFallback}")
            .AppendLine($"- **Active sessions before capture:** {inventory.ActiveCount}")
            .AppendLine($"- **Owned / foreign:** {inventory.PortSentinelCount} / {inventory.ForeignCount}")
            .AppendLine($"- **Summary:** {Escape(result.Diagnostics.Summary)}")
            .AppendLine()
            .AppendLine("## Attempts")
            .AppendLine()
            .AppendLine("| # | Session | Success | Failure kind | Native code | Message |")
            .AppendLine("|---:|---|---|---|---:|---|");

        foreach (EtwSessionAttempt attempt in result.Diagnostics.Attempts)
        {
            text.AppendLine(
                $"| {attempt.Number} | {Escape(attempt.SessionName)} | {attempt.Succeeded} | {attempt.FailureKind} | {attempt.NativeErrorCode?.ToString() ?? "—"} | {Escape(attempt.Message)} |");
        }

        text.AppendLine()
            .AppendLine("> Foreign ETW sessions are never stopped, restarted, or modified.")
            .AppendLine("> Packet payload, HTTP body, credentials, tokens, and decrypted TLS content are not captured.");

        await File.WriteAllTextAsync(path, text.ToString(), cancellationToken);
        return path;
    }

    public static bool IsOwnedSession(string? sessionName) =>
        !string.IsNullOrWhiteSpace(sessionName) &&
        sessionName.StartsWith(OwnedSessionPrefix, StringComparison.OrdinalIgnoreCase);

    public static (EtwSessionFailureKind Kind, int? NativeErrorCode) Classify(Exception exception)
    {
        Exception ex = Unwrap(exception);
        if (ex is UnauthorizedAccessException)
            return (EtwSessionFailureKind.AccessDenied, 5);

        int? nativeCode = ex is Win32Exception win32
            ? win32.NativeErrorCode
            : ExtractNativeCode(ex.HResult);

        return nativeCode switch
        {
            5 => (EtwSessionFailureKind.AccessDenied, nativeCode),
            8 or 1450 => (EtwSessionFailureKind.ResourceLimit, nativeCode),
            183 => (EtwSessionFailureKind.NameCollision, nativeCode),
            4201 => (EtwSessionFailureKind.SessionUnavailable, nativeCode),
            _ => (EtwSessionFailureKind.Unknown, nativeCode)
        };
    }

    public static string FormatError(Exception exception, int? nativeCode)
    {
        Exception ex = Unwrap(exception);
        string suffix = nativeCode.HasValue ? $" (native={nativeCode.Value})" : string.Empty;
        return $"{ex.Message}{suffix}";
    }

    private static Exception Unwrap(Exception exception)
    {
        Exception current = exception;
        while (current is AggregateException aggregate && aggregate.InnerExceptions.Count == 1)
            current = aggregate.InnerExceptions[0];
        return current;
    }

    private static int? ExtractNativeCode(int hresult)
    {
        int value = hresult & 0xFFFF;
        return value == 0 ? null : value;
    }

    private static string Escape(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal)
             .Replace("\r", " ", StringComparison.Ordinal)
             .Replace("\n", " ", StringComparison.Ordinal);

    private static string EscapeCode(string value) =>
        value.Replace("`", "'", StringComparison.Ordinal);
}
