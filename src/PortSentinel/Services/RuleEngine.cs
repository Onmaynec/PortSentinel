using PortSentinel.Models;

namespace PortSentinel.Services;

internal sealed class RuleEngine
{
    private const string NewListenerRule = "PS-RULE-001";
    private const string WildcardListenerRule = "PS-RULE-002";
    private const string UnsignedNetworkProcessRule = "PS-RULE-003";
    private const string TempDirectoryNetworkProcessRule = "PS-RULE-004";

    private readonly ProcessSecurityService _security;

    public RuleEngine(ProcessSecurityService security)
    {
        _security = security;
    }

    public async Task<RuleAnalysis> AnalyzeAsync(
        IReadOnlyList<NetworkEntry> entries,
        BaselineComparison? baseline,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, ProcessSecurityInfo> security =
            await _security.EnrichAsync(entries, cancellationToken);
        var findings = new List<RuleFinding>();

        HashSet<string> newListeners = baseline?.NewEntries
            .Where(static entry => entry.IsListener)
            .Select(static entry => entry.BaselineIdentity)
            .ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);

        foreach (NetworkEntry entry in entries)
        {
            ProcessSecurityInfo? processSecurity = GetSecurity(entry, security);

            if (entry.IsListener && newListeners.Contains(entry.BaselineIdentity))
            {
                findings.Add(new RuleFinding(
                    NewListenerRule,
                    RuleSeverity.High,
                    RuleConfidence.High,
                    "Новый listener относительно baseline",
                    $"{entry.ProcessName} открыл {entry.Protocol} listener {entry.LocalEndpoint}, которого нет в baseline «{baseline!.Name}».",
                    "Новая служба, обновление приложения или изменение конфигурации могут быть легитимной причиной.",
                    entry,
                    processSecurity));
            }

            if (entry.IsListener && IsWildcard(entry.LocalAddress))
            {
                findings.Add(new RuleFinding(
                    WildcardListenerRule,
                    RuleSeverity.Medium,
                    RuleConfidence.High,
                    "Listener доступен на всех интерфейсах",
                    $"{entry.ProcessName} слушает {entry.LocalEndpoint} и принимает трафик на wildcard-адресе.",
                    "Wildcard binding может быть штатным; фактическая доступность также зависит от Firewall и маршрутизации.",
                    entry,
                    processSecurity));
            }

            if (IsRiskyPath(entry.ExecutablePath))
            {
                findings.Add(new RuleFinding(
                    TempDirectoryNetworkProcessRule,
                    entry.IsListener ? RuleSeverity.High : RuleSeverity.Medium,
                    RuleConfidence.High,
                    "Сетевая активность из временного каталога",
                    $"{entry.ProcessName} использует сеть из «{entry.ExecutablePath}».",
                    "Расположение в Temp или Downloads повышает приоритет проверки, но само по себе не доказывает вредоносность.",
                    entry,
                    processSecurity));
            }

            if (processSecurity?.SignatureStatus == SignatureStatus.Unsigned)
            {
                findings.Add(new RuleFinding(
                    UnsignedNetworkProcessRule,
                    entry.IsListener ? RuleSeverity.High : RuleSeverity.Medium,
                    RuleConfidence.Medium,
                    "Сетевой executable не имеет Authenticode-подписи",
                    BuildUnsignedEvidence(entry, processSecurity),
                    "Отсутствие подписи не означает вредоносность. PortSentinel фиксирует наличие подписи, но не строит полный verdict цепочки доверия.",
                    entry,
                    processSecurity));
            }
        }

        RuleFinding[] ordered = findings
            .GroupBy(FindingKey, StringComparer.Ordinal)
            .Select(static group => group.First())
            .OrderByDescending(static finding => finding.Severity)
            .ThenByDescending(static finding => finding.Confidence)
            .ThenBy(static finding => finding.RuleId, StringComparer.Ordinal)
            .ThenBy(static finding => finding.Entry.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        int limited = security.Values.Count(info =>
            info.SignatureStatus == SignatureStatus.Unavailable ||
            string.IsNullOrWhiteSpace(info.Sha256) ||
            !string.IsNullOrWhiteSpace(info.Limitation));

        return new RuleAnalysis(
            ordered,
            RulesEvaluated: 4,
            EnrichedProcesses: security.Count,
            LimitedProcesses: limited,
            BaselineAvailable: baseline is not null);
    }

    private static ProcessSecurityInfo? GetSecurity(
        NetworkEntry entry,
        IReadOnlyDictionary<string, ProcessSecurityInfo> security)
    {
        if (string.IsNullOrWhiteSpace(entry.ExecutablePath))
        {
            return null;
        }

        return security.TryGetValue(entry.ExecutablePath, out ProcessSecurityInfo? value)
            ? value
            : null;
    }

    private static string BuildUnsignedEvidence(
        NetworkEntry entry,
        ProcessSecurityInfo security)
    {
        string hash = string.IsNullOrWhiteSpace(security.Sha256)
            ? "SHA-256 недоступен"
            : $"SHA-256 {security.Sha256}";
        return $"{entry.ProcessName} ({security.Path}) использует {entry.Protocol}; Authenticode отсутствует; {hash}.";
    }

    private static string FindingKey(RuleFinding finding) =>
        finding.RuleId == UnsignedNetworkProcessRule ||
        finding.RuleId == TempDirectoryNetworkProcessRule
            ? $"{finding.RuleId}|{finding.Entry.ProcessId}|{finding.Entry.ExecutablePath}"
            : $"{finding.RuleId}|{finding.Entry.BaselineIdentity}";

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
