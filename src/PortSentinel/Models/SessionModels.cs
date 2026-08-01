namespace PortSentinel.Models;

internal sealed record SessionSummary(
    long Id,
    string Name,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    int EntryCount,
    int ProcessCount,
    int ListenerCount,
    int ExternalCount);

internal sealed record BaselineSummary(
    long Id,
    string Name,
    DateTimeOffset CreatedAt,
    int EntryCount);

internal sealed record BaselineComparison(
    string Name,
    int CurrentEntries,
    int BaselineEntries,
    IReadOnlyList<NetworkEntry> NewEntries,
    IReadOnlyList<string> MissingIdentities);
