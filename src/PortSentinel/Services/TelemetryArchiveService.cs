using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using PortSentinel.Models;

namespace PortSentinel.Services;

internal sealed class TelemetryArchiveService
{
    private readonly string _databasePath;
    private readonly string _reportsDirectory;

    public TelemetryArchiveService(string databasePath, string reportsDirectory)
    {
        _databasePath = databasePath;
        _reportsDirectory = reportsDirectory;
        Directory.CreateDirectory(_reportsDirectory);
        Initialize();
    }

    public async Task<long> SaveCaptureAsync(
        EtwCaptureResult capture,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = Open();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var insertCapture = connection.CreateCommand();
        insertCapture.Transaction = (SqliteTransaction)transaction;
        insertCapture.CommandText = """
            INSERT INTO telemetry_captures(
                started_at, ended_at, mode, status, event_count,
                connect_count, accept_count, disconnect_count, retransmit_count,
                elevated, failure)
            VALUES(
                $started, $ended, $mode, $status, $events,
                $connect, $accept, $disconnect, $retransmit,
                $elevated, $failure);
            SELECT last_insert_rowid();
            """;
        insertCapture.Parameters.AddWithValue("$started", capture.StartedAt.ToString("O"));
        insertCapture.Parameters.AddWithValue("$ended", capture.EndedAt.ToString("O"));
        insertCapture.Parameters.AddWithValue("$mode", (int)capture.Mode);
        insertCapture.Parameters.AddWithValue("$status", capture.Status);
        insertCapture.Parameters.AddWithValue("$events", capture.Events.Count);
        insertCapture.Parameters.AddWithValue("$connect", capture.ConnectCount);
        insertCapture.Parameters.AddWithValue("$accept", capture.AcceptCount);
        insertCapture.Parameters.AddWithValue("$disconnect", capture.DisconnectCount);
        insertCapture.Parameters.AddWithValue("$retransmit", capture.RetransmitCount);
        insertCapture.Parameters.AddWithValue("$elevated", capture.Elevated);
        insertCapture.Parameters.AddWithValue("$failure", (object?)capture.Failure ?? DBNull.Value);
        long captureId = (long)(await insertCapture.ExecuteScalarAsync(cancellationToken) ?? 0L);

        foreach (EtwNetworkEvent item in capture.Events)
        {
            var insertEvent = connection.CreateCommand();
            insertEvent.Transaction = (SqliteTransaction)transaction;
            insertEvent.CommandText = """
                INSERT INTO telemetry_events(
                    capture_id, sequence, timestamp, kind, process_id, process_name,
                    protocol, local_address, local_port, remote_address, remote_port,
                    note, fingerprint)
                VALUES(
                    $capture, $sequence, $timestamp, $kind, $pid, $process,
                    $protocol, $localAddress, $localPort, $remoteAddress, $remotePort,
                    $note, $fingerprint);
                """;
            insertEvent.Parameters.AddWithValue("$capture", captureId);
            insertEvent.Parameters.AddWithValue("$sequence", item.Sequence);
            insertEvent.Parameters.AddWithValue("$timestamp", item.Timestamp.ToString("O"));
            insertEvent.Parameters.AddWithValue("$kind", item.Kind);
            insertEvent.Parameters.AddWithValue("$pid", item.ProcessId);
            insertEvent.Parameters.AddWithValue("$process", item.ProcessName);
            insertEvent.Parameters.AddWithValue("$protocol", item.Protocol);
            insertEvent.Parameters.AddWithValue("$localAddress", item.LocalAddress);
            insertEvent.Parameters.AddWithValue("$localPort", item.LocalPort);
            insertEvent.Parameters.AddWithValue("$remoteAddress", item.RemoteAddress);
            insertEvent.Parameters.AddWithValue("$remotePort", item.RemotePort);
            insertEvent.Parameters.AddWithValue("$note", item.Note);
            insertEvent.Parameters.AddWithValue("$fingerprint", Fingerprint(item));
            await insertEvent.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return captureId;
    }

    public async Task<IReadOnlyList<TelemetryCaptureSummary>> ListCapturesAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var result = new List<TelemetryCaptureSummary>();
        await using SqliteConnection connection = Open();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, started_at, ended_at, mode, status, event_count,
                   connect_count, accept_count, disconnect_count, retransmit_count,
                   elevated, failure
            FROM telemetry_captures
            ORDER BY id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 200));

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(ReadSummary(reader));

        return result;
    }

    public async Task<TelemetryCapture?> LoadCaptureAsync(
        long captureId,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = Open();
        var header = connection.CreateCommand();
        header.CommandText = """
            SELECT id, started_at, ended_at, mode, status, event_count,
                   connect_count, accept_count, disconnect_count, retransmit_count,
                   elevated, failure
            FROM telemetry_captures
            WHERE id = $id;
            """;
        header.Parameters.AddWithValue("$id", captureId);

        await using SqliteDataReader headerReader = await header.ExecuteReaderAsync(cancellationToken);
        if (!await headerReader.ReadAsync(cancellationToken))
            return null;

        TelemetryCaptureSummary summary = ReadSummary(headerReader);
        await headerReader.DisposeAsync();

        var events = new List<EtwNetworkEvent>();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT sequence, timestamp, kind, process_id, process_name,
                   protocol, local_address, local_port, remote_address, remote_port, note
            FROM telemetry_events
            WHERE capture_id = $capture
            ORDER BY sequence, id;
            """;
        command.Parameters.AddWithValue("$capture", captureId);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(new EtwNetworkEvent(
                reader.GetInt64(0),
                DateTimeOffset.Parse(reader.GetString(1)),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetInt32(7),
                reader.GetString(8),
                reader.GetInt32(9),
                reader.GetString(10)));
        }

        return new TelemetryCapture(summary, events);
    }

    public async Task<TelemetryCaptureComparison?> CompareLatestAsync(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<TelemetryCaptureSummary> captures =
            await ListCapturesAsync(2, cancellationToken);
        if (captures.Count < 2)
            return null;

        TelemetryCapture? newer = await LoadCaptureAsync(captures[0].Id, cancellationToken);
        TelemetryCapture? older = await LoadCaptureAsync(captures[1].Id, cancellationToken);
        if (newer is null || older is null)
            return null;

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
            .Where(fingerprint => !newerFingerprints.Contains(fingerprint))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        return new TelemetryCaptureComparison(
            older.Summary,
            newer.Summary,
            added,
            missing);
    }

    public async Task<string> ExportCaptureAsync(
        TelemetryCapture capture,
        string format,
        CancellationToken cancellationToken)
    {
        string extension = format.Equals("json", StringComparison.OrdinalIgnoreCase) ? "json" : "md";
        string path = Path.Combine(
            _reportsDirectory,
            $"telemetry-capture-{capture.Summary.Id}-{capture.Summary.StartedAt:yyyyMMdd-HHmmss}.{extension}");

        if (extension == "json")
        {
            var payload = new
            {
                schemaVersion = 1,
                capture = capture.Summary,
                privacy = "No packet payload, HTTP body, cookies, tokens or decrypted TLS content is stored.",
                events = capture.Events
            };
            await File.WriteAllTextAsync(path,
                JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);
            return path;
        }

        var text = new StringBuilder()
            .AppendLine($"# PortSentinel Telemetry Capture #{capture.Summary.Id}")
            .AppendLine()
            .AppendLine($"- **Backend:** {capture.Summary.Mode}")
            .AppendLine($"- **Started:** {capture.Summary.StartedAt:O}")
            .AppendLine($"- **Ended:** {capture.Summary.EndedAt:O}")
            .AppendLine($"- **Events:** {capture.Summary.EventCount}")
            .AppendLine($"- **Connect / Accept / Disconnect / Retransmit:** {capture.Summary.ConnectCount} / {capture.Summary.AcceptCount} / {capture.Summary.DisconnectCount} / {capture.Summary.RetransmitCount}")
            .AppendLine()
            .AppendLine("> Packet payload, HTTP body, cookies, tokens and decrypted TLS content are not stored.")
            .AppendLine()
            .AppendLine("| Time | Kind | Process | PID | Protocol | Local | Remote | Note |")
            .AppendLine("|---|---|---|---:|---|---|---|---|");

        foreach (EtwNetworkEvent item in capture.Events)
        {
            text.AppendLine(
                $"| {item.Timestamp:HH:mm:ss.fff} | {Escape(item.Kind)} | {Escape(item.ProcessName)} | {item.ProcessId} | {Escape(item.Protocol)} | {Escape(item.LocalEndpoint)} | {Escape(item.RemoteEndpoint)} | {Escape(item.Note)} |");
        }

        await File.WriteAllTextAsync(path, text.ToString(), cancellationToken);
        return path;
    }

    public async Task<string> ExportComparisonAsync(
        TelemetryCaptureComparison comparison,
        string format,
        CancellationToken cancellationToken)
    {
        string extension = format.Equals("json", StringComparison.OrdinalIgnoreCase) ? "json" : "md";
        string path = Path.Combine(
            _reportsDirectory,
            $"telemetry-diff-{comparison.Older.Id}-to-{comparison.Newer.Id}.{extension}");

        if (extension == "json")
        {
            var payload = new
            {
                schemaVersion = 1,
                older = comparison.Older,
                newer = comparison.Newer,
                comparison.AddedEvents,
                comparison.MissingFingerprints,
                limitation = "A lifecycle fingerprint excludes PID and is diagnostic metadata, not a threat verdict."
            };
            await File.WriteAllTextAsync(path,
                JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);
            return path;
        }

        var text = new StringBuilder()
            .AppendLine($"# PortSentinel Telemetry Diff #{comparison.Older.Id} → #{comparison.Newer.Id}")
            .AppendLine()
            .AppendLine($"- **Older events:** {comparison.OlderEventCount}")
            .AppendLine($"- **Newer events:** {comparison.NewerEventCount}")
            .AppendLine($"- **Added fingerprints:** {comparison.AddedEvents.Count}")
            .AppendLine($"- **Missing fingerprints:** {comparison.MissingFingerprints.Count}")
            .AppendLine()
            .AppendLine("## Added lifecycle events")
            .AppendLine()
            .AppendLine("| Kind | Process | Protocol | Local | Remote |")
            .AppendLine("|---|---|---|---|---|");

        foreach (EtwNetworkEvent item in comparison.AddedEvents)
        {
            text.AppendLine(
                $"| {Escape(item.Kind)} | {Escape(item.ProcessName)} | {Escape(item.Protocol)} | {Escape(item.LocalEndpoint)} | {Escape(item.RemoteEndpoint)} |");
        }

        text.AppendLine()
            .AppendLine("## Missing fingerprints")
            .AppendLine();
        foreach (string fingerprint in comparison.MissingFingerprints)
            text.AppendLine($"- `{fingerprint.Replace("`", "'", StringComparison.Ordinal)}`");

        await File.WriteAllTextAsync(path, text.ToString(), cancellationToken);
        return path;
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(
            $"Data Source={_databasePath};Cache=Shared;Foreign Keys=True");
        connection.Open();
        return connection;
    }

    private void Initialize()
    {
        using SqliteConnection connection = Open();
        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS telemetry_captures(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                started_at TEXT NOT NULL,
                ended_at TEXT NOT NULL,
                mode INTEGER NOT NULL,
                status TEXT NOT NULL,
                event_count INTEGER NOT NULL,
                connect_count INTEGER NOT NULL,
                accept_count INTEGER NOT NULL,
                disconnect_count INTEGER NOT NULL,
                retransmit_count INTEGER NOT NULL,
                elevated INTEGER NOT NULL,
                failure TEXT NULL
            );
            CREATE TABLE IF NOT EXISTS telemetry_events(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                capture_id INTEGER NOT NULL REFERENCES telemetry_captures(id) ON DELETE CASCADE,
                sequence INTEGER NOT NULL,
                timestamp TEXT NOT NULL,
                kind TEXT NOT NULL,
                process_id INTEGER NOT NULL,
                process_name TEXT NOT NULL,
                protocol TEXT NOT NULL,
                local_address TEXT NOT NULL,
                local_port INTEGER NOT NULL,
                remote_address TEXT NOT NULL,
                remote_port INTEGER NOT NULL,
                note TEXT NOT NULL,
                fingerprint TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_telemetry_events_capture
                ON telemetry_events(capture_id, sequence);
            CREATE INDEX IF NOT EXISTS ix_telemetry_events_fingerprint
                ON telemetry_events(fingerprint);
            """;
        command.ExecuteNonQuery();
    }

    private static TelemetryCaptureSummary ReadSummary(SqliteDataReader reader) =>
        new(
            reader.GetInt64(0),
            DateTimeOffset.Parse(reader.GetString(1)),
            DateTimeOffset.Parse(reader.GetString(2)),
            (EtwBackendMode)reader.GetInt32(3),
            reader.GetString(4),
            reader.GetInt32(5),
            reader.GetInt32(6),
            reader.GetInt32(7),
            reader.GetInt32(8),
            reader.GetInt32(9),
            reader.GetBoolean(10),
            reader.IsDBNull(11) ? null : reader.GetString(11));

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

    private static string Escape(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal)
             .Replace("\r", " ", StringComparison.Ordinal)
             .Replace("\n", " ", StringComparison.Ordinal);
}
