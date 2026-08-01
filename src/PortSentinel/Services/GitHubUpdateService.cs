using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace PortSentinel.Services;

internal enum UpdateStatus
{
    UpToDate,
    Available,
    Failed
}

internal sealed record ReleaseAsset(string Name, string DownloadUrl);

internal sealed record UpdateCheckResult(
    UpdateStatus Status,
    string Message,
    string? Version = null,
    string? ReleaseUrl = null,
    ReleaseAsset? Package = null,
    ReleaseAsset? Checksum = null);

internal sealed class GitHubUpdateService
{
    private readonly string _owner;
    private readonly string _repository;
    private readonly Version _currentVersion;
    private readonly HttpClient _httpClient;

    public GitHubUpdateService(string owner, string repository, string currentVersion)
    {
        _owner = owner;
        _repository = repository;
        _currentVersion = ParseVersion(currentVersion);
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(12)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("PortSentinel", currentVersion));
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            string endpoint = $"https://api.github.com/repos/{_owner}/{_repository}/releases/latest";
            using HttpResponseMessage response = await _httpClient.GetAsync(endpoint, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult(
                    UpdateStatus.Failed,
                    $"GitHub Releases вернул HTTP {(int)response.StatusCode}.");
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            JsonElement root = document.RootElement;

            string tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
            string versionText = tag.TrimStart('v', 'V');
            Version latest = ParseVersion(versionText);
            string? releaseUrl = root.TryGetProperty("html_url", out JsonElement urlElement)
                ? urlElement.GetString()
                : null;

            var assets = new List<ReleaseAsset>();
            if (root.TryGetProperty("assets", out JsonElement assetsElement))
            {
                foreach (JsonElement asset in assetsElement.EnumerateArray())
                {
                    string? name = asset.GetProperty("name").GetString();
                    string? downloadUrl = asset.GetProperty("browser_download_url").GetString();
                    if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(downloadUrl))
                    {
                        assets.Add(new ReleaseAsset(name, downloadUrl));
                    }
                }
            }

            if (latest <= _currentVersion)
            {
                return new UpdateCheckResult(
                    UpdateStatus.UpToDate,
                    $"Установлена актуальная версия {_currentVersion}.",
                    versionText,
                    releaseUrl);
            }

            string expectedZip = $"PortSentinel-{versionText}-win-x64.zip";
            ReleaseAsset? package = assets.FirstOrDefault(
                asset => asset.Name.Equals(expectedZip, StringComparison.OrdinalIgnoreCase));
            ReleaseAsset? checksum = assets.FirstOrDefault(
                asset => asset.Name.Equals(expectedZip + ".sha256", StringComparison.OrdinalIgnoreCase));

            return new UpdateCheckResult(
                UpdateStatus.Available,
                $"Доступна новая версия {versionText}.",
                versionText,
                releaseUrl,
                package,
                checksum);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or FormatException)
        {
            return new UpdateCheckResult(
                UpdateStatus.Failed,
                $"Не удалось проверить обновления: {ex.Message}");
        }
    }

    public async Task<string> DownloadAndPrepareAsync(
        UpdateCheckResult update,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        if (update.Status != UpdateStatus.Available || update.Package is null)
        {
            throw new InvalidOperationException("Для релиза отсутствует ZIP-пакет обновления.");
        }

        string root = Path.Combine(Path.GetTempPath(), "PortSentinel", "updates", update.Version ?? "latest");
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        Directory.CreateDirectory(root);
        string zipPath = Path.Combine(root, update.Package.Name);
        await DownloadFileAsync(update.Package.DownloadUrl, zipPath, progress, cancellationToken);

        if (update.Checksum is not null)
        {
            string checksumText = await _httpClient.GetStringAsync(update.Checksum.DownloadUrl, cancellationToken);
            string expected = checksumText
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? string.Empty;

            string actual = await ComputeSha256Async(zipPath, cancellationToken);
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("SHA-256 скачанного архива не совпадает с Release.");
            }
        }

        string extracted = Path.Combine(root, "package");
        Directory.CreateDirectory(extracted);
        ExtractSafely(zipPath, extracted);

        string appDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        string scriptPath = Path.Combine(root, "apply-update.cmd");
        string executable = Path.Combine(appDirectory, "portsentinel.exe");

        string script = $"""
            @echo off
            chcp 65001 >nul
            title PortSentinel Update
            echo [PortSentinel] Ожидание завершения программы...
            timeout /t 2 /nobreak >nul
            echo [PortSentinel] Установка версии {update.Version}...
            robocopy "{extracted}" "{appDirectory}" /E /R:5 /W:1 >nul
            if errorlevel 8 (
              echo [ERROR] Не удалось обновить файлы.
              pause
              exit /b 1
            )
            echo [OK] Обновление установлено.
            start "" "{executable}"
            del "%~f0"
            """;

        await File.WriteAllTextAsync(scriptPath, script, cancellationToken);
        return scriptPath;
    }

    public static void LaunchInstaller(string scriptPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = scriptPath,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(scriptPath) ?? AppContext.BaseDirectory
        });
    }

    public static void OpenReleasePage(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private async Task DownloadFileAsync(
        string url,
        string destination,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        long total = response.Content.Headers.ContentLength ?? -1;
        await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using FileStream target = new(destination, FileMode.Create, FileAccess.Write, FileShare.None);

        byte[] buffer = new byte[81920];
        long readTotal = 0;
        while (true)
        {
            int read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            readTotal += read;
            if (total > 0)
            {
                progress?.Report((int)Math.Clamp(readTotal * 100 / total, 0, 100));
            }
        }

        progress?.Report(100);
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static void ExtractSafely(string zipPath, string destination)
    {
        string destinationRoot = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        using ZipArchive archive = ZipFile.OpenRead(zipPath);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string fullPath = Path.GetFullPath(Path.Combine(destination, entry.FullName));
            if (!fullPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Архив обновления содержит небезопасный путь.");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(fullPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            entry.ExtractToFile(fullPath, overwrite: true);
        }
    }

    private static Version ParseVersion(string value)
    {
        string normalized = value.Split('-', '+')[0];
        return Version.Parse(normalized);
    }
}
