namespace PortSentinel.Models;

internal sealed record NetworkProtocolCoverage(
    string Protocol,
    int EventCount,
    int UniqueProcesses,
    int UniqueRemoteEndpoints,
    int SendCount,
    int ReceiveCount);

internal sealed record NetworkEndpointCoverage(
    string Protocol,
    string Endpoint,
    int EventCount,
    int UniqueProcesses);

internal sealed record NetworkCoverageReport(
    long? CaptureId,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    EtwBackendMode Mode,
    int EventCount,
    int Ipv4Count,
    int Ipv6Count,
    int TcpCount,
    int UdpCount,
    int UdpSendCount,
    int UdpReceiveCount,
    IReadOnlyList<NetworkProtocolCoverage> Protocols,
    IReadOnlyList<NetworkEndpointCoverage> TopRemoteEndpoints,
    IReadOnlyList<string> Limitations)
{
    public string ObservedFamilies => Protocols.Count == 0
        ? "none"
        : string.Join(", ", Protocols.Select(item => item.Protocol));
}
