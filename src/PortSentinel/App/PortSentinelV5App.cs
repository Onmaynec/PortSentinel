using PortSentinel.Models;
using PortSentinel.Services;
using PortSentinel.UI;

namespace PortSentinel.App;

internal sealed class PortSentinelV5App
{
    private readonly Terminal _terminal;
    private readonly NetworkSnapshotService _network;
    private readonly SessionStore _store;
    private readonly DnsCorrelationService _dns;
    private readonly ProcessTreeService _processTree;
    private readonly SessionComparisonService _sessionComparison;
    private readonly ApplicationWatchService _watch;
    private readonly PortSentinelV4App _previousPanel;
    private int _selected;

    private static readonly MenuItem[] Menu =
    [
        new("1", "APPLICATION WATCH", "Timeline, connection cycles и reconnect loops", ConsoleColor.Cyan),
        new("2", "DNS CORRELATION", "Reverse DNS для внешних remote addresses", ConsoleColor.DarkCyan),
        new("3", "NETWORK PROCESS TREE", "Родительские процессы для сетевой активности", ConsoleColor.Magenta),
        new("4", "SESSION COMPARISON", "Diff двух последних сохранённых сессий", ConsoleColor.Yellow),
        new("5", "V0.4 CONTROL CENTER", "Sessions, baselines, rules и network tools", ConsoleColor.Green),
        new("0", "EXIT", "Безопасно закрыть PortSentinel", ConsoleColor.Red)
    ];

    public PortSentinelV5App(
        Terminal terminal,
        NetworkSnapshotService network,
        SessionStore store,
        DnsCorrelationService dns,
        ProcessTreeService processTree,
        SessionComparisonService sessionComparison,
        ApplicationWatchService watch,
        PortSentinelV4App previousPanel)
    {
        _terminal = terminal;
        _network = network;
        _store = store;
        _dns = dns;
        _processTree = processTree;
        _sessionComparison = sessionComparison;
        _watch = watch;
        _previousPanel = previousPanel;
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
        _terminal.WriteLine($"  v{Program.Version}  •  EXTENDED TELEMETRY", ConsoleColor.DarkGray);
        _terminal.Rule("PORTSENTINEL OBSERVABILITY NODE");

        for (int i = 0; i < Menu.Length; i++)
        {
            MenuItem item = Menu[i];
            bool active = i == _selected;
            _terminal.Write(active ? "  ▶ " : "    ", active ? item.Accent : ConsoleColor.DarkGray);
            _terminal.Write($"[{item.Key}] {Terminal.PadOrTrim(item.Title, 25)}",
                active ? ConsoleColor.White : item.Accent);
            _terminal.WriteLine($"  {item.Description}",
                active ? ConsoleColor.Gray : ConsoleColor.DarkGray);
        }

        _terminal.Rule();
        _terminal.WriteLine("  ↑/↓ или W/S — выбор   Enter — открыть   Q/Esc — выход", ConsoleColor.DarkGray);
    }

    private Task OpenAsync(string key, CancellationToken token) => key switch
    {
        "1" => ApplicationWatchAsync(token),
        "2" => DnsCorrelationAsync(token),
        "3" => ProcessTreeAsync(token),
        "4" => SessionComparisonAsync(token),
        "5" => _previousPanel.RunAsync(token),
        _ => Task.CompletedTask
    };

    private async Task ApplicationWatchAsync(CancellationToken token)
    {
        IReadOnlyList<NetworkEntry> snapshot;
        try
        {
            snapshot = await _terminal.RunWithSpinnerAsync(
                "Поиск сетевых процессов", _network.CaptureAsync(token));
        }
        catch (Exception ex)
        {
            Message("APPLICATION WATCH", $"Не удалось получить сетевые процессы: {ex.Message}", ConsoleColor.Red);
            return;
        }

        NetworkEntry[] processes = snapshot
            .GroupBy(entry => entry.ProcessId)
            .Select(group => group.First())
            .OrderBy(entry => entry.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (processes.Length == 0)
        {
            Message("APPLICATION WATCH", "Сетевые процессы не найдены.", ConsoleColor.Yellow);
            return;
        }

        int selected = 0;
        while (true)
        {
            _terminal.Clear();
            Header("APPLICATION WATCH", "Выберите процесс для наблюдения");
            int start = Math.Clamp(selected - MaxRows() / 2, 0, Math.Max(0, processes.Length - MaxRows()));
            foreach ((NetworkEntry process, int index) in processes.Skip(start).Take(MaxRows())
                         .Select((process, i) => (process, start + i)))
            {
                bool active = index == selected;
                _terminal.Write(active ? "  ▶ " : "    ", active ? ConsoleColor.Cyan : ConsoleColor.DarkGray);
                _terminal.Write(Terminal.PadOrTrim(process.ProcessName, 30),
                    active ? ConsoleColor.White : ConsoleColor.Gray);
                _terminal.WriteLine($" PID {process.ProcessId,6}  {process.LocalEndpoint}",
                    active ? ConsoleColor.Cyan : ConsoleColor.DarkGray);
            }

            _terminal.Rule();
            _terminal.WriteLine("  ↑/↓ — выбор   Enter — начать watch   Q/Esc — назад", ConsoleColor.DarkGray);
            ConsoleKey key = Console.ReadKey(true).Key;
            if (key is ConsoleKey.Q or ConsoleKey.Escape) return;
            if (key == ConsoleKey.UpArrow) selected = (selected - 1 + processes.Length) % processes.Length;
            else if (key == ConsoleKey.DownArrow) selected = (selected + 1) % processes.Length;
            else if (key == ConsoleKey.Enter)
            {
                await RunWatchAsync(processes[selected], token);
                return;
            }
        }
    }

    private async Task RunWatchAsync(NetworkEntry target, CancellationToken token)
    {
        ApplicationWatchTracker tracker = _watch.CreateTracker(target);

        while (true)
        {
            IReadOnlyList<NetworkEntry> entries;
            try
            {
                entries = await _network.CaptureAsync(token);
            }
            catch (Exception ex)
            {
                Message("APPLICATION WATCH", $"Снимок завершился ошибкой: {ex.Message}", ConsoleColor.Red);
                return;
            }

            tracker.Observe(entries, DateTimeOffset.Now);
            IReadOnlyList<WatchConnection> connections = tracker.Snapshot();

            _terminal.Clear();
            Header("APPLICATION WATCH",
                $"{tracker.TargetProcess}  •  {DateTimeOffset.Now - tracker.StartedAt:hh\\:mm\\:ss}");
            _terminal.WriteLine(
                $"  Samples: {tracker.Samples}  •  Active: {tracker.ActiveConnections}  •  Unique: {tracker.UniqueConnections}  •  Reconnect loops: {tracker.ReconnectLoops}",
                tracker.ReconnectLoops > 0 ? ConsoleColor.Yellow : ConsoleColor.Cyan);
            _terminal.WriteLine();
            _terminal.WriteLine(
                $"  {Terminal.PadOrTrim("PROTO", 7)} {Terminal.PadOrTrim("LOCAL", 28)} {Terminal.PadOrTrim("REMOTE", 34)} {"CYCLES",7} STATUS",
                ConsoleColor.DarkCyan);

            foreach (WatchConnection connection in connections.Take(MaxRows() - 2))
            {
                ConsoleColor color = connection.ConnectCycles >= 3
                    ? ConsoleColor.Yellow
                    : connection.IsActive ? ConsoleColor.Green : ConsoleColor.DarkGray;
                _terminal.WriteLine(
                    $"  {Terminal.PadOrTrim(connection.Protocol, 7)} {Terminal.PadOrTrim(connection.LocalEndpoint, 28)} " +
                    $"{Terminal.PadOrTrim(connection.RemoteEndpoint, 34)} {connection.ConnectCycles,7} " +
                    (connection.IsActive ? "ACTIVE" : "CLOSED"),
                    color);
            }

            _terminal.Rule();
            _terminal.WriteLine("  Q/Esc — завершить, выполнить DNS enrichment и экспортировать JSON/Markdown", ConsoleColor.DarkGray);

            DateTime until = DateTime.UtcNow.AddSeconds(1);
            while (DateTime.UtcNow < until)
            {
                if (Console.KeyAvailable)
                {
                    ConsoleKey key = Console.ReadKey(true).Key;
                    if (key is ConsoleKey.Q or ConsoleKey.Escape)
                    {
                        ApplicationWatchReport report = await _terminal.RunWithSpinnerAsync(
                            "DNS enrichment и подготовка timeline",
                            _watch.CompleteAsync(tracker, token));
                        string json = await _watch.ExportAsync(report, "json", token);
                        string markdown = await _watch.ExportAsync(report, "markdown", token);
                        Message(
                            "WATCH COMPLETE",
                            $"Samples: {report.Samples}\nEndpoints: {report.Connections.Count}\nReconnect loops: {report.ReconnectLoops}\nJSON: {json}\nMarkdown: {markdown}",
                            report.ReconnectLoops > 0 ? ConsoleColor.Yellow : ConsoleColor.Green);
                        return;
                    }
                }

                await Task.Delay(50, token);
            }
        }
    }

    private async Task DnsCorrelationAsync(CancellationToken token)
    {
        IReadOnlyList<NetworkEntry> entries;
        try
        {
            entries = await _terminal.RunWithSpinnerAsync(
                "Сбор внешних соединений", _network.CaptureAsync(token));
        }
        catch (Exception ex)
        {
            Message("DNS CORRELATION", $"Не удалось получить соединения: {ex.Message}", ConsoleColor.Red);
            return;
        }

        IReadOnlyList<DnsCorrelation> correlations = await _terminal.RunWithSpinnerAsync(
            "Reverse DNS с timeout и кэшем", _dns.ResolveAsync(entries, token));

        _terminal.Clear();
        Header("DNS CORRELATION", $"Уникальных внешних адресов: {correlations.Count}");
        _terminal.WriteLine(
            $"  {Terminal.PadOrTrim("REMOTE ADDRESS", 42)} {Terminal.PadOrTrim("HOST NAME", Math.Max(24, _terminal.Width - 60))} STATUS",
            ConsoleColor.DarkCyan);
        foreach (DnsCorrelation item in correlations.Take(MaxRows()))
        {
            ConsoleColor color = item.Status == "resolved" ? ConsoleColor.Green : ConsoleColor.Yellow;
            _terminal.WriteLine(
                $"  {Terminal.PadOrTrim(item.Address, 42)} {Terminal.PadOrTrim(item.HostName ?? "—", Math.Max(24, _terminal.Width - 60))} {item.Status}",
                color);
        }

        if (correlations.Count == 0)
            _terminal.WriteLine("  Внешние IP-адреса для correlation не найдены.", ConsoleColor.Yellow);

        _terminal.Rule();
        _terminal.WriteLine("  Reverse DNS является best-effort metadata и не доказывает владельца трафика.", ConsoleColor.DarkGray);
        Back();
    }

    private async Task ProcessTreeAsync(CancellationToken token)
    {
        IReadOnlyList<NetworkEntry> entries;
        try
        {
            entries = await _terminal.RunWithSpinnerAsync(
                "Сбор сетевой активности", _network.CaptureAsync(token));
        }
        catch (Exception ex)
        {
            Message("NETWORK PROCESS TREE", $"Не удалось получить сеть: {ex.Message}", ConsoleColor.Red);
            return;
        }

        IReadOnlyList<ProcessTreeNode> nodes;
        try
        {
            nodes = _processTree.Capture(entries);
        }
        catch (Exception ex)
        {
            Message("NETWORK PROCESS TREE", $"Не удалось прочитать дерево процессов: {ex.Message}", ConsoleColor.Red);
            return;
        }

        _terminal.Clear();
        Header("NETWORK PROCESS TREE", $"Network processes: {nodes.Count(node => node.IsNetworkActive)}  •  Ancestors included");
        foreach (ProcessTreeNode node in nodes.Take(MaxRows()))
        {
            string prefix = new string(' ', Math.Min(node.Depth, 8) * 2) + (node.Depth == 0 ? "● " : "└─");
            ConsoleColor color = node.IsNetworkActive ? ConsoleColor.Cyan : ConsoleColor.DarkGray;
            _terminal.Write(prefix, color);
            _terminal.Write(Terminal.PadOrTrim(node.Name, 32),
                node.IsNetworkActive ? ConsoleColor.White : ConsoleColor.Gray);
            _terminal.WriteLine(
                $" PID {node.ProcessId,6}  PPID {node.ParentProcessId,6}  NET {node.NetworkEntryCount,3}",
                color);
        }

        if (nodes.Count == 0)
            _terminal.WriteLine("  Сетевые процессы не найдены.", ConsoleColor.Yellow);

        _terminal.Rule();
        _terminal.WriteLine("  Дерево читается напрямую из Windows Toolhelp32 snapshot.", ConsoleColor.DarkGray);
        Back();
    }

    private async Task SessionComparisonAsync(CancellationToken token)
    {
        IReadOnlyList<SessionSummary> sessions = await _store.ListSessionsAsync(50, token);
        if (sessions.Count < 2)
        {
            Message("SESSION COMPARISON", "Для сравнения нужны минимум две сохранённые сессии.", ConsoleColor.Yellow);
            return;
        }

        SessionSummary after = sessions[0];
        SessionSummary before = sessions[1];
        IReadOnlyList<NetworkEntry> beforeEntries = await _store.LoadSessionEntriesAsync(before.Id, token);
        IReadOnlyList<NetworkEntry> afterEntries = await _store.LoadSessionEntriesAsync(after.Id, token);
        SessionComparison comparison = _sessionComparison.Compare(
            before,
            beforeEntries,
            after,
            afterEntries);

        while (true)
        {
            _terminal.Clear();
            Header("SESSION COMPARISON", $"#{before.Id} → #{after.Id}");
            _terminal.Box(
            [
                $"Before:          {before.Name} / {before.StartedAt:G}",
                $"After:           {after.Name} / {after.StartedAt:G}",
                $"Added entries:   {comparison.AddedEntries.Count}",
                $"Removed entries: {comparison.RemovedEntries.Count}",
                $"Added processes: {comparison.AddedProcesses.Count}",
                $"Removed process: {comparison.RemovedProcesses.Count}",
                $"Listener delta:  {comparison.ListenerDelta:+#;-#;0}",
                $"External delta:  {comparison.ExternalDelta:+#;-#;0}"
            ], "LATEST SESSION DELTA", ConsoleColor.Cyan);

            _terminal.WriteLine();
            _terminal.WriteLine("  Новые записи:", ConsoleColor.Green);
            foreach (NetworkEntry entry in comparison.AddedEntries.Take(Math.Max(2, MaxRows() - 12)))
            {
                _terminal.WriteLine(
                    $"  + {Terminal.PadOrTrim(entry.ProcessName, 22)} {Terminal.PadOrTrim(entry.Protocol, 6)} " +
                    $"{Terminal.PadOrTrim(entry.LocalEndpoint, 26)} → {entry.RemoteEndpoint}",
                    ConsoleColor.Gray);
            }

            _terminal.Rule();
            _terminal.WriteLine("  J — export JSON   M — export Markdown   Q/Esc — назад", ConsoleColor.DarkGray);
            ConsoleKey key = Console.ReadKey(true).Key;
            if (key is ConsoleKey.Q or ConsoleKey.Escape) return;
            if (key is ConsoleKey.J or ConsoleKey.M)
            {
                string path = await _sessionComparison.ExportAsync(
                    comparison,
                    key == ConsoleKey.J ? "json" : "markdown",
                    token);
                Message("SESSION DIFF EXPORTED", path, ConsoleColor.Green);
            }
        }
    }

    private void Header(string title, string subtitle)
    {
        AsciiLogo.Draw(_terminal, compact: true);
        _terminal.Rule(title);
        _terminal.WriteLine($"  {subtitle}", ConsoleColor.DarkGray);
        _terminal.WriteLine();
    }

    private int MaxRows() => Math.Max(5, _terminal.Height - 13);

    private void Message(string title, string text, ConsoleColor color)
    {
        _terminal.Clear();
        Header(title, string.Empty);
        _terminal.Box(text.Split('\n'), "MESSAGE", color);
        Back();
    }

    private void Back()
    {
        _terminal.Rule();
        _terminal.WriteLine("  Нажмите любую клавишу, чтобы вернуться...", ConsoleColor.DarkGray);
        Console.ReadKey(true);
    }
}
