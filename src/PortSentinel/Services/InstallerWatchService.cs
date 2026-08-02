using System.Text;
using System.Text.Json;
using PortSentinel.Models;

namespace PortSentinel.Services;

internal sealed class InstallerWatchService
{
    private readonly string _reportsDirectory;

    public InstallerWatchService(string reportsDirectory)
    {
        _reportsDirectory = reportsDirectory;
        Directory.CreateDirectory(_reportsDirectory);
    }

    public InstallerWatchReport Analyze(
        TelemetryCapture baseline,
        TelemetryCapture watch,
        string? processHint)
    {
        string hint = (processHint ?? string.Empty).Trim();
        HashSet<string> baselineFingerprints = baseline.Events
            .Select(Fingerprint)
            .ToHashSet(StringComparer.Ordinal);

        EtwNetworkEvent[] added = watch.Events
            .Where(item => !baselineFingerprints.Contains(Fingerprint(item)))
            .GroupBy(Fingerprint, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderByDescending(item => MatchesHint(item.ProcessName, hint))
            .ThenBy(item => item.Timestamp)
            .ToArray();

        InstallerWatchProcessSummary[] processes = added
            .GroupBy(
                item => string.IsNullOrWhiteSpace(item.ProcessName) ? "unknown" : item.ProcessName,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new InstallerWatchProcessSummary(
                group.Key,
                group.Count(),
                group.Select(RemoteKey)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),
                group.Count(item => IsTcp(item.Protocol)),
                group.Count(item => IsUdp(item.Protocol)),
                group.Count(item => item.Kind is "FAIL" or "RETRANSMIT" or "RECONNECT"),
                MatchesHint(group.Key, hint)))
            .OrderByDescending(item => item.MatchesHint)
            .ThenByDescending(item => item.FailureSignalCount)
            .ThenByDescending(item => item.AddedEventCount)
            .ThenBy(item => item.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var limitations = new List<string>
        {
            "Baseline and watch are separate bounded captures; activity between them is not recorded.",
            "New metadata can come from unrelated background applications, services or scheduled tasks.",
            "Process-name matching is a prioritization hint, not proof that the installer caused an event.",
            "Installers may delegate network activity to child processes, service hosts, package managers or browsers.",
            "The comparison ignores PID and outbound local ephemeral ports to reduce expected noise.",
            "Packet payload, HTTP body, credentials, tokens and decrypted TLS content are not collected."
        };

        if (baseline.Summary.Mode == EtwBackendMode.SnapshotFallback ||
            watch.Summary.Mode == EtwBackendMode.SnapshotFallback)
        {
            limitations.Add(
                "At least one capture used SnapshotFallback, so short-lived lifecycle events and send/receive ordering may be missing.");
        }

        return new InstallerWatchReport(
            baseline.Summary.Id,
            watch.Summary.Id,
            baseline.Summary.StartedAt,
            watch.Summary.EndedAt,
            baseline.Summary.Mode,
            watch.Summary.Mode,
            hint,
            baseline.Summary.EventCount,
            watch.Summary.EventCount,
            added,
            processes,
            limitations);
    }

    public async Task<string> ExportAsync(
        InstallerWatchReport report,
        string format,
        CancellationToken cancellationToken)
    {
        string extension = format.Equals("json", StringComparison.OrdinalIgnoreCase) ? "json" : "md";
        string path = Path.Combine(
            _reportsDirectory,
            $"installer-watch-{report.BaselineCaptureId}-to-{report.WatchCaptureId}.{extension}");

        if (extension == "json")
        {
            var payload = new
            {
                schemaVersion = 1,
                report.BaselineCaptureId,
                report.WatchCaptureId,
                report.StartedAt,
                report.EndedAt,
                baselineBackend = report.BaselineMode.ToString(),
                watchBackend = report.WatchMode.ToString(),
                report.ProcessHint,
                report.BaselineEventCount,
                report.WatchEventCount,
                report.AddedProcessCount,
                report.AddedEndpointCount,
                report.FailureSignalCount,
                processes = report.Processes,
                addedEvents = report.AddedEvents,
                limitations = report.Limitations,
                privacy = "No packet payload, HTTP body, cookies, credentials, tokens or decrypted TLS content is collected."
            };
            await File.WriteAllTextAsync(
                path,
                JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);
            return path;
        }

        var text = new StringBuilder()
            .AppendLine($"# PortSentinel Installer Watch #{report.BaselineCaptureId} → #{report.WatchCaptureId}")
            .AppendLine()
            .AppendLine($"- **Baseline backend:** {report.BaselineMode}")
            .AppendLine($"- **Watch backend:** {report.WatchMode}")
            .AppendLine($"- **Started:** {report.StartedAt:O}")
            .AppendLine($"- **Ended:** {report.EndedAt:O}")
            .AppendLine($"- **Process hint:** {Escape(string.IsNullOrWhiteSpace(report.ProcessHint) ? "not set" : report.ProcessHint)}")
            .AppendLine($"- **Baseline / watch events:** {report.BaselineEventCount} / {report.WatchEventCount}")
            .AppendLine($"- **Added processes:** {report.AddedProcessCount}")
            .AppendLine($"- **Added endpoints:** {report.AddedEndpointCount}")
            .AppendLine($"- **Failure signals:** {report.FailureSignalCount}")
            .AppendLine()
            .AppendLine("## Process candidates")
            .AppendLine()
            .AppendLine("| Process | Hint | Added events | Endpoints | TCP | UDP | Failure signals |")
            .AppendLine("|---|---|---:|---:|---:|---:|---:|");

        foreach (InstallerWatchProcessSummary process in report.Processes)
        {
            text.AppendLine(
                $"| {Escape(process.ProcessName)} | {(process.MatchesHint ? "yes" : "no")} | {process.AddedEventCount} | {process.UniqueRemoteEndpoints} | {process.TcpEventCount} | {process.UdpEventCount} | {process.FailureSignalCount} |");
        }

        text.AppendLine()
            .AppendLine("## Added network metadata")
            .AppendLine()
            .AppendLine("| Time | Kind | Process | Protocol | Local | Remote | Note |")
            .AppendLine("|---|---|---|---|---|---|---|");

        foreach (EtwNetworkEvent item in report.AddedEvents)
        {
            text.AppendLine(
                $"| {item.Timestamp:O} | {Escape(item.Kind)} | {Escape(item.ProcessName)} | {Escape(item.Protocol)} | {Escape(item.LocalEndpoint)} | {Escape(item.RemoteEndpoint)} | {Escape(item.Note)} |");
        }

        text.AppendLine()
            .AppendLine("## Limitations")
            .AppendLine();
        foreach (string limitation in report.Limitations)
            text.AppendLine($"- {Escape(limitation)}");

        text.AppendLine()
            .AppendLine("> Installer Watch is diagnostic metadata comparison, not software ownership or security attribution.")
            .AppendLine("> Packet payload and decrypted application content are outside the collection boundary.");

        await File.WriteAllTextAsync(path, text.ToString(), cancellationToken);
        return path;
    }

    private static string Fingerprint(EtwNetworkEvent item)
    {
        string kind = Normalize(item.Kind);
        string protocol = Normalize(item.Protocol);
        string process = Normalize(item.ProcessName);
        string remote = Normalize(item.RemoteAddress);

        if (kind is "LISTENER" or "ACCEPT")
        {
            return string.Join('|',
                kind,
                protocol,
                process,
                Normalize(item.LocalAddress),
                item.LocalPort,
                remote,
                item.RemotePort);
        }

        return string.Join('|', kind, protocol, process, remote, item.RemotePort);
    }

    private static bool MatchesHint(string? processName, string hint) =>
        !string.IsNullOrWhiteSpace(hint) &&
        (processName ?? string.Empty).Contains(hint, StringComparison.OrdinalIgnoreCase);

    private static bool IsTcp(string? protocol) =>
        (protocol ?? string.Empty).StartsWith("TCP", StringComparison.OrdinalIgnoreCase);

    private static bool IsUdp(string? protocol) =>
        (protocol ?? string.Empty).StartsWith("UDP", StringComparison.OrdinalIgnoreCase);

    private static string RemoteKey(EtwNetworkEvent item) =>
        string.IsNullOrWhiteSpace(item.RemoteAddress)
            ? string.Empty
            : item.RemotePort > 0
                ? $"{item.RemoteAddress}:{item.RemotePort}"
                : item.RemoteAddress;

    private static string Normalize(string? value) =>
        (value ?? string.Empty).Trim().Replace('/', '\\').ToUpperInvariant();

    private static string Escape(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal)
             .Replace("\r", " ", StringComparison.Ordinal)
             .Replace("\n", " ", StringComparison.Ordinal);
}
