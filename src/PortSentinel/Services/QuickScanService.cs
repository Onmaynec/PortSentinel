using PortSentinel.Models;

namespace PortSentinel.Services;

internal static class QuickScanService
{
    public static IReadOnlyList<ScanFinding> Analyze(IReadOnlyList<NetworkEntry> entries)
    {
        var findings = new List<ScanFinding>();

        foreach (NetworkEntry entry in entries)
        {
            if (entry.IsListener && IsWildcard(entry.LocalAddress))
            {
                findings.Add(new ScanFinding(
                    FindingSeverity.Medium,
                    "Listener доступен на всех интерфейсах",
                    $"{entry.ProcessName} слушает {entry.LocalEndpoint}. Проверьте необходимость внешней доступности.",
                    entry));
            }

            if (IsRiskyPath(entry.ExecutablePath))
            {
                findings.Add(new ScanFinding(
                    entry.IsListener ? FindingSeverity.High : FindingSeverity.Medium,
                    "Сетевая активность из временного каталога",
                    $"Процесс {entry.ProcessName} запущен из Temp или Downloads и использует сеть.",
                    entry));
            }

            if (entry.IsExternal && entry.ExecutablePath is null)
            {
                findings.Add(new ScanFinding(
                    FindingSeverity.Low,
                    "Ограниченные сведения о процессе",
                    $"Для PID {entry.ProcessId} не удалось прочитать путь, но процесс имеет внешнее соединение.",
                    entry));
            }
        }

        return findings
            .GroupBy(static finding =>
                $"{finding.Severity}|{finding.Title}|{finding.Entry?.ProcessId}|{finding.Entry?.LocalEndpoint}")
            .Select(static group => group.First())
            .OrderByDescending(static finding => finding.Severity)
            .ThenBy(static finding => finding.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsWildcard(string address) =>
        address is "0.0.0.0" or "::" or "[::]";

    private static bool IsRiskyPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string normalized = path.Replace('/', '\\');
        return normalized.Contains("\\Temp\\", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("\\Downloads\\", StringComparison.OrdinalIgnoreCase);
    }
}
