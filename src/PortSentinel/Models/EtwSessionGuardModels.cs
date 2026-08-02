namespace PortSentinel.Models;

internal enum EtwSessionFailureKind
{
    None,
    NotElevated,
    AccessDenied,
    NameCollision,
    ResourceLimit,
    SessionUnavailable,
    Unknown
}

internal sealed record EtwSessionInventory(
    DateTimeOffset CollectedAt,
    bool Success,
    IReadOnlyList<string> ActiveSessions,
    IReadOnlyList<string> PortSentinelSessions,
    string? Error)
{
    public int ActiveCount => ActiveSessions.Count;

    public int PortSentinelCount => PortSentinelSessions.Count;

    public int ForeignCount => Math.Max(0, ActiveCount - PortSentinelCount);
}

internal sealed record EtwSessionAttempt(
    int Number,
    string SessionName,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    bool Succeeded,
    EtwSessionFailureKind FailureKind,
    int? NativeErrorCode,
    string Message);

internal sealed record EtwSessionDiagnostics(
    EtwSessionInventory InventoryBefore,
    IReadOnlyList<EtwSessionAttempt> Attempts,
    bool UsedSnapshotFallback,
    string Summary);

internal sealed record EtwGuardedCaptureResult(
    EtwCaptureResult Capture,
    EtwSessionDiagnostics Diagnostics);

internal sealed record EtwOwnedSessionCleanupItem(
    string SessionName,
    bool Stopped,
    string Message);

internal sealed record EtwOwnedSessionCleanupResult(
    DateTimeOffset CompletedAt,
    IReadOnlyList<EtwOwnedSessionCleanupItem> Items)
{
    public int RequestedCount => Items.Count;

    public int StoppedCount => Items.Count(item => item.Stopped);

    public int FailedCount => RequestedCount - StoppedCount;
}
