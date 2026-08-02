using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using PortSentinel.Models;

namespace PortSentinel.Services;

internal sealed class DnsCorrelationService
{
    private readonly ConcurrentDictionary<string, DnsCorrelation> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<DnsCorrelation>> ResolveAsync(
        IReadOnlyList<NetworkEntry> entries,
        CancellationToken cancellationToken) =>
        ResolveAddressesAsync(
            entries.Where(entry => entry.IsExternal)
                .Select(entry => entry.RemoteAddress),
            cancellationToken);

    public async Task<IReadOnlyList<DnsCorrelation>> ResolveAddressesAsync(
        IEnumerable<string> addresses,
        CancellationToken cancellationToken)
    {
        string[] unique = addresses
            .Where(address => IPAddress.TryParse(address, out _))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(32)
            .ToArray();

        using var gate = new SemaphoreSlim(4, 4);
        Task<DnsCorrelation>[] tasks = unique
            .Select(address => ResolveOneAsync(address, gate, cancellationToken))
            .ToArray();

        DnsCorrelation[] results = await Task.WhenAll(tasks);
        return results
            .OrderBy(result => result.Address, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<DnsCorrelation> ResolveOneAsync(
        string address,
        SemaphoreSlim gate,
        CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(address, out DnsCorrelation? cached))
        {
            return cached;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(address, out cached))
            {
                return cached;
            }

            DnsCorrelation result;
            try
            {
                IPAddress ipAddress = IPAddress.Parse(address);
                IPHostEntry host = await Dns.GetHostEntryAsync(ipAddress)
                    .WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
                result = new DnsCorrelation(address, host.HostName, "resolved");
            }
            catch (TimeoutException)
            {
                result = new DnsCorrelation(address, null, "timeout");
            }
            catch (SocketException)
            {
                result = new DnsCorrelation(address, null, "not-found");
            }
            catch (ArgumentException)
            {
                result = new DnsCorrelation(address, null, "invalid");
            }

            _cache[address] = result;
            return result;
        }
        finally
        {
            gate.Release();
        }
    }
}
