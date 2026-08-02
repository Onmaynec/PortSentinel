namespace PortSentinel.Models;

internal sealed record InstallerWatchProcessSummary(
    string ProcessName,
    int AddedEventCount,
    int UniqueRemoteEndpoints,
    int TcpEventCount,
    int UdpEventCount,
    int FailureSignalCount,
    bool MatchesHint);

internal sealed record InstallerWatchReport(
    long BaselineCaptureId,
    long WatchCaptureId,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    EtwBackendMode BaselineMode,
    EtwBackendMode WatchMode,
    string ProcessHint,
    int BaselineEventCount,
    int WatchEventCount,
    IReadOnlyList<EtwNetworkEvent> AddedEvents,
    IReadOnlyList<InstallerWatchProcessSummary> Processes,
    IReadOnlyList<string> Limitations)
{
    public int AddedProcessCount => Processes.Count;

    public int AddedEndpointCount => AddedEvents
        .Select(item => item.RemoteEndpoint)
        .Where(value => !string.IsNullOrWhiteSpace(value) && value != "—")
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    public int FailureSignalCount => AddedEvents.Count(item =>
        item.Kind is "FAIL" or "RETRANSMIT" or "RECONNECT");
}
