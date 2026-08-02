using System.Text;
using System.Text.Json;
using PortSentinel.Models;

namespace PortSentinel.Services;

internal sealed class SessionComparisonService
{
    private readonly SessionStore _store;

    public SessionComparisonService(SessionStore store)
    {
        _store = store;
    }

    public SessionComparison Compare(
        SessionSummary before,
        IReadOnlyList<NetworkEntry> beforeEntries,
        SessionSummary after,
        IReadOnlyList<NetworkEntry> afterEntries)
    {
        Dictionary<string, NetworkEntry> beforeMap = beforeEntries
            .GroupBy(entry => entry.BaselineIdentity, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        Dictionary<string, NetworkEntry> afterMap = afterEntries
            .GroupBy(entry => entry.BaselineIdentity, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        NetworkEntry[] added = afterMap
            .Where(pair => !beforeMap.ContainsKey(pair.Key))
            .Select(pair => pair.Value)
            .OrderBy(entry => entry.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Protocol, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        NetworkEntry[] removed = beforeMap
            .Where(pair => !afterMap.ContainsKey(pair.Key))
            .Select(pair => pair.Value)
            .OrderBy(entry => entry.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Protocol, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        HashSet<string> beforeProcesses = beforeEntries
            .Select(entry => entry.ProcessName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> afterProcesses = afterEntries
            .Select(entry => entry.ProcessName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string[] addedProcesses = afterProcesses
            .Where(process => !beforeProcesses.Contains(process))
            .OrderBy(process => process, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] removedProcesses = beforeProcesses
            .Where(process => !afterProcesses.Contains(process))
            .OrderBy(process => process, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new SessionComparison(
            before,
            after,
            added,
            removed,
            addedProcesses,
            removedProcesses,
            afterEntries.Count(entry => entry.IsListener) - beforeEntries.Count(entry => entry.IsListener),
            afterEntries.Count(entry => entry.IsExternal) - beforeEntries.Count(entry => entry.IsExternal));
    }

    public async Task<string> ExportAsync(
        SessionComparison comparison,
        string format,
        CancellationToken cancellationToken)
    {
        string extension = format.Equals("json", StringComparison.OrdinalIgnoreCase) ? "json" : "md";
        string path = Path.Combine(
            _store.ReportsDirectory,
            $"session-diff-{comparison.Before.Id}-{comparison.After.Id}.{extension}");

        if (extension == "json")
        {
            var payload = new
            {
                schemaVersion = 1,
                generatedAt = DateTimeOffset.Now,
                comparison
            };
            await File.WriteAllTextAsync(
                path,
                JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);
            return path;
        }

        var text = new StringBuilder()
            .AppendLine($"# PortSentinel Session Diff #{comparison.Before.Id} → #{comparison.After.Id}")
            .AppendLine()
            .AppendLine($"- **Before:** {comparison.Before.Name} ({comparison.Before.StartedAt:G})")
            .AppendLine($"- **After:** {comparison.After.Name} ({comparison.After.StartedAt:G})")
            .AppendLine($"- **Added entries:** {comparison.AddedEntries.Count}")
            .AppendLine($"- **Removed entries:** {comparison.RemovedEntries.Count}")
            .AppendLine($"- **Listener delta:** {comparison.ListenerDelta:+#;-#;0}")
            .AppendLine($"- **External delta:** {comparison.ExternalDelta:+#;-#;0}")
            .AppendLine()
            .AppendLine("## Added network entries")
            .AppendLine()
            .AppendLine("| Process | Protocol | Local | Remote | State |")
            .AppendLine("|---|---|---|---|---|");

        foreach (NetworkEntry entry in comparison.AddedEntries)
        {
            text.AppendLine($"| {Escape(entry.ProcessName)} | {entry.Protocol} | {Escape(entry.LocalEndpoint)} | {Escape(entry.RemoteEndpoint)} | {Escape(entry.State)} |");
        }

        text.AppendLine()
            .AppendLine("## Removed network entries")
            .AppendLine()
            .AppendLine("| Process | Protocol | Local | Remote | State |")
            .AppendLine("|---|---|---|---|---|");

        foreach (NetworkEntry entry in comparison.RemovedEntries)
        {
            text.AppendLine($"| {Escape(entry.ProcessName)} | {entry.Protocol} | {Escape(entry.LocalEndpoint)} | {Escape(entry.RemoteEndpoint)} | {Escape(entry.State)} |");
        }

        await File.WriteAllTextAsync(path, text.ToString(), cancellationToken);
        return path;
    }

    private static string Escape(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
}
