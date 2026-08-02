namespace PortSentinel.Models;

internal enum ConnectionHealthSeverity
{
    Info,
    Low,
    Medium,
    High
}

internal sealed record ConnectionHealthFinding(
    string RuleId,
    ConnectionHealthSeverity Severity,
    int Confidence,
    string Title,
    string Evidence,
    string Limitation,
    string ProcessName,
    string RemoteEndpoint,
    int Count);

internal sealed record ConnectionHealthReport(
    long? CaptureId,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    EtwBackendMode Mode,
    string SourceStatus,
    int EventCount,
    int FailCount,
    int ReconnectCount,
    int RetransmitCount,
    int Score,
    string Grade,
    IReadOnlyList<ConnectionHealthFinding> Findings);
