using CatsAssistant.App.ViewModels;
using CatsAssistant.Store;
using Microsoft.Data.Sqlite;

namespace CatsAssistant.Tests.App;

public class DataSettingsViewModelTests
{
    [Fact]
    public void Constructor_WithNoSettings_UsesNinetyDayDefault()
    {
        var viewModel = new DataSettingsViewModel();

        Assert.Equal(90, viewModel.RetentionDays);
        Assert.Equal(0, viewModel.EventCount);
        Assert.False(viewModel.CanPurge);
    }

    [Fact]
    public void SelectRetentionCommand_PersistsAndPurgesImmediately()
    {
        using var connection = OpenMigratedConnection();
        var events = new SqliteActivityEventRepository(connection);
        events.Insert(DateTime.UtcNow.AddDays(-100), ActivityEventKind.Foreground, "old.exe", "old", null);
        events.Insert(DateTime.UtcNow, ActivityEventKind.Foreground, "recent.exe", "recent", null);

        var settings = new FakeSettingsRepository();
        var viewModel = new DataSettingsViewModel(settings, events);

        viewModel.SelectRetentionCommand.Execute("30");

        Assert.Equal(30, viewModel.RetentionDays);
        Assert.Equal("30", settings.Get(DataSettingsViewModel.RetentionDaysKey));
        Assert.Equal(1, events.Count());
        Assert.Equal(1, viewModel.EventCount);
    }

    [Fact]
    public void PurgeService_IsNullWhenBusinessDatabaseUnavailable()
    {
        var viewModel = new DataSettingsViewModel();

        Assert.Null(viewModel.PurgeService);
        Assert.False(viewModel.CanPurge);
    }

    [Fact]
    public void PurgeService_IsExposedWhenProvided()
    {
        using var activityConnection = OpenMigratedConnection();
        var events = new SqliteActivityEventRepository(activityConnection);
        using var businessConnection = new SqliteConnection("DataSource=:memory:");
        businessConnection.Open();
        new SqliteMigrator(SqliteMigrator.BusinessMigrations).Migrate(businessConnection);
        var purgeService = new ManualPurgeService(
            events, new SqliteTimeBlockRepository(businessConnection), new SqliteRuleRepository(businessConnection));

        var viewModel = new DataSettingsViewModel(events: events, purgeService: purgeService);

        Assert.Same(purgeService, viewModel.PurgeService);
        Assert.True(viewModel.CanPurge);
    }

    private sealed class FakeSettingsRepository : ISettingsRepository
    {
        private readonly Dictionary<string, string> _values = [];

        public string? Get(string key) => _values.GetValueOrDefault(key);

        public void Set(string key, string value) => _values[key] = value;
    }

    private static SqliteConnection OpenMigratedConnection()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        new SqliteMigrator().Migrate(connection);
        return connection;
    }
}
