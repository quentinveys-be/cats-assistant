using CatsAssistant.Store;
using Microsoft.Data.Sqlite;

namespace CatsAssistant.Tests.Store;

public class SqliteActivityEventRepositoryTests
{
    [Fact]
    public void Insert_ThenGetByDateRange_ReturnsInsertedEvent()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteActivityEventRepository(connection);

        var timestamp = new DateTime(2026, 8, 10, 9, 30, 0, DateTimeKind.Utc);
        var id = repository.Insert(timestamp, ActivityEventKind.Foreground, "devenv.exe", "CatsAssistant - Visual Studio", null);

        var events = repository.GetByDateRange(
            new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc));

        var stored = Assert.Single(events);
        Assert.Equal(id, stored.Id);
        Assert.Equal(timestamp, stored.TimestampUtc);
        Assert.Equal(ActivityEventKind.Foreground, stored.Kind);
        Assert.Equal("devenv.exe", stored.Process);
        Assert.Equal("CatsAssistant - Visual Studio", stored.WindowTitle);
        Assert.Null(stored.Url);
    }

    [Fact]
    public void GetByDateRange_ExcludesEventsOutsideRange()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteActivityEventRepository(connection);

        repository.Insert(new DateTime(2026, 8, 9, 23, 59, 0, DateTimeKind.Utc), ActivityEventKind.Foreground, "before.exe", "before", null);
        repository.Insert(new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc), ActivityEventKind.Foreground, "inside.exe", "inside", null);
        repository.Insert(new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc), ActivityEventKind.Foreground, "after.exe", "after", null);

        var events = repository.GetByDateRange(
            new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc));

        var stored = Assert.Single(events);
        Assert.Equal("inside.exe", stored.Process);
    }

    [Fact]
    public void Delete_RemovesEvent()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteActivityEventRepository(connection);

        var id = repository.Insert(DateTime.UtcNow, ActivityEventKind.IdleStart, null, null, null);

        repository.Delete(id);

        var events = repository.GetByDateRange(DateTime.MinValue, DateTime.MaxValue);
        Assert.Empty(events);
    }

    [Fact]
    public void DeleteOlderThan_RemovesOnlyEventsBeforeThreshold()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteActivityEventRepository(connection);

        repository.Insert(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), ActivityEventKind.Foreground, "old.exe", "old", null);
        repository.Insert(new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc), ActivityEventKind.Foreground, "recent.exe", "recent", null);

        var deleted = repository.DeleteOlderThan(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(1, deleted);
        var remaining = repository.GetByDateRange(DateTime.MinValue, DateTime.MaxValue);
        var stored = Assert.Single(remaining);
        Assert.Equal("recent.exe", stored.Process);
    }

    private static SqliteConnection OpenMigratedConnection()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        new SqliteMigrator().Migrate(connection);
        return connection;
    }
}
