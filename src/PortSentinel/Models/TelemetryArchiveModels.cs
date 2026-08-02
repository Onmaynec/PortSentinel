namespace PortSentinel.Models;

internal sealed record TelemetryCaptureSummary(
    long Id,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    EtwBackendMode Mode,
    string Status,
    int EventCount,
    int ConnectCount,
    int AcceptCount,
    int DisconnectCount,
    int RetransmitCount,
    bool Elevated,
    string? Failure);

internal sealed record TelemetryCapture(
    TelemetryCaptureSummary Summary,
    IReadOnlyList<EtwNetworkEvent> Events);

internal sealed record TelemetryCaptureComparison(
    TelemetryCaptureSummary Older,
    TelemetryCaptureSummary Newer,
    IReadOnlyList<EtwNetworkEvent> AddedEvents,
    IReadOnlyList<string> MissingFingerprints)
{
    public int OlderEventCount => Older.EventCount;

    public int NewerEventCount => Newer.EventCount;
}
