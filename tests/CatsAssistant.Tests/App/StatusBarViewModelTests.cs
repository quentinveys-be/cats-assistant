using System.Windows.Threading;
using CatsAssistant.App;
using CatsAssistant.App.ViewModels;
using CatsAssistant.Secrets;
using CatsAssistant.Store;
using CatsAssistant.Tests.Secrets;
using Microsoft.Data.Sqlite;

namespace CatsAssistant.Tests.App;

public class StatusBarViewModelTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 8, 24, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_NoSyncService_AllPillsStayUnavailable()
    {
        var viewModel = new StatusBarViewModel(syncService: null);

        Assert.Equal(["SAP", "JIRA", "GitLab", "Outlook", "YubiKey"], viewModel.Pills.Select(p => p.Name));
        Assert.All(viewModel.Pills, p => Assert.Equal(SyncStatus.Unavailable, p.Status));
    }

    [Fact]
    public async Task Constructor_WithAlreadySyncedService_ReflectsInitialState()
    {
        using var connection = OpenMigratedConnection();
        var jira = new FakeJiraConnector([]);
        using var service = new SyncService(
            jira, gitLabConnector: null, outlookConnector: null,
            new SqliteJiraTicketRepository(connection),
            new SqliteVcsCommitRepository(connection),
            new SqliteCalendarEventRepository(connection),
            utcNow: () => FixedNow);
        await service.SyncAllAsync();

        var viewModel = new StatusBarViewModel(service);

        var jiraPill = viewModel.Pills.Single(p => p.Name == "JIRA");
        Assert.Equal(SyncStatus.Success, jiraPill.Status);
        Assert.Contains(FixedNow.ToLocalTime().ToString("HH:mm"), jiraPill.Tooltip);

        var gitLabPill = viewModel.Pills.Single(p => p.Name == "GitLab");
        Assert.Equal(SyncStatus.Unavailable, gitLabPill.Status);
        Assert.Equal("GitLab : non configuré", gitLabPill.Tooltip);
    }

    [Fact]
    public async Task StateChanged_OnSyncService_RefreshesPillsOnUiThread()
    {
        using var connection = OpenMigratedConnection();
        var jira = new FakeJiraConnector([]);
        using var service = new SyncService(
            jira, gitLabConnector: null, outlookConnector: null,
            new SqliteJiraTicketRepository(connection),
            new SqliteVcsCommitRepository(connection),
            new SqliteCalendarEventRepository(connection),
            utcNow: () => FixedNow);

        var viewModel = new StatusBarViewModel(service);
        Assert.Equal(SyncStatus.Idle, viewModel.Pills.Single(p => p.Name == "JIRA").Status);

        await service.SyncAllAsync();
        PumpPendingDispatcherOperations();

        Assert.Equal(SyncStatus.Success, viewModel.Pills.Single(p => p.Name == "JIRA").Status);
    }

    [Fact]
    public void PeriodText_IsNotEmpty()
    {
        var viewModel = new StatusBarViewModel(syncService: null);

        Assert.StartsWith("Semaine du ", viewModel.PeriodText);
    }

    [Fact]
    public async Task Dispose_UnsubscribesFromStateChanged()
    {
        using var connection = OpenMigratedConnection();
        var jira = new FakeJiraConnector([]);
        using var service = new SyncService(
            jira, gitLabConnector: null, outlookConnector: null,
            new SqliteJiraTicketRepository(connection),
            new SqliteVcsCommitRepository(connection),
            new SqliteCalendarEventRepository(connection),
            utcNow: () => FixedNow);

        var viewModel = new StatusBarViewModel(service);
        viewModel.Dispose();

        await service.SyncAllAsync();
        PumpPendingDispatcherOperations();

        Assert.Equal(SyncStatus.Idle, viewModel.Pills.Single(p => p.Name == "JIRA").Status);
    }

    [Fact]
    public void Constructor_NoVaultCoordinator_YubiKeyPillStaysUnavailable()
    {
        var viewModel = new StatusBarViewModel(syncService: null, vaultCoordinator: null);

        Assert.Equal(SyncStatus.Unavailable, viewModel.Pills.Single(p => p.Name == "YubiKey").Status);
    }

    [Fact]
    public void Constructor_WithLockedVaultCoordinator_YubiKeyPillIsError()
    {
        var coordinator = NewCoordinator();

        var viewModel = new StatusBarViewModel(syncService: null, coordinator);

        var pill = viewModel.Pills.Single(p => p.Name == "YubiKey");
        Assert.Equal(SyncStatus.Error, pill.Status);
        Assert.Equal("YubiKey : coffre verrouillé", pill.Tooltip);
    }

    [Fact]
    public void VaultStateChanged_ToUnlocked_RefreshesYubiKeyPillOnUiThread()
    {
        var coordinator = NewCoordinator();
        var viewModel = new StatusBarViewModel(syncService: null, coordinator);

        coordinator.TryUnlock();
        PumpPendingDispatcherOperations();

        var pill = viewModel.Pills.Single(p => p.Name == "YubiKey");
        Assert.Equal(SyncStatus.Success, pill.Status);
        Assert.Equal("YubiKey : coffre déverrouillé", pill.Tooltip);
    }

    [Fact]
    public void VaultStateChanged_ToDegraded_RefreshesYubiKeyPillOnUiThread()
    {
        var coordinator = NewCoordinator();
        var viewModel = new StatusBarViewModel(syncService: null, coordinator);

        coordinator.ContinueWithoutVault();
        PumpPendingDispatcherOperations();

        var pill = viewModel.Pills.Single(p => p.Name == "YubiKey");
        Assert.Equal(SyncStatus.Unavailable, pill.Status);
        Assert.Equal("YubiKey : mode dégradé (sans coffre)", pill.Tooltip);
    }

    [Fact]
    public void Dispose_UnsubscribesFromVaultCoordinator()
    {
        var coordinator = NewCoordinator();
        var viewModel = new StatusBarViewModel(syncService: null, coordinator);
        viewModel.Dispose();

        coordinator.TryUnlock();
        PumpPendingDispatcherOperations();

        Assert.Equal(SyncStatus.Error, viewModel.Pills.Single(p => p.Name == "YubiKey").Status);
    }

    private static YubiKeyVaultCoordinator NewCoordinator()
    {
        var challengeFilePath = Path.Combine(Path.GetTempPath(), $"cats-assistant-statusbar-tests-{Guid.NewGuid():N}.challenge");
        return new YubiKeyVaultCoordinator(new BusinessMasterKeyProvider(challengeFilePath, new FakeYubiKeyChallengeResponseClient()));
    }

    private static void PumpPendingDispatcherOperations()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static SqliteConnection OpenMigratedConnection()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        new SqliteMigrator(SqliteMigrator.BusinessMigrations).Migrate(connection);
        return connection;
    }
}
