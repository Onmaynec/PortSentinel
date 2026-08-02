using PortSentinel.Models;
using PortSentinel.Services;
using PortSentinel.UI;

namespace PortSentinel.App;

internal sealed class PortSentinelV55App
{
    private readonly Terminal _terminal;
    private readonly EtwTelemetryService _etw;
    private readonly TelemetryArchiveService _archive;
    private readonly NetworkCoverageService _coverage;
    private readonly PortSentinelV54App _connectionHealth;
    private int _selected;

    private static readonly MenuItem[] Menu =
    [
        new("1", "COVERAGE CAPTURE", "TCP4/TCP6 и UDP4/UDP6 capture с сохранением в SQLite", ConsoleColor.Cyan),
        new("2", "LATEST COVERAGE", "Protocol matrix последней сохранённой capture-сессии", ConsoleColor.Green),
        new("3", "ARCHIVE COVERAGE", "Выбор capture и анализ families/endpoints", ConsoleColor.Magenta),
        new("4", "CONNECTION HEALTH", "Fail, retransmit и reconnect analysis v0.5.4", ConsoleColor.Yellow),
        new("0", "EXIT", "Безопасно закрыть PortSentinel", ConsoleColor.Red)
    ];

    public PortSentinelV55App(
        Terminal terminal,
        EtwTelemetryService etw,
        TelemetryArchiveService archive,
        NetworkCoverageService coverage,
        PortSentinelV54App connectionHealth)
    {
        _terminal = terminal;
        _etw = etw;
        _archive = archive;
        _coverage = coverage;
        _connectionHealth = connectionHealth;
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
        _terminal.WriteLine($"  v{Program.Version}  •  NETWORK COVERAGE", ConsoleColor.DarkGray);
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
        "1" => CaptureCoverageAsync(token),
        "2" => LatestCoverageAsync(token),
        "3" => ArchiveCoverageAsync(token),
        "4" => _connectionHealth.RunAsync(token),
        _ => Task.CompletedTask
    };

    private async Task CaptureCoverageAsync(CancellationToken token)
    {
        EtwCaptureResult capture = await _terminal.RunWithSpinnerAsync(
            "Network coverage capture: 15 секунд",
            _etw.CaptureAsync(TimeSpan.FromSeconds(15), token));
        long id = await _archive.SaveCaptureAsync(capture, token);
        await ShowReportAsync(_coverage.Analyze(capture, id), token);
    }

    private async Task LatestCoverageAsync(CancellationToken token)
    {
        IReadOnlyList<TelemetryCaptureSummary> summaries = await _archive.ListCapturesAsync(1, token);
        if (summaries.Count == 0)
        {
            Message("LATEST COVERAGE", "Telemetry archive пуст. Сначала выполните Coverage Capture.", ConsoleColor.Yellow);
            return;
        }

        TelemetryCapture? capture = await _archive.LoadCaptureAsync(summaries[0].Id, token);
        if (capture is null)
        {
            Message("LATEST COVERAGE", "Не удалось загрузить последнюю capture-сессию.", ConsoleColor.Red);
            return;
        }

        await ShowReportAsync(_coverage.Analyze(capture), token);
    }

    private async Task ArchiveCoverageAsync(CancellationToken token)
    {
        int selected = 0;

        while (true)
        {
            IReadOnlyList<TelemetryCaptureSummary> captures = await _archive.ListCapturesAsync(50, token);
            if (captures.Count == 0)
            {
                Message("ARCHIVE COVERAGE", "Telemetry archive пуст.", ConsoleColor.Yellow);
                return;
            }

            selected = Math.Clamp(selected, 0, captures.Count - 1);
            _terminal.Clear();
            Header("ARCHIVE COVERAGE", $"Выберите capture • Records: {captures.Count}");
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
                _terminal.WriteLine($" Events:{capture.EventCount,5}",
                    active ? ConsoleColor.White : ConsoleColor.DarkGray);
            }

            _terminal.Rule();
            _terminal.WriteLine("  ↑/↓ — выбор   Enter — coverage report   R — обновить   Q/Esc — назад", ConsoleColor.DarkGray);
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
                    await ShowReportAsync(_coverage.Analyze(capture), token);
            }
        }
    }

    private async Task ShowReportAsync(NetworkCoverageReport report, CancellationToken token)
    {
        int selected = 0;

        while (true)
        {
            selected = Math.Clamp(selected, 0, Math.Max(0, report.Protocols.Count - 1));
            _terminal.Clear();
            Header(
                report.CaptureId is long id ? $"NETWORK COVERAGE #{id}" : "NETWORK COVERAGE",
                $"Backend: {report.Mode}  •  Events: {report.EventCount}  •  Families: {report.ObservedFamilies}");
            _terminal.WriteLine(
                $"  IPv4: {report.Ipv4Count}  IPv6: {report.Ipv6Count}  TCP: {report.TcpCount}  UDP: {report.UdpCount}",
                report.Mode == EtwBackendMode.KernelEtw ? ConsoleColor.Cyan : ConsoleColor.Yellow);
            _terminal.WriteLine(
                $"  UDP send: {report.UdpSendCount}  UDP receive: {report.UdpReceiveCount}  Protocol rows: {report.Protocols.Count}",
                ConsoleColor.DarkCyan);
            _terminal.WriteLine("  Отсутствие family в отчёте не доказывает отсутствие трафика.", ConsoleColor.Yellow);
            _terminal.WriteLine();

            DrawProtocols(report.Protocols, selected);
            _terminal.Rule();
            _terminal.WriteLine(
                "  ↑/↓ — выбор   Enter — protocol details   X — endpoints   L — limitations   J/M — экспорт   Q/Esc — назад",
                ConsoleColor.DarkGray);

            ConsoleKey key = Console.ReadKey(true).Key;
            if (key is ConsoleKey.Q or ConsoleKey.Escape) return;
            if (key is ConsoleKey.J or ConsoleKey.M)
            {
                string path = await _coverage.ExportAsync(
                    report,
                    key == ConsoleKey.J ? "json" : "markdown",
                    token);
                Message("COVERAGE EXPORT COMPLETE", $"Отчёт сохранён:\n{path}", ConsoleColor.Green);
                continue;
            }
            if (key == ConsoleKey.X)
            {
                ShowEndpoints(report.TopRemoteEndpoints);
                continue;
            }
            if (key == ConsoleKey.L)
            {
                ShowLimitations(report.Limitations);
                continue;
            }
            if (report.Protocols.Count == 0) continue;
            if (key == ConsoleKey.UpArrow)
                selected = (selected - 1 + report.Protocols.Count) % report.Protocols.Count;
            else if (key == ConsoleKey.DownArrow)
                selected = (selected + 1) % report.Protocols.Count;
            else if (key == ConsoleKey.Enter)
                ShowProtocol(report.Protocols[selected]);
        }
    }

    private void DrawProtocols(IReadOnlyList<NetworkProtocolCoverage> protocols, int selected)
    {
        if (protocols.Count == 0)
        {
            _terminal.WriteLine("  Protocol events не наблюдались в выбранном capture window.", ConsoleColor.Yellow);
            return;
        }

        int rows = MaxRows();
        int start = Math.Clamp(selected - rows / 2, 0, Math.Max(0, protocols.Count - rows));
        foreach ((NetworkProtocolCoverage protocol, int index) in protocols.Skip(start).Take(rows)
                     .Select((protocol, i) => (protocol, start + i)))
        {
            bool active = index == selected;
            ConsoleColor accent = ProtocolColor(protocol.Protocol);
            _terminal.Write(active ? "  ▶ " : "    ", active ? accent : ConsoleColor.DarkGray);
            _terminal.Write(Terminal.PadOrTrim(protocol.Protocol, 10), accent);
            _terminal.Write($" Events:{protocol.EventCount,5}", active ? ConsoleColor.White : ConsoleColor.Gray);
            _terminal.Write($"  Processes:{protocol.UniqueProcesses,4}", ConsoleColor.DarkGray);
            _terminal.Write($"  Endpoints:{protocol.UniqueRemoteEndpoints,4}", ConsoleColor.DarkGray);
            _terminal.WriteLine($"  Send:{protocol.SendCount,4}  Recv:{protocol.ReceiveCount,4}",
                active ? ConsoleColor.White : ConsoleColor.DarkGray);
        }
    }

    private void ShowProtocol(NetworkProtocolCoverage protocol)
    {
        _terminal.Clear();
        Header($"PROTOCOL {protocol.Protocol}", "Наблюдаемая metadata внутри bounded capture");
        _terminal.Box(
        [
            $"Events:            {protocol.EventCount}",
            $"Unique processes:  {protocol.UniqueProcesses}",
            $"Remote endpoints:  {protocol.UniqueRemoteEndpoints}",
            $"Send/connect:      {protocol.SendCount}",
            $"Receive/accept:    {protocol.ReceiveCount}",
            "Meaning:           observed events only",
            "Payload capture:   disabled"
        ], "PROTOCOL COVERAGE", ProtocolColor(protocol.Protocol));
        Back();
    }

    private void ShowEndpoints(IReadOnlyList<NetworkEndpointCoverage> endpoints)
    {
        _terminal.Clear();
        Header("TOP REMOTE ENDPOINTS", $"Показано: {endpoints.Count}");
        if (endpoints.Count == 0)
        {
            _terminal.WriteLine("  Remote endpoints отсутствуют.", ConsoleColor.Yellow);
        }
        else
        {
            foreach (NetworkEndpointCoverage endpoint in endpoints.Take(MaxRows()))
            {
                _terminal.Write($"  {Terminal.PadOrTrim(endpoint.Protocol, 8)}", ProtocolColor(endpoint.Protocol));
                _terminal.Write(Terminal.PadOrTrim(endpoint.Endpoint, Math.Max(18, _terminal.Width - 42)), ConsoleColor.Gray);
                _terminal.WriteLine($" events:{endpoint.EventCount,4} proc:{endpoint.UniqueProcesses,3}", ConsoleColor.DarkGray);
            }
        }
        Back();
    }

    private void ShowLimitations(IReadOnlyList<string> limitations)
    {
        _terminal.Clear();
        Header("COVERAGE LIMITATIONS", "Что нельзя заключить из отчёта");
        foreach (string limitation in limitations.Take(MaxRows()))
            _terminal.WriteLine($"  • {Terminal.PadOrTrim(limitation, Math.Max(20, _terminal.Width - 6))}", ConsoleColor.Yellow);
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

    private int MaxRows() => Math.Max(5, _terminal.Height - 18);

    private static ConsoleColor ProtocolColor(string protocol) => protocol.ToUpperInvariant() switch
    {
        "TCP4" => ConsoleColor.Cyan,
        "TCP6" => ConsoleColor.Blue,
        "UDP4" => ConsoleColor.Green,
        "UDP6" => ConsoleColor.Magenta,
        _ => ConsoleColor.Gray
    };
}
