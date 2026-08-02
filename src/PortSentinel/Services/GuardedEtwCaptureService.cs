using PortSentinel.Models;

namespace PortSentinel.Services;

internal sealed class GuardedEtwCaptureService
{
    private readonly EtwTelemetryService _etw;
    private readonly EtwSessionGuardService _guard;

    public GuardedEtwCaptureService(
        EtwTelemetryService etw,
        EtwSessionGuardService guard)
    {
        _etw = etw;
        _guard = guard;
    }

    public async Task<EtwGuardedCaptureResult> CaptureAsync(
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        EtwSessionInventory inventory = _guard.Inspect();
        var attempts = new List<EtwSessionAttempt>();

        EtwCaptureResult first = await RunAttemptAsync(
            1,
            duration,
            attempts,
            cancellationToken);

        EtwCaptureResult final = first;
        EtwSessionFailureKind firstFailure = attempts[0].FailureKind;
        if (first.Mode == EtwBackendMode.SnapshotFallback &&
            firstFailure == EtwSessionFailureKind.NameCollision)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(350), cancellationToken);
            final = await RunAttemptAsync(
                2,
                duration,
                attempts,
                cancellationToken);
        }

        bool fallback = final.Mode == EtwBackendMode.SnapshotFallback;
        string summary = fallback
            ? BuildFallbackSummary(inventory, attempts, final)
            : BuildSuccessSummary(inventory, attempts, final);

        return new EtwGuardedCaptureResult(
            final,
            new EtwSessionDiagnostics(
                inventory,
                attempts,
                fallback,
                summary));
    }

    private async Task<EtwCaptureResult> RunAttemptAsync(
        int number,
        TimeSpan duration,
        ICollection<EtwSessionAttempt> attempts,
        CancellationToken cancellationToken)
    {
        DateTimeOffset started = DateTimeOffset.Now;
        EtwCaptureResult capture = await _etw.CaptureAsync(duration, cancellationToken);
        DateTimeOffset ended = DateTimeOffset.Now;
        bool success = capture.Mode == EtwBackendMode.KernelEtw;
        EtwSessionFailureKind kind = success
            ? EtwSessionFailureKind.None
            : ClassifyFallback(capture);
        int? nativeCode = ExtractNativeCode(capture.Failure);

        attempts.Add(new EtwSessionAttempt(
            number,
            $"{EtwSessionGuardService.OwnedSessionPrefix}<generated-attempt-{number}>",
            started,
            ended,
            success,
            kind,
            nativeCode,
            success
                ? capture.Status
                : capture.Failure ?? capture.Status));
        return capture;
    }

    private static EtwSessionFailureKind ClassifyFallback(EtwCaptureResult capture)
    {
        if (!capture.Elevated)
            return EtwSessionFailureKind.NotElevated;

        string failure = capture.Failure ?? string.Empty;
        if (ContainsAny(failure, "access is denied", "access denied", "отказано в доступе"))
            return EtwSessionFailureKind.AccessDenied;
        if (ContainsAny(failure, "already exists", "уже существует", "name collision"))
            return EtwSessionFailureKind.NameCollision;
        if (ContainsAny(failure, "not enough memory", "system resources", "недостаточно системных ресурсов"))
            return EtwSessionFailureKind.ResourceLimit;
        if (ContainsAny(failure, "not active", "instance not found", "не найдена"))
            return EtwSessionFailureKind.SessionUnavailable;
        return EtwSessionFailureKind.Unknown;
    }

    private static int? ExtractNativeCode(string? failure)
    {
        if (string.IsNullOrWhiteSpace(failure))
            return null;

        foreach (string token in failure.Split(
                     [' ', ',', ';', ':', '(', ')', '[', ']'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(token[2..], System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out int hex))
                return hex & 0xFFFF;
            if (token.StartsWith("native=", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(token[7..], out int native))
                return native;
        }

        return null;
    }

    private static string BuildSuccessSummary(
        EtwSessionInventory inventory,
        IReadOnlyCollection<EtwSessionAttempt> attempts,
        EtwCaptureResult capture) =>
        $"Kernel ETW capture завершена. Attempts={attempts.Count}; active sessions before={inventory.ActiveCount}; " +
        $"owned={inventory.PortSentinelCount}; foreign={inventory.ForeignCount}; events={capture.Events.Count}. " +
        "Чужие ETW-сессии не изменялись.";

    private static string BuildFallbackSummary(
        EtwSessionInventory inventory,
        IReadOnlyList<EtwSessionAttempt> attempts,
        EtwCaptureResult capture)
    {
        EtwSessionAttempt last = attempts[^1];
        return $"Использован snapshot fallback. Failure={last.FailureKind}; attempts={attempts.Count}; " +
               $"active sessions before={inventory.ActiveCount}; owned={inventory.PortSentinelCount}; " +
               $"foreign={inventory.ForeignCount}; snapshot events={capture.Events.Count}. " +
               "Чужие ETW-сессии не останавливались и не перезапускались.";
    }

    private static bool ContainsAny(string value, params string[] candidates) =>
        candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));
}
