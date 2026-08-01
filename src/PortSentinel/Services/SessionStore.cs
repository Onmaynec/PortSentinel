using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using PortSentinel.Models;

namespace PortSentinel.Services;

internal sealed class SessionStore
{
    private readonly string _databasePath;
    private readonly string _reportsDirectory;

    public SessionStore()
    {
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PortSentinel");
        Directory.CreateDirectory(root);
        _reportsDirectory = Path.Combine(root, "reports");
        Directory.CreateDirectory(_reportsDirectory);
        _databasePath = Path.Combine(root, "portsentinel.db");
        Initialize();
    }

    public string DatabasePath => _databasePath;
    public string ReportsDirectory => _reportsDirectory;

    public async Task<long> SaveSessionAsync(
        string name,
        DateTimeOffset startedAt,
        IReadOnlyCollection<NetworkEntry> entries,
        CancellationToken cancellationToken)
    {
        DateTimeOffset endedAt = DateTimeOffset.Now;
        await using var connection = Open();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var insertSession = connection.CreateCommand();
        insertSession.Transaction = (SqliteTransaction)transaction;
        insertSession.CommandText = """
            INSERT INTO sessions(name, started_at, ended_at, entry_count, process_count, listener_count, external_count)
            VALUES($name, $started, $ended, $entries, $processes, $listeners, $external);
            SELECT last_insert_rowid();
            """;
        insertSession.Parameters.AddWithValue("$name", name);
        insertSession.Parameters.AddWithValue("$started", startedAt.ToString("O"));
        insertSession.Parameters.AddWithValue("$ended", endedAt.ToString("O"));
        insertSession.Parameters.AddWithValue("$entries", entries.Count);
        insertSession.Parameters.AddWithValue("$processes", entries.Select(e => e.ProcessId).Distinct().Count());
        insertSession.Parameters.AddWithValue("$listeners", entries.Count(e => e.IsListener));
        insertSession.Parameters.AddWithValue("$external", entries.Count(e => e.IsExternal));
        long sessionId = (long)(await insertSession.ExecuteScalarAsync(cancellationToken) ?? 0L);

        foreach (NetworkEntry entry in entries)
        {
            var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = """
                INSERT INTO session_entries(
                    session_id, identity, protocol, local_address, local_port,
                    remote_address, remote_port, state, process_id, process_name,
                    executable_path, is_listener, is_external)
                VALUES(
                    $session, $identity, $protocol, $localAddress, $localPort,
                    $remoteAddress, $remotePort, $state, $pid, $process,
                    $path, $listener, $external);
                """;
            AddEntryParameters(command, sessionId, entry);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return sessionId;
    }

    public async Task<IReadOnlyList<SessionSummary>> ListSessionsAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var result = new List<SessionSummary>();
        await using var connection = Open();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, started_at, ended_at, entry_count, process_count, listener_count, external_count
            FROM sessions ORDER BY id DESC LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 100));

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new SessionSummary(
                reader.GetInt64(0),
                reader.GetString(1),
                DateTimeOffset.Parse(reader.GetString(2)),
                DateTimeOffset.Parse(reader.GetString(3)),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetInt32(6),
                reader.GetInt32(7)));
        }

        return result;
    }

    public async Task<IReadOnlyList<NetworkEntry>> LoadSessionEntriesAsync(
        long sessionId,
        CancellationToken cancellationToken)
    {
        var entries = new List<NetworkEntry>();
        await using var connection = Open();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT protocol, local_address, local_port, remote_address, remote_port,
                   state, process_id, process_name, executable_path, is_listener, is_external
            FROM session_entries WHERE session_id = $session ORDER BY id;
            """;
        command.Parameters.AddWithValue("$session", sessionId);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new NetworkEntry(
                reader.GetString(0), reader.GetString(1), reader.GetInt32(2),
                reader.GetString(3), reader.GetInt32(4), reader.GetString(5),
                reader.GetInt32(6), reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetBoolean(9), reader.GetBoolean(10)));
        }

        return entries;
    }

    public async Task<string> ExportSessionAsync(
        SessionSummary session,
        IReadOnlyList<NetworkEntry> entries,
        string format,
        CancellationToken cancellationToken)
    {
        string safeName = string.Concat(session.Name.Select(c =>
            Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        string extension = format.Equals("json", StringComparison.OrdinalIgnoreCase) ? "json" : "md";
        string path = Path.Combine(_reportsDirectory, $"{session.Id}-{safeName}.{extension}");

        if (extension == "json")
        {
            var payload = new
            {
                schemaVersion = 1,
                session,
                entries
            };
            await File.WriteAllTextAsync(path,
                JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);
        }
        else
        {
            var text = new StringBuilder()
                .AppendLine($"# PortSentinel Session {session.Id}")
                .AppendLine()
                .AppendLine($"- **Название:** {session.Name}")
                .AppendLine($"- **Начало:** {session.StartedAt:G}")
                .AppendLine($"- **Окончание:** {session.EndedAt:G}")
                .AppendLine($"- **Записей:** {session.EntryCount}")
                .AppendLine($"- **Процессов:** {session.ProcessCount}")
                .AppendLine($"- **Listeners:** {session.ListenerCount}")
                .AppendLine()
                .AppendLine("| Process | PID | Proto | Local | Remote | State |")
                .AppendLine("|---|---:|---|---|---|---|");
            foreach (NetworkEntry entry in entries)
            {
                text.AppendLine($"| {Escape(entry.ProcessName)} | {entry.ProcessId} | {entry.Protocol} | {Escape(entry.LocalEndpoint)} | {Escape(entry.RemoteEndpoint)} | {Escape(entry.State)} |");
            }
            await File.WriteAllTextAsync(path, text.ToString(), cancellationToken);
        }

        return path;
    }

    public async Task CreateBaselineAsync(
        string name,
        IReadOnlyCollection<NetworkEntry> entries,
        CancellationToken cancellationToken)
    {
        await using var connection = Open();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var delete = connection.CreateCommand();
        delete.Transaction = (SqliteTransaction)transaction;
        delete.CommandText = "DELETE FROM baselines WHERE name = $name;";
        delete.Parameters.AddWithValue("$name", name);
        await delete.ExecuteNonQueryAsync(cancellationToken);

        var insert = connection.CreateCommand();
        insert.Transaction = (SqliteTransaction)transaction;
        insert.CommandText = """
            INSERT INTO baselines(name, created_at, entry_count)
            VALUES($name, $created, $count);
            SELECT last_insert_rowid();
            """;
        insert.Parameters.AddWithValue("$name", name);
        insert.Parameters.AddWithValue("$created", DateTimeOffset.Now.ToString("O"));
        insert.Parameters.AddWithValue("$count", entries.Count);
        long baselineId = (long)(await insert.ExecuteScalarAsync(cancellationToken) ?? 0L);

        foreach (NetworkEntry entry in entries)
        {
            var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = """
                INSERT INTO baseline_entries(baseline_id, identity, protocol, local_address, local_port,
                    remote_address, remote_port, state, process_id, process_name, executable_path,
                    is_listener, is_external)
                VALUES($baseline, $identity, $protocol, $localAddress, $localPort,
                    $remoteAddress, $remotePort, $state, $pid, $process, $path, $listener, $external);
                """;
            AddEntryParameters(command, baselineId, entry, "$baseline");
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BaselineSummary>> ListBaselinesAsync(CancellationToken cancellationToken)
    {
        var result = new List<BaselineSummary>();
        await using var connection = Open();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, created_at, entry_count FROM baselines ORDER BY id DESC;";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new BaselineSummary(
                reader.GetInt64(0), reader.GetString(1),
                DateTimeOffset.Parse(reader.GetString(2)), reader.GetInt32(3)));
        }
        return result;
    }

    public async Task<BaselineComparison?> CompareBaselineAsync(
        string name,
        IReadOnlyList<NetworkEntry> current,
        CancellationToken cancellationToken)
    {
        await using var connection = Open();
        var header = connection.CreateCommand();
        header.CommandText = "SELECT id, entry_count FROM baselines WHERE name = $name;";
        header.Parameters.AddWithValue("$name", name);
        await using SqliteDataReader headerReader = await header.ExecuteReaderAsync(cancellationToken);
        if (!await headerReader.ReadAsync(cancellationToken))
        {
            return null;
        }
        long id = headerReader.GetInt64(0);
        int count = headerReader.GetInt32(1);
        await headerReader.DisposeAsync();

        var identities = new HashSet<string>(StringComparer.Ordinal);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT identity FROM baseline_entries WHERE baseline_id = $id;";
        command.Parameters.AddWithValue("$id", id);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            identities.Add(reader.GetString(0));
        }

        NetworkEntry[] added = current.Where(e => !identities.Contains(e.Identity)).ToArray();
        HashSet<string> currentIds = current.Select(e => e.Identity).ToHashSet(StringComparer.Ordinal);
        string[] missing = identities.Where(idValue => !currentIds.Contains(idValue)).ToArray();
        return new BaselineComparison(name, current.Count, count, added, missing);
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection($"Data Source={_databasePath};Cache=Shared");
        connection.Open();
        return connection;
    }

    private void Initialize()
    {
        using var connection = Open();
        var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS sessions(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                started_at TEXT NOT NULL,
                ended_at TEXT NOT NULL,
                entry_count INTEGER NOT NULL,
                process_count INTEGER NOT NULL,
                listener_count INTEGER NOT NULL,
                external_count INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS session_entries(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id INTEGER NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
                identity TEXT NOT NULL,
                protocol TEXT NOT NULL,
                local_address TEXT NOT NULL,
                local_port INTEGER NOT NULL,
                remote_address TEXT NOT NULL,
                remote_port INTEGER NOT NULL,
                state TEXT NOT NULL,
                process_id INTEGER NOT NULL,
                process_name TEXT NOT NULL,
                executable_path TEXT NULL,
                is_listener INTEGER NOT NULL,
                is_external INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_session_entries_session ON session_entries(session_id);
            CREATE TABLE IF NOT EXISTS baselines(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE,
                created_at TEXT NOT NULL,
                entry_count INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS baseline_entries(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                baseline_id INTEGER NOT NULL REFERENCES baselines(id) ON DELETE CASCADE,
                identity TEXT NOT NULL,
                protocol TEXT NOT NULL,
                local_address TEXT NOT NULL,
                local_port INTEGER NOT NULL,
                remote_address TEXT NOT NULL,
                remote_port INTEGER NOT NULL,
                state TEXT NOT NULL,
                process_id INTEGER NOT NULL,
                process_name TEXT NOT NULL,
                executable_path TEXT NULL,
                is_listener INTEGER NOT NULL,
                is_external INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_baseline_entries_baseline ON baseline_entries(baseline_id);
            """;
        command.ExecuteNonQuery();
    }

    private static void AddEntryParameters(
        SqliteCommand command,
        long ownerId,
        NetworkEntry entry,
        string ownerParameter = "$session")
    {
        command.Parameters.AddWithValue(ownerParameter, ownerId);
        command.Parameters.AddWithValue("$identity", entry.Identity);
        command.Parameters.AddWithValue("$protocol", entry.Protocol);
        command.Parameters.AddWithValue("$localAddress", entry.LocalAddress);
        command.Parameters.AddWithValue("$localPort", entry.LocalPort);
        command.Parameters.AddWithValue("$remoteAddress", entry.RemoteAddress);
        command.Parameters.AddWithValue("$remotePort", entry.RemotePort);
        command.Parameters.AddWithValue("$state", entry.State);
        command.Parameters.AddWithValue("$pid", entry.ProcessId);
        command.Parameters.AddWithValue("$process", entry.ProcessName);
        command.Parameters.AddWithValue("$path", (object?)entry.ExecutablePath ?? DBNull.Value);
        command.Parameters.AddWithValue("$listener", entry.IsListener);
        command.Parameters.AddWithValue("$external", entry.IsExternal);
    }

    private static string Escape(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal)
             .Replace("\r", " ", StringComparison.Ordinal)
             .Replace("\n", " ", StringComparison.Ordinal);
}
