using PortSentinel.Models;
using PortSentinel.Services;
using PortSentinel.UI;

namespace PortSentinel.App;

internal sealed class PortSentinelV56App
{
    private readonly Terminal _terminal;
    private readonly TimelineExplorerService _timeline;
    private readonly PortSentinelV55App _networkCoverage;
    private int _selected;

    private static readonly string[] KindPresets =
    [
        string.Empty,
        "CONNECT",
        "ACCEPT",
        "DISCONNECT",
        "RETRANSMIT",
        "RECONNECT",
        "FAIL",
        "UDP_SEND",
        "UDP_RECV",
        "LISTENER",
        "SNAPSHOT"
    ];

    private static readonly string[] ProtocolPresets =
    [
        string.Empty,
        "TCP4",
        "TCP6",
        "UDP4",
        "UDP6"
    ];

    private static readonly MenuItem[] Menu =
    [
        new("1", "LATEST TIMELINE", "Открыть последнюю capture с server-side pagination", ConsoleColor.Cyan),
        new("2", "CAPTURE BROWSER", "Постраничный index всех telemetry captures", ConsoleColor.DarkCyan),
        new("3", "QUICK SEARCH", "Поиск process, IP, port или note в последней capture", ConsoleColor.Magenta),
        new("4", "NETWORK COVERAGE", "TCP4/TCP6 и UDP4/UDP6 reports v0.5.5", ConsoleColor.Yellow),
        new("0", "EXIT", "Безопасно закрыть PortSentinel", ConsoleColor.Red)
    ];

    public PortSentinelV56App(
        Terminal terminal,
        TimelineExplorerService timeline,
        PortSentinelV55App networkCoverage)
    {
        _terminal = terminal;
        _timeline = timeline;
        _networkCoverage = networkCoverage;
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
        _terminal.WriteLine($"  v{Program.Version}  •  TIMELINE EXPLORER", ConsoleColor.DarkGray);
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
        "1" => LatestTimelineAsync(token),
        "2" => CaptureBrowserAsync(token),
        "3" => QuickSearchAsync(token),
        "4" => _networkCoverage.RunAsync(token),
        _ => Task.CompletedTask
    };

    private async Task LatestTimelineAsync(CancellationToken token)
    {
        TimelineCapturePage captures = await _timeline.ListCapturesAsync(1, 10, token);
        if (captures.Items.Count == 0)
        {
            Message("LATEST TIMELINE", "Telemetry archive пуст. Сначала выполните capture.", ConsoleColor.Yellow);
            return;
        }

        await TimelineAsync(captures.Items[0].Id, TimelineFilter.Empty, token);
    }

    private async Task QuickSearchAsync(CancellationToken token)
    {
        TimelineCapturePage captures = await _timeline.ListCapturesAsync(1, 10, token);
        if (captures.Items.Count == 0)
        {
            Message("QUICK SEARCH", "Telemetry archive пуст. Сначала выполните capture.", ConsoleColor.Yellow);
            return;
        }

        string search = PromptText(
            "QUICK SEARCH",
            "Введите process name, IP address, port или фрагмент note");
        if (string.IsNullOrWhiteSpace(search))
            return;

        await TimelineAsync(
            captures.Items[0].Id,
            new TimelineFilter(string.Empty, string.Empty, search),
            token);
    }

    private async Task CaptureBrowserAsync(CancellationToken token)
    {
        int page = 1;
        int selected = 0;

        while (true)
        {
            int pageSize = Math.Clamp(MaxRows(), 10, 50);
            TimelineCapturePage captures = await _timeline.ListCapturesAsync(page, pageSize, token);
            page = captures.Page;

            if (captures.TotalItems == 0)
            {
                Message("CAPTURE BROWSER", "Telemetry archive пуст.", ConsoleColor.Yellow);
                return;
            }

            selected = Math.Clamp(selected, 0, Math.Max(0, captures.Items.Count - 1));
            _terminal.Clear();
            Header(
                "CAPTURE BROWSER",
                $"Page {captures.Page}/{captures.PageCount}  •  Captures: {captures.TotalItems}");

            foreach ((TelemetryCaptureSummary capture, int index) in captures.Items
                         .Select((capture, index) => (capture, index)))
            {
                bool active = index == selected;
                ConsoleColor accent = capture.Mode == EtwBackendMode.KernelEtw
                    ? ConsoleColor.Cyan
                    : ConsoleColor.Yellow;
                _terminal.Write(active ? "  ▶ " : "    ", active ? accent : ConsoleColor.DarkGray);
                _terminal.Write($"#{capture.Id,-6}", active ? ConsoleColor.White : ConsoleColor.Gray);
                _terminal.Write($" {capture.StartedAt:dd.MM HH:mm:ss} ", ConsoleColor.DarkGray);
                _terminal.Write(Terminal.PadOrTrim(capture.Mode.ToString(), 18), accent);
                _terminal.WriteLine(
                    $" Events:{capture.EventCount,5}  TCP:{capture.ConnectCount + capture.AcceptCount,4}  R:{capture.RetransmitCount,3}",
                    active ? ConsoleColor.White : ConsoleColor.DarkGray);
            }

            _terminal.Rule();
            _terminal.WriteLine(
                "  ↑/↓ — выбор   Enter — timeline   PgUp/PgDn — page   Home/End — границы   R — refresh   Q/Esc — назад",
                ConsoleColor.DarkGray);

            ConsoleKey key = Console.ReadKey(true).Key;
            if (key is ConsoleKey.Q or ConsoleKey.Escape) return;
            if (key == ConsoleKey.UpArrow && captures.Items.Count > 0)
                selected = (selected - 1 + captures.Items.Count) % captures.Items.Count;
            else if (key == ConsoleKey.DownArrow && captures.Items.Count > 0)
                selected = (selected + 1) % captures.Items.Count;
            else if (key == ConsoleKey.PageUp && captures.HasPrevious)
            {
                page--;
                selected = 0;
            }
            else if (key == ConsoleKey.PageDown && captures.HasNext)
            {
                page++;
                selected = 0;
            }
            else if (key == ConsoleKey.Home)
            {
                page = 1;
                selected = 0;
            }
            else if (key == ConsoleKey.End)
            {
                page = captures.PageCount;
                selected = 0;
            }
            else if (key == ConsoleKey.R)
            {
                continue;
            }
            else if (key == ConsoleKey.Enter && captures.Items.Count > 0)
            {
                await TimelineAsync(captures.Items[selected].Id, TimelineFilter.Empty, token);
            }
        }
    }

    private async Task TimelineAsync(
        long captureId,
        TimelineFilter initialFilter,
        CancellationToken token)
    {
        TelemetryCaptureSummary? summary = await _timeline.GetCaptureSummaryAsync(captureId, token);
        if (summary is null)
        {
            Message("TIMELINE EXPLORER", $"Capture #{captureId} не найдена.", ConsoleColor.Red);
            return;
        }

        TimelineFilter filter = initialFilter;
        int page = 1;
        int selected = 0;

        while (true)
        {
            int pageSize = Math.Clamp(MaxRows(), 10, 100);
            TimelineEventPage events = await _timeline.GetEventsAsync(
                captureId,
                page,
                pageSize,
                filter,
                token);
            page = events.Page;
            selected = Math.Clamp(selected, 0, Math.Max(0, events.Items.Count - 1));

            _terminal.Clear();
            Header(
                $"TIMELINE #{captureId}",
                $"Page {events.Page}/{events.PageCount}  •  Rows {events.FirstRow}–{events.LastRow} of {events.TotalItems}");
            _terminal.WriteLine(
                $"  Backend: {summary.Mode}  •  Started: {summary.StartedAt:O}",
                summary.Mode == EtwBackendMode.KernelEtw ? ConsoleColor.Cyan : ConsoleColor.Yellow);
            _terminal.WriteLine($"  Filter: {filter.Description}",
                filter.IsEmpty ? ConsoleColor.DarkGray : ConsoleColor.Magenta);
            _terminal.WriteLine();

            DrawEvents(events.Items, selected);
            _terminal.Rule();
            _terminal.WriteLine(
                "  ↑/↓ select  PgUp/PgDn page  Home/End first/last  Enter details  G sequence",
                ConsoleColor.DarkGray);
            _terminal.WriteLine(
                "  K kind  P protocol  F text search  C clear  J/M export page  R refresh  Q/Esc back",
                ConsoleColor.DarkGray);

            ConsoleKey key = Console.ReadKey(true).Key;
            if (key is ConsoleKey.Q or ConsoleKey.Escape) return;
            if (key == ConsoleKey.UpArrow && events.Items.Count > 0)
                selected = (selected - 1 + events.Items.Count) % events.Items.Count;
            else if (key == ConsoleKey.DownArrow && events.Items.Count > 0)
                selected = (selected + 1) % events.Items.Count;
            else if (key == ConsoleKey.PageUp && events.HasPrevious)
            {
                page--;
                selected = 0;
            }
            else if (key == ConsoleKey.PageDown && events.HasNext)
            {
                page++;
                selected = 0;
            }
            else if (key == ConsoleKey.Home)
            {
                page = 1;
                selected = 0;
            }
            else if (key == ConsoleKey.End)
            {
                page = events.PageCount;
                selected = 0;
            }
            else if (key == ConsoleKey.Enter && events.Items.Count > 0)
            {
                ShowEvent(events.Items[selected]);
            }
            else if (key == ConsoleKey.K)
            {
                filter = filter with { Kind = NextPreset(KindPresets, filter.Kind) };
                page = 1;
                selected = 0;
            }
            else if (key == ConsoleKey.P)
            {
                filter = filter with { Protocol = NextPreset(ProtocolPresets, filter.Protocol) };
                page = 1;
                selected = 0;
            }
            else if (key == ConsoleKey.F)
            {
                string search = PromptText(
                    "TIMELINE TEXT FILTER",
                    "Введите process, IP, port или note; пустая строка очищает text filter");
                filter = filter with { Search = search };
                page = 1;
                selected = 0;
            }
            else if (key == ConsoleKey.C)
            {
                filter = TimelineFilter.Empty;
                page = 1;
                selected = 0;
            }
            else if (key == ConsoleKey.G)
            {
                string input = PromptText("GO TO SEQUENCE", "Введите точный sequence number");
                if (!long.TryParse(input, out long sequence) || sequence < 1)
                {
                    Message("GO TO SEQUENCE", "Некорректный sequence number.", ConsoleColor.Yellow);
                    continue;
                }

                TimelineJumpResult? jump = await _timeline.FindSequenceAsync(
                    captureId,
                    sequence,
                    pageSize,
                    filter,
                    token);
                if (jump is null)
                {
                    Message(
                        "GO TO SEQUENCE",
                        $"Event #{sequence} не найден или не соответствует текущему фильтру.",
                        ConsoleColor.Yellow);
                    continue;
                }

                page = jump.Page;
                selected = jump.Index;
            }
            else if (key is ConsoleKey.J or ConsoleKey.M)
            {
                string path = await _timeline.ExportPageAsync(
                    events,
                    key == ConsoleKey.J ? "json" : "markdown",
                    token);
                Message("TIMELINE EXPORT COMPLETE", $"Текущая SQL-page сохранена:\n{path}", ConsoleColor.Green);
            }
            else if (key == ConsoleKey.R)
            {
                continue;
            }
        }
    }

    private void DrawEvents(IReadOnlyList<EtwNetworkEvent> events, int selected)
    {
        if (events.Count == 0)
        {
            _terminal.WriteLine("  Matching events отсутствуют.", ConsoleColor.Yellow);
            return;
        }

        foreach ((EtwNetworkEvent item, int index) in events.Select((item, index) => (item, index)))
        {
            bool active = index == selected;
            ConsoleColor accent = EventColor(item.Kind);
            _terminal.Write(active ? "  ▶ " : "    ", active ? accent : ConsoleColor.DarkGray);
            _terminal.Write($"#{item.Sequence,-6}", ConsoleColor.DarkGray);
            _terminal.Write($" {item.Timestamp:HH:mm:ss.fff} ", ConsoleColor.DarkGray);
            _terminal.Write(Terminal.PadOrTrim(item.Kind, 12), accent);
            _terminal.Write(Terminal.PadOrTrim(item.Protocol, 7), ConsoleColor.DarkCyan);
            _terminal.Write(Terminal.PadOrTrim(item.ProcessName, 18), active ? ConsoleColor.White : ConsoleColor.Gray);
            _terminal.WriteLine(
                $" {Terminal.PadOrTrim(item.RemoteEndpoint, Math.Max(16, _terminal.Width - 66))}",
                active ? ConsoleColor.Cyan : ConsoleColor.DarkGray);
        }
    }

    private void ShowEvent(EtwNetworkEvent item)
    {
        _terminal.Clear();
        Header($"TIMELINE EVENT #{item.Sequence}", item.Kind);
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

    private int MaxRows() => Math.Max(10, _terminal.Height - 19);

    private static string NextPreset(string[] presets, string current)
    {
        int index = Array.FindIndex(
            presets,
            value => value.Equals(current, StringComparison.OrdinalIgnoreCase));
        return presets[(index + 1 + presets.Length) % presets.Length];
    }

    private static ConsoleColor EventColor(string kind) => kind.ToUpperInvariant() switch
    {
        "FAIL" => ConsoleColor.Red,
        "RETRANSMIT" => ConsoleColor.Yellow,
        "RECONNECT" => ConsoleColor.Magenta,
        "DISCONNECT" => ConsoleColor.DarkYellow,
        "UDP_SEND" => ConsoleColor.Cyan,
        "UDP_RECV" => ConsoleColor.DarkCyan,
        "CONNECT" or "ACCEPT" => ConsoleColor.Green,
        "LISTENER" => ConsoleColor.Blue,
        _ => ConsoleColor.Gray
    };
}
