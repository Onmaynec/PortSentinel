namespace PortSentinel.Models;

internal sealed record ProcessMetadata(
    int ProcessId,
    string Name,
    string? Path,
    string? Company,
    string? Description,
    DateTimeOffset? StartTime,
    bool IsElevated,
    string AccessStatus);
