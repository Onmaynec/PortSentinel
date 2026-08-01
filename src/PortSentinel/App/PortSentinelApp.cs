using PortSentinel.Models;
using PortSentinel.Services;
using PortSentinel.UI;

namespace PortSentinel.App;

internal sealed class PortSentinelApp
{
    private readonly Terminal _terminal;
    private readonly NetworkSnapshotService _network;
    private readonly ProcessMetadataService _processes;
    private readonly GitHubUpdateService _updater;
    private int _selected;

    private static readonly MenuItem[] Menu =
    [
        new("1", "LIVE NETWORK MONITOR", "Живой поток TCP/UDP с обновлением каждую секунду", ConsoleColor.Cyan),
        new("2", "LISTENING PORTS", "Процессы, принимающие входящие подключения", ConsoleColor.Green),
        new("3", "ACTIVE CONNECTIONS", "Установленные и создаваемые TCP-соединения", ConsoleColor.Blue),
        new("4", "PROCESS INSPECTOR", "Карточка процесса, путь и сетевые endpoints", ConsoleColor.Magenta),
        new("5", "QUICK SCAN", "Проверка wildcard listeners и подозрительных путей", ConsoleColor.Yellow),
        new("6", "UPDATE CENTER", "Проверка и установка GitHub Releases", ConsoleColor.DarkCyan),
        new("7", "ABOUT / SYSTEM", "Версия, режим доступа и горячие клавиши", ConsoleColor.DarkGray),
        new("0", "EXIT", "Безопасно закрыть PortSentinel", ConsoleColor.Red)
    ];

    public PortSentinelApp(
        Terminal terminal,
        NetworkSnapshotService network,
        ProcessMetadataService processes,
        GitHubUpdateService updater)
    {
        _terminal = terminal;
        _network = network;
        _processes = processes;
        _updater = updater;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await _terminal.RunIntroAsync();
        Console.CursorVisible = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            DrawMainMenu();
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);

            if (key.Key is ConsoleKey.UpArrow or ConsoleKey.W)
            {
                _selected = (_selected - 1 + Menu.Length) % Menu.Length;
                continue;
            }

            if (key.Key is ConsoleKey.DownArrow or ConsoleKey.S)
            {
                _selected = (_selected + 1) % Menu.Length;
                continue;
            }

            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q)
            {
                return;
            }

            if (char.IsDigit(key.KeyChar))
            {
                int index = Array.FindIndex(Menu, item => item.Key[0] == key.KeyChar);
                if (index >= 0)
                {
                    _selected = index;
                    if (key.KeyChar != '0')
                    {
                        await OpenSelectedAsync(cancellationToken);
                        continue;
                    }

                    return;
                }
            }

            if (key.Key == ConsoleKey.Enter)
            {
                if (Menu[_selected].Key == "0")
                {
                    return;
                }

                await OpenSelectedAsync(cancellationToken);
            }
        }
    }

    private void DrawMainMenu()
    {
        _terminal.Clear();
        AsciiLogo.Draw(_terminal);
        _terminal.WriteLine($"  v{Program.Version}  •  WINDOWS NETWORK CONTROL CENTER", ConsoleColor.DarkGray);
        _terminal.Rule("CONTROL NODE");

        for (int index = 0; index < Menu.Length; index++)
        {
            MenuItem item = Menu[index];
            bool selected = index == _selected;
            _terminal.Write(selected ? "  ▶ " : "    ", selected ? item.Accent : ConsoleColor.DarkGray);
            _terminal.Write($"[{item.Key}] {Terminal.PadOrTrim(item.Title, 24)}", selected ? ConsoleColor.White : item.Accent);
            _terminal.WriteLine($"  {item.Description}", selected ? ConsoleColor.Gray : ConsoleColor.DarkGray);
        }

        _terminal.Rule();
        _terminal.WriteLine("  ↑/↓ или W/S — выбор   Enter — открыть   0/Q/Esc — выход", ConsoleColor.DarkGray);
    }

    private Task OpenSelectedAsync(CancellationToken cancellationToken) => Menu[_selected].Key switch
    {
        "1" => ShowLiveAsync(cancellationToken),
        "2" => ShowSnapshotAsync("LISTENING PORTS", static entry => entry.IsListener, cancellationToken),
        "3" => ShowSnapshotAsync("ACTIVE CONNECTIONS", static entry => !entry.IsListener, cancellationToken),
        "4" => ShowProcessInspectorAsync(cancellationToken),
        "5" => ShowQuickScanAsync(cancellationToken),
        "6" => ShowUpdateCenterAsync(cancellationToken),
        "7" => ShowAboutAsync(),
        _ => Task.CompletedTask
    };

    private async Task ShowLiveAsync(CancellationToken cancellationToken)
    {
        var previous = new HashSet<string>(StringComparer.Ordinal);
        DateTimeOffset started = DateTimeOffset.Now;

        while (true)
        {
            IReadOnlyList<NetworkEntry> entries;
            try
            {
                entries = await _network.CaptureAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                ShowError("Не удалось прочитать сетевые таблицы", ex);
                return;
            }

            _terminal.Clear();
            DrawHeader("LIVE NETWORK MONITOR", $"Сессия: {DateTimeOffset.Now - started:hh\\:mm\\:ss}  •  Записей: {entries.Count}");
            DrawNetworkTable(entries.Take(MaxRows()).ToArray(), previous);
            _terminal.Rule();
            _terminal.WriteLine("  R — обновить сейчас   Q/Esc — назад", ConsoleColor.DarkGray);

            previous = entries.Select(entry => entry.Identity).ToHashSet(StringComparer.Ordinal);

            DateTime until = DateTime.UtcNow.AddSeconds(1);
            while (DateTime.UtcNow < until)
            {
                if (Console.KeyAvailable)
                {
                    ConsoleKey key = Console.ReadKey(intercept: true).Key;
                    if (key is ConsoleKey.Escape or ConsoleKey.Q)
                    {
                        return;
                    }

                    if (key == ConsoleKey.R)
                    {
                        break;
                    }
                }

                await Task.Delay(50, cancellationToken);
            }
        }
    }

    private async Task ShowSnapshotAsync(
        string title,
        Func<NetworkEntry, bool> predicate,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            IReadOnlyList<NetworkEntry> snapshot;
            try
            {
                snapshot = await _terminal.RunWithSpinnerAsync(
                    "Чтение сетевой таблицы Windows",
                    _network.CaptureAsync(cancellationToken));
            }
            catch (Exception ex)
            {
                ShowError("Не удалось получить сетевые данные", ex);
                return;
            }

            NetworkEntry[] entries = snapshot.Where(predicate).ToArray();
            _terminal.Clear();
            DrawHeader(title, $"Найдено: {entries.Length}");
            DrawNetworkTable(entries.Take(MaxRows()).ToArray());
            _terminal.Rule();
            _terminal.WriteLine("  R — обновить   Q/Esc — назад", ConsoleColor.DarkGray);

            ConsoleKey key = Console.ReadKey(intercept: true).Key;
            if (key is ConsoleKey.Escape or ConsoleKey.Q)
            {
                return;
            }
        }
    }

    private async Task ShowProcessInspectorAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<NetworkEntry> snapshot;
        try
        {
            snapshot = await _terminal.RunWithSpinnerAsync(
                "Поиск сетевых процессов",
                _network.CaptureAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            ShowError("Не удалось получить список процессов", ex);
            return;
        }

        NetworkEntry[] entries = snapshot
            .GroupBy(entry => entry.ProcessId)
            .Select(group => group.First())
            .OrderBy(entry => entry.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (entries.Length == 0)
        {
            ShowMessage("PROCESS INSPECTOR", "Сетевые процессы не найдены.", ConsoleColor.Yellow);
            return;
        }

        int selected = 0;
        while (true)
        {
            _terminal.Clear();
            DrawHeader("PROCESS INSPECTOR", "Выберите процесс стрелками и нажмите Enter");
            int start = Math.Clamp(selected - Math.Max(1, MaxRows() / 2), 0, Math.Max(0, entries.Length - MaxRows()));
            foreach ((NetworkEntry entry, int index) in entries.Skip(start).Take(MaxRows()).Select((entry, i) => (entry, start + i)))
            {
                bool active = index == selected;
                _terminal.Write(active ? "  ▶ " : "    ", active ? ConsoleColor.Cyan : ConsoleColor.DarkGray);
                _terminal.Write(Terminal.PadOrTrim(entry.ProcessName, 28), active ? ConsoleColor.White : ConsoleColor.Gray);
                _terminal.WriteLine($" PID {entry.ProcessId,6}   {entry.Protocol,-5} {entry.LocalEndpoint}", active ? ConsoleColor.Cyan : ConsoleColor.DarkGray);
            }

            ConsoleKey key = Console.ReadKey(intercept: true).Key;
            if (key is ConsoleKey.Escape or ConsoleKey.Q)
            {
                return;
            }

            if (key == ConsoleKey.UpArrow)
            {
                selected = (selected - 1 + entries.Length) % entries.Length;
            }
            else if (key == ConsoleKey.DownArrow)
            {
                selected = (selected + 1) % entries.Length;
            }
            else if (key == ConsoleKey.Enter)
            {
                ShowProcessCard(entries[selected].ProcessId, snapshot);
            }
        }
    }

    private void ShowProcessCard(int processId, IReadOnlyList<NetworkEntry> snapshot)
    {
        ProcessMetadata metadata = _processes.Get(processId);
        NetworkEntry[] entries = snapshot.Where(entry => entry.ProcessId == processId).ToArray();

        _terminal.Clear();
        DrawHeader("PROCESS CARD", $"{metadata.Name}  •  PID {metadata.ProcessId}");
        _terminal.Box(
        [
            $"Имя:          {metadata.Name}",
            $"PID:          {metadata.ProcessId}",
            $"Путь:         {metadata.Path ?? "недоступен"}",
            $"Компания:     {metadata.Company ?? "не указана"}",
            $"Описание:     {metadata.Description ?? "не указано"}",
            $"Запущен:      {metadata.StartTime?.ToString("G") ?? "неизвестно"}",
            $"Доступ:       {metadata.AccessStatus}",
            $"Endpoints:    {entries.Length}"
        ], "PROCESS METADATA");
        _terminal.WriteLine();
        DrawNetworkTable(entries.Take(Math.Max(3, MaxRows() - 12)).ToArray());
        WaitBack();
    }

    private async Task ShowQuickScanAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<NetworkEntry> entries;
        try
        {
            entries = await _terminal.RunWithSpinnerAsync(
                "Сканирование listeners и процессов",
                _network.CaptureAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            ShowError("Quick Scan завершился ошибкой", ex);
            return;
        }

        IReadOnlyList<ScanFinding> findings = QuickScanService.Analyze(entries);
        _terminal.Clear();
        DrawHeader("QUICK SCAN", $"Проверено записей: {entries.Count}  •  Findings: {findings.Count}");

        if (findings.Count == 0)
        {
            _terminal.Box(
            [
                "Критичных эвристических признаков не найдено.",
                "Это не является гарантией безопасности системы."
            ], "RESULT", ConsoleColor.Green);
        }
        else
        {
            foreach (ScanFinding finding in findings.Take(MaxRows() / 2))
            {
                ConsoleColor color = SeverityColor(finding.Severity);
                _terminal.Write($"  [{finding.Severity.ToString().ToUpperInvariant(),-6}] ", color);
                _terminal.WriteLine(finding.Title, ConsoleColor.White);
                _terminal.WriteLine($"           {finding.Description}", ConsoleColor.DarkGray);
            }
        }

        _terminal.Rule();
        _terminal.WriteLine("  Findings объясняют факты и не являются malware verdict.", ConsoleColor.DarkGray);
        WaitBack();
    }

    private async Task ShowUpdateCenterAsync(CancellationToken cancellationToken)
    {
        _terminal.Clear();
        DrawHeader("UPDATE CENTER", $"Текущая версия: {Program.Version}");

        UpdateCheckResult result = await _terminal.RunWithSpinnerAsync(
            "Проверка GitHub Releases",
            _updater.CheckAsync(cancellationToken));

        ConsoleColor statusColor = result.Status switch
        {
            UpdateStatus.UpToDate => ConsoleColor.Green,
            UpdateStatus.Available => ConsoleColor.Yellow,
            _ => ConsoleColor.Red
        };

        _terminal.WriteLine();
        _terminal.Box(
        [
            result.Message,
            $"Текущая версия: {Program.Version}",
            $"Последний Release: {result.Version ?? "не определён"}",
            $"ZIP: {(result.Package is null ? "не найден" : result.Package.Name)}",
            $"SHA-256: {(result.Checksum is null ? "не найден" : result.Checksum.Name)}"
        ], "GITHUB RELEASES", statusColor);

        if (result.Status != UpdateStatus.Available)
        {
            WaitBack();
            return;
        }

        _terminal.WriteLine();
        _terminal.WriteLine("  U — скачать, проверить SHA-256 и установить", ConsoleColor.Cyan);
        _terminal.WriteLine("  O — открыть страницу Release   Q/Esc — назад", ConsoleColor.DarkGray);

        while (true)
        {
            ConsoleKey key = Console.ReadKey(intercept: true).Key;
            if (key is ConsoleKey.Escape or ConsoleKey.Q)
            {
                return;
            }

            if (key == ConsoleKey.O)
            {
                GitHubUpdateService.OpenReleasePage(result.ReleaseUrl);
            }

            if (key == ConsoleKey.U)
            {
                if (result.Package is null)
                {
                    ShowMessage("UPDATE CENTER", "В Release отсутствует portable ZIP.", ConsoleColor.Red);
                    return;
                }

                _terminal.WriteLine();
                _terminal.WriteLine("  Подтвердите установку: Y — да, любая другая клавиша — отмена", ConsoleColor.Yellow);
                if (Console.ReadKey(intercept: true).Key != ConsoleKey.Y)
                {
                    return;
                }

                var progress = new Progress<int>(value =>
                {
                    _terminal.Write($"\r  Загрузка: {value,3}% ", ConsoleColor.Cyan);
                    _terminal.Write(new string('█', value / 5), ConsoleColor.Cyan);
                    _terminal.Write(new string('░', 20 - value / 5), ConsoleColor.DarkGray);
                });

                try
                {
                    string script = await _updater.DownloadAndPrepareAsync(result, progress, cancellationToken);
                    _terminal.WriteLine();
                    _terminal.WriteLine("  SHA-256 проверен. PortSentinel будет перезапущен.", ConsoleColor.Green);
                    await Task.Delay(800, cancellationToken);
                    GitHubUpdateService.LaunchInstaller(script);
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    ShowError("Не удалось подготовить обновление", ex);
                    return;
                }
            }
        }
    }

    private Task ShowAboutAsync()
    {
        _terminal.Clear();
        DrawHeader("ABOUT / SYSTEM", "Самостоятельная Windows TUI-утилита");
        _terminal.Box(
        [
            $"PortSentinel:   {Program.Version}",
            ".NET:           8.0 / win-x64 / self-contained",
            "Telemetry:      Windows IP Helper API",
            "Протоколы:      TCP, TCP6, UDP, UDP6",
            "Интерфейс:      полноэкранная Console TUI",
            "Обновления:     GitHub Releases + SHA-256",
            "Режим:          read-only по умолчанию",
            "Автор:          Onmaynec"
        ], "PORTSENTINEL");
        _terminal.WriteLine();
        _terminal.WriteLine("  PortSentinel не является антивирусом и не анализирует payload.", ConsoleColor.Yellow);
        WaitBack();
        return Task.CompletedTask;
    }

    private void DrawHeader(string title, string subtitle)
    {
        AsciiLogo.Draw(_terminal, compact: true);
        _terminal.Rule(title);
        _terminal.WriteLine($"  {subtitle}", ConsoleColor.DarkGray);
        _terminal.WriteLine();
    }

    private void DrawNetworkTable(IReadOnlyList<NetworkEntry> entries, HashSet<string>? previous = null)
    {
        int processWidth = _terminal.Width >= 115 ? 24 : 18;
        int endpointWidth = _terminal.Width >= 115 ? 32 : 24;

        _terminal.WriteLine(
            $"  {Terminal.PadOrTrim("PROCESS", processWidth)} {"PID",6} {"PROTO",6} " +
            $"{Terminal.PadOrTrim("LOCAL", endpointWidth)} {Terminal.PadOrTrim("REMOTE / STATE", endpointWidth)}",
            ConsoleColor.DarkCyan);
        _terminal.WriteLine("  " + new string('─', Math.Min(_terminal.Width - 4, processWidth + endpointWidth * 2 + 22)), ConsoleColor.DarkGray);

        foreach (NetworkEntry entry in entries)
        {
            bool isNew = previous is not null && previous.Count > 0 && !previous.Contains(entry.Identity);
            ConsoleColor color = isNew ? ConsoleColor.Yellow : entry.IsListener ? ConsoleColor.Green : ConsoleColor.Gray;
            string remote = entry.IsListener ? entry.State : $"{entry.RemoteEndpoint} {entry.State}";
            _terminal.WriteLine(
                $"{(isNew ? "+" : " ")} {Terminal.PadOrTrim(entry.ProcessName, processWidth)} {entry.ProcessId,6} " +
                $"{Terminal.PadOrTrim(entry.Protocol, 6)} {Terminal.PadOrTrim(entry.LocalEndpoint, endpointWidth)} " +
                Terminal.PadOrTrim(remote, endpointWidth),
                color);
        }

        if (entries.Count == 0)
        {
            _terminal.WriteLine("  Нет записей для отображения.", ConsoleColor.Yellow);
        }
    }

    private int MaxRows() => Math.Max(5, _terminal.Height - 13);

    private static ConsoleColor SeverityColor(FindingSeverity severity) => severity switch
    {
        FindingSeverity.High => ConsoleColor.Red,
        FindingSeverity.Medium => ConsoleColor.Yellow,
        FindingSeverity.Low => ConsoleColor.DarkYellow,
        _ => ConsoleColor.Cyan
    };

    private void ShowMessage(string title, string message, ConsoleColor color)
    {
        _terminal.Clear();
        DrawHeader(title, string.Empty);
        _terminal.Box([message], "MESSAGE", color);
        WaitBack();
    }

    private void ShowError(string title, Exception exception)
    {
        _terminal.Clear();
        DrawHeader("ERROR", title);
        _terminal.Box([exception.Message], "DIAGNOSTICS", ConsoleColor.Red);
        WaitBack();
    }

    private void WaitBack()
    {
        _terminal.Rule();
        _terminal.WriteLine("  Нажмите любую клавишу, чтобы вернуться...", ConsoleColor.DarkGray);
        Console.ReadKey(intercept: true);
    }
}
