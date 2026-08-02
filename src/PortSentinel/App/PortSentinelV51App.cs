using PortSentinel.Models;
using PortSentinel.Services;
using PortSentinel.UI;

namespace PortSentinel.App;

internal sealed class PortSentinelV51App
{
    private readonly Terminal _terminal;
    private readonly EtwTelemetryService _etw;
    private readonly PortSentinelV5App _extendedTelemetry;
    private int _selected;

    private static readonly MenuItem[] Menu =
    [
        new("1", "ETW NETWORK CAPTURE", "Kernel TCP events с автоматическим snapshot fallback", ConsoleColor.Cyan),
        new("2", "ETW CAPABILITY", "Права, backend и privacy boundary", ConsoleColor.Yellow),
        new("3", "EXTENDED TELEMETRY", "Application Watch, DNS, process tree и session diff", ConsoleColor.Magenta),
        new("0", "EXIT", "Безопасно закрыть PortSentinel", ConsoleColor.Red)
    ];

    public PortSentinelV51App(
        Terminal terminal,
        EtwTelemetryService etw,
        PortSentinelV5App extendedTelemetry)
    {
        _terminal = terminal;
        _etw = etw;
        _extendedTelemetry = extendedTelemetry;
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
        _terminal.WriteLine($"  v{Program.Version}  •  ETW TELEMETRY UPDATE", ConsoleColor.DarkGray);
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
        "1" => CaptureCenterAsync(token),
        "2" => CapabilityAsync(),
        "3" => _extendedTelemetry.RunAsync(token),
        _ => Task.CompletedTask
    };

    private async Task CaptureCenterAsync(CancellationToken token)
    {
        int selected = 0;

        while (true)
        {
            EtwCaptureResult capture = await _terminal.RunWithSpinnerAsync(
                "ETW network capture: 12 секунд",
                _etw.CaptureAsync(TimeSpan.FromSeconds(12), token));
            selected = Math.Clamp(selected, 0, Math.Max(0, capture.Events.Count - 1));

            while (true)
            {
                _terminal.Clear();
                string mode = capture.Mode == EtwBackendMode.KernelEtw
                    ? "KERNEL ETW"
                    : "SNAPSHOT FALLBACK";
                Header("ETW NETWORK CAPTURE",
                    $"{mode}  •  Events: {capture.Events.Count}  •  Elevated: {(capture.Elevated ? "yes" : "no")}");
                _terminal.WriteLine($"  {capture.Status}",
                    capture.Mode == EtwBackendMode.KernelEtw ? ConsoleColor.Green : ConsoleColor.Yellow);
                _terminal.WriteLine(
                    $"  Connect: {capture.ConnectCount}  Accept: {capture.AcceptCount}  Disconnect: {capture.DisconnectCount}  Retransmit: {capture.RetransmitCount}",
                    ConsoleColor.DarkCyan);
                if (!string.IsNullOrWhiteSpace(capture.Failure))
                    _terminal.WriteLine($"  Fallback reason: {capture.Failure}", ConsoleColor.DarkYellow);
                _terminal.WriteLine();

                DrawEvents(capture.Events, selected);
                _terminal.Rule();
                _terminal.WriteLine(
                    capture.Events.Count == 0
                        ? "  R — повторить   J/M — экспорт   Q/Esc — назад"
                        : "  ↑/↓ — выбор   Enter — детали   R — повторить   J/M — экспорт   Q/Esc — назад",
                    ConsoleColor.DarkGray);

                ConsoleKey key = Console.ReadKey(true).Key;
                if (key is ConsoleKey.Q or ConsoleKey.Escape) return;
                if (key == ConsoleKey.R) break;
                if (key is ConsoleKey.J or ConsoleKey.M)
                {
                    string path = await _etw.ExportAsync(
                        capture,
                        key == ConsoleKey.J ? "json" : "markdown",
                        token);
                    Message("ETW EXPORT COMPLETE", $"Отчёт сохранён:\n{path}", ConsoleColor.Green);
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
    }

    private Task CapabilityAsync()
    {
        EtwCapability capability = _etw.Probe();
        _terminal.Clear();
        Header("ETW CAPABILITY", "Проверка доступа к Windows Event Tracing");
        _terminal.Box(
        [
            $"Windows:             {(capability.IsWindows ? "yes" : "no")}",
            $"Elevated:            {(capability.IsElevated ? "yes" : "no")}",
            $"Kernel ETW control:  {(capability.CanControlKernelSession ? "available" : "fallback")}",
            $"Status:              {capability.Message}",
            "Provider:            Windows Kernel NetworkTCPIP",
            "Fallback:            Windows IP Helper API snapshot",
            "Payload capture:     disabled",
            "TLS/HTTP content:    never collected"
        ], "ETW BACKEND", capability.CanControlKernelSession ? ConsoleColor.Green : ConsoleColor.Yellow);
        Back();
        return Task.CompletedTask;
    }

    private void DrawEvents(IReadOnlyList<EtwNetworkEvent> events, int selected)
    {
        if (events.Count == 0)
        {
            _terminal.WriteLine("  События не получены за выбранное окно.", ConsoleColor.Yellow);
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
        Header($"ETW EVENT #{item.Sequence}", item.Kind);
        _terminal.Box(
        [
            $"Time:          {item.Timestamp:O}",
            $"Process:       {item.ProcessName}",
            $"PID:           {item.ProcessId}",
            $"Protocol:      {item.Protocol}",
            $"Local:         {item.LocalEndpoint}",
            $"Remote:        {item.RemoteEndpoint}",
            $"Note:          {item.Note}",
            "Privacy:       packet payload и TLS/HTTP content не собираются"
        ], "EVENT METADATA", EventColor(item.Kind));
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
