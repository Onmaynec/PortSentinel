using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using PortSentinel.Models;

namespace PortSentinel.Services;

internal sealed class TimelineExplorerService
{
    private readonly string _databasePath;
    private readonly string _reportsDirectory;

    public TimelineExplorerService(string databasePath, string reportsDirectory)
    {
        _databasePath = databasePath;
        _reportsDirectory = reportsDirectory;
        Directory.CreateDirectory(_reportsDirectory);
        InitializeIndexes();
    }

    public async Task<TimelineCapturePage> ListCapturesAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        pageSize = Math.Clamp(pageSize, 10, 100);
        await using SqliteConnection connection = Open();

        var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM telemetry_captures;";
        int total = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));
        int pageCount = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, pageCount);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, started_at, ended_at, mode, status, event_count,
                   connect_count, accept_count, disconnect_count, retransmit_count,
                   elevated, failure
            FROM telemetry_captures
            ORDER BY id DESC
            LIMIT $limit OFFSET $offset;
            """;
        command.Parameters.AddWithValue("$limit", pageSize);
        command.Parameters.AddWithValue("$offset", (page - 1) * pageSize);

        var items = new List<TelemetryCaptureSummary>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            items.Add(ReadSummary(reader));

        return new TimelineCapturePage(items, page, pageSize, total);
    }

    public async Task<TelemetryCaptureSummary?> GetCaptureSummaryAsync(
        long captureId,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = Open();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, started_at, ended_at, mode, status, event_count,
                   connect_count, accept_count, disconnect_count, retransmit_count,
                   elevated, failure
            FROM telemetry_captures
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", captureId);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadSummary(reader) : null;
    }

    public async Task<TimelineEventPage> GetEventsAsync(
        long captureId,
        int page,
        int pageSize,
        TimelineFilter filter,
        CancellationToken cancellationToken)
    {
        pageSize = Math.Clamp(pageSize, 10, 200);
        filter = Normalize(filter);
        string where = BuildWhere(filter);

        await using SqliteConnection connection = Open();
        var countCommand = connection.CreateCommand();
        countCommand.CommandText = $"SELECT COUNT(*) FROM telemetry_events WHERE {where};";
        AddParameters(countCommand, captureId, filter);
        int total = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));
        int pageCount = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, pageCount);

        var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT sequence, timestamp, kind, process_id, process_name,
                   protocol, local_address, local_port, remote_address, remote_port, note
            FROM telemetry_events
            WHERE {where}
            ORDER BY sequence, id
            LIMIT $limit OFFSET $offset;
            """;
        AddParameters(command, captureId, filter);
        command.Parameters.AddWithValue("$limit", pageSize);
        command.Parameters.AddWithValue("$offset", (page - 1) * pageSize);

        var items = new List<EtwNetworkEvent>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            items.Add(ReadEvent(reader));

        return new TimelineEventPage(captureId, filter, items, page, pageSize, total);
    }

    public async Task<TimelineJumpResult?> FindSequenceAsync(
        long captureId,
        long sequence,
        int pageSize,
        TimelineFilter filter,
        CancellationToken cancellationToken)
    {
        pageSize = Math.Clamp(pageSize, 10, 200);
        filter = Normalize(filter);
        string where = BuildWhere(filter);

        await using SqliteConnection connection = Open();
        var target = connection.CreateCommand();
        target.CommandText = $"""
            SELECT sequence, timestamp, kind, process_id, process_name,
                   protocol, local_address, local_port, remote_address, remote_port, note
            FROM telemetry_events
            WHERE {where} AND sequence = $sequence
            ORDER BY id
            LIMIT 1;
            """;
        AddParameters(target, captureId, filter);
        target.Parameters.AddWithValue("$sequence", sequence);

        EtwNetworkEvent? item;
        await using (SqliteDataReader reader = await target.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
                return null;
            item = ReadEvent(reader);
        }

        var position = connection.CreateCommand();
        position.CommandText = $"SELECT COUNT(*) FROM telemetry_events WHERE {where} AND sequence <= $sequence;";
        AddParameters(position, captureId, filter);
        position.Parameters.AddWithValue("$sequence", sequence);
        int row = Convert.ToInt32(await position.ExecuteScalarAsync(cancellationToken));
        if (row <= 0)
            return null;

        return new TimelineJumpResult(
            ((row - 1) / pageSize) + 1,
            (row - 1) % pageSize,
            item);
    }

    public async Task<string> ExportPageAsync(
        TimelineEventPage page,
        string format,
        CancellationToken cancellationToken)
    {
        string extension = format.Equals("json", StringComparison.OrdinalIgnoreCase) ? "json" : "md";
        string path = Path.Combine(
            _reportsDirectory,
            $"timeline-capture-{page.CaptureId}-page-{page.Page}-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.{extension}");

        if (extension == "json")
        {
            var payload = new
            {
                schemaVersion = 1,
                captureId = page.CaptureId,
                page = page.Page,
                pageSize = page.PageSize,
                page.FirstRow,
                page.LastRow,
                totalMatchingEvents = page.TotalItems,
                filter = page.Filter,
                privacy = "No packet payload, HTTP body, cookies, credentials, tokens or decrypted TLS content is stored.",
                events = page.Items
            };
            await File.WriteAllTextAsync(
                path,
                JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);
            return path;
        }

        var text = new StringBuilder()
            .AppendLine($"# PortSentinel Timeline — Capture #{page.CaptureId}")
            .AppendLine()
            .AppendLine($"- **Page:** {page.Page} / {page.PageCount}")
            .AppendLine($"- **Rows:** {page.FirstRow}–{page.LastRow} of {page.TotalItems}")
            .AppendLine($"- **Filter:** {Escape(page.Filter.Description)}")
            .AppendLine()
            .AppendLine("| Sequence | Time | Kind | Process | PID | Protocol | Local | Remote | Note |")
            .AppendLine("|---:|---|---|---|---:|---|---|---|---|");

        foreach (EtwNetworkEvent item in page.Items)
        {
            text.AppendLine(
                $"| {item.Sequence} | {item.Timestamp:O} | {Escape(item.Kind)} | {Escape(item.ProcessName)} | {item.ProcessId} | {Escape(item.Protocol)} | {Escape(item.LocalEndpoint)} | {Escape(item.RemoteEndpoint)} | {Escape(item.Note)} |");
        }

        text.AppendLine()
            .AppendLine("> This export contains only the currently displayed server-side page.")
            .AppendLine("> Packet payload and decrypted application content are not stored.");

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

    private void InitializeIndexes()
    {
        using SqliteConnection connection = Open();
        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE INDEX IF NOT EXISTS ix_telemetry_events_capture_sequence
                ON telemetry_events(capture_id, sequence);
            CREATE INDEX IF NOT EXISTS ix_telemetry_events_capture_kind_sequence
                ON telemetry_events(capture_id, kind, sequence);
            CREATE INDEX IF NOT EXISTS ix_telemetry_events_capture_protocol_sequence
                ON telemetry_events(capture_id, protocol, sequence);
            """;
        command.ExecuteNonQuery();
    }

    private static string BuildWhere(TimelineFilter filter)
    {
        var conditions = new List<string> { "capture_id = $capture" };
        if (!string.IsNullOrWhiteSpace(filter.Kind))
            conditions.Add("kind = $kind COLLATE NOCASE");
        if (!string.IsNullOrWhiteSpace(filter.Protocol))
            conditions.Add("protocol = $protocol COLLATE NOCASE");
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            conditions.Add("""
                (
                    process_name LIKE $search ESCAPE '\' COLLATE NOCASE OR
                    local_address LIKE $search ESCAPE '\' COLLATE NOCASE OR
                    remote_address LIKE $search ESCAPE '\' COLLATE NOCASE OR
                    note LIKE $search ESCAPE '\' COLLATE NOCASE OR
                    CAST(local_port AS TEXT) LIKE $search ESCAPE '\' OR
                    CAST(remote_port AS TEXT) LIKE $search ESCAPE '\'
                )
                """);
        }
        return string.Join(" AND ", conditions);
    }

    private static void AddParameters(
        SqliteCommand command,
        long captureId,
        TimelineFilter filter)
    {
        command.Parameters.AddWithValue("$capture", captureId);
        if (!string.IsNullOrWhiteSpace(filter.Kind))
            command.Parameters.AddWithValue("$kind", filter.Kind);
        if (!string.IsNullOrWhiteSpace(filter.Protocol))
            command.Parameters.AddWithValue("$protocol", filter.Protocol);
        if (!string.IsNullOrWhiteSpace(filter.Search))
            command.Parameters.AddWithValue("$search", $"%{EscapeLike(filter.Search)}%");
    }

    private static TimelineFilter Normalize(TimelineFilter filter) => new(
        (filter.Kind ?? string.Empty).Trim().ToUpperInvariant(),
        (filter.Protocol ?? string.Empty).Trim().ToUpperInvariant(),
        (filter.Search ?? string.Empty).Trim());

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("%", "\\%", StringComparison.Ordinal)
             .Replace("_", "\\_", StringComparison.Ordinal);

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

    private static EtwNetworkEvent ReadEvent(SqliteDataReader reader) =>
        new(
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
            reader.GetString(10));

    private static string Escape(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal)
             .Replace("\r", " ", StringComparison.Ordinal)
             .Replace("\n", " ", StringComparison.Ordinal);
}
