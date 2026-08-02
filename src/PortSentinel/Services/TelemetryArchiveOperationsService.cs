using Microsoft.Data.Sqlite;
using PortSentinel.Models;

namespace PortSentinel.Services;

internal sealed class TelemetryArchiveOperationsService
{
    private readonly string _databasePath;
    private readonly TelemetryArchiveService _archive;

    public TelemetryArchiveOperationsService(
        string databasePath,
        TelemetryArchiveService archive)
    {
        _databasePath = databasePath;
        _archive = archive;
    }

    public async Task<IReadOnlyList<TelemetrySearchResult>> SearchAsync(
        TelemetrySearchQuery query,
        CancellationToken cancellationToken)
    {
        var result = new List<TelemetrySearchResult>();
        string text = (query.Text ?? string.Empty).Trim();
        string kind = (query.Kind ?? string.Empty).Trim().ToUpperInvariant();
        int mode = query.Mode.HasValue ? (int)query.Mode.Value : -1;

        await using SqliteConnection connection = Open();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.id, c.started_at, c.mode,
                   e.sequence, e.timestamp, e.kind, e.process_id, e.process_name,
                   e.protocol, e.local_address, e.local_port,
                   e.remote_address, e.remote_port, e.note
            FROM telemetry_events e
            INNER JOIN telemetry_captures c ON c.id = e.capture_id
            WHERE ($kind = '' OR UPPER(e.kind) = $kind)
              AND ($mode = -1 OR c.mode = $mode)
              AND (
                    $text = ''
                    OR UPPER(e.process_name) LIKE $like
                    OR UPPER(e.local_address) LIKE $like
                    OR UPPER(e.remote_address) LIKE $like
                    OR UPPER(e.note) LIKE $like
                  )
            ORDER BY e.id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$kind", kind);
        command.Parameters.AddWithValue("$mode", mode);
        command.Parameters.AddWithValue("$text", text);
        command.Parameters.AddWithValue("$like", $"%{text.ToUpperInvariant()}%");
        command.Parameters.AddWithValue("$limit", Math.Clamp(query.Limit, 1, 500));

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var item = new EtwNetworkEvent(
                reader.GetInt64(3),
                DateTimeOffset.Parse(reader.GetString(4)),
                reader.GetString(5),
                reader.GetInt32(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.GetString(9),
                reader.GetInt32(10),
                reader.GetString(11),
                reader.GetInt32(12),
                reader.GetString(13));

            result.Add(new TelemetrySearchResult(
                reader.GetInt64(0),
                DateTimeOffset.Parse(reader.GetString(1)),
                (EtwBackendMode)reader.GetInt32(2),
                item));
        }

        return result;
    }

    public async Task<TelemetryCaptureComparison?> CompareAsync(
        long firstCaptureId,
        long secondCaptureId,
        CancellationToken cancellationToken)
    {
        if (firstCaptureId == secondCaptureId)
            return null;

        TelemetryCapture? first = await _archive.LoadCaptureAsync(firstCaptureId, cancellationToken);
        TelemetryCapture? second = await _archive.LoadCaptureAsync(secondCaptureId, cancellationToken);
        if (first is null || second is null)
            return null;

        TelemetryCapture older = first.Summary.StartedAt <= second.Summary.StartedAt ? first : second;
        TelemetryCapture newer = ReferenceEquals(older, first) ? second : first;

        HashSet<string> olderFingerprints = older.Events
            .Select(Fingerprint)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> newerFingerprints = newer.Events
            .Select(Fingerprint)
            .ToHashSet(StringComparer.Ordinal);

        EtwNetworkEvent[] added = newer.Events
            .Where(item => !olderFingerprints.Contains(Fingerprint(item)))
            .GroupBy(Fingerprint, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(item => item.Timestamp)
            .ToArray();
        string[] missing = olderFingerprints
            .Where(value => !newerFingerprints.Contains(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        return new TelemetryCaptureComparison(
            older.Summary,
            newer.Summary,
            added,
            missing);
    }

    public async Task<TelemetryArchiveStats> GetStatsAsync(
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = Open();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                (SELECT COUNT(*) FROM telemetry_captures),
                (SELECT COUNT(*) FROM telemetry_events),
                (SELECT MIN(started_at) FROM telemetry_captures),
                (SELECT MAX(started_at) FROM telemetry_captures);
            """;

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        int captures = reader.GetInt32(0);
        int events = reader.GetInt32(1);
        DateTimeOffset? oldest = reader.IsDBNull(2)
            ? null
            : DateTimeOffset.Parse(reader.GetString(2));
        DateTimeOffset? newest = reader.IsDBNull(3)
            ? null
            : DateTimeOffset.Parse(reader.GetString(3));
        long bytes = File.Exists(_databasePath) ? new FileInfo(_databasePath).Length : 0L;

        return new TelemetryArchiveStats(captures, events, oldest, newest, bytes);
    }

    public async Task<TelemetryRetentionPreview> PreviewRetentionAsync(
        int keepLatest,
        CancellationToken cancellationToken)
    {
        keepLatest = Math.Clamp(keepLatest, 1, 10_000);
        await using SqliteConnection connection = Open();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*),
                   COALESCE(SUM((SELECT COUNT(*) FROM telemetry_events e WHERE e.capture_id = c.id)), 0),
                   MAX(c.started_at)
            FROM telemetry_captures c
            WHERE c.id NOT IN (
                SELECT id FROM telemetry_captures ORDER BY id DESC LIMIT $keep
            );
            """;
        command.Parameters.AddWithValue("$keep", keepLatest);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        int captures = reader.GetInt32(0);
        int events = reader.GetInt32(1);
        DateTimeOffset? through = reader.IsDBNull(2)
            ? null
            : DateTimeOffset.Parse(reader.GetString(2));

        return new TelemetryRetentionPreview(keepLatest, captures, events, through);
    }

    public async Task<TelemetryRetentionPreview> ApplyRetentionAsync(
        int keepLatest,
        CancellationToken cancellationToken)
    {
        TelemetryRetentionPreview preview =
            await PreviewRetentionAsync(keepLatest, cancellationToken);
        if (preview.DeleteCaptureCount == 0)
            return preview;

        await using SqliteConnection connection = Open();
        await using SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            DELETE FROM telemetry_captures
            WHERE id NOT IN (
                SELECT id FROM telemetry_captures ORDER BY id DESC LIMIT $keep
            );
            """;
        command.Parameters.AddWithValue("$keep", preview.KeepLatest);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return preview;
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(
            $"Data Source={_databasePath};Cache=Shared;Foreign Keys=True");
        connection.Open();
        return connection;
    }

    private static string Fingerprint(EtwNetworkEvent item) => string.Join('|',
        Normalize(item.Kind),
        Normalize(item.Protocol),
        Normalize(item.LocalAddress),
        item.LocalPort,
        Normalize(item.RemoteAddress),
        item.RemotePort,
        Normalize(item.ProcessName));

    private static string Normalize(string? value) =>
        (value ?? string.Empty).Trim().Replace('/', '\\').ToUpperInvariant();
}
