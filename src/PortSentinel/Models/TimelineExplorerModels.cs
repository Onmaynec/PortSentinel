namespace PortSentinel.Models;

internal sealed record TimelineFilter(
    string Kind,
    string Protocol,
    string Search)
{
    public static TimelineFilter Empty { get; } = new(string.Empty, string.Empty, string.Empty);

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Kind) &&
        string.IsNullOrWhiteSpace(Protocol) &&
        string.IsNullOrWhiteSpace(Search);

    public string Description
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Kind)) parts.Add($"kind={Kind}");
            if (!string.IsNullOrWhiteSpace(Protocol)) parts.Add($"protocol={Protocol}");
            if (!string.IsNullOrWhiteSpace(Search)) parts.Add($"search=\"{Search}\"");
            return parts.Count == 0 ? "none" : string.Join(", ", parts);
        }
    }
}

internal sealed record TimelineCapturePage(
    IReadOnlyList<TelemetryCaptureSummary> Items,
    int Page,
    int PageSize,
    int TotalItems)
{
    public int PageCount => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < PageCount;
}

internal sealed record TimelineEventPage(
    long CaptureId,
    TimelineFilter Filter,
    IReadOnlyList<EtwNetworkEvent> Items,
    int Page,
    int PageSize,
    int TotalItems)
{
    public int PageCount => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < PageCount;

    public int FirstRow => TotalItems == 0 ? 0 : ((Page - 1) * PageSize) + 1;

    public int LastRow => TotalItems == 0 ? 0 : Math.Min(Page * PageSize, TotalItems);
}

internal sealed record TimelineJumpResult(
    int Page,
    int Index,
    EtwNetworkEvent Event);
