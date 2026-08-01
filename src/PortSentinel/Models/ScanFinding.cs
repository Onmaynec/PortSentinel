namespace PortSentinel.Models;

internal enum FindingSeverity
{
    Info,
    Low,
    Medium,
    High
}

internal sealed record ScanFinding(
    FindingSeverity Severity,
    string Title,
    string Description,
    NetworkEntry? Entry = null);
