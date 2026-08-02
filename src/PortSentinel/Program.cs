using System.Text;
using PortSentinel.App;
using PortSentinel.Services;
using PortSentinel.UI;

namespace PortSentinel;

internal static class Program
{
    public const string Version = "0.5.5";

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        Console.Title = $"PortSentinel {Version} — Network Coverage";

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
            var store = new SessionStore();
            var dns = new DnsCorrelationService();
            var legacyPanel = new PortSentinelApp(
                terminal,
                network,
                new ProcessMetadataService(),
                updater);
            var v4Panel = new PortSentinelV4App(
                terminal,
                network,
                store,
                new BaselineFingerprintService(store),
                new RuleEngine(new ProcessSecurityService()),
                legacyPanel);
            var v5Panel = new PortSentinelV5App(
                terminal,
                network,
                store,
                dns,
                new ProcessTreeService(),
                new SessionComparisonService(store),
                new ApplicationWatchService(store, dns),
                v4Panel);
            var etw = new EtwTelemetryService(network, store.ReportsDirectory);
            var v51Panel = new PortSentinelV51App(
                terminal,
                etw,
                v5Panel);
            var archive = new TelemetryArchiveService(
                store.DatabasePath,
                store.ReportsDirectory);
            var v52Panel = new PortSentinelV52App(
                terminal,
                etw,
                archive,
                v51Panel);
            var v53Panel = new PortSentinelV53App(
                terminal,
                etw,
                archive,
                new TelemetryArchiveOperationsService(store.DatabasePath, archive),
                v52Panel);
            var v54Panel = new PortSentinelV54App(
                terminal,
                etw,
                archive,
                new ConnectionHealthService(store.ReportsDirectory),
                v53Panel);
            var app = new PortSentinelV55App(
                terminal,
                etw,
                archive,
                new NetworkCoverageService(store.ReportsDirectory),
                v54Panel);

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
        Console.WriteLine("v0.5.5: TCP4/TCP6 и UDP4/UDP6 ETW coverage с protocol reports.");
        Console.WriteLine("Запуск без аргументов открывает полноэкранную панель.");
        Console.WriteLine();
        Console.WriteLine("Параметры:");
        Console.WriteLine("  --version         Показать версию");
        Console.WriteLine("  --check-update    Проверить обновления через GitHub Releases");
        Console.WriteLine("  --no-animation    Отключить анимации");
        Console.WriteLine("  --help            Показать справку");
    }
}
