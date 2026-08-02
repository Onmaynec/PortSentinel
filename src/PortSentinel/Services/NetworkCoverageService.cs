using System.Text;
using System.Text.Json;
using PortSentinel.Models;

namespace PortSentinel.Services;

internal sealed class NetworkCoverageService
{
    private readonly string _reportsDirectory;

    public NetworkCoverageService(string reportsDirectory)
    {
        _reportsDirectory = reportsDirectory;
        Directory.CreateDirectory(_reportsDirectory);
    }

    public NetworkCoverageReport Analyze(EtwCaptureResult capture, long? captureId = null) =>
        AnalyzeCore(
            captureId,
            capture.StartedAt,
            capture.EndedAt,
            capture.Mode,
            capture.Events);

    public NetworkCoverageReport Analyze(TelemetryCapture capture) =>
        AnalyzeCore(
            capture.Summary.Id,
            capture.Summary.StartedAt,
            capture.Summary.EndedAt,
            capture.Summary.Mode,
            capture.Events);

    public async Task<string> ExportAsync(
        NetworkCoverageReport report,
        string format,
        CancellationToken cancellationToken)
    {
        string extension = format.Equals("json", StringComparison.OrdinalIgnoreCase) ? "json" : "md";
        string id = report.CaptureId?.ToString() ?? report.StartedAt.ToString("yyyyMMdd-HHmmss");
        string path = Path.Combine(_reportsDirectory, $"network-coverage-{id}.{extension}");

        if (extension == "json")
        {
            var payload = new
            {
                schemaVersion = 1,
                report.CaptureId,
                report.StartedAt,
                report.EndedAt,
                backend = report.Mode.ToString(),
                report.EventCount,
                report.Ipv4Count,
                report.Ipv6Count,
                report.TcpCount,
                report.UdpCount,
                report.UdpSendCount,
                report.UdpReceiveCount,
                report.ObservedFamilies,
                protocols = report.Protocols,
                topRemoteEndpoints = report.TopRemoteEndpoints,
                limitations = report.Limitations,
                privacy = "No packet payload, HTTP body, cookies, credentials, tokens or decrypted TLS content is captured."
            };

            await File.WriteAllTextAsync(
                path,
                JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);
            return path;
        }

        var text = new StringBuilder()
            .AppendLine(report.CaptureId is long captureId
                ? $"# PortSentinel Network Coverage — Capture #{captureId}"
                : "# PortSentinel Network Coverage")
            .AppendLine()
            .AppendLine($"- **Backend:** {report.Mode}")
            .AppendLine($"- **Started:** {report.StartedAt:O}")
            .AppendLine($"- **Ended:** {report.EndedAt:O}")
            .AppendLine($"- **Events:** {report.EventCount}")
            .AppendLine($"- **IPv4 / IPv6:** {report.Ipv4Count} / {report.Ipv6Count}")
            .AppendLine($"- **TCP / UDP:** {report.TcpCount} / {report.UdpCount}")
            .AppendLine($"- **UDP send / receive:** {report.UdpSendCount} / {report.UdpReceiveCount}")
            .AppendLine($"- **Observed families:** {Escape(report.ObservedFamilies)}")
            .AppendLine()
            .AppendLine("## Protocol matrix")
            .AppendLine()
            .AppendLine("| Protocol | Events | Processes | Remote endpoints | Send | Receive |")
            .AppendLine("|---|---:|---:|---:|---:|---:|");

        foreach (NetworkProtocolCoverage protocol in report.Protocols)
        {
            text.AppendLine(
                $"| {Escape(protocol.Protocol)} | {protocol.EventCount} | {protocol.UniqueProcesses} | {protocol.UniqueRemoteEndpoints} | {protocol.SendCount} | {protocol.ReceiveCount} |");
        }

        text.AppendLine()
            .AppendLine("## Top remote endpoints")
            .AppendLine()
            .AppendLine("| Protocol | Endpoint | Events | Processes |")
            .AppendLine("|---|---|---:|---:|");

        foreach (NetworkEndpointCoverage endpoint in report.TopRemoteEndpoints)
        {
            text.AppendLine(
                $"| {Escape(endpoint.Protocol)} | {Escape(endpoint.Endpoint)} | {endpoint.EventCount} | {endpoint.UniqueProcesses} |");
        }

        text.AppendLine()
            .AppendLine("## Limitations")
            .AppendLine();
        foreach (string limitation in report.Limitations)
            text.AppendLine($"- {Escape(limitation)}");

        text.AppendLine()
            .AppendLine("> Observed coverage describes this bounded capture only. Absence of a protocol does not prove absence of traffic.")
            .AppendLine("> Packet payload and decrypted application content are not collected.");

        await File.WriteAllTextAsync(path, text.ToString(), cancellationToken);
        return path;
    }

    private static NetworkCoverageReport AnalyzeCore(
        long? captureId,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        EtwBackendMode mode,
        IReadOnlyList<EtwNetworkEvent> events)
    {
        NetworkProtocolCoverage[] protocols = events
            .GroupBy(item => NormalizeProtocol(item.Protocol), StringComparer.OrdinalIgnoreCase)
            .Select(group => new NetworkProtocolCoverage(
                group.Key,
                group.Count(),
                group.Select(item => item.ProcessName)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),
                group.Select(RemoteKey)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),
                group.Count(item => item.Kind is "CONNECT" or "UDP_SEND"),
                group.Count(item => item.Kind is "ACCEPT" or "UDP_RECV")))
            .OrderByDescending(item => item.EventCount)
            .ThenBy(item => item.Protocol, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        NetworkEndpointCoverage[] endpoints = events
            .Select(item => new
            {
                Item = item,
                Endpoint = RemoteKey(item),
                Protocol = NormalizeProtocol(item.Protocol)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Endpoint))
            .GroupBy(
                item => $"{item.Protocol}|{item.Endpoint}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new NetworkEndpointCoverage(
                group.First().Protocol,
                group.First().Endpoint,
                group.Count(),
                group.Select(item => item.Item.ProcessName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count()))
            .OrderByDescending(item => item.EventCount)
            .ThenBy(item => item.Protocol, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Endpoint, StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();

        int ipv6 = events.Count(IsIpv6);
        int udp = events.Count(item => IsUdp(item.Protocol));
        var limitations = new List<string>
        {
            "Coverage reflects only the selected bounded capture window.",
            "A missing protocol family means no matching event was observed, not that traffic was absent.",
            "Capture results are capped at 5000 normalized events.",
            "Packet payload, HTTP body and decrypted TLS content are outside the collection boundary."
        };

        if (events.Any(item =>
                (item.Kind is "UDP_SEND" or "UDP_RECV") &&
                (item.LocalPort == 0 || item.RemotePort == 0)))
        {
            limitations.Add(
                "Kernel UDP callbacks can omit source-port metadata; unavailable ports are represented as 0.");
        }

        if (mode == EtwBackendMode.SnapshotFallback)
        {
            limitations.Add(
                "SnapshotFallback is a point-in-time table and does not provide send/receive ordering or ETW lifecycle events.");
        }

        return new NetworkCoverageReport(
            captureId,
            startedAt,
            endedAt,
            mode,
            events.Count,
            events.Count - ipv6,
            ipv6,
            events.Count(item => IsTcp(item.Protocol)),
            udp,
            events.Count(item => item.Kind == "UDP_SEND"),
            events.Count(item => item.Kind == "UDP_RECV"),
            protocols,
            endpoints,
            limitations);
    }

    private static string NormalizeProtocol(string? protocol)
    {
        string value = (protocol ?? string.Empty).Trim().ToUpperInvariant();
        return value switch
        {
            "TCPV4" => "TCP4",
            "TCPV6" => "TCP6",
            "UDPV4" => "UDP4",
            "UDPV6" => "UDP6",
            "TCP" => "TCP4",
            "UDP" => "UDP4",
            "" => "UNKNOWN",
            _ => value
        };
    }

    private static bool IsTcp(string? protocol) =>
        NormalizeProtocol(protocol).StartsWith("TCP", StringComparison.Ordinal);

    private static bool IsUdp(string? protocol) =>
        NormalizeProtocol(protocol).StartsWith("UDP", StringComparison.Ordinal);

    private static bool IsIpv6(EtwNetworkEvent item) =>
        NormalizeProtocol(item.Protocol).EndsWith('6') ||
        item.LocalAddress.Contains(':', StringComparison.Ordinal) ||
        item.RemoteAddress.Contains(':', StringComparison.Ordinal);

    private static string RemoteKey(EtwNetworkEvent item)
    {
        if (string.IsNullOrWhiteSpace(item.RemoteAddress))
            return string.Empty;

        string address = item.RemoteAddress.Contains(':', StringComparison.Ordinal)
            ? $"[{item.RemoteAddress}]"
            : item.RemoteAddress;
        return item.RemotePort > 0 ? $"{address}:{item.RemotePort}" : address;
    }

    private static string Escape(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal)
             .Replace("\r", " ", StringComparison.Ordinal)
             .Replace("\n", " ", StringComparison.Ordinal);
}
