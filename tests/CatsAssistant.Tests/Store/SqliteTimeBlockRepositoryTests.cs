using CatsAssistant.Store;
using Microsoft.Data.Sqlite;

namespace CatsAssistant.Tests.Store;

public class SqliteTimeBlockRepositoryTests
{
    private static readonly TimeBlock SampleTimeBlock = new(
        new DateOnly(2026, 8, 13),
        new DateTime(2026, 8, 13, 9, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 8, 13, 9, 30, 0, DateTimeKind.Utc),
        "Daily standup",
        "ULISTROIS-3377",
        "P.ACSICAT01-01-P-0005",
        "ZS042",
        "Correctif",
        0.5,
        TimeBlockStatus.Proposed,
        null);

    [Fact]
    public void Insert_ThenGetById_ReturnsStoredTimeBlock()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteTimeBlockRepository(connection);

        var id = repository.Insert(SampleTimeBlock);

        Assert.True(id > 0);
        var stored = repository.GetById(id);
        Assert.NotNull(stored);
        Assert.Equal(SampleTimeBlock, stored!.TimeBlock);
    }

    [Fact]
    public void GetById_UnknownId_ReturnsNull()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteTimeBlockRepository(connection);

        Assert.Null(repository.GetById(999));
    }

    [Fact]
    public void Update_UnknownId_Throws()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteTimeBlockRepository(connection);

        Assert.Throws<KeyNotFoundException>(() => repository.Update(999, SampleTimeBlock));
    }

    [Fact]
    public void Update_OverwritesFieldsAndStatus()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteTimeBlockRepository(connection);
        var id = repository.Insert(SampleTimeBlock);

        var edited = SampleTimeBlock with { Note = "Note corrigée", Status = TimeBlockStatus.Validated };
        repository.Update(id, edited);

        var stored = repository.GetById(id);
        Assert.NotNull(stored);
        Assert.Equal(edited, stored!.TimeBlock);
    }

    [Fact]
    public void Update_SetsSapCounterOnSubmission()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteTimeBlockRepository(connection);
        var id = repository.Insert(SampleTimeBlock);

        var submitted = SampleTimeBlock with { Status = TimeBlockStatus.Submitted, SapCounter = "000123" };
        repository.Update(id, submitted);

        var stored = repository.GetById(id);
        Assert.Equal(TimeBlockStatus.Submitted, stored!.TimeBlock.Status);
        Assert.Equal("000123", stored.TimeBlock.SapCounter);
    }

    [Fact]
    public void Insert_NullJiraKey_PersistsAsNull()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteTimeBlockRepository(connection);

        var id = repository.Insert(SampleTimeBlock with { JiraKey = null });

        var stored = repository.GetById(id);
        Assert.Null(stored!.TimeBlock.JiraKey);
    }

    [Fact]
    public void GetByDateRange_ExcludesTimeBlocksOutsideRange()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteTimeBlockRepository(connection);

        repository.Insert(SampleTimeBlock with { Date = new DateOnly(2026, 8, 12) });
        repository.Insert(SampleTimeBlock with { Date = new DateOnly(2026, 8, 13) });
        repository.Insert(SampleTimeBlock with { Date = new DateOnly(2026, 8, 14) });

        var result = repository.GetByDateRange(new DateOnly(2026, 8, 13), new DateOnly(2026, 8, 13));

        var stored = Assert.Single(result);
        Assert.Equal(new DateOnly(2026, 8, 13), stored.TimeBlock.Date);
    }

    [Fact]
    public void CountUnsubmitted_ExcludesSubmittedBlocks()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteTimeBlockRepository(connection);

        repository.Insert(SampleTimeBlock with { Status = TimeBlockStatus.Proposed });
        repository.Insert(SampleTimeBlock with { Status = TimeBlockStatus.Validated });
        repository.Insert(SampleTimeBlock with { Status = TimeBlockStatus.Submitted, SapCounter = "000123" });

        Assert.Equal(2, repository.CountUnsubmitted());
    }

    [Fact]
    public void DeleteUnsubmitted_KeepsSubmittedBlocksAndTheirCounter()
    {
        using var connection = OpenMigratedConnection();
        var repository = new SqliteTimeBlockRepository(connection);

        repository.Insert(SampleTimeBlock with { Status = TimeBlockStatus.Proposed });
        var submittedId = repository.Insert(SampleTimeBlock with { Status = TimeBlockStatus.Submitted, SapCounter = "000123" });

        var deleted = repository.DeleteUnsubmitted();

        Assert.Equal(1, deleted);
        var remaining = repository.GetByDateRange(DateOnly.MinValue, DateOnly.MaxValue);
        var stored = Assert.Single(remaining);
        Assert.Equal(submittedId, stored.Id);
        Assert.Equal("000123", stored.TimeBlock.SapCounter);
    }

    private static SqliteConnection OpenMigratedConnection()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        new SqliteMigrator(SqliteMigrator.BusinessMigrations).Migrate(connection);
        return connection;
    }
}
