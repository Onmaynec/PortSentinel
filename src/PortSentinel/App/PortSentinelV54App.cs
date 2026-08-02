using PortSentinel.Models;
using PortSentinel.Services;
using PortSentinel.UI;

namespace PortSentinel.App;

internal sealed class PortSentinelV54App
{
    private readonly Terminal _terminal;
    private readonly EtwTelemetryService _etw;
    private readonly TelemetryArchiveService _archive;
    private readonly ConnectionHealthService _health;
    private readonly PortSentinelV53App _archiveOperations;
    private int _selected;

    private static readonly MenuItem[] Menu =
    [
        new("1", "CAPTURE & HEALTH", "15-секундный ETW/fallback capture, archive и health report", ConsoleColor.Cyan),
        new("2", "LATEST HEALTH", "Анализ последней сохранённой capture-сессии", ConsoleColor.Green),
        new("3", "ARCHIVE HEALTH", "Выбор capture из истории и explainable findings", ConsoleColor.Magenta),
        new("4", "ARCHIVE OPERATIONS", "Profiles, search, comparison и retention v0.5.3", ConsoleColor.Yellow),
        new("0", "EXIT", "Безопасно закрыть PortSentinel", ConsoleColor.Red)
    ];

    public PortSentinelV54App(
        Terminal terminal,
        EtwTelemetryService etw,
        TelemetryArchiveService archive,
        ConnectionHealthService health,
        PortSentinelV53App archiveOperations)
    {
        _terminal = terminal;
        _etw = etw;
        _archive = archive;
        _health = health;
        _archiveOperations = archiveOperations;
    }

    public async Task RunAsync(CancellationToken token)
    {
        await _terminal.RunIntroAsync();

        while (!token.IsCancellationRequested)
        {
            DrawMenu();
            ConsoleKeyInfo key = Console.ReadKey(true);
            if (key.Key is ConsoleKey.UpArrow or ConsoleKey.W)
                _selected = (_selected - 1 + Menu.Length) % Menu.Length;
            else if (key.Key is ConsoleKey.DownArrow or ConsoleKey.S)
                _selected = (_selected + 1) % Menu.Length;
            else if (key.Key is ConsoleKey.Q or ConsoleKey.Escape)
                return;
            else if (key.Key == ConsoleKey.Enter)
            {
                if (Menu[_selected].Key == "0") return;
                await OpenAsync(Menu[_selected].Key, token);
            }
            else if (char.IsDigit(key.KeyChar))
            {
                int index = Array.FindIndex(Menu, item => item.Key[0] == key.KeyChar);
                if (index < 0) continue;
                _selected = index;
                if (Menu[index].Key == "0") return;
                await OpenAsync(Menu[index].Key, token);
            }
        }
    }

    private void DrawMenu()
    {
        _terminal.Clear();
        AsciiLogo.Draw(_terminal);
        _terminal.WriteLine($"  v{Program.Version}  •  CONNECTION HEALTH", ConsoleColor.DarkGray);
        _terminal.Rule("PORTSENTINEL CONTROL NODE");

        foreach ((MenuItem item, int index) in Menu.Select((item, index) => (item, index)))
        {
            bool active = index == _selected;
            _terminal.Write(active ? "  ▶ " : "    ", active ? item.Accent : ConsoleColor.DarkGray);
            _terminal.Write($"[{item.Key}] {Terminal.PadOrTrim(item.Title, 24)}",
                active ? ConsoleColor.White : item.Accent);
            _terminal.WriteLine($"  {item.Description}",
                active ? ConsoleColor.Gray : ConsoleColor.DarkGray);
        }

        _terminal.Rule();
        _terminal.WriteLine("  ↑/↓ или W/S — выбор   Enter — открыть   Q/Esc — выход", ConsoleColor.DarkGray);
    }

    private Task OpenAsync(string key, CancellationToken token) => key switch
    {
        "1" => CaptureAndAnalyzeAsync(token),
        "2" => LatestHealthAsync(token),
        "3" => ArchiveHealthAsync(token),
        "4" => _archiveOperations.RunAsync(token),
        _ => Task.CompletedTask
    };

    private async Task CaptureAndAnalyzeAsync(CancellationToken token)
    {
        EtwCaptureResult capture = await _terminal.RunWithSpinnerAsync(
            "Connection Health capture: 15 секунд",
            _etw.CaptureAsync(TimeSpan.FromSeconds(15), token));
        long id = await _archive.SaveCaptureAsync(capture, token);
        ConnectionHealthReport report = _health.Analyze(capture, id);
        await ShowReportAsync(report, token);
    }

    private async Task LatestHealthAsync(CancellationToken token)
    {
        IReadOnlyList<TelemetryCaptureSummary> summaries = await _archive.ListCapturesAsync(1, token);
        if (summaries.Count == 0)
        {
            Message("LATEST HEALTH", "Telemetry archive пуст. Сначала выполните Capture & Health.", ConsoleColor.Yellow);
            return;
        }

        TelemetryCapture? capture = await _archive.LoadCaptureAsync(summaries[0].Id, token);
        if (capture is null)
        {
            Message("LATEST HEALTH", "Не удалось загрузить последнюю capture-сессию.", ConsoleColor.Red);
            return;
        }

        await ShowReportAsync(_health.Analyze(capture), token);
    }

    private async Task ArchiveHealthAsync(CancellationToken token)
    {
        int selected = 0;

        while (true)
        {
            IReadOnlyList<TelemetryCaptureSummary> captures = await _archive.ListCapturesAsync(50, token);
            if (captures.Count == 0)
            {
                Message("ARCHIVE HEALTH", "Telemetry archive пуст.", ConsoleColor.Yellow);
                return;
            }

            selected = Math.Clamp(selected, 0, captures.Count - 1);
            _terminal.Clear();
            Header("ARCHIVE HEALTH", $"Выберите capture для анализа • Records: {captures.Count}");
            int rows = MaxRows();
            int start = Math.Clamp(selected - rows / 2, 0, Math.Max(0, captures.Count - rows));

            foreach ((TelemetryCaptureSummary capture, int index) in captures.Skip(start).Take(rows)
                         .Select((capture, i) => (capture, start + i)))
            {
                bool active = index == selected;
                ConsoleColor accent = capture.Mode == EtwBackendMode.KernelEtw
                    ? ConsoleColor.Cyan
                    : ConsoleColor.Yellow;
                _terminal.Write(active ? "  ▶ " : "    ", active ? accent : ConsoleColor.DarkGray);
                _terminal.Write($"#{capture.Id,-5} ", active ? ConsoleColor.White : ConsoleColor.Gray);
                _terminal.Write($"{capture.StartedAt:dd.MM HH:mm:ss} ", ConsoleColor.DarkGray);
                _terminal.Write(Terminal.PadOrTrim(capture.Mode.ToString(), 18), accent);
                _terminal.WriteLine(
                    $" Events:{capture.EventCount,5}  Retransmit:{capture.RetransmitCount,4}",
                    active ? ConsoleColor.White : ConsoleColor.DarkGray);
            }

            _terminal.Rule();
            _terminal.WriteLine("  ↑/↓ — выбор   Enter — health report   R — обновить   Q/Esc — назад", ConsoleColor.DarkGray);
            ConsoleKey key = Console.ReadKey(true).Key;
            if (key is ConsoleKey.Q or ConsoleKey.Escape) return;
            if (key == ConsoleKey.UpArrow)
                selected = (selected - 1 + captures.Count) % captures.Count;
            else if (key == ConsoleKey.DownArrow)
                selected = (selected + 1) % captures.Count;
            else if (key == ConsoleKey.R)
                continue;
            else if (key == ConsoleKey.Enter)
            {
                TelemetryCapture? capture = await _archive.LoadCaptureAsync(captures[selected].Id, token);
                if (capture is not null)
                    await ShowReportAsync(_health.Analyze(capture), token);
            }
        }
    }

    private async Task ShowReportAsync(ConnectionHealthReport report, CancellationToken token)
    {
        int selected = 0;

        while (true)
        {
            selected = Math.Clamp(selected, 0, Math.Max(0, report.Findings.Count - 1));
            _terminal.Clear();
            Header(
                report.CaptureId is long id ? $"CONNECTION HEALTH #{id}" : "CONNECTION HEALTH",
                $"Score: {report.Score}/100 {report.Grade}  •  Backend: {report.Mode}  •  Events: {report.EventCount}");
            _terminal.WriteLine(
                $"  Fail: {report.FailCount}  Reconnect: {report.ReconnectCount}  Retransmit: {report.RetransmitCount}  Findings: {report.Findings.Count}",
                ScoreColor(report.Score));
            _terminal.WriteLine("  Explainable diagnostic analysis; malware verdict не формируется.", ConsoleColor.Yellow);
            _terminal.WriteLine();

            DrawFindings(report.Findings, selected);
            _terminal.Rule();
            _terminal.WriteLine("  ↑/↓ — выбор   Enter — evidence   J/M — экспорт   Q/Esc — назад", ConsoleColor.DarkGray);

            ConsoleKey key = Console.ReadKey(true).Key;
            if (key is ConsoleKey.Q or ConsoleKey.Escape) return;
            if (key is ConsoleKey.J or ConsoleKey.M)
            {
                string path = await _health.ExportAsync(
                    report,
                    key == ConsoleKey.J ? "json" : "markdown",
                    token);
                Message("HEALTH EXPORT COMPLETE", $"Отчёт сохранён:\n{path}", ConsoleColor.Green);
                continue;
            }
            if (report.Findings.Count == 0) continue;
            if (key == ConsoleKey.UpArrow)
                selected = (selected - 1 + report.Findings.Count) % report.Findings.Count;
            else if (key == ConsoleKey.DownArrow)
                selected = (selected + 1) % report.Findings.Count;
            else if (key == ConsoleKey.Enter)
                ShowFinding(report.Findings[selected]);
        }
    }

    private void DrawFindings(IReadOnlyList<ConnectionHealthFinding> findings, int selected)
    {
        if (findings.Count == 0)
        {
            _terminal.WriteLine("  Findings отсутствуют.", ConsoleColor.Green);
            return;
        }

        int rows = MaxRows();
        int start = Math.Clamp(selected - rows / 2, 0, Math.Max(0, findings.Count - rows));
        foreach ((ConnectionHealthFinding finding, int index) in findings.Skip(start).Take(rows)
                     .Select((finding, i) => (finding, start + i)))
        {
            bool active = index == selected;
            ConsoleColor accent = SeverityColor(finding.Severity);
            _terminal.Write(active ? "  ▶ " : "    ", active ? accent : ConsoleColor.DarkGray);
            _terminal.Write(Terminal.PadOrTrim(finding.RuleId, 16), ConsoleColor.DarkGray);
            _terminal.Write(Terminal.PadOrTrim(finding.Severity.ToString().ToUpperInvariant(), 10), accent);
            _terminal.Write(Terminal.PadOrTrim(finding.ProcessName, 20), active ? ConsoleColor.White : ConsoleColor.Gray);
            _terminal.WriteLine(
                $" {Terminal.PadOrTrim(finding.Title, Math.Max(20, _terminal.Width - 54))}",
                active ? ConsoleColor.White : ConsoleColor.DarkGray);
        }
    }

    private void ShowFinding(ConnectionHealthFinding finding)
    {
        _terminal.Clear();
        Header(finding.RuleId, finding.Title);
        _terminal.Box(
        [
            $"Severity:    {finding.Severity}",
            $"Confidence:  {finding.Confidence}%",
            $"Process:     {finding.ProcessName}",
            $"Remote:      {finding.RemoteEndpoint}",
            $"Count:       {finding.Count}",
            $"Evidence:    {finding.Evidence}",
            $"Limitation:  {finding.Limitation}",
            "Verdict:      diagnostic finding only"
        ], "CONNECTION HEALTH FINDING", SeverityColor(finding.Severity));
        Back();
    }

    private void Header(string title, string subtitle)
    {
        AsciiLogo.Draw(_terminal, compact: true);
        _terminal.Rule(title);
        _terminal.WriteLine($"  {subtitle}", ConsoleColor.DarkGray);
        _terminal.WriteLine();
    }

    private void Message(string title, string text, ConsoleColor color)
    {
        _terminal.Clear();
        Header(title, string.Empty);
        _terminal.Box([text], "MESSAGE", color);
        Back();
    }

    private void Back()
    {
        _terminal.Rule();
        _terminal.WriteLine("  Нажмите любую клавишу, чтобы вернуться...", ConsoleColor.DarkGray);
        Console.ReadKey(true);
    }

    private int MaxRows() => Math.Max(5, _terminal.Height - 17);

    private static ConsoleColor SeverityColor(ConnectionHealthSeverity severity) => severity switch
    {
        ConnectionHealthSeverity.High => ConsoleColor.Red,
        ConnectionHealthSeverity.Medium => ConsoleColor.Yellow,
        ConnectionHealthSeverity.Low => ConsoleColor.DarkYellow,
        _ => ConsoleColor.Cyan
    };

    private static ConsoleColor ScoreColor(int score) => score switch
    {
        >= 90 => ConsoleColor.Green,
        >= 70 => ConsoleColor.Yellow,
        >= 40 => ConsoleColor.DarkYellow,
        _ => ConsoleColor.Red
    };
}
