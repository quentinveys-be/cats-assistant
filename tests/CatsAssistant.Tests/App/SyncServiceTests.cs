using System.Net.Http;
using CatsAssistant.App;
using CatsAssistant.Collector;
using CatsAssistant.Connectors;
using CatsAssistant.Store;
using Microsoft.Data.Sqlite;

namespace CatsAssistant.Tests.App;

public class SyncServiceTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 8, 24, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SyncAllAsync_AllConnectorsSucceed_PersistsResultsAndMarksSuccess()
    {
        using var connection = OpenMigratedConnection();
        var jiraRepository = new SqliteJiraTicketRepository(connection);
        var vcsRepository = new SqliteVcsCommitRepository(connection);
        var calendarRepository = new SqliteCalendarEventRepository(connection);

        var jira = new FakeJiraConnector([new JiraTicket("ULISTROIS-1", "Résumé", "In Progress", null, null, null, null, null)]);
        var gitLab = new FakeGitLabConnector([new VcsCommit("abc", FixedNow, "42", "main", "fix ULISTROIS-1", "ULISTROIS-1")]);
        var outlook = new FakeOutlookConnector([new CalendarEventData(FixedNow.AddHours(-1).UtcDateTime, FixedNow.UtcDateTime, "Réunion", "Alice")]);

        var service = new SyncService(
            jira, gitLab, outlook,
            jiraRepository, vcsRepository, calendarRepository,
            gitLabTargets: [new GitLabSyncTarget("42", "main")],
            utcNow: () => FixedNow);

        await service.SyncAllAsync();

        Assert.NotNull(jiraRepository.GetByKey("ULISTROIS-1"));
        Assert.Single(vcsRepository.GetByDateRange(FixedNow.AddDays(-1), FixedNow.AddDays(1)));
        Assert.Single(calendarRepository.GetByDateRange(FixedNow.AddDays(-1).UtcDateTime, FixedNow.AddDays(1).UtcDateTime));

        foreach (var connector in new[] { SyncConnector.Jira, SyncConnector.GitLab, SyncConnector.Outlook })
        {
            var state = service.GetState(connector);
            Assert.Equal(SyncStatus.Success, state.Status);
            Assert.Equal(FixedNow, state.LastSyncUtc);
            Assert.Null(state.LastError);
        }
    }

    [Fact]
    public async Task SyncAllAsync_NullConnectors_MarksUnavailableWithoutThrowing()
    {
        using var connection = OpenMigratedConnection();
        var service = new SyncService(
            jiraConnector: null,
            gitLabConnector: null,
            outlookConnector: null,
            new SqliteJiraTicketRepository(connection),
            new SqliteVcsCommitRepository(connection),
            new SqliteCalendarEventRepository(connection));

        await service.SyncAllAsync();

        foreach (var connector in new[] { SyncConnector.Jira, SyncConnector.GitLab, SyncConnector.Outlook })
        {
            Assert.Equal(SyncStatus.Unavailable, service.GetState(connector).Status);
        }
    }

    [Fact]
    public async Task SyncAllAsync_GitLabConnectorWithoutTargets_MarksUnavailable()
    {
        using var connection = OpenMigratedConnection();
        var gitLab = new FakeGitLabConnector([]);

        var service = new SyncService(
            new FakeJiraConnector([]), gitLab, new FakeOutlookConnector([]),
            new SqliteJiraTicketRepository(connection),
            new SqliteVcsCommitRepository(connection),
            new SqliteCalendarEventRepository(connection));

        await service.SyncAllAsync();

        Assert.Equal(SyncStatus.Unavailable, service.GetState(SyncConnector.GitLab).Status);
        Assert.Equal(0, gitLab.CallCount);
    }

    [Fact]
    public async Task SyncAllAsync_TransientNetworkError_RetriesWithBackoffThenSucceeds()
    {
        using var connection = OpenMigratedConnection();
        var jira = new FakeJiraConnector(callNumber => callNumber < 3
            ? throw new HttpRequestException("Panne réseau simulée")
            : [new JiraTicket("ULISTROIS-1", null, null, null, null, null, null, null)]);

        var service = new SyncService(
            jira, gitLabConnector: null, outlookConnector: null,
            new SqliteJiraTicketRepository(connection),
            new SqliteVcsCommitRepository(connection),
            new SqliteCalendarEventRepository(connection),
            maxAttempts: 3,
            backoffFactory: () => new RetryBackoff(TimeSpan.Zero, TimeSpan.Zero));

        await service.SyncAllAsync();

        Assert.Equal(3, jira.CallCount);
        Assert.Equal(SyncStatus.Success, service.GetState(SyncConnector.Jira).Status);
    }

    [Fact]
    public async Task SyncAllAsync_NetworkErrorExhaustsRetries_MarksErrorWithoutThrowingAndRunsOtherConnectors()
    {
        using var connection = OpenMigratedConnection();
        var jira = new FakeJiraConnector(_ => throw new HttpRequestException("Panne réseau simulée"));
        var outlook = new FakeOutlookConnector([new CalendarEventData(FixedNow.AddHours(-1).UtcDateTime, FixedNow.UtcDateTime, "Réunion", null)]);

        var service = new SyncService(
            jira, gitLabConnector: null, outlook,
            new SqliteJiraTicketRepository(connection),
            new SqliteVcsCommitRepository(connection),
            new SqliteCalendarEventRepository(connection),
            maxAttempts: 2,
            backoffFactory: () => new RetryBackoff(TimeSpan.Zero, TimeSpan.Zero),
            utcNow: () => FixedNow);

        await service.SyncAllAsync();

        Assert.Equal(2, jira.CallCount);
        var jiraState = service.GetState(SyncConnector.Jira);
        Assert.Equal(SyncStatus.Error, jiraState.Status);
        Assert.Equal("Panne réseau simulée", jiraState.LastError);

        // Une panne JIRA ne doit jamais bloquer les autres connecteurs.
        Assert.Equal(SyncStatus.Success, service.GetState(SyncConnector.Outlook).Status);
        Assert.Equal(1, outlook.CallCount);
    }

    [Fact]
    public async Task SyncAllAsync_ConfigurationError_FailsImmediatelyWithoutRetrying()
    {
        using var connection = OpenMigratedConnection();
        var jira = new FakeJiraConnector(_ => throw new InvalidOperationException("Aucun token JIRA disponible."));

        var service = new SyncService(
            jira, gitLabConnector: null, outlookConnector: null,
            new SqliteJiraTicketRepository(connection),
            new SqliteVcsCommitRepository(connection),
            new SqliteCalendarEventRepository(connection),
            maxAttempts: 5,
            backoffFactory: () => new RetryBackoff(TimeSpan.Zero, TimeSpan.Zero));

        await service.SyncAllAsync();

        Assert.Equal(1, jira.CallCount);
        Assert.Equal(SyncStatus.Error, service.GetState(SyncConnector.Jira).Status);
    }

    [Fact]
    public async Task SyncAllAsync_CalledAgainWhileRunning_IsNoOp()
    {
        using var connection = OpenMigratedConnection();
        var jira = new GatedJiraConnector();

        var service = new SyncService(
            jira, gitLabConnector: null, outlookConnector: null,
            new SqliteJiraTicketRepository(connection),
            new SqliteVcsCommitRepository(connection),
            new SqliteCalendarEventRepository(connection));

        var firstSync = service.SyncAllAsync();
        SpinWaitUntil(() => jira.CallCount > 0);

        await service.SyncAllAsync();
        Assert.Equal(1, jira.CallCount);

        jira.Release([]);
        await firstSync;
    }

    [Fact]
    public async Task SyncAllAsync_ResyncingSameOutlookWindow_DoesNotDuplicateEvents()
    {
        using var connection = OpenMigratedConnection();
        var calendarRepository = new SqliteCalendarEventRepository(connection);
        var outlook = new FakeOutlookConnector([new CalendarEventData(FixedNow.AddHours(-1).UtcDateTime, FixedNow.UtcDateTime, "Réunion", "Alice")]);

        var service = new SyncService(
            jiraConnector: null, gitLabConnector: null, outlook,
            new SqliteJiraTicketRepository(connection),
            new SqliteVcsCommitRepository(connection),
            calendarRepository,
            utcNow: () => FixedNow);

        await service.SyncAllAsync();
        await service.SyncAllAsync();

        Assert.Equal(2, outlook.CallCount);
        Assert.Single(calendarRepository.GetByDateRange(FixedNow.AddDays(-1).UtcDateTime, FixedNow.AddDays(1).UtcDateTime));
    }

    private static void SpinWaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition jamais atteinte.");
            }

            Thread.Sleep(10);
        }
    }

    private static SqliteConnection OpenMigratedConnection()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        new SqliteMigrator(SqliteMigrator.BusinessMigrations).Migrate(connection);
        return connection;
    }
}
