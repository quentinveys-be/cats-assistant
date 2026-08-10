using CatsAssistant.Store;
using Microsoft.Data.Sqlite;

namespace CatsAssistant.Tests.Store;

public class ActivityEventRetentionPurgerTests
{
    [Fact]
    public void Purge_UsesDefaultNinetyDayRetention()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteActivityEventRepository(connection);
        var purger = new ActivityEventRetentionPurger(repository);

        var now = new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);
        repository.Insert(now.AddDays(-91), ActivityEventKind.Foreground, "old.exe", "old", null);
        repository.Insert(now.AddDays(-1), ActivityEventKind.Foreground, "recent.exe", "recent", null);

        var deleted = purger.Purge(now);

        Assert.Equal(1, deleted);
        var remaining = repository.GetByDateRange(DateTime.MinValue, DateTime.MaxValue);
        var stored = Assert.Single(remaining);
        Assert.Equal("recent.exe", stored.Process);
    }

    [Fact]
    public void Purge_HonorsCustomRetention()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteActivityEventRepository(connection);
        var purger = new ActivityEventRetentionPurger(repository, TimeSpan.FromDays(7));

        var now = new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);
        repository.Insert(now.AddDays(-8), ActivityEventKind.Foreground, "old.exe", "old", null);
        repository.Insert(now.AddDays(-1), ActivityEventKind.Foreground, "recent.exe", "recent", null);

        var deleted = purger.Purge(now);

        Assert.Equal(1, deleted);
    }

    private static SqliteConnection OpenMigratedConnection()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        new SqliteMigrator().Migrate(connection);
        return connection;
    }
}
