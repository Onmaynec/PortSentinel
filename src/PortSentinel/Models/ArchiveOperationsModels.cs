namespace PortSentinel.Models;

internal sealed record CaptureProfile(
    string Key,
    string Name,
    TimeSpan Duration,
    string Description);

internal sealed record TelemetrySearchQuery(
    string Text,
    string? Kind,
    EtwBackendMode? Mode,
    int Limit = 200);

internal sealed record TelemetrySearchResult(
    long CaptureId,
    DateTimeOffset CaptureStartedAt,
    EtwBackendMode Mode,
    EtwNetworkEvent Event);

internal sealed record TelemetryArchiveStats(
    int CaptureCount,
    int EventCount,
    DateTimeOffset? OldestCapture,
    DateTimeOffset? NewestCapture,
    long DatabaseBytes);

internal sealed record TelemetryRetentionPreview(
    int KeepLatest,
    int DeleteCaptureCount,
    int DeleteEventCount,
    DateTimeOffset? DeleteThrough);
