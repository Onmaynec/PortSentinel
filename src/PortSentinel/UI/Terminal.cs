using System.Runtime.InteropServices;

namespace PortSentinel.UI;

internal sealed class Terminal
{
    private static readonly string[] SpinnerFrames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];

    public Terminal(bool animationsEnabled)
    {
        AnimationsEnabled = animationsEnabled;
        TryEnableVirtualTerminal();
    }

    public bool AnimationsEnabled { get; }

    public int Width => Math.Max(80, SafeWindowWidth());

    public int Height => Math.Max(25, SafeWindowHeight());

    public void Clear()
    {
        Console.ResetColor();
        Console.Clear();
    }

    public void Write(string text, ConsoleColor color = ConsoleColor.Gray)
    {
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ResetColor();
    }

    public void WriteLine(string text = "", ConsoleColor color = ConsoleColor.Gray)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    public void Rule(string? title = null)
    {
        int width = Math.Min(Width - 2, 112);
        if (string.IsNullOrWhiteSpace(title))
        {
            WriteLine(new string('─', width), ConsoleColor.DarkCyan);
            return;
        }

        string prefix = $"── {title} ";
        int tail = Math.Max(0, width - prefix.Length);
        WriteLine(prefix + new string('─', tail), ConsoleColor.DarkCyan);
    }

    public void Box(IEnumerable<string> lines, string? title = null, ConsoleColor color = ConsoleColor.DarkCyan)
    {
        string[] materialized = lines.ToArray();
        int contentWidth = materialized.Length == 0 ? 20 : materialized.Max(VisibleLength);
        if (!string.IsNullOrWhiteSpace(title))
        {
            contentWidth = Math.Max(contentWidth, VisibleLength(title) + 2);
        }

        contentWidth = Math.Min(contentWidth, Width - 6);
        WriteLine("┌" + new string('─', contentWidth + 2) + "┐", color);
        if (!string.IsNullOrWhiteSpace(title))
        {
            Write("│ ", color);
            Write(PadOrTrim(title, contentWidth), ConsoleColor.Cyan);
            WriteLine(" │", color);
            WriteLine("├" + new string('─', contentWidth + 2) + "┤", color);
        }

        foreach (string line in materialized)
        {
            Write("│ ", color);
            Write(PadOrTrim(line, contentWidth));
            WriteLine(" │", color);
        }

        WriteLine("└" + new string('─', contentWidth + 2) + "┘", color);
    }

    public async Task<T> RunWithSpinnerAsync<T>(string label, Task<T> operation)
    {
        if (!AnimationsEnabled)
        {
            WriteLine($"[•] {label}...", ConsoleColor.Cyan);
            return await operation;
        }

        int left = Console.CursorLeft;
        int top = Console.CursorTop;
        int frame = 0;
        Console.CursorVisible = false;

        while (!operation.IsCompleted)
        {
            SetCursorSafe(left, top);
            Write($"{SpinnerFrames[frame++ % SpinnerFrames.Length]} ", ConsoleColor.Cyan);
            Write(PadOrTrim(label, Math.Max(10, Width - 8)), ConsoleColor.Gray);
            await Task.Delay(70);
        }

        SetCursorSafe(left, top);
        Write("✔ ", ConsoleColor.Green);
        WriteLine(PadOrTrim(label, Math.Max(10, Width - 8)), ConsoleColor.Gray);
        return await operation;
    }

    public async Task RunIntroAsync()
    {
        if (!AnimationsEnabled)
        {
            return;
        }

        Clear();
        Console.CursorVisible = false;
        string[] stages =
        [
            "Инициализация сетевых модулей",
            "Подключение Windows IP Helper API",
            "Загрузка интерфейса Control Center"
        ];

        foreach (string stage in stages)
        {
            for (int index = 0; index <= 24; index++)
            {
                SetCursorSafe(0, 0);
                AsciiLogo.Draw(this, compact: true);
                WriteLine();
                WriteLine($"  {stage}", ConsoleColor.Cyan);
                Write("  [", ConsoleColor.DarkGray);
                int filled = index;
                Write(new string('█', filled), ConsoleColor.Cyan);
                Write(new string('░', 24 - filled), ConsoleColor.DarkGray);
                WriteLine($"] {index * 100 / 24,3}%", ConsoleColor.DarkGray);
                await Task.Delay(12);
            }
        }
    }

    public static string PadOrTrim(string text, int width)
    {
        if (width <= 0)
        {
            return string.Empty;
        }

        string value = text ?? string.Empty;
        if (value.Length > width)
        {
            return width <= 1 ? value[..width] : value[..(width - 1)] + "…";
        }

        return value.PadRight(width);
    }

    public static void ResetConsole()
    {
        try
        {
            Console.ResetColor();
            Console.CursorVisible = true;
        }
        catch
        {
            // Console may already be unavailable during process shutdown.
        }
    }

    private static int VisibleLength(string text) => text?.Length ?? 0;

    private static int SafeWindowWidth()
    {
        try
        {
            return Console.WindowWidth;
        }
        catch
        {
            return 100;
        }
    }

    private static int SafeWindowHeight()
    {
        try
        {
            return Console.WindowHeight;
        }
        catch
        {
            return 30;
        }
    }

    private static void SetCursorSafe(int left, int top)
    {
        try
        {
            Console.SetCursorPosition(Math.Max(0, left), Math.Max(0, top));
        }
        catch
        {
            // Redirected or resized terminals can reject cursor positioning.
        }
    }

    private static void TryEnableVirtualTerminal()
    {
        try
        {
            IntPtr handle = GetStdHandle(-11);
            if (handle == IntPtr.Zero || handle == new IntPtr(-1))
            {
                return;
            }

            if (GetConsoleMode(handle, out uint mode))
            {
                SetConsoleMode(handle, mode | 0x0004);
            }
        }
        catch
        {
            // Colors still work through ConsoleColor without VT mode.
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int standardHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr consoleHandle, out uint mode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr consoleHandle, uint mode);
}
