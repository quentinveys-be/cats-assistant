using CatsAssistant.Store;
using Microsoft.Data.Sqlite;

namespace CatsAssistant.Tests.Store;

public class SqliteMigratorTests
{
    private const int LatestSchemaVersion = 2;

    private static readonly string[] ExpectedTables =
    {
        "activity_events",
        "calendar_events",
        "vcs_commits",
        "jira_tickets",
        "time_blocks",
        "rules",
        "settings",
    };

    [Fact]
    public void Migrate_CreatesSchemaVersionAndAllTables()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        new SqliteMigrator().Migrate(connection);

        Assert.Equal(LatestSchemaVersion, GetCurrentSchemaVersion(connection));

        foreach (var table in ExpectedTables)
        {
            Assert.True(TableExists(connection, table), $"table manquante : {table}");
        }
    }

    [Fact]
    public void Migrate_CreatesTimestampIndexOnActivityEvents()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        new SqliteMigrator().Migrate(connection);

        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'ix_activity_events_ts';";
        Assert.Equal(1, Convert.ToInt32(command.ExecuteScalar()));
    }

    [Fact]
    public void Migrate_IsIdempotent()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var migrator = new SqliteMigrator();
        migrator.Migrate(connection);
        migrator.Migrate(connection);

        Assert.Equal(LatestSchemaVersion, GetCurrentSchemaVersion(connection));
    }

    private static int GetCurrentSchemaVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_version;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", tableName);
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }
}
