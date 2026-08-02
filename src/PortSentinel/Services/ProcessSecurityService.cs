using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using PortSentinel.Models;

namespace PortSentinel.Services;

internal sealed class ProcessSecurityService
{
    public async Task<IReadOnlyDictionary<string, ProcessSecurityInfo>> EnrichAsync(
        IReadOnlyCollection<NetworkEntry> entries,
        CancellationToken cancellationToken)
    {
        string[] paths = entries
            .Select(entry => entry.ExecutablePath)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        ProcessSecurityInfo[] results = await Task.WhenAll(
            paths.Select(path => AnalyzeAsync(path, cancellationToken)));

        return results.ToDictionary(
            result => result.Path,
            result => result,
            StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<ProcessSecurityInfo> AnalyzeAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return new ProcessSecurityInfo(
                path,
                null,
                SignatureStatus.Unavailable,
                null,
                "Executable path известен, но файл недоступен для чтения.");
        }

        string? sha256 = null;
        string? hashLimitation = null;
        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
            sha256 = Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            hashLimitation = $"SHA-256 недоступен: {ex.Message}";
        }

        SignatureStatus signatureStatus;
        string? publisher = null;
        string? signatureLimitation = null;
        try
        {
#pragma warning disable SYSLIB0057
            using X509Certificate certificate = X509Certificate.CreateFromSignedFile(path);
            using var certificate2 = new X509Certificate2(certificate);
#pragma warning restore SYSLIB0057
            signatureStatus = SignatureStatus.Signed;
            publisher = certificate2.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
            if (string.IsNullOrWhiteSpace(publisher))
            {
                publisher = certificate2.Subject;
            }
        }
        catch (CryptographicException)
        {
            signatureStatus = SignatureStatus.Unsigned;
        }
        catch (Exception ex)
        {
            signatureStatus = SignatureStatus.Unavailable;
            signatureLimitation = $"Authenticode недоступен: {ex.Message}";
        }

        string? limitation = JoinLimitations(hashLimitation, signatureLimitation);
        return new ProcessSecurityInfo(path, sha256, signatureStatus, publisher, limitation);
    }

    private static string? JoinLimitations(params string?[] limitations)
    {
        string[] values = limitations
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();
        return values.Length == 0 ? null : string.Join(" ", values);
    }
}
