namespace PortSentinel.Models;

internal sealed record NetworkEntry(
    string Protocol,
    string LocalAddress,
    int LocalPort,
    string RemoteAddress,
    int RemotePort,
    string State,
    int ProcessId,
    string ProcessName,
    string? ExecutablePath,
    bool IsListener,
    bool IsExternal)
{
    public string LocalEndpoint => FormatEndpoint(LocalAddress, LocalPort);

    public string RemoteEndpoint => string.IsNullOrWhiteSpace(RemoteAddress)
        ? "—"
        : FormatEndpoint(RemoteAddress, RemotePort);

    public string Identity =>
        $"{Protocol}|{LocalAddress}|{LocalPort}|{RemoteAddress}|{RemotePort}|{ProcessId}|{State}";

    public string BaselineIdentity
    {
        get
        {
            string processIdentity = NormalizeIdentityPart(ExecutablePath ?? ProcessName);
            string protocol = NormalizeIdentityPart(Protocol);
            string localAddress = NormalizeIdentityPart(LocalAddress);
            string remoteAddress = NormalizeIdentityPart(RemoteAddress);

            return IsListener
                ? $"{protocol}|listener|{localAddress}|{LocalPort}|{processIdentity}"
                : $"{protocol}|connection|{localAddress}|{LocalPort}|{remoteAddress}|{RemotePort}|{processIdentity}|{NormalizeIdentityPart(State)}";
        }
    }

    private static string FormatEndpoint(string address, int port)
    {
        string formattedAddress = address.Contains(':', StringComparison.Ordinal)
            ? $"[{address}]"
            : address;

        return $"{formattedAddress}:{port}";
    }

    private static string NormalizeIdentityPart(string? value) =>
        (value ?? string.Empty).Trim().Replace('/', '\\').ToUpperInvariant();
}
