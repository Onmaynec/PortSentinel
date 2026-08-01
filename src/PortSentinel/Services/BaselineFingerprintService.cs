using Microsoft.Data.Sqlite;
using PortSentinel.Models;

namespace PortSentinel.Services;

internal sealed class BaselineFingerprintService
{
    private readonly SessionStore _store;

    public BaselineFingerprintService(SessionStore store)
    {
        _store = store;
    }

    public async Task<BaselineComparison?> CompareAsync(
        string name,
        IReadOnlyList<NetworkEntry> current,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(
            $"Data Source={_store.DatabasePath};Cache=Shared");
        await connection.OpenAsync(cancellationToken);

        var header = connection.CreateCommand();
        header.CommandText = "SELECT id, entry_count FROM baselines WHERE name = $name;";
        header.Parameters.AddWithValue("$name", name);

        await using SqliteDataReader headerReader =
            await header.ExecuteReaderAsync(cancellationToken);
        if (!await headerReader.ReadAsync(cancellationToken))
        {
            return null;
        }

        long baselineId = headerReader.GetInt64(0);
        int baselineCount = headerReader.GetInt32(1);
        await headerReader.DisposeAsync();

        var fingerprints = new HashSet<string>(StringComparer.Ordinal);
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT protocol, local_address, local_port, remote_address, remote_port,
                   state, process_id, process_name, executable_path, is_listener, is_external
            FROM baseline_entries
            WHERE baseline_id = $id;
            """;
        command.Parameters.AddWithValue("$id", baselineId);

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var entry = new NetworkEntry(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetInt32(4),
                reader.GetString(5),
                reader.GetInt32(6),
                reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetBoolean(9),
                reader.GetBoolean(10));
            fingerprints.Add(entry.BaselineIdentity);
        }

        NetworkEntry[] added = current
            .Where(entry => !fingerprints.Contains(entry.BaselineIdentity))
            .ToArray();
        HashSet<string> currentFingerprints = current
            .Select(entry => entry.BaselineIdentity)
            .ToHashSet(StringComparer.Ordinal);
        string[] missing = fingerprints
            .Where(identity => !currentFingerprints.Contains(identity))
            .ToArray();

        return new BaselineComparison(
            name,
            current.Count,
            baselineCount,
            added,
            missing);
    }
}
