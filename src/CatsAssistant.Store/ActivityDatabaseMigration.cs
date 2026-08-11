using Microsoft.Data.Sqlite;

namespace CatsAssistant.Store;

public enum ActivityDatabaseMigrationStatus
{
    AlreadyMigrated,
    NothingToMigrate,
    Migrated,
}

public sealed record ActivityDatabaseMigrationResult(ActivityDatabaseMigrationStatus Status, long RowsMigrated);

/// <summary>
/// Bascule one-shot, idempotente, de l'ancienne base activité en clair (Phase 1) vers la base activité
/// chiffrée DPAPI (décision #10/#12). Ne touche jamais aux 5 tables métier : step-2.2 ne migre que
/// activity_events, seule table qui porte des données dans l'ancienne base.
/// </summary>
public sealed class ActivityDatabaseMigration
{
    private readonly string _legacyDatabasePath;
    private readonly string _activityDatabasePath;
    private readonly string _key;

    public ActivityDatabaseMigration(string legacyDatabasePath, string activityDatabasePath, string key)
    {
        _legacyDatabasePath = legacyDatabasePath;
        _activityDatabasePath = activityDatabasePath;
        _key = key;
    }

    public ActivityDatabaseMigrationResult Run()
    {
        if (File.Exists(_activityDatabasePath))
        {
            return new ActivityDatabaseMigrationResult(ActivityDatabaseMigrationStatus.AlreadyMigrated, 0);
        }

        if (!File.Exists(_legacyDatabasePath))
        {
            return new ActivityDatabaseMigrationResult(ActivityDatabaseMigrationStatus.NothingToMigrate, 0);
        }

        BackupLegacyDatabase();

        try
        {
            var rowsMigrated = MigrateRows();

            // Microsoft.Data.Sqlite pools native handles by connection string: Dispose() alone does not
            // release the OS file lock, so a rename right after would race it on Windows (seen with the
            // real production database, not reproduced reliably by fast in-memory-sized test fixtures).
            SqliteConnection.ClearAllPools();

            RenameLegacyDatabase();
            return new ActivityDatabaseMigrationResult(ActivityDatabaseMigrationStatus.Migrated, rowsMigrated);
        }
        catch
        {
            DeletePartialTarget();
            throw;
        }
    }

    private void BackupLegacyDatabase()
    {
        var backupPath = $"{_legacyDatabasePath}.backup-{DateTime.UtcNow:yyyyMMddTHHmmssZ}";
        File.Copy(_legacyDatabasePath, backupPath, overwrite: false);
    }

    private long MigrateRows()
    {
        using var legacyConnection = new SqliteConnection($"Data Source={_legacyDatabasePath}");
        legacyConnection.Open();

        var rows = ReadLegacyRows(legacyConnection);

        using (var activityConnection = new SqliteConnectionFactory(_activityDatabasePath, _key).OpenConnection())
        {
            InsertRows(activityConnection, rows);
            VerifyTransfer(legacyConnection, activityConnection);
        }

        return rows.Count;
    }

    private static List<LegacyRow> ReadLegacyRows(SqliteConnection legacyConnection)
    {
        var rows = new List<LegacyRow>();

        using var command = legacyConnection.CreateCommand();
        command.CommandText = "SELECT id, ts, kind, process, window_title, url FROM activity_events ORDER BY id;";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new LegacyRow(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return rows;
    }

    private static void InsertRows(SqliteConnection activityConnection, List<LegacyRow> rows)
    {
        using var transaction = activityConnection.BeginTransaction();
        using var command = activityConnection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO activity_events (id, ts, kind, process, window_title, url)
            VALUES ($id, $ts, $kind, $process, $windowTitle, $url);
            """;

        var idParam = command.Parameters.Add("$id", SqliteType.Integer);
        var tsParam = command.Parameters.Add("$ts", SqliteType.Text);
        var kindParam = command.Parameters.Add("$kind", SqliteType.Text);
        var processParam = command.Parameters.Add("$process", SqliteType.Text);
        var windowTitleParam = command.Parameters.Add("$windowTitle", SqliteType.Text);
        var urlParam = command.Parameters.Add("$url", SqliteType.Text);

        foreach (var row in rows)
        {
            idParam.Value = row.Id;
            tsParam.Value = row.Ts;
            kindParam.Value = row.Kind;
            processParam.Value = (object?)row.Process ?? DBNull.Value;
            windowTitleParam.Value = (object?)row.WindowTitle ?? DBNull.Value;
            urlParam.Value = (object?)row.Url ?? DBNull.Value;
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private static void VerifyTransfer(SqliteConnection legacyConnection, SqliteConnection activityConnection)
    {
        var legacyStats = GetStats(legacyConnection);
        var activityStats = GetStats(activityConnection);

        if (legacyStats != activityStats)
        {
            throw new InvalidOperationException(
                $"Vérification de la migration échouée : source={legacyStats.Count} lignes [{legacyStats.Min}, {legacyStats.Max}], "
                + $"cible={activityStats.Count} lignes [{activityStats.Min}, {activityStats.Max}].");
        }
    }

    private static ActivityEventStats GetStats(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*), MIN(ts), MAX(ts) FROM activity_events;";
        using var reader = command.ExecuteReader();
        reader.Read();
        return new ActivityEventStats(
            reader.GetInt64(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    private void RenameLegacyDatabase()
    {
        var migratedPath = $"{_legacyDatabasePath}.migrated";
        if (File.Exists(migratedPath))
        {
            migratedPath = $"{_legacyDatabasePath}.migrated-{DateTime.UtcNow:yyyyMMddTHHmmssZ}";
        }

        File.Move(_legacyDatabasePath, migratedPath);
    }

    private void DeletePartialTarget()
    {
        SqliteConnection.ClearAllPools();

        foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
        {
            var path = _activityDatabasePath + suffix;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private readonly record struct LegacyRow(long Id, string Ts, string Kind, string? Process, string? WindowTitle, string? Url);

    private readonly record struct ActivityEventStats(long Count, string? Min, string? Max);
}
