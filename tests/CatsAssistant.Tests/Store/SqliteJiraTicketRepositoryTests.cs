using CatsAssistant.Connectors;
using CatsAssistant.Store;
using Microsoft.Data.Sqlite;

namespace CatsAssistant.Tests.Store;

public class SqliteJiraTicketRepositoryTests
{
    [Fact]
    public void Upsert_ThenGetByKey_ReturnsStoredTicket()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteJiraTicketRepository(connection);
        var ticket = new JiraTicket("ULISTROIS-3377", "Résumé", "In Progress", "Contexte", "ULIS (P.ACSICAT01-01-P-0005 ZS042)", "P.ACSICAT01-01-P-0005", "ZS042", 3.5);
        var lastSync = new DateTime(2026, 8, 13, 9, 0, 0, DateTimeKind.Utc);

        repository.Upsert(ticket, lastSync);
        var stored = repository.GetByKey("ULISTROIS-3377");

        Assert.NotNull(stored);
        Assert.Equal(ticket, stored!.Ticket);
        Assert.Equal(lastSync, stored.LastSyncUtc);
    }

    [Fact]
    public void GetByKey_UnknownKey_ReturnsNull()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteJiraTicketRepository(connection);

        Assert.Null(repository.GetByKey("ULISTROIS-9999"));
    }

    [Fact]
    public void Upsert_ExistingKey_OverwritesFields()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteJiraTicketRepository(connection);
        var firstSync = new DateTime(2026, 8, 12, 9, 0, 0, DateTimeKind.Utc);
        var secondSync = new DateTime(2026, 8, 13, 9, 0, 0, DateTimeKind.Utc);

        repository.Upsert(new JiraTicket("ULISTROIS-3377", "Ancien résumé", "To Do", null, null, null, null, null), firstSync);
        repository.Upsert(new JiraTicket("ULISTROIS-3377", "Nouveau résumé", "Done", null, null, null, null, 5), secondSync);

        var stored = repository.GetByKey("ULISTROIS-3377");

        Assert.NotNull(stored);
        Assert.Equal("Nouveau résumé", stored!.Ticket.Summary);
        Assert.Equal("Done", stored.Ticket.Status);
        Assert.Equal(5, stored.Ticket.Effort);
        Assert.Equal(secondSync, stored.LastSyncUtc);
    }

    [Fact]
    public void GetAll_ReturnsAllTicketsOrderedByKey()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteJiraTicketRepository(connection);
        var sync = new DateTime(2026, 8, 13, 9, 0, 0, DateTimeKind.Utc);

        repository.Upsert(new JiraTicket("ULISTROIS-2", null, null, null, null, null, null, null), sync);
        repository.Upsert(new JiraTicket("ULISTROIS-1", null, null, null, null, null, null, null), sync);

        var all = repository.GetAll();

        Assert.Equal(new[] { "ULISTROIS-1", "ULISTROIS-2" }, all.Select(r => r.Ticket.Key));
    }

    [Fact]
    public void Upsert_NullableFieldsPersistAsNull()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteJiraTicketRepository(connection);
        var sync = new DateTime(2026, 8, 13, 9, 0, 0, DateTimeKind.Utc);

        repository.Upsert(new JiraTicket("ULISTROIS-3377", null, null, null, null, null, null, null), sync);
        var stored = repository.GetByKey("ULISTROIS-3377");

        Assert.NotNull(stored);
        Assert.Null(stored!.Ticket.Summary);
        Assert.Null(stored.Ticket.Status);
        Assert.Null(stored.Ticket.Context);
        Assert.Null(stored.Ticket.ImputationCodeRaw);
        Assert.Null(stored.Ticket.Posid);
        Assert.Null(stored.Ticket.Zwpid);
        Assert.Null(stored.Ticket.Effort);
    }

    private static SqliteConnection OpenMigratedConnection()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        new SqliteMigrator(SqliteMigrator.BusinessMigrations).Migrate(connection);
        return connection;
    }
}
