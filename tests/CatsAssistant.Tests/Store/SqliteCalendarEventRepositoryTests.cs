using CatsAssistant.Connectors;
using CatsAssistant.Store;
using Microsoft.Data.Sqlite;

namespace CatsAssistant.Tests.Store;

public class SqliteCalendarEventRepositoryTests
{
    [Fact]
    public void Insert_ThenGetByDateRange_ReturnsInsertedEvent()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteCalendarEventRepository(connection);
        var calendarEvent = new CalendarEventData(
            new DateTime(2026, 8, 13, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 13, 9, 30, 0, DateTimeKind.Utc),
            "Daily",
            "Alice Dupont");

        var id = repository.Insert(calendarEvent);

        Assert.True(id > 0);
        var events = repository.GetByDateRange(
            new DateTime(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 14, 0, 0, 0, DateTimeKind.Utc));
        var stored = Assert.Single(events);
        Assert.Equal(calendarEvent, stored);
    }

    [Fact]
    public void GetByDateRange_ExcludesEventsOutsideRange()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteCalendarEventRepository(connection);

        repository.Insert(new CalendarEventData(new DateTime(2026, 8, 12, 23, 59, 0, DateTimeKind.Utc), new DateTime(2026, 8, 12, 23, 59, 0, DateTimeKind.Utc), "before", null));
        repository.Insert(new CalendarEventData(new DateTime(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc), new DateTime(2026, 8, 13, 12, 30, 0, DateTimeKind.Utc), "inside", null));
        repository.Insert(new CalendarEventData(new DateTime(2026, 8, 14, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 8, 14, 0, 30, 0, DateTimeKind.Utc), "after", null));

        var events = repository.GetByDateRange(
            new DateTime(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 14, 0, 0, 0, DateTimeKind.Utc));

        var stored = Assert.Single(events);
        Assert.Equal("inside", stored.Subject);
    }

    [Fact]
    public void Insert_NullOrganizer_PersistsAsNull()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteCalendarEventRepository(connection);

        repository.Insert(new CalendarEventData(
            new DateTime(2026, 8, 13, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 13, 9, 30, 0, DateTimeKind.Utc),
            "Sans organisateur",
            null));

        var events = repository.GetByDateRange(DateTime.MinValue, DateTime.MaxValue);
        var stored = Assert.Single(events);
        Assert.Null(stored.Organizer);
    }

    [Fact]
    public void GetByDateRange_ReturnsResultsOrderedByStart()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteCalendarEventRepository(connection);

        repository.Insert(new CalendarEventData(new DateTime(2026, 8, 13, 15, 0, 0, DateTimeKind.Utc), new DateTime(2026, 8, 13, 15, 30, 0, DateTimeKind.Utc), "Aprem", null));
        repository.Insert(new CalendarEventData(new DateTime(2026, 8, 13, 9, 0, 0, DateTimeKind.Utc), new DateTime(2026, 8, 13, 9, 30, 0, DateTimeKind.Utc), "Matin", null));

        var events = repository.GetByDateRange(DateTime.MinValue, DateTime.MaxValue);

        Assert.Equal(new[] { "Matin", "Aprem" }, events.Select(e => e.Subject));
    }

    private static SqliteConnection OpenMigratedConnection()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        new SqliteMigrator(SqliteMigrator.BusinessMigrations).Migrate(connection);
        return connection;
    }
}
