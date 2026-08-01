using System.Collections.Concurrent;
using System.Diagnostics;
using PortSentinel.Models;

namespace PortSentinel.Services;

internal sealed class ProcessMetadataService
{
    private readonly ConcurrentDictionary<int, CacheItem> _cache = new();
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(5);

    public ProcessMetadata Get(int processId)
    {
        if (processId <= 0)
        {
            return new ProcessMetadata(processId, "System", null, null, null, null, false, "system");
        }

        if (_cache.TryGetValue(processId, out CacheItem? cached) &&
            DateTimeOffset.UtcNow - cached.CreatedAt < CacheLifetime)
        {
            return cached.Value;
        }

        ProcessMetadata metadata = Resolve(processId);
        _cache[processId] = new CacheItem(metadata, DateTimeOffset.UtcNow);
        return metadata;
    }

    private static ProcessMetadata Resolve(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            string name = Safe(() => process.ProcessName) ?? $"PID {processId}";
            string? path = Safe(() => process.MainModule?.FileName);
            string? company = null;
            string? description = null;

            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                try
                {
                    FileVersionInfo version = FileVersionInfo.GetVersionInfo(path);
                    company = version.CompanyName;
                    description = version.FileDescription;
                }
                catch
                {
                    // Optional enrichment must not break monitoring.
                }
            }

            DateTimeOffset? startTime = null;
            try
            {
                startTime = new DateTimeOffset(process.StartTime);
            }
            catch
            {
                // Protected and short-lived processes can deny access.
            }

            bool isElevated = false;
            if (processId == Environment.ProcessId)
            {
                isElevated = IsCurrentProcessElevated();
            }

            return new ProcessMetadata(
                processId,
                name,
                path,
                company,
                description,
                startTime,
                isElevated,
                path is null ? "limited" : "ok");
        }
        catch
        {
            return new ProcessMetadata(
                processId,
                $"PID {processId}",
                null,
                null,
                null,
                null,
                false,
                "process unavailable");
        }
    }

    private static string? Safe(Func<string?> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return null;
        }
    }

    private static bool IsCurrentProcessElevated()
    {
        try
        {
            using System.Security.Principal.WindowsIdentity identity =
                System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private sealed record CacheItem(ProcessMetadata Value, DateTimeOffset CreatedAt);
}
