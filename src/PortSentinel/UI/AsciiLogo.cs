namespace PortSentinel.UI;

internal static class AsciiLogo
{
    private static readonly string[] Full =
    [
        "██████╗  ██████╗ ██████╗ ████████╗",
        "██╔══██╗██╔═══██╗██╔══██╗╚══██╔══╝",
        "██████╔╝██║   ██║██████╔╝   ██║   ",
        "██╔═══╝ ██║   ██║██╔══██╗   ██║   ",
        "██║     ╚██████╔╝██║  ██║   ██║   ",
        "╚═╝      ╚═════╝ ╚═╝  ╚═╝   ╚═╝   ",
        "      S E N T I N E L   //   N E T W O R K   C O N T R O L"
    ];

    private static readonly string[] Compact =
    [
        "╔═╗╔═╗╦═╗╔╦╗  ╔═╗╔═╗╔╗╔╔╦╗╦╔╗╔╔═╗╦",
        "╠═╝║ ║╠╦╝ ║   ╚═╗║╣ ║║║ ║ ║║║║║╣ ║",
        "╩  ╚═╝╩╚═ ╩   ╚═╝╚═╝╝╚╝ ╩ ╩╝╚╝╚═╝╩"
    ];

    public static void Draw(Terminal terminal, bool compact = false)
    {
        string[] logo = compact || terminal.Width < 100 ? Compact : Full;
        foreach (string line in logo)
        {
            terminal.WriteLine(line, ConsoleColor.Cyan);
        }
    }
}
