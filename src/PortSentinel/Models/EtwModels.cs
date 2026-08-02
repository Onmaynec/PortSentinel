namespace PortSentinel.Models;

internal enum EtwBackendMode
{
    KernelEtw,
    SnapshotFallback
}

internal sealed record EtwCapability(
    bool IsWindows,
    bool IsElevated,
    bool CanControlKernelSession,
    string Message);

internal sealed record EtwNetworkEvent(
    long Sequence,
    DateTimeOffset Timestamp,
    string Kind,
    int ProcessId,
    string ProcessName,
    string Protocol,
    string LocalAddress,
    int LocalPort,
    string RemoteAddress,
    int RemotePort,
    string Note)
{
    public string LocalEndpoint => FormatEndpoint(LocalAddress, LocalPort);

    public string RemoteEndpoint => FormatEndpoint(RemoteAddress, RemotePort);

    private static string FormatEndpoint(string address, int port)
    {
        if (string.IsNullOrWhiteSpace(address) || address == "—")
            return "—";

        string formatted = address.Contains(':', StringComparison.Ordinal)
            ? $"[{address}]"
            : address;
        return port > 0 ? $"{formatted}:{port}" : formatted;
    }
}

internal sealed record EtwCaptureResult(
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    EtwBackendMode Mode,
    string Status,
    IReadOnlyList<EtwNetworkEvent> Events,
    int ConnectCount,
    int AcceptCount,
    int DisconnectCount,
    int RetransmitCount,
    bool Elevated,
    string? Failure);
