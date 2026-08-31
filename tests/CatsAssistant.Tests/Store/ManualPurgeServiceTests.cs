using CatsAssistant.Store;
using Microsoft.Data.Sqlite;

namespace CatsAssistant.Tests.Store;

public class ManualPurgeServiceTests
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

    private static readonly Rule SampleRule = new(
        RuleMatcherKind.TitleRegex,
        @"ULISTROIS[-/](\d+)",
        "ULISTROIS-<n>",
        1,
        RuleOrigin.Manual);

    [Fact]
    public void Preview_ReportsExactlyWhatPurgeWillDelete()
    {
        using var fixture = Fixture.Seed();
        var service = new ManualPurgeService(fixture.Events, fixture.TimeBlocks, fixture.Rules);

        var preview = service.Preview();
        var result = service.Purge();

        Assert.Equal(preview, result);
    }

    [Fact]
    public void Purge_DeletesAllActivityEvents()
    {
        using var fixture = Fixture.Seed();
        var service = new ManualPurgeService(fixture.Events, fixture.TimeBlocks, fixture.Rules);

        service.Purge();

        Assert.Equal(0, fixture.Events.Count());
    }

    [Fact]
    public void Purge_DeletesUnsubmittedTimeBlocksButKeepsSubmittedAndTheirCounter()
    {
        using var fixture = Fixture.Seed();
        var submittedId = fixture.TimeBlocks.Insert(SampleTimeBlock with { Status = TimeBlockStatus.Submitted, SapCounter = "000123" });
        var service = new ManualPurgeService(fixture.Events, fixture.TimeBlocks, fixture.Rules);

        service.Purge();

        var remaining = fixture.TimeBlocks.GetByDateRange(DateOnly.MinValue, DateOnly.MaxValue);
        var stored = Assert.Single(remaining);
        Assert.Equal(submittedId, stored.Id);
        Assert.Equal("000123", stored.TimeBlock.SapCounter);
    }

    [Fact]
    public void Purge_DeletesLearnedRulesButKeepsManualRules()
    {
        using var fixture = Fixture.Seed();
        var manualId = fixture.Rules.Insert(SampleRule with { Origin = RuleOrigin.Manual });
        var service = new ManualPurgeService(fixture.Events, fixture.TimeBlocks, fixture.Rules);

        service.Purge();

        var stored = Assert.Single(fixture.Rules.GetAll());
        Assert.Equal(manualId, stored.Id);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly SqliteConnection _activityConnection;
        private readonly SqliteConnection _businessConnection;

        public required IActivityEventRepository Events { get; init; }

        public required ITimeBlockRepository TimeBlocks { get; init; }

        public required IRuleRepository Rules { get; init; }

        private Fixture(SqliteConnection activityConnection, SqliteConnection businessConnection)
        {
            _activityConnection = activityConnection;
            _businessConnection = businessConnection;
        }

        public static Fixture Seed()
        {
            var activityConnection = new SqliteConnection("DataSource=:memory:");
            activityConnection.Open();
            new SqliteMigrator().Migrate(activityConnection);
            var events = new SqliteActivityEventRepository(activityConnection);
            events.Insert(DateTime.UtcNow, ActivityEventKind.Foreground, "a.exe", "a", null);

            var businessConnection = new SqliteConnection("DataSource=:memory:");
            businessConnection.Open();
            new SqliteMigrator(SqliteMigrator.BusinessMigrations).Migrate(businessConnection);
            var timeBlocks = new SqliteTimeBlockRepository(businessConnection);
            timeBlocks.Insert(SampleTimeBlock);
            var rules = new SqliteRuleRepository(businessConnection);
            rules.Insert(SampleRule with { Origin = RuleOrigin.Learned });

            return new Fixture(activityConnection, businessConnection)
            {
                Events = events,
                TimeBlocks = timeBlocks,
                Rules = rules,
            };
        }

        public void Dispose()
        {
            _activityConnection.Dispose();
            _businessConnection.Dispose();
        }
    }
}
