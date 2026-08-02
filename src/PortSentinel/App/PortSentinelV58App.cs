using PortSentinel.Models;
using PortSentinel.Services;
using PortSentinel.UI;

namespace PortSentinel.App;

internal sealed class PortSentinelV58App
{
    private readonly Terminal _terminal;
    private readonly GuardedEtwCaptureService _guardedCapture;
    private readonly EtwSessionGuardService _sessionGuard;
    private readonly TelemetryArchiveService _archive;
    private readonly PortSentinelV57App _installerWatch;
    private int _selected;

    private static readonly MenuItem[] Menu =
    [
        new("1", "GUARDED CAPTURE", "15-sec ETW capture with session preflight", ConsoleColor.Cyan),
        new("2", "SESSION INVENTORY", "Active ETW names and ownership boundary", ConsoleColor.Green),
        new("3", "OWNED CLEANUP", "Dry-run cleanup of PortSentinel-* sessions only", ConsoleColor.Magenta),
        new("4", "INSTALLER WATCH", "Baseline/watch workflow v0.5.7", ConsoleColor.Yellow),
        new("0", "EXIT", "Безопасно закрыть PortSentinel", ConsoleColor.Red)
    ];

    public PortSentinelV58App(
        Terminal terminal,
        GuardedEtwCaptureService guardedCapture,
        EtwSessionGuardService sessionGuard,
        TelemetryArchiveService archive,
        PortSentinelV57App installerWatch)
    {
        _terminal = terminal;
        _guardedCapture = guardedCapture;
        _sessionGuard = sessionGuard;
        _archive = archive;
        _installerWatch = installerWatch;
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
        _terminal.WriteLine($"  v{Program.Version}  •  ETW SESSION GUARD", ConsoleColor.DarkGray);
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
        "1" => GuardedCaptureAsync(token),
        "2" => InventoryAsync(token),
        "3" => CleanupAsync(token),
        "4" => _installerWatch.RunAsync(token),
        _ => Task.CompletedTask
    };

    private async Task GuardedCaptureAsync(CancellationToken token)
    {
        _terminal.Clear();
        Header("GUARDED ETW CAPTURE", "Preflight inventory • 15 seconds • safe snapshot fallback");
        _terminal.Box(
        [
            "Session Guard сначала получает список активных ETW session names.",
            "Чужие sessions не останавливаются, не перезапускаются и не изменяются.",
            "При вероятном name collision выполняется один bounded retry.",
            "При отказе используется snapshot fallback через Windows IP Helper API.",
            "Результат capture автоматически сохраняется в SQLite archive."
        ], "GUARD POLICY", ConsoleColor.Cyan);
        _terminal.Rule();
        _terminal.WriteLine("  Enter — начать capture   Esc/Q — отмена", ConsoleColor.DarkGray);
        ConsoleKey start = Console.ReadKey(true).Key;
        if (start is ConsoleKey.Escape or ConsoleKey.Q)
            return;

        EtwGuardedCaptureResult guarded = await _terminal.RunWithSpinnerAsync(
            "ETW Session Guard: capture 15 секунд",
            _guardedCapture.CaptureAsync(TimeSpan.FromSeconds(15), token));
        long captureId = await _archive.SaveCaptureAsync(guarded.Capture, token);
        await ShowDiagnosticsAsync(guarded, captureId, token);
    }

    private async Task ShowDiagnosticsAsync(
        EtwGuardedCaptureResult guarded,
        long captureId,
        CancellationToken token)
    {
        while (true)
        {
            EtwSessionDiagnostics diagnostics = guarded.Diagnostics;
            EtwSessionInventory inventory = diagnostics.InventoryBefore;
            _terminal.Clear();
            Header(
                $"SESSION GUARD — CAPTURE #{captureId}",
                diagnostics.UsedSnapshotFallback ? "SNAPSHOT FALLBACK" : "KERNEL ETW");

            ConsoleColor backendColor = diagnostics.UsedSnapshotFallback
                ? ConsoleColor.Yellow
                : ConsoleColor.Green;
            _terminal.Box(
            [
                $"Backend:          {guarded.Capture.Mode}",
                $"Events:           {guarded.Capture.Events.Count}",
                $"Elevated:         {guarded.Capture.Elevated}",
                $"Active before:    {inventory.ActiveCount}",
                $"Owned / foreign:  {inventory.PortSentinelCount} / {inventory.ForeignCount}",
                $"Attempts:         {diagnostics.Attempts.Count}",
                $"Archive ID:       {captureId}",
                $"Summary:          {diagnostics.Summary}"
            ], "CAPTURE DIAGNOSTICS", backendColor);

            _terminal.WriteLine();
            _terminal.WriteLine("  Attempts", ConsoleColor.Cyan);
            foreach (EtwSessionAttempt attempt in diagnostics.Attempts)
            {
                ConsoleColor color = attempt.Succeeded ? ConsoleColor.Green : ConsoleColor.Yellow;
                _terminal.Write($"  #{attempt.Number} ", color);
                _terminal.Write(Terminal.PadOrTrim(attempt.FailureKind.ToString(), 20), color);
                _terminal.WriteLine(
                    $" success={attempt.Succeeded} native={attempt.NativeErrorCode?.ToString() ?? "—"}",
                    ConsoleColor.Gray);
                _terminal.WriteLine($"     {Terminal.PadOrTrim(attempt.Message, Math.Max(24, _terminal.Width - 8))}", ConsoleColor.DarkGray);
            }

            _terminal.Rule();
            _terminal.WriteLine("  J — JSON diagnostics   M — Markdown diagnostics   Q/Esc — назад", ConsoleColor.DarkGray);
            ConsoleKey key = Console.ReadKey(true).Key;
            if (key is ConsoleKey.Q or ConsoleKey.Escape)
                return;
            if (key is ConsoleKey.J or ConsoleKey.M)
            {
                string path = await _sessionGuard.ExportDiagnosticsAsync(
                    guarded,
                    key == ConsoleKey.J ? "json" : "markdown",
                    token);
                Message("SESSION GUARD EXPORT", $"Диагностика сохранена:\n{path}", ConsoleColor.Green);
            }
        }
    }

    private async Task InventoryAsync(CancellationToken token)
    {
        while (true)
        {
            EtwSessionInventory inventory = _sessionGuard.Inspect();
            _terminal.Clear();
            Header(
                "ETW SESSION INVENTORY",
                $"Active: {inventory.ActiveCount} • Owned: {inventory.PortSentinelCount} • Foreign: {inventory.ForeignCount}");

            if (!inventory.Success)
            {
                _terminal.WriteLine($"  Inventory failed: {inventory.Error}", ConsoleColor.Red);
            }
            else if (inventory.ActiveSessions.Count == 0)
            {
                _terminal.WriteLine("  Активные ETW-сессии не обнаружены.", ConsoleColor.Green);
            }
            else
            {
                int rows = Math.Max(5, Console.WindowHeight - 13);
                foreach (string name in inventory.ActiveSessions.Take(rows))
                {
                    bool owned = EtwSessionGuardService.IsOwnedSession(name);
                    _terminal.Write(owned ? "  OWNED    " : "  FOREIGN  ",
                        owned ? ConsoleColor.Magenta : ConsoleColor.DarkGray);
                    _terminal.WriteLine(Terminal.PadOrTrim(name, Math.Max(20, _terminal.Width - 14)),
                        owned ? ConsoleColor.White : ConsoleColor.Gray);
                }
                if (inventory.ActiveSessions.Count > rows)
                    _terminal.WriteLine($"  … ещё {inventory.ActiveSessions.Count - rows} sessions", ConsoleColor.DarkGray);
            }

            _terminal.Rule();
            _terminal.WriteLine(
                "  R — refresh   J — JSON export   M — Markdown export   Q/Esc — назад",
                ConsoleColor.DarkGray);
            ConsoleKey key = Console.ReadKey(true).Key;
            if (key is ConsoleKey.Q or ConsoleKey.Escape)
                return;
            if (key == ConsoleKey.R)
                continue;
            if (key is ConsoleKey.J or ConsoleKey.M)
            {
                string path = await _sessionGuard.ExportInventoryAsync(
                    inventory,
                    key == ConsoleKey.J ? "json" : "markdown",
                    token);
                Message("SESSION INVENTORY EXPORT", $"Inventory сохранён:\n{path}", ConsoleColor.Green);
            }
        }
    }

    private async Task CleanupAsync(CancellationToken token)
    {
        EtwSessionInventory inventory = _sessionGuard.Inspect();
        if (!inventory.Success)
        {
            Message("OWNED SESSION CLEANUP", $"Inventory недоступен:\n{inventory.Error}", ConsoleColor.Red);
            return;
        }
        if (inventory.PortSentinelSessions.Count == 0)
        {
            Message("OWNED SESSION CLEANUP", "Сессии PortSentinel-* не обнаружены.", ConsoleColor.Green);
            return;
        }

        _terminal.Clear();
        Header("OWNED SESSION CLEANUP — DRY RUN", $"Candidates: {inventory.PortSentinelCount}");
        _terminal.Box(
        [
            $"Будут обработаны только имена с префиксом {EtwSessionGuardService.OwnedSessionPrefix}",
            "Foreign sessions исключены до выполнения cleanup.",
            "Cleanup предназначен для orphan sessions после аварийного завершения.",
            "Активный capture в другом экземпляре PortSentinel может быть остановлен.",
            "Продолжайте только если другие экземпляры PortSentinel закрыты."
        ], "SAFETY BOUNDARY", ConsoleColor.Yellow);
        _terminal.WriteLine();
        foreach (string name in inventory.PortSentinelSessions.Take(Math.Max(3, Console.WindowHeight - 18)))
            _terminal.WriteLine($"  - {name}", ConsoleColor.Magenta);
        _terminal.Rule();
        _terminal.WriteLine("  Y — подтвердить cleanup   любая другая клавиша — отмена", ConsoleColor.DarkGray);
        if (Console.ReadKey(true).Key != ConsoleKey.Y)
            return;

        EtwOwnedSessionCleanupResult cleanup = await _terminal.RunWithSpinnerAsync(
            "Остановка PortSentinel-owned ETW sessions",
            _sessionGuard.CleanupOwnedAsync(inventory.PortSentinelSessions, token));

        _terminal.Clear();
        Header(
            "OWNED SESSION CLEANUP COMPLETE",
            $"Stopped: {cleanup.StoppedCount} • Failed: {cleanup.FailedCount}");
        foreach (EtwOwnedSessionCleanupItem item in cleanup.Items)
        {
            _terminal.Write(item.Stopped ? "  OK   " : "  FAIL ",
                item.Stopped ? ConsoleColor.Green : ConsoleColor.Red);
            _terminal.WriteLine($"{item.SessionName} — {item.Message}", ConsoleColor.Gray);
        }
        Back();
    }

    private void Header(string title, string subtitle)
    {
        _terminal.WriteLine($"  {title}", ConsoleColor.White);
        _terminal.WriteLine($"  {subtitle}", ConsoleColor.DarkGray);
        _terminal.Rule();
    }

    private void Message(string title, string message, ConsoleColor color)
    {
        _terminal.Clear();
        Header(title, string.Empty);
        _terminal.Box(message.Split('\n'), title, color);
        Back();
    }

    private void Back()
    {
        _terminal.Rule();
        _terminal.WriteLine("  Нажмите любую клавишу, чтобы вернуться", ConsoleColor.DarkGray);
        Console.ReadKey(true);
    }
}
