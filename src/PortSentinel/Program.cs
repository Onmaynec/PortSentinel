using System.Text;
using PortSentinel.App;
using PortSentinel.Services;
using PortSentinel.UI;

namespace PortSentinel;

internal static class Program
{
    public const string Version = "0.3.0";

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        Console.Title = $"PortSentinel {Version} — Network Control Center";

        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("PortSentinel поддерживает только Windows 10/11 x64.");
            return 16;
        }

        bool animationsEnabled = !args.Contains("--no-animation", StringComparer.OrdinalIgnoreCase)
            && !Console.IsOutputRedirected;

        if (args.Contains("--version", StringComparer.OrdinalIgnoreCase) ||
            args.Contains("-v", StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"PortSentinel {Version}");
            return 0;
        }

        var terminal = new Terminal(animationsEnabled);
        var updater = new GitHubUpdateService("Onmaynec", "PortSentinel", Version);

        if (args.Contains("--check-update", StringComparer.OrdinalIgnoreCase))
        {
            UpdateCheckResult result = await terminal.RunWithSpinnerAsync(
                "Проверка GitHub Releases",
                updater.CheckAsync(CancellationToken.None));
            Console.WriteLine(result.Message);
            return result.Status == UpdateStatus.Failed ? 1 : 0;
        }

        if (args.Contains("--help", StringComparer.OrdinalIgnoreCase) ||
            args.Contains("-h", StringComparer.OrdinalIgnoreCase))
        {
            PrintHelp();
            return 0;
        }

        try
        {
            var network = new NetworkSnapshotService();
            var legacyPanel = new PortSentinelApp(
                terminal,
                network,
                new ProcessMetadataService(),
                updater);
            var app = new PortSentinelV3App(
                terminal,
                network,
                new SessionStore(),
                legacyPanel);

            await app.RunAsync(CancellationToken.None);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 15;
        }
        catch (Exception ex)
        {
            Terminal.ResetConsole();
            Console.Error.WriteLine($"Критическая ошибка: {ex.Message}");
            return 1;
        }
        finally
        {
            Terminal.ResetConsole();
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("PortSentinel — интерактивный монитор сетевой активности Windows");
        Console.WriteLine();
        Console.WriteLine("v0.3.0: SQLite Session History, Baseline Center и экспорт отчётов.");
        Console.WriteLine("Запуск без аргументов открывает полноэкранную панель.");
        Console.WriteLine();
        Console.WriteLine("Параметры:");
        Console.WriteLine("  --version         Показать версию");
        Console.WriteLine("  --check-update    Проверить обновления через GitHub Releases");
        Console.WriteLine("  --no-animation    Отключить анимации");
        Console.WriteLine("  --help            Показать справку");
    }
}
