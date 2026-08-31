using CatsAssistant.App.ViewModels;
using CatsAssistant.Store;
using Microsoft.Data.Sqlite;

namespace CatsAssistant.Tests.App;

public class PurgeConfirmationViewModelTests
{
    [Fact]
    public void PurgeCommand_CannotExecute_UntilExactPhraseTyped()
    {
        using var fixture = Fixture.Create();
        var viewModel = new PurgeConfirmationViewModel(fixture.PurgeService);

        Assert.False(viewModel.PurgeCommand.CanExecute(null));

        viewModel.ConfirmationText = "purger";
        Assert.False(viewModel.PurgeCommand.CanExecute(null));

        viewModel.ConfirmationText = "PURGER";
        Assert.True(viewModel.PurgeCommand.CanExecute(null));
    }

    [Fact]
    public void Preview_MatchesSeededData()
    {
        using var fixture = Fixture.Create();
        var viewModel = new PurgeConfirmationViewModel(fixture.PurgeService);

        Assert.Equal(1, viewModel.Preview.ActivityEvents);
        Assert.Equal(1, viewModel.Preview.UnsubmittedTimeBlocks);
        Assert.Equal(1, viewModel.Preview.LearnedRules);
    }

    [Fact]
    public void PurgeCommand_DeletesDataAndRequestsClose()
    {
        using var fixture = Fixture.Create();
        var viewModel = new PurgeConfirmationViewModel(fixture.PurgeService);
        viewModel.ConfirmationText = "PURGER";
        var closeRequested = false;
        viewModel.RequestClose += (_, _) => closeRequested = true;

        viewModel.PurgeCommand.Execute(null);

        Assert.True(closeRequested);
        Assert.True(viewModel.IsPurged);
        Assert.Equal(0, fixture.Events.Count());
    }

    private sealed class Fixture : IDisposable
    {
        private readonly SqliteConnection _activityConnection;
        private readonly SqliteConnection _businessConnection;

        public required IActivityEventRepository Events { get; init; }

        public required ManualPurgeService PurgeService { get; init; }

        private Fixture(SqliteConnection activityConnection, SqliteConnection businessConnection)
        {
            _activityConnection = activityConnection;
            _businessConnection = businessConnection;
        }

        public static Fixture Create()
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
            timeBlocks.Insert(new TimeBlock(
                new DateOnly(2026, 8, 13),
                new DateTime(2026, 8, 13, 9, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 8, 13, 9, 30, 0, DateTimeKind.Utc),
                "Daily standup", "ULISTROIS-3377", "P.ACSICAT01-01-P-0005", "ZS042", "Correctif", 0.5,
                TimeBlockStatus.Proposed, null));
            var rules = new SqliteRuleRepository(businessConnection);
            rules.Insert(new Rule(RuleMatcherKind.TitleRegex, @"ULISTROIS[-/](\d+)", "ULISTROIS-<n>", 1, RuleOrigin.Learned));

            return new Fixture(activityConnection, businessConnection)
            {
                Events = events,
                PurgeService = new ManualPurgeService(events, timeBlocks, rules),
            };
        }

        public void Dispose()
        {
            _activityConnection.Dispose();
            _businessConnection.Dispose();
        }
    }
}
