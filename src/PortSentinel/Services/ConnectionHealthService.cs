using System.Text;
using System.Text.Json;
using PortSentinel.Models;

namespace PortSentinel.Services;

internal sealed class ConnectionHealthService
{
    private readonly string _reportsDirectory;

    public ConnectionHealthService(string reportsDirectory)
    {
        _reportsDirectory = reportsDirectory;
        Directory.CreateDirectory(_reportsDirectory);
    }

    public ConnectionHealthReport Analyze(EtwCaptureResult capture, long? captureId = null) =>
        AnalyzeCore(
            captureId,
            capture.StartedAt,
            capture.EndedAt,
            capture.Mode,
            capture.Status,
            capture.Events);

    public ConnectionHealthReport Analyze(TelemetryCapture capture) =>
        AnalyzeCore(
            capture.Summary.Id,
            capture.Summary.StartedAt,
            capture.Summary.EndedAt,
            capture.Summary.Mode,
            capture.Summary.Status,
            capture.Events);

    public async Task<string> ExportAsync(
        ConnectionHealthReport report,
        string format,
        CancellationToken cancellationToken)
    {
        string extension = format.Equals("json", StringComparison.OrdinalIgnoreCase) ? "json" : "md";
        string id = report.CaptureId?.ToString() ?? report.StartedAt.ToString("yyyyMMdd-HHmmss");
        string path = Path.Combine(_reportsDirectory, $"connection-health-{id}.{extension}");

        if (extension == "json")
        {
            var payload = new
            {
                schemaVersion = 1,
                report.CaptureId,
                report.StartedAt,
                report.EndedAt,
                backend = report.Mode.ToString(),
                report.SourceStatus,
                report.EventCount,
                report.FailCount,
                report.ReconnectCount,
                report.RetransmitCount,
                report.Score,
                report.Grade,
                limitation = "Connection Health is explainable diagnostic analysis, not a malware or ownership verdict.",
                report.Findings
            };
            await File.WriteAllTextAsync(
                path,
                JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);
            return path;
        }

        var text = new StringBuilder()
            .AppendLine("# PortSentinel Connection Health")
            .AppendLine()
            .AppendLine($"- **Capture:** {(report.CaptureId?.ToString() ?? "live")}")
            .AppendLine($"- **Backend:** {report.Mode}")
            .AppendLine($"- **Window:** {report.StartedAt:O} — {report.EndedAt:O}")
            .AppendLine($"- **Events:** {report.EventCount}")
            .AppendLine($"- **Fail / Reconnect / Retransmit:** {report.FailCount} / {report.ReconnectCount} / {report.RetransmitCount}")
            .AppendLine($"- **Health score:** {report.Score}/100 ({report.Grade})")
            .AppendLine()
            .AppendLine("> Findings are bounded to the capture window and are not a malware verdict.")
            .AppendLine()
            .AppendLine("| Rule | Severity | Confidence | Process | Remote | Count | Finding |")
            .AppendLine("|---|---|---:|---|---|---:|---|");

        foreach (ConnectionHealthFinding finding in report.Findings)
        {
            text.AppendLine(
                $"| {Escape(finding.RuleId)} | {finding.Severity} | {finding.Confidence}% | {Escape(finding.ProcessName)} | {Escape(finding.RemoteEndpoint)} | {finding.Count} | {Escape(finding.Title)} |")
                .AppendLine($"  - Evidence: {Escape(finding.Evidence)}")
                .AppendLine($"  - Limitation: {Escape(finding.Limitation)}");
        }

        await File.WriteAllTextAsync(path, text.ToString(), cancellationToken);
        return path;
    }

    private static ConnectionHealthReport AnalyzeCore(
        long? captureId,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        EtwBackendMode mode,
        string status,
        IReadOnlyList<EtwNetworkEvent> events)
    {
        var findings = new List<ConnectionHealthFinding>();

        foreach (IGrouping<string, EtwNetworkEvent> group in events
                     .Where(item => item.Kind == "FAIL")
                     .GroupBy(item => item.ProcessName, StringComparer.OrdinalIgnoreCase))
        {
            string evidence = string.Join("; ", group
                .Select(item => item.Note)
                .Distinct(StringComparer.Ordinal)
                .Take(3));
            findings.Add(new ConnectionHealthFinding(
                "PS-HEALTH-001",
                ConnectionHealthSeverity.High,
                95,
                "Kernel TCP failure events",
                evidence,
                "Numeric failure codes are preserved as evidence; PortSentinel does not guess undocumented semantics.",
                group.Key,
                "—",
                group.Count()));
        }

        foreach (IGrouping<string, EtwNetworkEvent> group in events
                     .Where(item => item.Kind == "RETRANSMIT")
                     .GroupBy(EndpointKey, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() >= 3))
        {
            EtwNetworkEvent sample = group.First();
            int count = group.Count();
            findings.Add(new ConnectionHealthFinding(
                "PS-HEALTH-002",
                count >= 10 ? ConnectionHealthSeverity.High : ConnectionHealthSeverity.Medium,
                80,
                "TCP retransmit burst",
                $"{count} retransmit events for {sample.ProcessName} → {sample.RemoteEndpoint} during one capture window.",
                "Retransmits can be caused by packet loss, Wi-Fi quality, congestion, middleboxes, or normal recovery.",
                sample.ProcessName,
                sample.RemoteEndpoint,
                count));
        }

        foreach (IGrouping<string, EtwNetworkEvent> group in events
                     .Where(item => item.Kind == "RECONNECT")
                     .GroupBy(EndpointKey, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() >= 2))
        {
            EtwNetworkEvent sample = group.First();
            int count = group.Count();
            findings.Add(new ConnectionHealthFinding(
                "PS-HEALTH-003",
                count >= 5 ? ConnectionHealthSeverity.High : ConnectionHealthSeverity.Medium,
                85,
                "Repeated kernel reconnects",
                $"{count} reconnect events for {sample.ProcessName} → {sample.RemoteEndpoint}.",
                "Reconnects may be expected for roaming clients, short-lived services, proxies, or application retry logic.",
                sample.ProcessName,
                sample.RemoteEndpoint,
                count));
        }

        foreach (IGrouping<string, EtwNetworkEvent> group in events
                     .Where(item => item.Kind == "CONNECT")
                     .GroupBy(EndpointKey, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() >= 6))
        {
            EtwNetworkEvent sample = group.First();
            int count = group.Count();
            findings.Add(new ConnectionHealthFinding(
                "PS-HEALTH-004",
                count >= 15 ? ConnectionHealthSeverity.Medium : ConnectionHealthSeverity.Low,
                65,
                "Rapid repeated connections",
                $"{count} connect events for {sample.ProcessName} → {sample.RemoteEndpoint}.",
                "Connection pooling, browsers, launchers, update agents, and service discovery can create legitimate bursts.",
                sample.ProcessName,
                sample.RemoteEndpoint,
                count));
        }

        HashSet<string> observedConnects = events
            .Where(item => item.Kind is "CONNECT" or "RECONNECT")
            .Select(EndpointKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        EtwNetworkEvent[] orphanDisconnects = events
            .Where(item => item.Kind == "DISCONNECT" && !observedConnects.Contains(EndpointKey(item)))
            .Take(1000)
            .ToArray();
        if (orphanDisconnects.Length >= 3)
        {
            findings.Add(new ConnectionHealthFinding(
                "PS-HEALTH-005",
                ConnectionHealthSeverity.Info,
                45,
                "Disconnects without an observed connect",
                $"{orphanDisconnects.Length} disconnect events had no matching connect inside this capture window.",
                "The connection may have started before capture began; this is a capture-boundary indicator, not a failure verdict.",
                "multiple",
                "multiple",
                orphanDisconnects.Length));
        }

        if (mode == EtwBackendMode.SnapshotFallback)
        {
            findings.Add(new ConnectionHealthFinding(
                "PS-HEALTH-006",
                ConnectionHealthSeverity.Info,
                100,
                "Snapshot fallback limits lifecycle diagnosis",
                "Kernel fail, reconnect, and retransmit events are unavailable in snapshot mode.",
                "Run elevated and ensure the kernel logger is available for lifecycle diagnostics.",
                "system",
                "—",
                1));
        }

        if (findings.Count == 0)
        {
            findings.Add(new ConnectionHealthFinding(
                "PS-HEALTH-000",
                ConnectionHealthSeverity.Info,
                70,
                "No health patterns crossed the configured thresholds",
                $"Analyzed {events.Count} events in the current capture window.",
                "A quiet capture does not prove that the network or application is healthy outside the observed window.",
                "system",
                "—",
                0));
        }

        findings = findings
            .OrderByDescending(item => item.Severity)
            .ThenByDescending(item => item.Count)
            .ThenBy(item => item.RuleId, StringComparer.Ordinal)
            .ToList();

        int penalty = findings.Sum(item => item.Severity switch
        {
            ConnectionHealthSeverity.High => 25,
            ConnectionHealthSeverity.Medium => 12,
            ConnectionHealthSeverity.Low => 5,
            _ => 0
        });
        int score = Math.Max(0, 100 - Math.Min(100, penalty));
        string grade = score switch
        {
            >= 90 => "STABLE",
            >= 70 => "OBSERVE",
            >= 40 => "DEGRADED",
            _ => "CRITICAL"
        };

        return new ConnectionHealthReport(
            captureId,
            startedAt,
            endedAt,
            mode,
            status,
            events.Count,
            events.Count(item => item.Kind == "FAIL"),
            events.Count(item => item.Kind == "RECONNECT"),
            events.Count(item => item.Kind == "RETRANSMIT"),
            score,
            grade,
            findings);
    }

    private static string EndpointKey(EtwNetworkEvent item) => string.Join('|',
        item.ProcessName.Trim(),
        item.Protocol.Trim(),
        item.RemoteAddress.Trim(),
        item.RemotePort);

    private static string Escape(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal)
             .Replace("\r", " ", StringComparison.Ordinal)
             .Replace("\n", " ", StringComparison.Ordinal);
}
