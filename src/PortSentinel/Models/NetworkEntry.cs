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

    private static string FormatEndpoint(string address, int port)
    {
        string formattedAddress = address.Contains(':', StringComparison.Ordinal)
            ? $"[{address}]"
            : address;

        return $"{formattedAddress}:{port}";
    }
}
