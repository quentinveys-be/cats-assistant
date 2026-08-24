using CatsAssistant.Store;
using Microsoft.Data.Sqlite;

namespace CatsAssistant.Tests.Store;

public class BusinessSqliteMigratorTests
{
    private const int LatestSchemaVersion = 1;

    private static readonly string[] ExpectedTables =
        { "jira_tickets", "vcs_commits", "calendar_events", "time_blocks", "rules" };

    [Fact]
    public void Migrate_CreatesBusinessTablesOnly()
    {
        using var connection = OpenMigratedConnection();

        Assert.Equal(LatestSchemaVersion, GetCurrentSchemaVersion(connection));

        foreach (var table in ExpectedTables)
        {
            Assert.True(TableExists(connection, table), $"table manquante : {table}");
        }

        Assert.False(TableExists(connection, "activity_events"));
        Assert.False(TableExists(connection, "settings"));
    }

    [Fact]
    public void Migrate_IsIdempotent()
    {
        var migrator = new SqliteMigrator(SqliteMigrator.BusinessMigrations);
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        migrator.Migrate(connection);
        migrator.Migrate(connection);

        Assert.Equal(LatestSchemaVersion, GetCurrentSchemaVersion(connection));
    }

    [Fact]
    public void Migrate_JiraTickets_RejectsNullKey()
    {
        using var connection = OpenMigratedConnection();

        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO jira_tickets (\"key\", last_sync) VALUES (NULL, '2026-08-13T00:00:00Z');";

        Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
    }

    [Fact]
    public void Migrate_VcsCommits_RejectsNullRequiredColumns()
    {
        using var connection = OpenMigratedConnection();

        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO vcs_commits (sha, ts, repo, branch, message) VALUES ('abc', '2026-08-13T00:00:00Z', 'repo', NULL, 'msg');";

        Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
    }

    [Fact]
    public void Migrate_VcsCommits_RejectsDuplicateShaWithinSameRepo()
    {
        using var connection = OpenMigratedConnection();
        InsertCommit(connection, "abc123", "repo");

        Assert.Throws<SqliteException>(() => InsertCommit(connection, "abc123", "repo"));
    }

    [Fact]
    public void Migrate_VcsCommits_AllowsSameShaAcrossDifferentRepos()
    {
        using var connection = OpenMigratedConnection();
        InsertCommit(connection, "abc123", "repo-a");

        InsertCommit(connection, "abc123", "repo-b");
    }

    [Fact]
    public void Migrate_CalendarEvents_RejectsNullSubject()
    {
        using var connection = OpenMigratedConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO calendar_events ("start", "end", subject)
            VALUES ('2026-08-13T09:00:00Z', '2026-08-13T09:30:00Z', NULL);
            """;

        Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
    }

    [Fact]
    public void Migrate_CalendarEvents_AllowsNullOrganizer()
    {
        using var connection = OpenMigratedConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO calendar_events ("start", "end", subject, organizer)
            VALUES ('2026-08-13T09:00:00Z', '2026-08-13T09:30:00Z', 'Daily', NULL);
            """;

        command.ExecuteNonQuery();
    }

    [Fact]
    public void Migrate_TimeBlocks_RejectsNullSourceSummary()
    {
        using var connection = OpenMigratedConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO time_blocks (date, "start", "end", source_summary, posid, zwpid, note, duration_hours, status)
            VALUES ('2026-08-13', '2026-08-13T09:00:00Z', '2026-08-13T09:30:00Z', NULL, 'P.X', 'ZS042', 'note', 0.5, 'proposed');
            """;

        Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
    }

    [Fact]
    public void Migrate_TimeBlocks_AllowsNullJiraKeyAndSapCounter()
    {
        using var connection = OpenMigratedConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO time_blocks (date, "start", "end", source_summary, jira_key, posid, zwpid, note, duration_hours, status, sap_counter)
            VALUES ('2026-08-13', '2026-08-13T09:00:00Z', '2026-08-13T09:30:00Z', 'Résumé', NULL, 'P.X', 'ZS042', 'note', 0.5, 'proposed', NULL);
            """;

        command.ExecuteNonQuery();
    }

    [Fact]
    public void Migrate_Rules_RejectsNullMatcherValue()
    {
        using var connection = OpenMigratedConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO rules (matcher_kind, matcher_value, target, priority, origin)
            VALUES ('process', NULL, 'ULISTROIS-1', 1, 'manual');
            """;

        Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
    }

    private static void InsertCommit(SqliteConnection connection, string sha, string repo)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO vcs_commits (sha, ts, repo, branch, message) VALUES ($sha, '2026-08-13T00:00:00Z', $repo, 'main', 'msg');";
        command.Parameters.AddWithValue("$sha", sha);
        command.Parameters.AddWithValue("$repo", repo);
        command.ExecuteNonQuery();
    }

    private static SqliteConnection OpenMigratedConnection()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        new SqliteMigrator(SqliteMigrator.BusinessMigrations).Migrate(connection);
        return connection;
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
