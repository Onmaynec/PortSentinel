using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
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

        if (extension == "json")
        {
            var payload = new
            {
                schemaVersion = 1,
                backend = capture.Mode.ToString(),
                capture.StartedAt,
                capture.EndedAt,
                capture.Status,
                capture.Failure,
                capture.ConnectCount,
                capture.AcceptCount,
                capture.DisconnectCount,
                capture.RetransmitCount,
                privacy = "No packet payload, HTTP body, cookies, tokens or decrypted TLS content is captured.",
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
            .AppendLine($"- **Connect / Accept / Disconnect / Retransmit:** {capture.ConnectCount} / {capture.AcceptCount} / {capture.DisconnectCount} / {capture.RetransmitCount}")
            .AppendLine()
            .AppendLine("> PortSentinel does not capture packet payload, HTTP body, cookies, tokens, or decrypted TLS content.")
            .AppendLine()
            .AppendLine("| Time | Kind | Process | PID | Protocol | Local | Remote | Note |")
            .AppendLine("|---|---|---|---:|---|---|---|---|");

        foreach (EtwNetworkEvent item in capture.Events)
        {
            text.AppendLine(
                $"| {item.Timestamp:HH:mm:ss.fff} | {Escape(item.Kind)} | {Escape(item.ProcessName)} | {item.ProcessId} | {item.Protocol} | {Escape(item.LocalEndpoint)} | {Escape(item.RemoteEndpoint)} | {Escape(item.Note)} |");
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

        void Add(
            string kind,
            int processId,
            int sourceAddress,
            int sourcePort,
            int destinationAddress,
            int destinationPort,
            bool inbound = false)
        {
            string source = FormatIpv4(sourceAddress);
            string destination = FormatIpv4(destinationAddress);
            int sourcePortValue = FormatPort(sourcePort);
            int destinationPortValue = FormatPort(destinationPort);

            events.Enqueue(new EtwNetworkEvent(
                Interlocked.Increment(ref sequence),
                DateTimeOffset.Now,
                kind,
                processId,
                ResolveProcessName(processId),
                "TCP4",
                inbound ? destination : source,
                inbound ? destinationPortValue : sourcePortValue,
                inbound ? source : destination,
                inbound ? sourcePortValue : destinationPortValue,
                kind == "RETRANSMIT"
                    ? "Повторная передача TCP-сегмента; возможна потеря пакета или congestion."
                    : "Kernel ETW metadata; packet payload не собирается."));
        }

        session.Source.Kernel.TcpIpConnectIPV4 += data =>
            Add("CONNECT", data.PID, data.saddr, data.sport, data.daddr, data.dport);
        session.Source.Kernel.TcpIpAcceptIPV4 += data =>
            Add("ACCEPT", data.PID, data.saddr, data.sport, data.daddr, data.dport, inbound: true);
        session.Source.Kernel.TcpIpDisconnectIPV4 += data =>
            Add("DISCONNECT", data.ProcessID, data.saddr, data.sport, data.daddr, data.dport);
        session.Source.Kernel.TcpIpRetransmitIPV4 += data =>
            Add("RETRANSMIT", data.ProcessID, data.saddr, data.sport, data.daddr, data.dport);

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
            $"Kernel ETW capture завершена за {duration.TotalSeconds:0} сек.",
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
        {
            return "System";
        }

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

    private static string FormatIpv4(int value) =>
        new IPAddress(BitConverter.GetBytes(value)).ToString();

    private static int FormatPort(int value)
    {
        ushort raw = unchecked((ushort)value);
        return unchecked((ushort)IPAddress.NetworkToHostOrder(unchecked((short)raw)));
    }

    private static string Escape(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal)
             .Replace("\r", " ", StringComparison.Ordinal)
             .Replace("\n", " ", StringComparison.Ordinal);
}
