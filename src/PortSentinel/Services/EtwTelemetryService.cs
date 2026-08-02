using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using PortSentinel.Models;

namespace PortSentinel.Services;

internal sealed class EtwTelemetryService
{
    private readonly NetworkSnapshotService _network;
    private readonly string _reportsDirectory;

    public EtwTelemetryService(NetworkSnapshotService network, string reportsDirectory)
    {
        _network = network;
        _reportsDirectory = reportsDirectory;
        Directory.CreateDirectory(_reportsDirectory);
    }

    public EtwCapability Probe()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new EtwCapability(false, false, false,
                "ETW доступен только на Windows.");
        }

        bool elevated = IsElevated();
        return new EtwCapability(
            true,
            elevated,
            elevated,
            elevated
                ? "Kernel ETW session доступна. PortSentinel не включает packet capture."
                : "Для управления kernel ETW запустите PortSentinel от администратора; иначе используется snapshot fallback.");
    }

    public async Task<EtwCaptureResult> CaptureAsync(
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        duration = TimeSpan.FromSeconds(Math.Clamp(duration.TotalSeconds, 3, 60));
        EtwCapability capability = Probe();
        if (!capability.CanControlKernelSession)
        {
            return await SnapshotFallbackAsync(
                "Kernel ETW недоступна без повышенных прав.", cancellationToken);
        }

        try
        {
            return await CaptureKernelAsync(duration, capability, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return await SnapshotFallbackAsync(
                $"ETW session не запущена: {ex.Message}", cancellationToken);
        }
    }

    public async Task<string> ExportAsync(
        EtwCaptureResult capture,
        string format,
        CancellationToken cancellationToken)
    {
        string extension = format.Equals("json", StringComparison.OrdinalIgnoreCase) ? "json" : "md";
        string stamp = capture.StartedAt.ToString("yyyyMMdd-HHmmss");
        string path = Path.Combine(_reportsDirectory, $"etw-network-{stamp}.{extension}");
        int reconnectCount = capture.Events.Count(item => item.Kind == "RECONNECT");
        int failCount = capture.Events.Count(item => item.Kind == "FAIL");
        int udpSendCount = capture.Events.Count(item => item.Kind == "UDP_SEND");
        int udpReceiveCount = capture.Events.Count(item => item.Kind == "UDP_RECV");
        int ipv6Count = capture.Events.Count(item => IsIpv6(item));
        var protocolCounts = capture.Events
            .GroupBy(item => item.Protocol, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        if (extension == "json")
        {
            var payload = new
            {
                schemaVersion = 3,
                backend = capture.Mode.ToString(),
                capture.StartedAt,
                capture.EndedAt,
                capture.Status,
                capture.Failure,
                capture.ConnectCount,
                capture.AcceptCount,
                capture.DisconnectCount,
                capture.RetransmitCount,
                reconnectCount,
                failCount,
                udpSendCount,
                udpReceiveCount,
                ipv6Count,
                protocolCounts,
                privacy = "No packet payload, HTTP body, cookies, tokens or decrypted TLS content is captured.",
                udpLimitation = "Kernel UDP callbacks expose destination-port metadata; source port can be unavailable and is stored as 0.",
                events = capture.Events
            };
            await File.WriteAllTextAsync(path,
                JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);
            return path;
        }

        var text = new StringBuilder()
            .AppendLine("# PortSentinel ETW Network Capture")
            .AppendLine()
            .AppendLine($"- **Backend:** {capture.Mode}")
            .AppendLine($"- **Started:** {capture.StartedAt:O}")
            .AppendLine($"- **Ended:** {capture.EndedAt:O}")
            .AppendLine($"- **Status:** {Escape(capture.Status)}")
            .AppendLine($"- **Events:** {capture.Events.Count}")
            .AppendLine($"- **TCP connect / accept / disconnect / retransmit / reconnect / fail:** {capture.ConnectCount} / {capture.AcceptCount} / {capture.DisconnectCount} / {capture.RetransmitCount} / {reconnectCount} / {failCount}")
            .AppendLine($"- **UDP send / receive:** {udpSendCount} / {udpReceiveCount}")
            .AppendLine($"- **IPv6 events:** {ipv6Count}")
            .AppendLine()
            .AppendLine("## Protocol coverage")
            .AppendLine();

        foreach ((string protocol, int count) in protocolCounts)
            text.AppendLine($"- **{Escape(protocol)}:** {count}");

        text.AppendLine()
            .AppendLine("> PortSentinel does not capture packet payload, HTTP body, cookies, tokens, or decrypted TLS content.")
            .AppendLine("> UDP source ports may be unavailable in kernel callbacks and are represented as port 0.")
            .AppendLine()
            .AppendLine("| Time | Kind | Process | PID | Protocol | Local | Remote | Note |")
            .AppendLine("|---|---|---|---:|---|---|---|---|");

        foreach (EtwNetworkEvent item in capture.Events)
        {
            text.AppendLine(
                $"| {item.Timestamp:HH:mm:ss.fff} | {Escape(item.Kind)} | {Escape(item.ProcessName)} | {item.ProcessId} | {Escape(item.Protocol)} | {Escape(item.LocalEndpoint)} | {Escape(item.RemoteEndpoint)} | {Escape(item.Note)} |");
        }

        await File.WriteAllTextAsync(path, text.ToString(), cancellationToken);
        return path;
    }

    private async Task<EtwCaptureResult> CaptureKernelAsync(
        TimeSpan duration,
        EtwCapability capability,
        CancellationToken cancellationToken)
    {
        DateTimeOffset started = DateTimeOffset.Now;
        var events = new ConcurrentQueue<EtwNetworkEvent>();
        long sequence = 0;
        string sessionName = $"PortSentinel-{Environment.ProcessId}-{Guid.NewGuid():N}";

        using var session = new TraceEventSession(sessionName)
        {
            StopOnDispose = true
        };

        void AddTcp(
            string kind,
            string protocol,
            int processId,
            IPAddress sourceAddress,
            int sourcePort,
            IPAddress destinationAddress,
            int destinationPort,
            bool inbound = false)
        {
            string source = sourceAddress.ToString();
            string destination = destinationAddress.ToString();
            int sourcePortValue = NormalizePort(sourcePort);
            int destinationPortValue = NormalizePort(destinationPort);

            string note = kind switch
            {
                "RETRANSMIT" => "Повторная передача TCP-сегмента; возможна потеря пакета или congestion.",
                "RECONNECT" => "Kernel TCP reconnect event; повторное установление соединения требует контекста приложения.",
                _ => "Kernel ETW metadata; packet payload не собирается."
            };

            events.Enqueue(new EtwNetworkEvent(
                Interlocked.Increment(ref sequence),
                DateTimeOffset.Now,
                kind,
                processId,
                ResolveProcessName(processId),
                protocol,
                inbound ? destination : source,
                inbound ? destinationPortValue : sourcePortValue,
                inbound ? source : destination,
                inbound ? sourcePortValue : destinationPortValue,
                note));
        }

        void AddUdp(
            string kind,
            string protocol,
            int processId,
            IPAddress sourceAddress,
            IPAddress destinationAddress,
            int destinationPort,
            int datagramSize,
            bool inbound)
        {
            string source = sourceAddress.ToString();
            string destination = destinationAddress.ToString();
            int port = NormalizePort(destinationPort);
            int size = Math.Max(0, datagramSize);

            events.Enqueue(new EtwNetworkEvent(
                Interlocked.Increment(ref sequence),
                DateTimeOffset.Now,
                kind,
                processId,
                ResolveProcessName(processId),
                protocol,
                inbound ? destination : source,
                inbound ? port : 0,
                inbound ? source : destination,
                inbound ? 0 : port,
                $"Kernel UDP metadata; datagramSize={size}. Source port может быть недоступен и сохраняется как 0; payload не собирается."));
        }

        void AddFailure(int processId, int protocol, int failureCode)
        {
            events.Enqueue(new EtwNetworkEvent(
                Interlocked.Increment(ref sequence),
                DateTimeOffset.Now,
                "FAIL",
                processId,
                ResolveProcessName(processId),
                protocol == 6 ? "TCP4" : $"IP-PROTO-{protocol}",
                string.Empty,
                0,
                string.Empty,
                0,
                $"Kernel TCP fail event: protocol={protocol}, failureCode={failureCode}. Numeric code сохранён без speculative verdict."));
        }

        session.Source.Kernel.TcpIpConnect += data =>
            AddTcp("CONNECT", "TCP4", data.ProcessID, data.saddr, data.sport, data.daddr, data.dport);
        session.Source.Kernel.TcpIpAccept += data =>
            AddTcp("ACCEPT", "TCP4", data.ProcessID, data.saddr, data.sport, data.daddr, data.dport, inbound: true);
        session.Source.Kernel.TcpIpDisconnect += data =>
            AddTcp("DISCONNECT", "TCP4", data.ProcessID, data.saddr, data.sport, data.daddr, data.dport);
        session.Source.Kernel.TcpIpRetransmit += data =>
            AddTcp("RETRANSMIT", "TCP4", data.ProcessID, data.saddr, data.sport, data.daddr, data.dport);
        session.Source.Kernel.TcpIpReconnect += data =>
            AddTcp("RECONNECT", "TCP4", data.ProcessID, data.saddr, data.sport, data.daddr, data.dport);
        session.Source.Kernel.TcpIpFail += data =>
            AddFailure(data.ProcessID, data.Proto, data.FailureCode);

        session.Source.Kernel.TcpIpConnectIPV6 += data =>
            AddTcp("CONNECT", "TCP6", data.ProcessID, data.saddr, data.sport, data.daddr, data.dport);
        session.Source.Kernel.TcpIpAcceptIPV6 += data =>
            AddTcp("ACCEPT", "TCP6", data.ProcessID, data.saddr, data.sport, data.daddr, data.dport, inbound: true);
        session.Source.Kernel.TcpIpDisconnectIPV6 += data =>
            AddTcp("DISCONNECT", "TCP6", data.ProcessID, data.saddr, data.sport, data.daddr, data.dport);
        session.Source.Kernel.TcpIpRetransmitIPV6 += data =>
            AddTcp("RETRANSMIT", "TCP6", data.ProcessID, data.saddr, data.sport, data.daddr, data.dport);
        session.Source.Kernel.TcpIpReconnectIPV6 += data =>
            AddTcp("RECONNECT", "TCP6", data.ProcessID, data.saddr, data.sport, data.daddr, data.dport);

        session.Source.Kernel.UdpIpSend += data =>
            AddUdp("UDP_SEND", "UDP4", data.ProcessID, data.saddr, data.daddr, data.dport, data.dsize, inbound: false);
        session.Source.Kernel.UdpIpRecv += data =>
            AddUdp("UDP_RECV", "UDP4", data.ProcessID, data.saddr, data.daddr, data.dport, data.dsize, inbound: true);
        session.Source.Kernel.UdpIpSendIPV6 += data =>
            AddUdp("UDP_SEND", "UDP6", data.ProcessID, data.saddr, data.daddr, data.dport, data.size, inbound: false);
        session.Source.Kernel.UdpIpRecvIPV6 += data =>
            AddUdp("UDP_RECV", "UDP6", data.ProcessID, data.saddr, data.daddr, data.dport, data.size, inbound: true);

        session.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP);
        Task processor = Task.Run(() => session.Source.Process(), CancellationToken.None);

        try
        {
            await Task.Delay(duration, cancellationToken);
        }
        finally
        {
            session.Stop();
        }

        await Task.WhenAny(processor, Task.Delay(TimeSpan.FromSeconds(3), CancellationToken.None));
        EtwNetworkEvent[] result = events
            .OrderBy(item => item.Sequence)
            .Take(5000)
            .ToArray();

        return BuildResult(
            started,
            DateTimeOffset.Now,
            EtwBackendMode.KernelEtw,
            $"Kernel ETW capture завершена за {duration.TotalSeconds:0} сек. TCP4/TCP6 и UDP4/UDP6 metadata включена.",
            result,
            capability.IsElevated,
            null);
    }

    private async Task<EtwCaptureResult> SnapshotFallbackAsync(
        string reason,
        CancellationToken cancellationToken)
    {
        DateTimeOffset started = DateTimeOffset.Now;
        IReadOnlyList<NetworkEntry> snapshot = await _network.CaptureAsync(cancellationToken);
        EtwNetworkEvent[] events = snapshot
            .Select((entry, index) => new EtwNetworkEvent(
                index + 1,
                DateTimeOffset.Now,
                entry.IsListener ? "LISTENER" : "SNAPSHOT",
                entry.ProcessId,
                entry.ProcessName,
                entry.Protocol,
                entry.LocalAddress,
                entry.LocalPort,
                entry.RemoteAddress,
                entry.RemotePort,
                reason))
            .Take(5000)
            .ToArray();

        return BuildResult(
            started,
            DateTimeOffset.Now,
            EtwBackendMode.SnapshotFallback,
            "Использован snapshot fallback через Windows IP Helper API.",
            events,
            IsElevated(),
            reason);
    }

    private static EtwCaptureResult BuildResult(
        DateTimeOffset started,
        DateTimeOffset ended,
        EtwBackendMode mode,
        string status,
        IReadOnlyList<EtwNetworkEvent> events,
        bool elevated,
        string? failure) =>
        new(
            started,
            ended,
            mode,
            status,
            events,
            events.Count(item => item.Kind == "CONNECT"),
            events.Count(item => item.Kind == "ACCEPT"),
            events.Count(item => item.Kind == "DISCONNECT"),
            events.Count(item => item.Kind == "RETRANSMIT"),
            elevated,
            failure);

    private static bool IsElevated()
    {
        try
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity)
                .IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveProcessName(int processId)
    {
        if (processId <= 0)
            return "System";

        try
        {
            using Process process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch
        {
            return $"pid-{processId}";
        }
    }

    private static int NormalizePort(int value) =>
        value is >= 0 and <= 65535 ? value : 0;

    private static bool IsIpv6(EtwNetworkEvent item) =>
        item.Protocol.EndsWith('6') ||
        item.LocalAddress.Contains(':', StringComparison.Ordinal) ||
        item.RemoteAddress.Contains(':', StringComparison.Ordinal);

    private static string Escape(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal)
             .Replace("\r", " ", StringComparison.Ordinal)
             .Replace("\n", " ", StringComparison.Ordinal);
}
