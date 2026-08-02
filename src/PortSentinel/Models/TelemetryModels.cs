namespace PortSentinel.Models;

internal sealed record DnsCorrelation(
    string Address,
    string? HostName,
    string Status);

internal sealed record ProcessTreeNode(
    int ProcessId,
    int ParentProcessId,
    string Name,
    int Depth,
    bool IsNetworkActive,
    int NetworkEntryCount);

internal sealed record SessionComparison(
    SessionSummary Before,
    SessionSummary After,
    IReadOnlyList<NetworkEntry> AddedEntries,
    IReadOnlyList<NetworkEntry> RemovedEntries,
    IReadOnlyList<string> AddedProcesses,
    IReadOnlyList<string> RemovedProcesses,
    int ListenerDelta,
    int ExternalDelta);

internal sealed record WatchConnection(
    string Identity,
    string Protocol,
    string LocalEndpoint,
    string RemoteAddress,
    int RemotePort,
    string RemoteEndpoint,
    string? HostName,
    DateTimeOffset FirstSeen,
    DateTimeOffset LastSeen,
    int Observations,
    int ConnectCycles,
    bool IsActive,
    bool IsListener);

internal sealed record ApplicationWatchReport(
    string TargetProcess,
    string? ExecutablePath,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    int Samples,
    IReadOnlyList<WatchConnection> Connections)
{
    public int ReconnectLoops => Connections.Count(connection => connection.ConnectCycles >= 3);
    public int ActiveConnections => Connections.Count(connection => connection.IsActive);
}
