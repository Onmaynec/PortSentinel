using PortSentinel.Models;
using PortSentinel.Services;
using PortSentinel.UI;

namespace PortSentinel.App;

internal sealed class PortSentinelV57App
{
    private readonly Terminal _terminal;
    private readonly EtwTelemetryService _etw;
    private readonly TelemetryArchiveService _archive;
    private readonly InstallerWatchService _watch;
    private readonly PortSentinelV56App _timelineExplorer;
    private int _selected;

    private static readonly MenuItem[] Menu =
    [
        new("1", "STANDARD WATCH", "8-sec baseline + 30-sec installer watch", ConsoleColor.Cyan),
        new("2", "DEEP WATCH", "10-sec baseline + 60-sec installer watch", ConsoleColor.Magenta),
        new("3", "LATEST PAIR", "Analyze the latest two archived captures", ConsoleColor.Green),
        new("4", "TIMELINE EXPLORER", "Paged archive browser and filters v0.5.6", ConsoleColor.Yellow),
        new("0", "EXIT", "Безопасно закрыть PortSentinel", ConsoleColor.Red)
    ];

    public PortSentinelV57App(
        Terminal terminal,
        EtwTelemetryService etw,
        TelemetryArchiveService archive,
        InstallerWatchService watch,
        PortSentinelV56App timelineExplorer)
    {
        _terminal = terminal;
        _etw = etw;
        _archive = archive;
        _watch = watch;
        _timelineExplorer = timelineExplorer;
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
        _terminal.WriteLine($"  v{Program.Version}  •  INSTALLER WATCH", ConsoleColor.DarkGray);
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
        "1" => GuidedWatchAsync(8, 30, token),
        "2" => GuidedWatchAsync(10, 60, token),
        "3" => LatestPairAsync(token),
        "4" => _timelineExplorer.RunAsync(token),
        _ => Task.CompletedTask
    };

    private async Task GuidedWatchAsync(
        int baselineSeconds,
        int watchSeconds,
        CancellationToken token)
    {
        string hint = PromptText(
            "INSTALLER PROCESS HINT",
            "Optional process fragment, for example setup, installer or appname");

        _terminal.Clear();
        Header(
            "INSTALLER WATCH — BASELINE",
            $"Baseline {baselineSeconds}s • Watch {watchSeconds}s • Hint: {(string.IsNullOrWhiteSpace(hint) ? "none" : hint)}");
        _terminal.Box(
        [
            "1. Закройте лишние приложения и не запускайте установщик.",
            "2. PortSentinel сначала запишет baseline network metadata.",
            "3. Затем запустите установщик вручную и сразу начните watch.",
            "4. PortSentinel не запускает EXE и не изменяет систему.",
            "5. Attribution остаётся эвристическим и может включать background traffic."
        ], "WORKFLOW", ConsoleColor.Cyan);
        _terminal.Rule();
        _terminal.WriteLine("  Enter — записать baseline   Esc/Q — отмена", ConsoleColor.DarkGray);
        ConsoleKey start = Console.ReadKey(true).Key;
        if (start is ConsoleKey.Escape or ConsoleKey.Q)
            return;

        EtwCaptureResult baselineResult = await _terminal.RunWithSpinnerAsync(
            $"Installer baseline: {baselineSeconds} секунд",
            _etw.CaptureAsync(TimeSpan.FromSeconds(baselineSeconds), token));
        long baselineId = await _archive.SaveCaptureAsync(baselineResult, token);
        TelemetryCapture? baseline = await _archive.LoadCaptureAsync(baselineId, token);
        if (baseline is null)
        {
            Message("INSTALLER WATCH", "Baseline сохранён, но не удалось его перечитать.", ConsoleColor.Red);
            return;
        }

        _terminal.Clear();
        Header("INSTALLER WATCH — READY", $"Baseline #{baselineId} сохранён");
        _terminal.Box(
        [
            "Запустите установщик вручную.",
            "Вернитесь в PortSentinel и сразу нажмите Enter.",
            $"После Enter начнётся watch capture длительностью {watchSeconds} секунд.",
            "Не закрывайте установщик до окончания capture, если это безопасно."
        ], "MANUAL START", ConsoleColor.Yellow);
        _terminal.Rule();
        _terminal.WriteLine("  Enter — начать watch   Esc/Q — оставить baseline и выйти", ConsoleColor.DarkGray);
        ConsoleKey watchStart = Console.ReadKey(true).Key;
        if (watchStart is ConsoleKey.Escape or ConsoleKey.Q)
            return;

        EtwCaptureResult watchResult = await _terminal.RunWithSpinnerAsync(
            $"Installer watch: {watchSeconds} секунд",
            _etw.CaptureAsync(TimeSpan.FromSeconds(watchSeconds), token));
        long watchId = await _archive.SaveCaptureAsync(watchResult, token);
        TelemetryCapture? watchCapture = await _archive.LoadCaptureAsync(watchId, token);
        if (watchCapture is null)
        {
            Message("INSTALLER WATCH", "Watch capture сохранён, но не удалось его перечитать.", ConsoleColor.Red);
            return;
        }

        InstallerWatchReport report = _watch.Analyze(baseline, watchCapture, hint);
        await ShowReportAsync(report, token);
    }

    private async Task LatestPairAsync(CancellationToken token)
    {
        IReadOnlyList<TelemetryCaptureSummary> captures = await _archive.ListCapturesAsync(2, token);
        if (captures.Count < 2)
        {
            Message("LATEST PAIR", "Для анализа нужны минимум две capture-сессии.", ConsoleColor.Yellow);
            return;
        }

        TelemetryCapture? watch = await _archive.LoadCaptureAsync(captures[0].Id, token);
        TelemetryCapture? baseline = await _archive.LoadCaptureAsync(captures[1].Id, token);
        if (baseline is null || watch is null)
        {
            Message("LATEST PAIR", "Не удалось загрузить выбранную пару captures.", ConsoleColor.Red);
            return;
        }

        string hint = PromptText(
            "LATEST PAIR PROCESS HINT",
            "Optional process fragment; empty means no prioritization");
        await ShowReportAsync(_watch.Analyze(baseline, watch, hint), token);
    }

    private async Task ShowReportAsync(
        InstallerWatchReport report,
        CancellationToken token)
    {
        int selected = 0;

        while (true)
        {
            selected = Math.Clamp(selected, 0, Math.Max(0, report.Processes.Count - 1));
            _terminal.Clear();
            Header(
                $"INSTALLER WATCH #{report.BaselineCaptureId} → #{report.WatchCaptureId}",
                $"Processes: {report.AddedProcessCount} • Endpoints: {report.AddedEndpointCount} • Signals: {report.FailureSignalCount}");
            _terminal.WriteLine(
                $"  Backend: {report.BaselineMode} → {report.WatchMode}  •  Events: {report.BaselineEventCount} → {report.WatchEventCount}",
                ConsoleColor.DarkCyan);
            _terminal.WriteLine(
                $"  Hint: {(string.IsNullOrWhiteSpace(report.ProcessHint) ? "not set" : report.ProcessHint)}  •  Added metadata: {report.AddedEvents.Count}",
                string.IsNullOrWhiteSpace(report.ProcessHint) ? ConsoleColor.DarkGray : ConsoleColor.Magenta);
            _terminal.WriteLine("  Diagnostic comparison only; installer attribution is not proven.", ConsoleColor.Yellow);
            _terminal.WriteLine();

            DrawProcesses(report.Processes, selected);
            _terminal.Rule();
            _terminal.WriteLine(
                report.Processes.Count == 0
                    ? "  E — added events   L — limitations   J/M — export   Q/Esc — back"
                    : "  ↑/↓ select  Enter process  E events  L limitations  J/M export  Q/Esc back",
                ConsoleColor.DarkGray);

            ConsoleKey key = Console.ReadKey(true).Key;
            if (key is ConsoleKey.Q or ConsoleKey.Escape) return;
            if (key == ConsoleKey.E)
            {
                ShowEvents(report.AddedEvents);
                continue;
            }
            if (key == ConsoleKey.L)
            {
                ShowLimitations(report.Limitations);
                continue;
            }
            if (key is ConsoleKey.J or ConsoleKey.M)
            {
                string path = await _watch.ExportAsync(
                    report,
                    key == ConsoleKey.J ? "json" : "markdown",
                    token);
                Message("INSTALLER WATCH EXPORT", $"Отчёт сохранён:\n{path}", ConsoleColor.Green);
                continue;
            }
            if (report.Processes.Count == 0) continue;
            if (key == ConsoleKey.UpArrow)
                selected = (selected - 1 + report.Processes.Count) % report.Processes.Count;
            else if (key == ConsoleKey.DownArrow)
                selected = (selected + 1) % report.Processes.Count;
            else if (key == ConsoleKey.Enter)
                ShowProcess(report.Processes[selected], report.AddedEvents);
        }
    }

    private void DrawProcesses(
        IReadOnlyList<InstallerWatchProcessSummary> processes,
        int selected)
    {
        if (processes.Count == 0)
        {
            _terminal.WriteLine("  Новые process/network fingerprints не обнаружены.", ConsoleColor.Green);
            return;
        }

        int rows = MaxRows();
        int start = Math.Clamp(selected - rows / 2, 0, Math.Max(0, processes.Count - rows));
        foreach ((InstallerWatchProcessSummary process, int index) in processes
                     .Skip(start)
                     .Take(rows)
                     .Select((process, i) => (process, start + i)))
        {
            bool active = index == selected;
            ConsoleColor accent = process.MatchesHint
                ? ConsoleColor.Magenta
                : process.FailureSignalCount > 0
                    ? ConsoleColor.Yellow
                    : ConsoleColor.Cyan;
            _terminal.Write(active ? "  ▶ " : "    ", active ? accent : ConsoleColor.DarkGray);
            _terminal.Write(Terminal.PadOrTrim(process.ProcessName, 24),
                active ? ConsoleColor.White : ConsoleColor.Gray);
            _terminal.Write(process.MatchesHint ? " HINT " : "      ", ConsoleColor.Magenta);
            _terminal.WriteLine(
                $" Events:{process.AddedEventCount,4} Endpoints:{process.UniqueRemoteEndpoints,3} TCP:{process.TcpEventCount,3} UDP:{process.UdpEventCount,3} Signals:{process.FailureSignalCount,3}",
                active ? ConsoleColor.White : ConsoleColor.DarkGray);
        }
    }

    private void ShowProcess(
        InstallerWatchProcessSummary process,
        IReadOnlyList<EtwNetworkEvent> events)
    {
        EtwNetworkEvent[] matching = events
            .Where(item => item.ProcessName.Equals(process.ProcessName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        _terminal.Clear();
        Header("INSTALLER PROCESS CANDIDATE", process.ProcessName);
        _terminal.Box(
        [
            $"Hint match:       {(process.MatchesHint ? "yes" : "no")}",
            $"Added events:     {process.AddedEventCount}",
            $"Remote endpoints: {process.UniqueRemoteEndpoints}",
            $"TCP / UDP:        {process.TcpEventCount} / {process.UdpEventCount}",
            $"Failure signals:  {process.FailureSignalCount}",
            "Attribution:      heuristic process-name and metadata correlation"
        ], "PROCESS SUMMARY", process.MatchesHint ? ConsoleColor.Magenta : ConsoleColor.Cyan);
        _terminal.WriteLine();
        foreach (EtwNetworkEvent item in matching.Take(Math.Max(3, MaxRows() - 8)))
        {
            _terminal.Write($"  {item.Kind,-12}", EventColor(item.Kind));
            _terminal.Write($" {item.Protocol,-6}", ConsoleColor.DarkCyan);
            _terminal.WriteLine($" {Terminal.PadOrTrim(item.RemoteEndpoint, Math.Max(18, _terminal.Width - 25))}", ConsoleColor.Gray);
        }
        if (matching.Length > Math.Max(3, MaxRows() - 8))
            _terminal.WriteLine($"  … ещё {matching.Length - Math.Max(3, MaxRows() - 8)} events", ConsoleColor.DarkGray);
        Back();
    }

    private void ShowEvents(IReadOnlyList<EtwNetworkEvent> events)
    {
        int selected = 0;
        while (true)
        {
            selected = Math.Clamp(selected, 0, Math.Max(0, events.Count - 1));
            _terminal.Clear();
            Header("ADDED INSTALLER-WATCH METADATA", $"Unique fingerprints: {events.Count}");

            if (events.Count == 0)
            {
                _terminal.WriteLine("  Added events отсутствуют.", ConsoleColor.Green);
            }
            else
            {
                int rows = MaxRows();
                int start = Math.Clamp(selected - rows / 2, 0, Math.Max(0, events.Count - rows));
                foreach ((EtwNetworkEvent item, int index) in events.Skip(start).Take(rows)
                             .Select((item, i) => (item, start + i)))
                {
                    bool active = index == selected;
                    ConsoleColor accent = EventColor(item.Kind);
                    _terminal.Write(active ? "  ▶ " : "    ", active ? accent : ConsoleColor.DarkGray);
                    _terminal.Write(Terminal.PadOrTrim(item.Kind, 12), accent);
                    _terminal.Write(Terminal.PadOrTrim(item.Protocol, 7), ConsoleColor.DarkCyan);
                    _terminal.Write(Terminal.PadOrTrim(item.ProcessName, 20), active ? ConsoleColor.White : ConsoleColor.Gray);
                    _terminal.WriteLine(
                        $" {Terminal.PadOrTrim(item.RemoteEndpoint, Math.Max(18, _terminal.Width - 48))}",
                        active ? ConsoleColor.Cyan : ConsoleColor.DarkGray);
                }
            }

            _terminal.Rule();
            _terminal.WriteLine("  ↑/↓ select   Enter details   Q/Esc back", ConsoleColor.DarkGray);
            ConsoleKey key = Console.ReadKey(true).Key;
            if (key is ConsoleKey.Q or ConsoleKey.Escape) return;
            if (events.Count == 0) continue;
            if (key == ConsoleKey.UpArrow)
                selected = (selected - 1 + events.Count) % events.Count;
            else if (key == ConsoleKey.DownArrow)
                selected = (selected + 1) % events.Count;
            else if (key == ConsoleKey.Enter)
                ShowEvent(events[selected]);
        }
    }

    private void ShowEvent(EtwNetworkEvent item)
    {
        _terminal.Clear();
        Header($"INSTALLER WATCH EVENT #{item.Sequence}", item.Kind);
        _terminal.Box(
        [
            $"Time:       {item.Timestamp:O}",
            $"Process:    {item.ProcessName}",
            $"PID:        {item.ProcessId}",
            $"Protocol:   {item.Protocol}",
            $"Local:      {item.LocalEndpoint}",
            $"Remote:     {item.RemoteEndpoint}",
            $"Note:       {item.Note}",
            "Attribution: diagnostic metadata only"
        ], "EVENT METADATA", EventColor(item.Kind));
        Back();
    }

    private void ShowLimitations(IReadOnlyList<string> limitations)
    {
        _terminal.Clear();
        Header("INSTALLER WATCH LIMITATIONS", $"Items: {limitations.Count}");
        foreach (string limitation in limitations.Take(MaxRows()))
            _terminal.WriteLine($"  • {Terminal.PadOrTrim(limitation, Math.Max(20, _terminal.Width - 6))}", ConsoleColor.Gray);
        Back();
    }

    private string PromptText(string title, string prompt)
    {
        _terminal.Clear();
        Header(title, prompt);
        _terminal.Write("  > ", ConsoleColor.Cyan);
        try
        {
            Console.CursorVisible = true;
            return Console.ReadLine()?.Trim() ?? string.Empty;
        }
        finally
        {
            Console.CursorVisible = false;
        }
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

    private static ConsoleColor EventColor(string kind) => kind switch
    {
        "FAIL" => ConsoleColor.Red,
        "RETRANSMIT" or "RECONNECT" => ConsoleColor.Yellow,
        "CONNECT" or "UDP_SEND" => ConsoleColor.Cyan,
        "ACCEPT" or "UDP_RECV" => ConsoleColor.Green,
        "DISCONNECT" => ConsoleColor.DarkYellow,
        _ => ConsoleColor.Gray
    };
}
