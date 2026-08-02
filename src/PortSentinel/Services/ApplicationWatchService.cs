using System.Text;
using System.Text.Json;
using PortSentinel.Models;

namespace PortSentinel.Services;

internal sealed class ApplicationWatchService
{
    private readonly SessionStore _store;
    private readonly DnsCorrelationService _dns;

    public ApplicationWatchService(SessionStore store, DnsCorrelationService dns)
    {
        _store = store;
        _dns = dns;
    }

    public ApplicationWatchTracker CreateTracker(NetworkEntry target) =>
        new(target.ProcessName, target.ExecutablePath);

    public async Task<ApplicationWatchReport> CompleteAsync(
        ApplicationWatchTracker tracker,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DnsCorrelation> correlations = await _dns.ResolveAddressesAsync(
            tracker.RemoteAddresses,
            cancellationToken);
        Dictionary<string, string?> hosts = correlations.ToDictionary(
            item => item.Address,
            item => item.HostName,
            StringComparer.OrdinalIgnoreCase);
        return tracker.BuildReport(hosts, DateTimeOffset.Now);
    }

    public async Task<string> ExportAsync(
        ApplicationWatchReport report,
        string format,
        CancellationToken cancellationToken)
    {
        string safeTarget = string.Concat(report.TargetProcess.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        string extension = format.Equals("json", StringComparison.OrdinalIgnoreCase) ? "json" : "md";
        string stamp = report.StartedAt.ToString("yyyyMMdd-HHmmss");
        string path = Path.Combine(
            _store.ReportsDirectory,
            $"watch-{safeTarget}-{stamp}.{extension}");

        if (extension == "json")
        {
            var payload = new
            {
                schemaVersion = 1,
                report
            };
            await File.WriteAllTextAsync(
                path,
                JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);
            return path;
        }

        var text = new StringBuilder()
            .AppendLine($"# PortSentinel Application Watch — {report.TargetProcess}")
            .AppendLine()
            .AppendLine($"- **Started:** {report.StartedAt:G}")
            .AppendLine($"- **Ended:** {report.EndedAt:G}")
            .AppendLine($"- **Samples:** {report.Samples}")
            .AppendLine($"- **Unique endpoints:** {report.Connections.Count}")
            .AppendLine($"- **Reconnect loops:** {report.ReconnectLoops}")
            .AppendLine($"- **Executable:** {report.ExecutablePath ?? "unavailable"}")
            .AppendLine()
            .AppendLine("| Protocol | Local | Remote | DNS | First seen | Last seen | Samples | Cycles | Active |")
            .AppendLine("|---|---|---|---|---|---|---:|---:|---|");

        foreach (WatchConnection connection in report.Connections)
        {
            text.AppendLine(
                $"| {connection.Protocol} | {Escape(connection.LocalEndpoint)} | {Escape(connection.RemoteEndpoint)} | " +
                $"{Escape(connection.HostName ?? "—")} | {connection.FirstSeen:G} | {connection.LastSeen:G} | " +
                $"{connection.Observations} | {connection.ConnectCycles} | {(connection.IsActive ? "yes" : "no")} |");
        }

        await File.WriteAllTextAsync(path, text.ToString(), cancellationToken);
        return path;
    }

    private static string Escape(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
}

internal sealed class ApplicationWatchTracker
{
    private readonly Dictionary<string, ObservedConnection> _connections =
        new(StringComparer.Ordinal);

    public ApplicationWatchTracker(string processName, string? executablePath)
    {
        TargetProcess = processName;
        ExecutablePath = executablePath;
        StartedAt = DateTimeOffset.Now;
    }

    public string TargetProcess { get; }
    public string? ExecutablePath { get; }
    public DateTimeOffset StartedAt { get; }
    public int Samples { get; private set; }
    public int ActiveConnections => _connections.Values.Count(connection => connection.IsActive);
    public int UniqueConnections => _connections.Count;
    public int ReconnectLoops => _connections.Values.Count(connection => connection.ConnectCycles >= 3);
    public IEnumerable<string> RemoteAddresses => _connections.Values
        .Where(connection => !connection.IsListener && !string.IsNullOrWhiteSpace(connection.RemoteAddress))
        .Select(connection => connection.RemoteAddress)
        .Distinct(StringComparer.OrdinalIgnoreCase);

    public void Observe(IReadOnlyList<NetworkEntry> entries, DateTimeOffset observedAt)
    {
        NetworkEntry[] current = entries
            .Where(MatchesTarget)
            .GroupBy(WatchIdentity, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        HashSet<string> activeKeys = current
            .Select(WatchIdentity)
            .ToHashSet(StringComparer.Ordinal);

        foreach (ObservedConnection connection in _connections.Values)
        {
            if (!activeKeys.Contains(connection.Identity))
            {
                connection.IsActive = false;
            }
        }

        foreach (NetworkEntry entry in current)
        {
            string identity = WatchIdentity(entry);
            if (!_connections.TryGetValue(identity, out ObservedConnection? connection))
            {
                connection = new ObservedConnection(entry, identity, observedAt);
                _connections.Add(identity, connection);
            }
            else
            {
                if (!connection.IsActive)
                {
                    connection.ConnectCycles++;
                }

                connection.LocalEndpoint = entry.LocalEndpoint;
                connection.LastSeen = observedAt;
                connection.Observations++;
                connection.IsActive = true;
            }
        }

        Samples++;
    }

    public IReadOnlyList<WatchConnection> Snapshot() =>
        BuildConnections(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase));

    public ApplicationWatchReport BuildReport(
        IReadOnlyDictionary<string, string?> hostNames,
        DateTimeOffset endedAt) =>
        new(
            TargetProcess,
            ExecutablePath,
            StartedAt,
            endedAt,
            Samples,
            BuildConnections(hostNames));

    private IReadOnlyList<WatchConnection> BuildConnections(
        IReadOnlyDictionary<string, string?> hostNames) =>
        _connections.Values
            .Select(connection => new WatchConnection(
                connection.Identity,
                connection.Protocol,
                connection.LocalEndpoint,
                connection.RemoteAddress,
                connection.RemotePort,
                connection.RemoteEndpoint,
                hostNames.TryGetValue(connection.RemoteAddress, out string? hostName) ? hostName : null,
                connection.FirstSeen,
                connection.LastSeen,
                connection.Observations,
                connection.ConnectCycles,
                connection.IsActive,
                connection.IsListener))
            .OrderByDescending(connection => connection.ConnectCycles)
            .ThenByDescending(connection => connection.IsActive)
            .ThenBy(connection => connection.RemoteEndpoint, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private bool MatchesTarget(NetworkEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(ExecutablePath) &&
            string.Equals(entry.ExecutablePath, ExecutablePath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(entry.ProcessName, TargetProcess, StringComparison.OrdinalIgnoreCase);
    }

    private static string WatchIdentity(NetworkEntry entry)
    {
        string process = Normalize(entry.ExecutablePath ?? entry.ProcessName);
        string protocol = Normalize(entry.Protocol);
        if (entry.IsListener)
        {
            return $"{process}|{protocol}|listener|{Normalize(entry.LocalAddress)}|{entry.LocalPort}";
        }

        return $"{process}|{protocol}|remote|{Normalize(entry.RemoteAddress)}|{entry.RemotePort}";
    }

    private static string Normalize(string? value) =>
        (value ?? string.Empty).Trim().Replace('/', '\\').ToUpperInvariant();

    private sealed class ObservedConnection
    {
        public ObservedConnection(NetworkEntry entry, string identity, DateTimeOffset observedAt)
        {
            Identity = identity;
            Protocol = entry.Protocol;
            LocalEndpoint = entry.LocalEndpoint;
            RemoteAddress = entry.RemoteAddress;
            RemotePort = entry.RemotePort;
            RemoteEndpoint = entry.RemoteEndpoint;
            FirstSeen = observedAt;
            LastSeen = observedAt;
            Observations = 1;
            ConnectCycles = 1;
            IsActive = true;
            IsListener = entry.IsListener;
        }

        public string Identity { get; }
        public string Protocol { get; }
        public string LocalEndpoint { get; set; }
        public string RemoteAddress { get; }
        public int RemotePort { get; }
        public string RemoteEndpoint { get; }
        public DateTimeOffset FirstSeen { get; }
        public DateTimeOffset LastSeen { get; set; }
        public int Observations { get; set; }
        public int ConnectCycles { get; set; }
        public bool IsActive { get; set; }
        public bool IsListener { get; }
    }
}
