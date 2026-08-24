using CatsAssistant.Connectors;
using CatsAssistant.Store;
using Microsoft.Data.Sqlite;

namespace CatsAssistant.Tests.Store;

public class SqliteVcsCommitRepositoryTests
{
    [Fact]
    public void Upsert_ThenGetByDateRange_ReturnsInsertedCommit()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteVcsCommitRepository(connection);
        var timestamp = new DateTimeOffset(2026, 8, 13, 9, 30, 0, TimeSpan.Zero);
        var commit = new VcsCommit("abc123", timestamp, "cats-assistant", "ULISTROIS/3377", "fix: correctif", "ULISTROIS-3377");

        repository.Upsert(commit);

        var commits = repository.GetByDateRange(
            new DateTimeOffset(2026, 8, 13, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 14, 0, 0, 0, TimeSpan.Zero));

        var stored = Assert.Single(commits);
        Assert.Equal(commit, stored);
    }

    [Fact]
    public void GetByDateRange_ExcludesCommitsOutsideRange()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteVcsCommitRepository(connection);

        repository.Upsert(new VcsCommit("before", new DateTimeOffset(2026, 8, 12, 23, 59, 0, TimeSpan.Zero), "repo", "main", "before", null));
        repository.Upsert(new VcsCommit("inside", new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero), "repo", "main", "inside", null));
        repository.Upsert(new VcsCommit("after", new DateTimeOffset(2026, 8, 14, 0, 0, 0, TimeSpan.Zero), "repo", "main", "after", null));

        var commits = repository.GetByDateRange(
            new DateTimeOffset(2026, 8, 13, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 14, 0, 0, 0, TimeSpan.Zero));

        var stored = Assert.Single(commits);
        Assert.Equal("inside", stored.Sha);
    }

    [Fact]
    public void Upsert_SameSha_OverwritesInsteadOfDuplicating()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteVcsCommitRepository(connection);
        var timestamp = new DateTimeOffset(2026, 8, 13, 9, 0, 0, TimeSpan.Zero);

        repository.Upsert(new VcsCommit("abc123", timestamp, "repo", "main", "message initial", null));
        repository.Upsert(new VcsCommit("abc123", timestamp, "repo", "main", "message corrigé", "ULISTROIS-3377"));

        var commits = repository.GetByDateRange(DateTimeOffset.MinValue, DateTimeOffset.MaxValue);

        var stored = Assert.Single(commits);
        Assert.Equal("message corrigé", stored.Message);
        Assert.Equal("ULISTROIS-3377", stored.JiraKey);
    }

    [Fact]
    public void Upsert_NullJiraKey_PersistsAsNull()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteVcsCommitRepository(connection);

        repository.Upsert(new VcsCommit("abc123", new DateTimeOffset(2026, 8, 13, 9, 0, 0, TimeSpan.Zero), "repo", "main", "message sans ticket", null));

        var commits = repository.GetByDateRange(DateTimeOffset.MinValue, DateTimeOffset.MaxValue);
        var stored = Assert.Single(commits);
        Assert.Null(stored.JiraKey);
    }

    private static SqliteConnection OpenMigratedConnection()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        new SqliteMigrator(SqliteMigrator.BusinessMigrations).Migrate(connection);
        return connection;
    }
}
