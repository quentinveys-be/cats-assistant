using CatsAssistant.Store;
using Microsoft.Data.Sqlite;

namespace CatsAssistant.Tests.Store;

public class ActivityDatabaseMigrationTests
{
    private const string Key = "activity-migration-tests-key";

    [Fact]
    public void Run_TransfersAllRowsPreservingIdsAndTimestamps()
    {
        var legacyPath = TempPath("legacy");
        var activityPath = TempPath("activity");

        try
        {
            var inserted = CreateLegacyDatabaseWithRows(legacyPath, rowCount: 5);

            var result = new ActivityDatabaseMigration(legacyPath, activityPath, Key).Run();

            Assert.Equal(ActivityDatabaseMigrationStatus.Migrated, result.Status);
            Assert.Equal(inserted.Count, result.RowsMigrated);

            using var activityConnection = new SqliteConnectionFactory(activityPath, Key).OpenConnection();
            using var command = activityConnection.CreateCommand();
            command.CommandText = "SELECT id, ts, kind, process, window_title, url FROM activity_events ORDER BY id;";
            using var reader = command.ExecuteReader();

            var index = 0;
            while (reader.Read())
            {
                var expected = inserted[index];
                Assert.Equal(expected.Id, reader.GetInt64(0));
                Assert.Equal(expected.Ts, reader.GetString(1));
                index++;
            }

            Assert.Equal(inserted.Count, index);
            Assert.False(File.Exists(legacyPath));
            Assert.True(File.Exists($"{legacyPath}.migrated"));
            Assert.True(Directory.GetFiles(Path.GetTempPath(), $"{Path.GetFileName(legacyPath)}.backup-*").Length >= 1);
        }
        finally
        {
            CleanUp(legacyPath, activityPath);
        }
    }

    [Fact]
    public void Run_IsIdempotent_SecondRunIsNoOp()
    {
        var legacyPath = TempPath("legacy");
        var activityPath = TempPath("activity");

        try
        {
            CreateLegacyDatabaseWithRows(legacyPath, rowCount: 3);

            var first = new ActivityDatabaseMigration(legacyPath, activityPath, Key).Run();
            Assert.Equal(ActivityDatabaseMigrationStatus.Migrated, first.Status);

            var second = new ActivityDatabaseMigration(legacyPath, activityPath, Key).Run();

            Assert.Equal(ActivityDatabaseMigrationStatus.AlreadyMigrated, second.Status);
            Assert.Equal(0, second.RowsMigrated);
        }
        finally
        {
            CleanUp(legacyPath, activityPath);
        }
    }

    [Fact]
    public void Run_NothingToMigrate_WhenLegacyDatabaseDoesNotExist()
    {
        var legacyPath = TempPath("legacy");
        var activityPath = TempPath("activity");

        try
        {
            var result = new ActivityDatabaseMigration(legacyPath, activityPath, Key).Run();

            Assert.Equal(ActivityDatabaseMigrationStatus.NothingToMigrate, result.Status);
            Assert.Equal(0, result.RowsMigrated);
            Assert.False(File.Exists(activityPath));
        }
        finally
        {
            CleanUp(legacyPath, activityPath);
        }
    }

    [Fact]
    public void Run_TargetCannotBeCreated_LeavesSourceIntact()
    {
        var legacyPath = TempPath("legacy");
        var blockingFilePath = TempPath("blocking-file");
        var activityPath = Path.Combine(blockingFilePath, "activity.db");

        try
        {
            CreateLegacyDatabaseWithRows(legacyPath, rowCount: 2);
            File.WriteAllText(blockingFilePath, "cette entrée n'est pas un dossier");

            Assert.ThrowsAny<IOException>(() => new ActivityDatabaseMigration(legacyPath, activityPath, Key).Run());

            Assert.True(File.Exists(legacyPath));
            using var legacyConnection = new SqliteConnection($"Data Source={legacyPath}");
            legacyConnection.Open();
            using var command = legacyConnection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM activity_events;";
            Assert.Equal(2L, (long)command.ExecuteScalar()!);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(legacyPath);
            File.Delete(blockingFilePath);
            foreach (var backup in Directory.GetFiles(Path.GetTempPath(), $"{Path.GetFileName(legacyPath)}.backup-*"))
            {
                File.Delete(backup);
            }
        }
    }

    private static List<(long Id, string Ts)> CreateLegacyDatabaseWithRows(string legacyPath, int rowCount)
    {
        var inserted = new List<(long Id, string Ts)>();

        using (var connection = new SqliteConnectionFactory(legacyPath).OpenConnection())
        {
            var repository = new SqliteActivityEventRepository(connection);
            var baseTimestamp = new DateTime(2026, 8, 1, 8, 0, 0, DateTimeKind.Utc);

            for (var i = 0; i < rowCount; i++)
            {
                var timestamp = baseTimestamp.AddMinutes(i);
                var id = repository.Insert(timestamp, ActivityEventKind.Foreground, "process.exe", "Titre", null);
                inserted.Add((id, timestamp.ToString("o")));
            }
        }

        SqliteConnection.ClearAllPools();
        return inserted;
    }

    private static string TempPath(string label) =>
        Path.Combine(Path.GetTempPath(), $"cats-assistant-tests-{label}-{Guid.NewGuid():N}.db");

    private static void CleanUp(string legacyPath, string activityPath)
    {
        SqliteConnection.ClearAllPools();

        foreach (var path in new[]
                 {
                     legacyPath, $"{legacyPath}.migrated", activityPath,
                 })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        foreach (var backup in Directory.GetFiles(Path.GetTempPath(), $"{Path.GetFileName(legacyPath)}.backup-*"))
        {
            File.Delete(backup);
        }

        foreach (var migratedWithSuffix in Directory.GetFiles(Path.GetTempPath(), $"{Path.GetFileName(legacyPath)}.migrated-*"))
        {
            File.Delete(migratedWithSuffix);
        }
    }
}
