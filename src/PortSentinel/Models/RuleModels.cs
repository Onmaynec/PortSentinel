namespace PortSentinel.Models;

internal enum RuleSeverity
{
    Info,
    Low,
    Medium,
    High,
    Critical
}

internal enum RuleConfidence
{
    Low,
    Medium,
    High
}

internal enum SignatureStatus
{
    Signed,
    Unsigned,
    Unavailable
}

internal sealed record ProcessSecurityInfo(
    string Path,
    string? Sha256,
    SignatureStatus SignatureStatus,
    string? Publisher,
    string? Limitation);

internal sealed record RuleFinding(
    string RuleId,
    RuleSeverity Severity,
    RuleConfidence Confidence,
    string Title,
    string Evidence,
    string Limitation,
    NetworkEntry Entry,
    ProcessSecurityInfo? Security);

internal sealed record RuleAnalysis(
    IReadOnlyList<RuleFinding> Findings,
    int RulesEvaluated,
    int EnrichedProcesses,
    int LimitedProcesses,
    bool BaselineAvailable);
