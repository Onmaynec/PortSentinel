using PortSentinel.Models;
using PortSentinel.Services;
using PortSentinel.UI;

namespace PortSentinel.App;

internal sealed class PortSentinelV52App
{
    private readonly Terminal _terminal;
    private readonly EtwTelemetryService _etw;
    private readonly TelemetryArchiveService _archive;
    private readonly PortSentinelV51App _etwControlCenter;
    private int _selected;

    private static readonly MenuItem[] Menu =
    [
        new("1", "CAPTURE & ARCHIVE", "ETW/fallback capture с автоматическим сохранением в SQLite", ConsoleColor.Cyan),
        new("2", "TELEMETRY HISTORY", "История capture-сессий, события и exports", ConsoleColor.DarkCyan),
        new("3", "CAPTURE COMPARISON", "Diff двух последних capture по lifecycle fingerprint", ConsoleColor.Magenta),
        new("4", "ETW CONTROL CENTER", "Capture, capability и Extended Telemetry v0.5.1", ConsoleColor.Yellow),
        new("0", "EXIT", "Безопасно закрыть PortSentinel", ConsoleColor.Red)
    ];

    public PortSentinelV52App(
        Terminal terminal,
        EtwTelemetryService etw,
        TelemetryArchiveService archive,
        PortSentinelV51App etwControlCenter)
    {
        _terminal = terminal;
        _etw = etw;
        _archive = archive;
        _etwControlCenter = etwControlCenter;
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
        _terminal.WriteLine($"  v{Program.Version}  •  TELEMETRY ARCHIVE", ConsoleColor.DarkGray);
        _terminal.Rule("PORTSENTINEL CONTROL NODE");

        for (int i = 0; i < Menu.Length; i++)
        {
            MenuItem item = Menu[i];
            bool active = i == _selected;
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
        "1" => CaptureAndArchiveAsync(token),
        "2" => HistoryAsync(token),
        "3" => ComparisonAsync(token),
        "4" => _etwControlCenter.RunAsync(token),
        _ => Task.CompletedTask
    };

    private async Task CaptureAndArchiveAsync(CancellationToken token)
    {
        while (true)
        {
            EtwCaptureResult result = await _terminal.RunWithSpinnerAsync(
                "ETW/fallback capture: 12 секунд",
                _etw.CaptureAsync(TimeSpan.FromSeconds(12), token));
            long id = await _archive.SaveCaptureAsync(result, token);
            TelemetryCapture? capture = await _archive.LoadCaptureAsync(id, token);
            if (capture is null)
            {
                Message("TELEMETRY ARCHIVE", "Capture сохранён, но не удалось перечитать запись.", ConsoleColor.Red);
                return;
            }

            ConsoleKey action = await CaptureViewAsync(capture, token, allowRepeat: true);
            if (action != ConsoleKey.R)
                return;
        }
    }

    private async Task HistoryAsync(CancellationToken token)
    {
        int selected = 0;

        while (true)
        {
            IReadOnlyList<TelemetryCaptureSummary> captures =
                await _archive.ListCapturesAsync(100, token);
            if (captures.Count == 0)
            {
                Message("TELEMETRY HISTORY", "Архив пуст. Сначала выполните Capture & Archive.", ConsoleColor.Yellow);
                return;
            }

            selected = Math.Clamp(selected, 0, captures.Count - 1);
            _terminal.Clear();
            Header("TELEMETRY HISTORY", $"Сохранено capture-сессий: {captures.Count}");
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
                    $" E:{capture.EventCount,4} C:{capture.ConnectCount,3} A:{capture.AcceptCount,3} D:{capture.DisconnectCount,3} R:{capture.RetransmitCount,3}",
                    active ? ConsoleColor.White : ConsoleColor.DarkGray);
            }

            _terminal.Rule();
            _terminal.WriteLine("  ↑/↓ — выбор   Enter — события   J/M — экспорт   R — обновить   Q/Esc — назад", ConsoleColor.DarkGray);
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
                    await CaptureViewAsync(capture, token, allowRepeat: false);
            }
            else if (key is ConsoleKey.J or ConsoleKey.M)
            {
                TelemetryCapture? capture = await _archive.LoadCaptureAsync(captures[selected].Id, token);
                if (capture is null) continue;
                string path = await _archive.ExportCaptureAsync(
                    capture,
                    key == ConsoleKey.J ? "json" : "markdown",
                    token);
                Message("ARCHIVE EXPORT COMPLETE", $"Отчёт сохранён:\n{path}", ConsoleColor.Green);
            }
        }
    }

    private async Task ComparisonAsync(CancellationToken token)
    {
        TelemetryCaptureComparison? comparison = await _archive.CompareLatestAsync(token);
        if (comparison is null)
        {
            Message("CAPTURE COMPARISON", "Для сравнения нужны минимум две capture-сессии.", ConsoleColor.Yellow);
            return;
        }

        int selected = 0;
        while (true)
        {
            selected = Math.Clamp(selected, 0, Math.Max(0, comparison.AddedEvents.Count - 1));
            _terminal.Clear();
            Header("CAPTURE COMPARISON",
                $"#{comparison.Older.Id} → #{comparison.Newer.Id}  •  Added: {comparison.AddedEvents.Count}  •  Missing: {comparison.MissingFingerprints.Count}");
            _terminal.WriteLine(
                $"  Events: {comparison.OlderEventCount} → {comparison.NewerEventCount}",
                ConsoleColor.DarkCyan);
            _terminal.WriteLine(
                "  Fingerprint исключает PID. Diff является диагностикой, а не threat verdict.",
                ConsoleColor.Yellow);
            _terminal.WriteLine();
            DrawEvents(comparison.AddedEvents, selected);
            _terminal.Rule();
            _terminal.WriteLine(
                comparison.AddedEvents.Count == 0
                    ? "  X — missing fingerprints   J/M — экспорт   Q/Esc — назад"
                    : "  ↑/↓ — выбор   Enter — детали   X — missing   J/M — экспорт   Q/Esc — назад",
                ConsoleColor.DarkGray);

            ConsoleKey key = Console.ReadKey(true).Key;
            if (key is ConsoleKey.Q or ConsoleKey.Escape) return;
            if (key == ConsoleKey.X)
            {
                ShowMissing(comparison.MissingFingerprints);
                continue;
            }
            if (key is ConsoleKey.J or ConsoleKey.M)
            {
                string path = await _archive.ExportComparisonAsync(
                    comparison,
                    key == ConsoleKey.J ? "json" : "markdown",
                    token);
                Message("COMPARISON EXPORT COMPLETE", $"Отчёт сохранён:\n{path}", ConsoleColor.Green);
                continue;
            }
            if (comparison.AddedEvents.Count == 0) continue;
            if (key == ConsoleKey.UpArrow)
                selected = (selected - 1 + comparison.AddedEvents.Count) % comparison.AddedEvents.Count;
            else if (key == ConsoleKey.DownArrow)
                selected = (selected + 1) % comparison.AddedEvents.Count;
            else if (key == ConsoleKey.Enter)
                ShowEvent(comparison.AddedEvents[selected]);
        }
    }

    private async Task<ConsoleKey> CaptureViewAsync(
        TelemetryCapture capture,
        CancellationToken token,
        bool allowRepeat)
    {
        int selected = 0;
        while (true)
        {
            TelemetryCaptureSummary summary = capture.Summary;
            selected = Math.Clamp(selected, 0, Math.Max(0, capture.Events.Count - 1));
            _terminal.Clear();
            Header($"TELEMETRY CAPTURE #{summary.Id}",
                $"{summary.Mode}  •  Events: {summary.EventCount}  •  Elevated: {(summary.Elevated ? "yes" : "no")}");
            _terminal.WriteLine($"  {summary.Status}",
                summary.Mode == EtwBackendMode.KernelEtw ? ConsoleColor.Green : ConsoleColor.Yellow);
            _terminal.WriteLine(
                $"  Connect: {summary.ConnectCount}  Accept: {summary.AcceptCount}  Disconnect: {summary.DisconnectCount}  Retransmit: {summary.RetransmitCount}",
                ConsoleColor.DarkCyan);
            if (!string.IsNullOrWhiteSpace(summary.Failure))
                _terminal.WriteLine($"  Fallback reason: {summary.Failure}", ConsoleColor.DarkYellow);
            _terminal.WriteLine();
            DrawEvents(capture.Events, selected);
            _terminal.Rule();
            _terminal.WriteLine(
                allowRepeat
                    ? "  ↑/↓ — выбор   Enter — детали   R — новый capture   J/M — экспорт   Q/Esc — назад"
                    : "  ↑/↓ — выбор   Enter — детали   J/M — экспорт   Q/Esc — назад",
                ConsoleColor.DarkGray);

            ConsoleKey key = Console.ReadKey(true).Key;
            if (key is ConsoleKey.Q or ConsoleKey.Escape) return key;
            if (allowRepeat && key == ConsoleKey.R) return key;
            if (key is ConsoleKey.J or ConsoleKey.M)
            {
                string path = await _archive.ExportCaptureAsync(
                    capture,
                    key == ConsoleKey.J ? "json" : "markdown",
                    token);
                Message("ARCHIVE EXPORT COMPLETE", $"Отчёт сохранён:\n{path}", ConsoleColor.Green);
                continue;
            }
            if (capture.Events.Count == 0) continue;
            if (key == ConsoleKey.UpArrow)
                selected = (selected - 1 + capture.Events.Count) % capture.Events.Count;
            else if (key == ConsoleKey.DownArrow)
                selected = (selected + 1) % capture.Events.Count;
            else if (key == ConsoleKey.Enter)
                ShowEvent(capture.Events[selected]);
        }
    }

    private void DrawEvents(IReadOnlyList<EtwNetworkEvent> events, int selected)
    {
        if (events.Count == 0)
        {
            _terminal.WriteLine("  События не получены за capture window.", ConsoleColor.Yellow);
            return;
        }

        int rows = MaxRows();
        int start = Math.Clamp(selected - rows / 2, 0, Math.Max(0, events.Count - rows));
        foreach ((EtwNetworkEvent item, int index) in events.Skip(start).Take(rows)
                     .Select((item, i) => (item, start + i)))
        {
            bool active = index == selected;
            ConsoleColor accent = EventColor(item.Kind);
            _terminal.Write(active ? "  ▶ " : "    ", active ? accent : ConsoleColor.DarkGray);
            _terminal.Write($"{item.Timestamp:HH:mm:ss.fff} ", ConsoleColor.DarkGray);
            _terminal.Write(Terminal.PadOrTrim(item.Kind, 12), accent);
            _terminal.Write(Terminal.PadOrTrim(item.ProcessName, 20), active ? ConsoleColor.White : ConsoleColor.Gray);
            _terminal.WriteLine(
                $" {Terminal.PadOrTrim(item.RemoteEndpoint, Math.Max(18, _terminal.Width - 55))}",
                active ? ConsoleColor.Cyan : ConsoleColor.DarkGray);
        }
    }

    private void ShowEvent(EtwNetworkEvent item)
    {
        _terminal.Clear();
        Header($"TELEMETRY EVENT #{item.Sequence}", item.Kind);
        _terminal.Box(
        [
            $"Time:          {item.Timestamp:O}",
            $"Process:       {item.ProcessName}",
            $"PID:           {item.ProcessId}",
            $"Protocol:      {item.Protocol}",
            $"Local:         {item.LocalEndpoint}",
            $"Remote:        {item.RemoteEndpoint}",
            $"Note:          {item.Note}",
            "Privacy:       packet payload и TLS/HTTP content не сохраняются"
        ], "EVENT METADATA", EventColor(item.Kind));
        Back();
    }

    private void ShowMissing(IReadOnlyList<string> missing)
    {
        _terminal.Clear();
        Header("MISSING LIFECYCLE FINGERPRINTS", $"Исчезло между capture: {missing.Count}");
        if (missing.Count == 0)
        {
            _terminal.WriteLine("  Исчезнувших fingerprints нет.", ConsoleColor.Green);
        }
        else
        {
            foreach (string fingerprint in missing.Take(MaxRows()))
                _terminal.WriteLine($"  • {Terminal.PadOrTrim(fingerprint, Math.Max(20, _terminal.Width - 6))}", ConsoleColor.Gray);
        }
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

    private int MaxRows() => Math.Max(5, _terminal.Height - 16);

    private static ConsoleColor EventColor(string kind) => kind switch
    {
        "CONNECT" => ConsoleColor.Cyan,
        "ACCEPT" => ConsoleColor.Green,
        "DISCONNECT" => ConsoleColor.DarkGray,
        "RETRANSMIT" => ConsoleColor.Yellow,
        "LISTENER" => ConsoleColor.Magenta,
        _ => ConsoleColor.Gray
    };
}
