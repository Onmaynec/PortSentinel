using PortSentinel.Models;
using PortSentinel.Services;
using PortSentinel.UI;

namespace PortSentinel.App;

internal sealed class PortSentinelV53App
{
    private readonly Terminal _terminal;
    private readonly EtwTelemetryService _etw;
    private readonly TelemetryArchiveService _archive;
    private readonly TelemetryArchiveOperationsService _operations;
    private readonly PortSentinelV52App _archiveControlCenter;
    private int _selected;

    private static readonly CaptureProfile[] Profiles =
    [
        new("1", "QUICK", TimeSpan.FromSeconds(5), "Короткая проверка backend и текущей активности"),
        new("2", "STANDARD", TimeSpan.FromSeconds(15), "Обычное окно для повседневной диагностики"),
        new("3", "DEEP", TimeSpan.FromSeconds(30), "Расширенный lifecycle capture"),
        new("4", "INVESTIGATOR", TimeSpan.FromSeconds(60), "Максимальное окно текущего release")
    ];

    private static readonly MenuItem[] Menu =
    [
        new("1", "CAPTURE PROFILES", "5/15/30/60 секунд с автоматическим архивированием", ConsoleColor.Cyan),
        new("2", "ARCHIVE SEARCH", "Поиск по процессам, IP, notes, kind и backend", ConsoleColor.DarkCyan),
        new("3", "SELECTIVE COMPARISON", "Выбор произвольной пары capture-сессий", ConsoleColor.Magenta),
        new("4", "RETENTION CENTER", "Preview и безопасная очистка старых captures", ConsoleColor.Yellow),
        new("5", "TELEMETRY ARCHIVE", "History и latest comparison из v0.5.2", ConsoleColor.Green),
        new("0", "EXIT", "Безопасно закрыть PortSentinel", ConsoleColor.Red)
    ];

    public PortSentinelV53App(
        Terminal terminal,
        EtwTelemetryService etw,
        TelemetryArchiveService archive,
        TelemetryArchiveOperationsService operations,
        PortSentinelV52App archiveControlCenter)
    {
        _terminal = terminal;
        _etw = etw;
        _archive = archive;
        _operations = operations;
        _archiveControlCenter = archiveControlCenter;
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
        _terminal.WriteLine($"  v{Program.Version}  •  ARCHIVE OPERATIONS", ConsoleColor.DarkGray);
        _terminal.Rule("PORTSENTINEL CONTROL NODE");

        for (int i = 0; i < Menu.Length; i++)
        {
            MenuItem item = Menu[i];
            bool active = i == _selected;
            _terminal.Write(active ? "  ▶ " : "    ", active ? item.Accent : ConsoleColor.DarkGray);
            _terminal.Write($"[{item.Key}] {Terminal.PadOrTrim(item.Title, 26)}",
                active ? ConsoleColor.White : item.Accent);
            _terminal.WriteLine($"  {item.Description}",
                active ? ConsoleColor.Gray : ConsoleColor.DarkGray);
        }

        _terminal.Rule();
        _terminal.WriteLine("  ↑/↓ или W/S — выбор   Enter — открыть   Q/Esc — выход", ConsoleColor.DarkGray);
    }

    private Task OpenAsync(string key, CancellationToken token) => key switch
    {
        "1" => CaptureProfilesAsync(token),
        "2" => ArchiveSearchAsync(token),
        "3" => SelectiveComparisonAsync(token),
        "4" => RetentionCenterAsync(token),
        "5" => _archiveControlCenter.RunAsync(token),
        _ => Task.CompletedTask
    };

    private async Task CaptureProfilesAsync(CancellationToken token)
    {
        int selected = 0;
        while (true)
        {
            _terminal.Clear();
            Header("CAPTURE PROFILES", "Выберите ограниченное capture window");
            for (int i = 0; i < Profiles.Length; i++)
            {
                CaptureProfile profile = Profiles[i];
                bool active = i == selected;
                _terminal.Write(active ? "  ▶ " : "    ", active ? ConsoleColor.Cyan : ConsoleColor.DarkGray);
                _terminal.Write($"[{profile.Key}] {Terminal.PadOrTrim(profile.Name, 16)}",
                    active ? ConsoleColor.White : ConsoleColor.Cyan);
                _terminal.Write($" {profile.Duration.TotalSeconds,3:0} sec  ", ConsoleColor.DarkCyan);
                _terminal.WriteLine(profile.Description, active ? ConsoleColor.Gray : ConsoleColor.DarkGray);
            }
            _terminal.Rule();
            _terminal.WriteLine("  ↑/↓ — выбор   Enter — запустить   Q/Esc — назад", ConsoleColor.DarkGray);

            ConsoleKeyInfo key = Console.ReadKey(true);
            if (key.Key is ConsoleKey.Q or ConsoleKey.Escape) return;
            if (key.Key == ConsoleKey.UpArrow)
                selected = (selected - 1 + Profiles.Length) % Profiles.Length;
            else if (key.Key == ConsoleKey.DownArrow)
                selected = (selected + 1) % Profiles.Length;
            else if (char.IsDigit(key.KeyChar))
            {
                int index = Array.FindIndex(Profiles, item => item.Key[0] == key.KeyChar);
                if (index >= 0) selected = index;
                else continue;
                await RunProfileAsync(Profiles[selected], token);
            }
            else if (key.Key == ConsoleKey.Enter)
            {
                await RunProfileAsync(Profiles[selected], token);
            }
        }
    }

    private async Task RunProfileAsync(CaptureProfile profile, CancellationToken token)
    {
        EtwCaptureResult result = await _terminal.RunWithSpinnerAsync(
            $"{profile.Name} capture: {profile.Duration.TotalSeconds:0} секунд",
            _etw.CaptureAsync(profile.Duration, token));
        long id = await _archive.SaveCaptureAsync(result, token);
        TelemetryCapture? capture = await _archive.LoadCaptureAsync(id, token);
        if (capture is null)
        {
            Message("CAPTURE PROFILE", "Capture сохранён, но не удалось перечитать запись.", ConsoleColor.Red);
            return;
        }

        await CaptureResultAsync(profile, capture, token);
    }

    private async Task CaptureResultAsync(
        CaptureProfile profile,
        TelemetryCapture capture,
        CancellationToken token)
    {
        int selected = 0;
        while (true)
        {
            selected = Math.Clamp(selected, 0, Math.Max(0, capture.Events.Count - 1));
            TelemetryCaptureSummary summary = capture.Summary;
            _terminal.Clear();
            Header($"{profile.Name} CAPTURE #{summary.Id}",
                $"{profile.Duration.TotalSeconds:0} sec  •  {summary.Mode}  •  Events: {summary.EventCount}");
            _terminal.WriteLine($"  {summary.Status}",
                summary.Mode == EtwBackendMode.KernelEtw ? ConsoleColor.Green : ConsoleColor.Yellow);
            _terminal.WriteLine(
                $"  Connect: {summary.ConnectCount}  Accept: {summary.AcceptCount}  Disconnect: {summary.DisconnectCount}  Retransmit: {summary.RetransmitCount}",
                ConsoleColor.DarkCyan);
            _terminal.WriteLine();
            DrawEvents(capture.Events, selected);
            _terminal.Rule();
            _terminal.WriteLine("  ↑/↓ — выбор   Enter — детали   J/M — экспорт   Q/Esc — назад", ConsoleColor.DarkGray);

            ConsoleKey key = Console.ReadKey(true).Key;
            if (key is ConsoleKey.Q or ConsoleKey.Escape) return;
            if (key is ConsoleKey.J or ConsoleKey.M)
            {
                string path = await _archive.ExportCaptureAsync(
                    capture,
                    key == ConsoleKey.J ? "json" : "markdown",
                    token);
                Message("CAPTURE EXPORT COMPLETE", $"Отчёт сохранён:\n{path}", ConsoleColor.Green);
                continue;
            }
            if (capture.Events.Count == 0) continue;
            if (key == ConsoleKey.UpArrow)
                selected = (selected - 1 + capture.Events.Count) % capture.Events.Count;
            else if (key == ConsoleKey.DownArrow)
                selected = (selected + 1) % capture.Events.Count;
            else if (key == ConsoleKey.Enter)
                ShowEvent(capture.Events[selected], summary.Id, summary.Mode);
        }
    }

    private async Task ArchiveSearchAsync(CancellationToken token)
    {
        while (true)
        {
            _terminal.Clear();
            Header("ARCHIVE SEARCH", "Параметризованный поиск по локальной SQLite telemetry");
            _terminal.WriteLine("  [1] TEXT          process, IP address или note", ConsoleColor.Cyan);
            _terminal.WriteLine("  [2] RETRANSMIT    только retransmit events", ConsoleColor.Yellow);
            _terminal.WriteLine("  [3] DISCONNECT    только disconnect events", ConsoleColor.DarkGray);
            _terminal.WriteLine("  [4] FALLBACK      события из SnapshotFallback captures", ConsoleColor.Magenta);
            _terminal.WriteLine("  [5] LISTENER      fallback listeners", ConsoleColor.Green);
            _terminal.WriteLine();
            _terminal.WriteLine("  Q/Esc — назад", ConsoleColor.DarkGray);

            ConsoleKey key = Console.ReadKey(true).Key;
            if (key is ConsoleKey.Q or ConsoleKey.Escape) return;

            TelemetrySearchQuery? query = key switch
            {
                ConsoleKey.D2 or ConsoleKey.NumPad2 => new("", "RETRANSMIT", null),
                ConsoleKey.D3 or ConsoleKey.NumPad3 => new("", "DISCONNECT", null),
                ConsoleKey.D4 or ConsoleKey.NumPad4 => new("", null, EtwBackendMode.SnapshotFallback),
                ConsoleKey.D5 or ConsoleKey.NumPad5 => new("", "LISTENER", EtwBackendMode.SnapshotFallback),
                _ => null
            };

            if (key is ConsoleKey.D1 or ConsoleKey.NumPad1)
            {
                _terminal.Clear();
                Header("TEXT SEARCH", "Process name, local/remote IP или diagnostic note");
                _terminal.Write("  Search: ", ConsoleColor.Cyan);
                string text = (Console.ReadLine() ?? string.Empty).Trim();
                if (text.Length == 0) continue;
                query = new TelemetrySearchQuery(text, null, null);
            }

            if (query is null) continue;
            IReadOnlyList<TelemetrySearchResult> results =
                await _operations.SearchAsync(query, token);
            await SearchResultsAsync(results);
        }
    }

    private Task SearchResultsAsync(IReadOnlyList<TelemetrySearchResult> results)
    {
        if (results.Count == 0)
        {
            Message("ARCHIVE SEARCH", "Совпадений не найдено.", ConsoleColor.Yellow);
            return Task.CompletedTask;
        }

        int selected = 0;
        while (true)
        {
            selected = Math.Clamp(selected, 0, results.Count - 1);
            _terminal.Clear();
            Header("ARCHIVE SEARCH RESULTS", $"Найдено событий: {results.Count}");
            int rows = MaxRows();
            int start = Math.Clamp(selected - rows / 2, 0, Math.Max(0, results.Count - rows));

            foreach ((TelemetrySearchResult result, int index) in results.Skip(start).Take(rows)
                         .Select((item, i) => (item, start + i)))
            {
                bool active = index == selected;
                ConsoleColor accent = EventColor(result.Event.Kind);
                _terminal.Write(active ? "  ▶ " : "    ", active ? accent : ConsoleColor.DarkGray);
                _terminal.Write($"#{result.CaptureId,-5} ", active ? ConsoleColor.White : ConsoleColor.Gray);
                _terminal.Write(Terminal.PadOrTrim(result.Event.Kind, 12), accent);
                _terminal.Write(Terminal.PadOrTrim(result.Event.ProcessName, 20), active ? ConsoleColor.White : ConsoleColor.Gray);
                _terminal.WriteLine(
                    $" {Terminal.PadOrTrim(result.Event.RemoteEndpoint, Math.Max(18, _terminal.Width - 57))}",
                    active ? ConsoleColor.Cyan : ConsoleColor.DarkGray);
            }

            _terminal.Rule();
            _terminal.WriteLine("  ↑/↓ — выбор   Enter — детали   Q/Esc — назад", ConsoleColor.DarkGray);
            ConsoleKey key = Console.ReadKey(true).Key;
            if (key is ConsoleKey.Q or ConsoleKey.Escape) return Task.CompletedTask;
            if (key == ConsoleKey.UpArrow)
                selected = (selected - 1 + results.Count) % results.Count;
            else if (key == ConsoleKey.DownArrow)
                selected = (selected + 1) % results.Count;
            else if (key == ConsoleKey.Enter)
            {
                TelemetrySearchResult result = results[selected];
                ShowEvent(result.Event, result.CaptureId, result.Mode);
            }
        }
    }

    private async Task SelectiveComparisonAsync(CancellationToken token)
    {
        IReadOnlyList<TelemetryCaptureSummary> captures = await _archive.ListCapturesAsync(50, token);
        if (captures.Count < 2)
        {
            Message("SELECTIVE COMPARISON", "Для сравнения нужны минимум две capture-сессии.", ConsoleColor.Yellow);
            return;
        }

        TelemetryCaptureSummary? first = SelectCapture(captures, "SELECT FIRST CAPTURE", null);
        if (first is null) return;
        TelemetryCaptureSummary? second = SelectCapture(captures, "SELECT SECOND CAPTURE", first.Id);
        if (second is null) return;

        TelemetryCaptureComparison? comparison =
            await _operations.CompareAsync(first.Id, second.Id, token);
        if (comparison is null)
        {
            Message("SELECTIVE COMPARISON", "Не удалось загрузить выбранную пару captures.", ConsoleColor.Red);
            return;
        }

        await ComparisonViewAsync(comparison, token);
    }

    private TelemetryCaptureSummary? SelectCapture(
        IReadOnlyList<TelemetryCaptureSummary> captures,
        string title,
        long? excludedId)
    {
        TelemetryCaptureSummary[] available = captures
            .Where(item => item.Id != excludedId)
            .ToArray();
        int selected = 0;

        while (true)
        {
            _terminal.Clear();
            Header(title, excludedId.HasValue ? $"Capture #{excludedId.Value} уже выбран" : "Выберите capture");
            int rows = MaxRows();
            int start = Math.Clamp(selected - rows / 2, 0, Math.Max(0, available.Length - rows));
            foreach ((TelemetryCaptureSummary item, int index) in available.Skip(start).Take(rows)
                         .Select((item, i) => (item, start + i)))
            {
                bool active = index == selected;
                ConsoleColor accent = item.Mode == EtwBackendMode.KernelEtw
                    ? ConsoleColor.Cyan
                    : ConsoleColor.Yellow;
                _terminal.Write(active ? "  ▶ " : "    ", active ? accent : ConsoleColor.DarkGray);
                _terminal.Write($"#{item.Id,-5} ", active ? ConsoleColor.White : ConsoleColor.Gray);
                _terminal.Write($"{item.StartedAt:dd.MM HH:mm:ss} ", ConsoleColor.DarkGray);
                _terminal.Write(Terminal.PadOrTrim(item.Mode.ToString(), 18), accent);
                _terminal.WriteLine($" Events: {item.EventCount}", active ? ConsoleColor.White : ConsoleColor.DarkGray);
            }
            _terminal.Rule();
            _terminal.WriteLine("  ↑/↓ — выбор   Enter — подтвердить   Q/Esc — отмена", ConsoleColor.DarkGray);

            ConsoleKey key = Console.ReadKey(true).Key;
            if (key is ConsoleKey.Q or ConsoleKey.Escape) return null;
            if (key == ConsoleKey.UpArrow)
                selected = (selected - 1 + available.Length) % available.Length;
            else if (key == ConsoleKey.DownArrow)
                selected = (selected + 1) % available.Length;
            else if (key == ConsoleKey.Enter)
                return available[selected];
        }
    }

    private async Task ComparisonViewAsync(
        TelemetryCaptureComparison comparison,
        CancellationToken token)
    {
        int selected = 0;
        while (true)
        {
            selected = Math.Clamp(selected, 0, Math.Max(0, comparison.AddedEvents.Count - 1));
            _terminal.Clear();
            Header("SELECTIVE COMPARISON",
                $"#{comparison.Older.Id} → #{comparison.Newer.Id}  •  Added: {comparison.AddedEvents.Count}  •  Missing: {comparison.MissingFingerprints.Count}");
            _terminal.WriteLine(
                $"  Events: {comparison.OlderEventCount} → {comparison.NewerEventCount}",
                ConsoleColor.DarkCyan);
            _terminal.WriteLine("  Fingerprint исключает PID; результат не является threat verdict.", ConsoleColor.Yellow);
            _terminal.WriteLine();
            DrawEvents(comparison.AddedEvents, selected);
            _terminal.Rule();
            _terminal.WriteLine("  ↑/↓ — выбор   Enter — детали   X — missing   J/M — экспорт   Q/Esc — назад", ConsoleColor.DarkGray);

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
                ShowEvent(comparison.AddedEvents[selected], comparison.Newer.Id, comparison.Newer.Mode);
        }
    }

    private async Task RetentionCenterAsync(CancellationToken token)
    {
        int[] policies = [25, 50, 100, 250];
        int selected = 1;

        while (true)
        {
            TelemetryArchiveStats stats = await _operations.GetStatsAsync(token);
            _terminal.Clear();
            Header("RETENTION CENTER", "Удаляются только старые telemetry captures и связанные events");
            _terminal.Box(
            [
                $"Captures:       {stats.CaptureCount}",
                $"Events:         {stats.EventCount}",
                $"Oldest:         {stats.OldestCapture?.ToString("O") ?? "—"}",
                $"Newest:         {stats.NewestCapture?.ToString("O") ?? "—"}",
                $"Database size:  {FormatBytes(stats.DatabaseBytes)}"
            ], "ARCHIVE STATUS", ConsoleColor.DarkCyan);
            _terminal.WriteLine();

            for (int i = 0; i < policies.Length; i++)
            {
                bool active = i == selected;
                _terminal.Write(active ? "  ▶ " : "    ", active ? ConsoleColor.Yellow : ConsoleColor.DarkGray);
                _terminal.WriteLine($"Keep latest {policies[i]} captures",
                    active ? ConsoleColor.White : ConsoleColor.Gray);
            }
            _terminal.Rule();
            _terminal.WriteLine("  ↑/↓ — политика   Enter — preview   Q/Esc — назад", ConsoleColor.DarkGray);

            ConsoleKey key = Console.ReadKey(true).Key;
            if (key is ConsoleKey.Q or ConsoleKey.Escape) return;
            if (key == ConsoleKey.UpArrow)
                selected = (selected - 1 + policies.Length) % policies.Length;
            else if (key == ConsoleKey.DownArrow)
                selected = (selected + 1) % policies.Length;
            else if (key == ConsoleKey.Enter)
            {
                TelemetryRetentionPreview preview =
                    await _operations.PreviewRetentionAsync(policies[selected], token);
                bool apply = ConfirmRetention(preview);
                if (!apply) continue;
                TelemetryRetentionPreview deleted =
                    await _operations.ApplyRetentionAsync(policies[selected], token);
                Message(
                    "RETENTION COMPLETE",
                    $"Удалено captures: {deleted.DeleteCaptureCount}\nУдалено events: {deleted.DeleteEventCount}\nСохранено последних: {deleted.KeepLatest}",
                    ConsoleColor.Green);
            }
        }
    }

    private bool ConfirmRetention(TelemetryRetentionPreview preview)
    {
        _terminal.Clear();
        Header("RETENTION PREVIEW", $"Keep latest: {preview.KeepLatest}");
        _terminal.Box(
        [
            $"Captures to delete: {preview.DeleteCaptureCount}",
            $"Events to delete:   {preview.DeleteEventCount}",
            $"Delete through:     {preview.DeleteThrough?.ToString("O") ?? "—"}",
            "Scope:              telemetry archive only",
            "Sessions/baselines: untouched"
        ], "DRY RUN", preview.DeleteCaptureCount == 0 ? ConsoleColor.Green : ConsoleColor.Yellow);

        if (preview.DeleteCaptureCount == 0)
        {
            Back();
            return false;
        }

        _terminal.Rule();
        _terminal.WriteLine("  Нажмите Y для удаления. Любая другая клавиша отменяет операцию.", ConsoleColor.Red);
        return Console.ReadKey(true).Key == ConsoleKey.Y;
    }

    private void DrawEvents(IReadOnlyList<EtwNetworkEvent> events, int selected)
    {
        if (events.Count == 0)
        {
            _terminal.WriteLine("  События отсутствуют.", ConsoleColor.Yellow);
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

    private void ShowEvent(
        EtwNetworkEvent item,
        long captureId,
        EtwBackendMode mode)
    {
        _terminal.Clear();
        Header($"TELEMETRY EVENT #{item.Sequence}", $"Capture #{captureId}  •  {mode}");
        _terminal.Box(
        [
            $"Time:          {item.Timestamp:O}",
            $"Kind:          {item.Kind}",
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
            _terminal.WriteLine("  Исчезнувших fingerprints нет.", ConsoleColor.Green);
        else
            foreach (string item in missing.Take(MaxRows()))
                _terminal.WriteLine($"  • {Terminal.PadOrTrim(item, Math.Max(20, _terminal.Width - 6))}", ConsoleColor.Gray);
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

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return $"{value:0.##} {units[unit]}";
    }

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
