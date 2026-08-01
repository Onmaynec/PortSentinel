using PortSentinel.Models;
using PortSentinel.Services;
using PortSentinel.UI;

namespace PortSentinel.App;

internal sealed class PortSentinelV4App
{
    private readonly Terminal _terminal;
    private readonly NetworkSnapshotService _network;
    private readonly SessionStore _store;
    private readonly BaselineFingerprintService _baselines;
    private readonly RuleEngine _rules;
    private readonly PortSentinelApp _networkTools;
    private int _selected;

    private static readonly MenuItem[] Menu =
    [
        new("1", "LIVE SESSION RECORDER", "Мониторинг с автоматической записью в SQLite", ConsoleColor.Cyan),
        new("2", "SESSION HISTORY", "История, просмотр и экспорт JSON/Markdown", ConsoleColor.DarkCyan),
        new("3", "BASELINE CENTER", "Стабильные fingerprints и сравнение состояния", ConsoleColor.Magenta),
        new("4", "EXPLAINABLE RULES", "Evidence, confidence, Authenticode и SHA-256", ConsoleColor.Yellow),
        new("5", "NETWORK TOOLS", "Live, listeners, connections, processes, scan и updates", ConsoleColor.Green),
        new("6", "STORAGE STATUS", "Пути базы данных и каталога отчётов", ConsoleColor.Blue),
        new("0", "EXIT", "Безопасно закрыть PortSentinel", ConsoleColor.Red)
    ];

    public PortSentinelV4App(
        Terminal terminal,
        NetworkSnapshotService network,
        SessionStore store,
        BaselineFingerprintService baselines,
        RuleEngine rules,
        PortSentinelApp networkTools)
    {
        _terminal = terminal;
        _network = network;
        _store = store;
        _baselines = baselines;
        _rules = rules;
        _networkTools = networkTools;
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
        _terminal.WriteLine($"  v{Program.Version}  •  BASELINE & EXPLAINABLE RULES", ConsoleColor.DarkGray);
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
        "1" => RecordLiveAsync(token),
        "2" => SessionHistoryAsync(token),
        "3" => BaselineCenterAsync(token),
        "4" => RulesCenterAsync(token),
        "5" => _networkTools.RunAsync(token),
        "6" => StorageStatusAsync(),
        _ => Task.CompletedTask
    };

    private async Task RecordLiveAsync(CancellationToken token)
    {
        DateTimeOffset started = DateTimeOffset.Now;
        var observed = new Dictionary<string, NetworkEntry>(StringComparer.Ordinal);
        var previous = new HashSet<string>(StringComparer.Ordinal);

        while (true)
        {
            IReadOnlyList<NetworkEntry> entries = await _network.CaptureAsync(token);
            foreach (NetworkEntry entry in entries)
                observed[entry.Identity] = entry;

            _terminal.Clear();
            Header("LIVE SESSION RECORDER",
                $"Время: {DateTimeOffset.Now - started:hh\\:mm\\:ss}  •  Сейчас: {entries.Count}  •  Уникальных: {observed.Count}");
            DrawTable(entries.Take(MaxRows()).ToArray(), previous);
            _terminal.Rule();
            _terminal.WriteLine("  R — обновить   S — сохранить копию   Q/Esc — сохранить и выйти", ConsoleColor.DarkGray);
            previous = entries.Select(entry => entry.Identity).ToHashSet(StringComparer.Ordinal);

            DateTime until = DateTime.UtcNow.AddSeconds(1);
            while (DateTime.UtcNow < until)
            {
                if (!Console.KeyAvailable)
                {
                    await Task.Delay(50, token);
                    continue;
                }

                ConsoleKey key = Console.ReadKey(true).Key;
                if (key is ConsoleKey.Q or ConsoleKey.Escape)
                {
                    await SaveAsync("Live session", started, observed.Values, token);
                    return;
                }

                if (key == ConsoleKey.S)
                    await SaveAsync("Live snapshot", started, observed.Values, token);
                if (key == ConsoleKey.R)
                    break;
            }
        }
    }

    private async Task SaveAsync(
        string name,
        DateTimeOffset started,
        IEnumerable<NetworkEntry> entries,
        CancellationToken token)
    {
        NetworkEntry[] data = entries.ToArray();
        if (data.Length == 0) return;
        long id = await _store.SaveSessionAsync(name, started, data, token);
        _terminal.WriteLine($"\n  [OK] Сессия #{id} сохранена.", ConsoleColor.Green);
        await Task.Delay(500, token);
    }

    private async Task SessionHistoryAsync(CancellationToken token)
    {
        IReadOnlyList<SessionSummary> sessions = await _store.ListSessionsAsync(50, token);
        if (sessions.Count == 0)
        {
            Message("SESSION HISTORY", "Сессий пока нет. Запустите Live Session Recorder.", ConsoleColor.Yellow);
            return;
        }

        int selected = 0;
        while (true)
        {
            _terminal.Clear();
            Header("SESSION HISTORY", $"Последние сессии: {sessions.Count}");
            int start = Math.Clamp(selected - MaxRows() / 2, 0, Math.Max(0, sessions.Count - MaxRows()));
            foreach ((SessionSummary session, int index) in sessions.Skip(start).Take(MaxRows())
                         .Select((session, i) => (session, start + i)))
            {
                bool active = index == selected;
                _terminal.Write(active ? "  ▶ " : "    ", active ? ConsoleColor.Cyan : ConsoleColor.DarkGray);
                _terminal.Write($"#{session.Id,-5} {Terminal.PadOrTrim(session.Name, 22)}",
                    active ? ConsoleColor.White : ConsoleColor.Gray);
                _terminal.WriteLine(
                    $" {session.StartedAt:dd.MM HH:mm}  E:{session.EntryCount,4} P:{session.ProcessCount,3} L:{session.ListenerCount,3}",
                    active ? ConsoleColor.Cyan : ConsoleColor.DarkGray);
            }

            _terminal.Rule();
            _terminal.WriteLine("  Enter — открыть   J — JSON   M — Markdown   Q/Esc — назад", ConsoleColor.DarkGray);
            ConsoleKey key = Console.ReadKey(true).Key;
            if (key is ConsoleKey.Q or ConsoleKey.Escape) return;
            if (key == ConsoleKey.UpArrow) selected = (selected - 1 + sessions.Count) % sessions.Count;
            else if (key == ConsoleKey.DownArrow) selected = (selected + 1) % sessions.Count;
            else if (key == ConsoleKey.Enter)
            {
                IReadOnlyList<NetworkEntry> entries =
                    await _store.LoadSessionEntriesAsync(sessions[selected].Id, token);
                _terminal.Clear();
                Header($"SESSION #{sessions[selected].Id}", sessions[selected].Name);
                DrawTable(entries.Take(MaxRows()).ToArray());
                Back();
            }
            else if (key is ConsoleKey.J or ConsoleKey.M)
            {
                IReadOnlyList<NetworkEntry> entries =
                    await _store.LoadSessionEntriesAsync(sessions[selected].Id, token);
                string path = await _store.ExportSessionAsync(
                    sessions[selected], entries, key == ConsoleKey.J ? "json" : "markdown", token);
                Message("EXPORT COMPLETE", $"Отчёт сохранён:\n{path}", ConsoleColor.Green);
            }
        }
    }

    private async Task BaselineCenterAsync(CancellationToken token)
    {
        while (true)
        {
            IReadOnlyList<BaselineSummary> baselines = await _store.ListBaselinesAsync(token);
            _terminal.Clear();
            Header("BASELINE CENTER", $"Сохранено профилей: {baselines.Count}");
            _terminal.WriteLine("  [C] Создать или перезаписать baseline «default»", ConsoleColor.Cyan);
            _terminal.WriteLine("  [R] Сравнить текущее состояние со стабильным fingerprint", ConsoleColor.Magenta);
            _terminal.WriteLine();
            foreach (BaselineSummary baseline in baselines.Take(MaxRows() - 4))
                _terminal.WriteLine(
                    $"  • {baseline.Name,-18} {baseline.CreatedAt:G}  entries: {baseline.EntryCount}",
                    ConsoleColor.Gray);
            _terminal.Rule();
            _terminal.WriteLine("  C — создать   R — сравнить   Q/Esc — назад", ConsoleColor.DarkGray);

            ConsoleKey key = Console.ReadKey(true).Key;
            if (key is ConsoleKey.Q or ConsoleKey.Escape) return;
            if (key == ConsoleKey.C)
            {
                IReadOnlyList<NetworkEntry> current = await _terminal.RunWithSpinnerAsync(
                    "Создание baseline", _network.CaptureAsync(token));
                await _store.CreateBaselineAsync("default", current, token);
                Message("BASELINE SAVED", $"Baseline «default»: {current.Count} записей.", ConsoleColor.Green);
            }
            else if (key == ConsoleKey.R)
            {
                IReadOnlyList<NetworkEntry> current = await _terminal.RunWithSpinnerAsync(
                    "Сравнение с baseline", _network.CaptureAsync(token));
                BaselineComparison? comparison =
                    await _baselines.CompareAsync("default", current, token);
                if (comparison is null)
                {
                    Message("BASELINE", "Сначала создайте baseline «default».", ConsoleColor.Yellow);
                    continue;
                }

                _terminal.Clear();
                Header("BASELINE DEVIATIONS",
                    $"Новых: {comparison.NewEntries.Count}  •  Исчезло: {comparison.MissingIdentities.Count}");
                DrawTable(comparison.NewEntries.Take(MaxRows()).ToArray());
                _terminal.Rule();
                _terminal.WriteLine("  Fingerprint не зависит от PID и переживает перезапуск процесса.", ConsoleColor.Cyan);
                _terminal.WriteLine("  Отличие от baseline не является доказательством угрозы.", ConsoleColor.Yellow);
                Back();
            }
        }
    }

    private async Task RulesCenterAsync(CancellationToken token)
    {
        int selected = 0;

        while (true)
        {
            IReadOnlyList<NetworkEntry> entries;
            try
            {
                entries = await _terminal.RunWithSpinnerAsync(
                    "Сбор сетевого состояния", _network.CaptureAsync(token));
            }
            catch (Exception ex)
            {
                Message("EXPLAINABLE RULES", $"Не удалось получить сетевые данные: {ex.Message}", ConsoleColor.Red);
                return;
            }

            BaselineComparison? baseline =
                await _baselines.CompareAsync("default", entries, token);
            RuleAnalysis analysis = await _terminal.RunWithSpinnerAsync(
                "Authenticode, SHA-256 и rule engine",
                _rules.AnalyzeAsync(entries, baseline, token));

            selected = Math.Clamp(selected, 0, Math.Max(0, analysis.Findings.Count - 1));

            while (true)
            {
                _terminal.Clear();
                Header("EXPLAINABLE RULES",
                    $"Rules: {analysis.RulesEvaluated}  •  Findings: {analysis.Findings.Count}  •  Enriched: {analysis.EnrichedProcesses}");
                _terminal.WriteLine(
                    analysis.BaselineAvailable
                        ? "  Baseline «default» подключён: NewListenerRule активен."
                        : "  Baseline «default» отсутствует: NewListenerRule пропущен.",
                    analysis.BaselineAvailable ? ConsoleColor.Green : ConsoleColor.Yellow);

                if (analysis.LimitedProcesses > 0)
                {
                    _terminal.WriteLine(
                        $"  Ограниченное enrichment: {analysis.LimitedProcesses} executable.",
                        ConsoleColor.Yellow);
                }

                _terminal.WriteLine();

                if (analysis.Findings.Count == 0)
                {
                    _terminal.Box(
                    [
                        "Активные правила не нашли отклонений.",
                        "Это не является гарантией безопасности системы."
                    ], "RESULT", ConsoleColor.Green);
                }
                else
                {
                    int rows = Math.Max(3, MaxRows() - 3);
                    int start = Math.Clamp(selected - rows / 2, 0, Math.Max(0, analysis.Findings.Count - rows));
                    foreach ((RuleFinding finding, int index) in analysis.Findings.Skip(start).Take(rows)
                                 .Select((finding, i) => (finding, start + i)))
                    {
                        bool active = index == selected;
                        ConsoleColor color = RuleSeverityColor(finding.Severity);
                        _terminal.Write(active ? "  ▶ " : "    ", active ? color : ConsoleColor.DarkGray);
                        _terminal.Write($"[{finding.Severity.ToString().ToUpperInvariant(),-8}] ", color);
                        _terminal.Write(Terminal.PadOrTrim(finding.RuleId, 12), ConsoleColor.DarkCyan);
                        _terminal.WriteLine(
                            Terminal.PadOrTrim(finding.Title, Math.Max(24, _terminal.Width - 34)),
                            active ? ConsoleColor.White : ConsoleColor.Gray);
                    }
                }

                _terminal.Rule();
                _terminal.WriteLine(
                    analysis.Findings.Count == 0
                        ? "  R — повторить анализ   Q/Esc — назад"
                        : "  ↑/↓ — выбор   Enter — evidence   R — повторить   Q/Esc — назад",
                    ConsoleColor.DarkGray);

                ConsoleKey key = Console.ReadKey(true).Key;
                if (key is ConsoleKey.Q or ConsoleKey.Escape) return;
                if (key == ConsoleKey.R) break;
                if (analysis.Findings.Count == 0) continue;
                if (key == ConsoleKey.UpArrow)
                    selected = (selected - 1 + analysis.Findings.Count) % analysis.Findings.Count;
                else if (key == ConsoleKey.DownArrow)
                    selected = (selected + 1) % analysis.Findings.Count;
                else if (key == ConsoleKey.Enter)
                    ShowRuleFinding(analysis.Findings[selected]);
            }
        }
    }

    private void ShowRuleFinding(RuleFinding finding)
    {
        _terminal.Clear();
        Header($"{finding.RuleId} — {finding.Severity.ToString().ToUpperInvariant()}", finding.Title);

        var lines = new List<string>
        {
            $"Confidence:    {finding.Confidence}",
            $"Process:       {finding.Entry.ProcessName} (PID {finding.Entry.ProcessId})",
            $"Endpoint:      {finding.Entry.Protocol} {finding.Entry.LocalEndpoint}",
            $"Executable:    {finding.Entry.ExecutablePath ?? "недоступен"}",
            $"Evidence:      {finding.Evidence}",
            $"Limitation:    {finding.Limitation}"
        };

        if (finding.Security is not null)
        {
            lines.Add($"Signature:     {finding.Security.SignatureStatus}");
            lines.Add($"Publisher:     {finding.Security.Publisher ?? "не определён"}");
            lines.Add($"SHA-256:       {finding.Security.Sha256 ?? "недоступен"}");
            if (!string.IsNullOrWhiteSpace(finding.Security.Limitation))
                lines.Add($"Enrichment:    {finding.Security.Limitation}");
        }

        _terminal.Box(lines, "EXPLAINABLE FINDING", RuleSeverityColor(finding.Severity));
        _terminal.WriteLine();
        _terminal.WriteLine("  Finding описывает наблюдаемый факт и не является malware verdict.", ConsoleColor.Yellow);
        Back();
    }

    private Task StorageStatusAsync()
    {
        _terminal.Clear();
        Header("STORAGE STATUS", "Локальное хранилище PortSentinel");
        _terminal.Box([
            "Engine:          SQLite",
            "Journal mode:    WAL",
            $"Database:        {_store.DatabasePath}",
            $"Reports:         {_store.ReportsDirectory}",
            "Exports:         JSON schema v1 / GitHub Markdown",
            "Rule enrichment: Authenticode + SHA-256",
            "Network payload: никогда не сохраняется"
        ], "DATA NODE", ConsoleColor.Cyan);
        Back();
        return Task.CompletedTask;
    }

    private void Header(string title, string subtitle)
    {
        AsciiLogo.Draw(_terminal, compact: true);
        _terminal.Rule(title);
        _terminal.WriteLine($"  {subtitle}", ConsoleColor.DarkGray);
        _terminal.WriteLine();
    }

    private void DrawTable(IReadOnlyList<NetworkEntry> entries, HashSet<string>? previous = null)
    {
        int processWidth = _terminal.Width >= 115 ? 24 : 18;
        int endpointWidth = _terminal.Width >= 115 ? 32 : 24;
        _terminal.WriteLine(
            $"  {Terminal.PadOrTrim("PROCESS", processWidth)} {"PID",6} {"PROTO",6} " +
            $"{Terminal.PadOrTrim("LOCAL", endpointWidth)} {Terminal.PadOrTrim("REMOTE / STATE", endpointWidth)}",
            ConsoleColor.DarkCyan);

        foreach (NetworkEntry entry in entries)
        {
            bool fresh = previous is not null && previous.Count > 0 && !previous.Contains(entry.Identity);
            ConsoleColor color = fresh ? ConsoleColor.Yellow :
                entry.IsListener ? ConsoleColor.Green : ConsoleColor.Gray;
            string remote = entry.IsListener ? entry.State : $"{entry.RemoteEndpoint} {entry.State}";
            _terminal.WriteLine(
                $"{(fresh ? "+" : " ")} {Terminal.PadOrTrim(entry.ProcessName, processWidth)} {entry.ProcessId,6} " +
                $"{Terminal.PadOrTrim(entry.Protocol, 6)} {Terminal.PadOrTrim(entry.LocalEndpoint, endpointWidth)} " +
                Terminal.PadOrTrim(remote, endpointWidth),
                color);
        }

        if (entries.Count == 0)
            _terminal.WriteLine("  Нет записей для отображения.", ConsoleColor.Yellow);
    }

    private int MaxRows() => Math.Max(5, _terminal.Height - 13);

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

    private static ConsoleColor RuleSeverityColor(RuleSeverity severity) => severity switch
    {
        RuleSeverity.Critical => ConsoleColor.Red,
        RuleSeverity.High => ConsoleColor.Red,
        RuleSeverity.Medium => ConsoleColor.Yellow,
        RuleSeverity.Low => ConsoleColor.Cyan,
        _ => ConsoleColor.Gray
    };
}
