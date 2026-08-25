using System.Net.Http;
using CatsAssistant.Collector;
using CatsAssistant.Connectors;
using CatsAssistant.Store;

namespace CatsAssistant.App;

/// <summary>
/// Orchestre les 3 connecteurs (JIRA, GitLab, Outlook) et persiste leurs résultats dans business.db
/// (docs/phases.md, étape 2.6). Chaque connecteur est optionnel (null = non configuré ou coffre
/// verrouillé) ; l'échec de l'un n'empêche jamais les deux autres de tourner. Les erreurs réseau
/// transitoires sont réessayées avec <see cref="RetryBackoff"/> avant d'être remontées comme état
/// (jamais de crash — docs/adr/D6, mode dégradé).
/// </summary>
public sealed class SyncService : IDisposable
{
    private static readonly TimeSpan DefaultGitLabLookback = TimeSpan.FromDays(30);
    private static readonly TimeSpan DefaultOutlookLookback = TimeSpan.FromDays(7);

    private readonly IJiraConnector? _jiraConnector;
    private readonly IGitLabConnector? _gitLabConnector;
    private readonly IOutlookConnector? _outlookConnector;
    private readonly IJiraTicketRepository _jiraTicketRepository;
    private readonly IVcsCommitRepository _vcsCommitRepository;
    private readonly ICalendarEventRepository _calendarEventRepository;
    private readonly IReadOnlyList<GitLabSyncTarget> _gitLabTargets;
    private readonly TimeSpan _gitLabLookback;
    private readonly TimeSpan _outlookLookback;
    private readonly int _maxAttempts;
    private readonly Func<RetryBackoff> _backoffFactory;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly SemaphoreSlim _syncGate = new(1, 1);
    private readonly object _stateGate = new();

    private readonly Dictionary<SyncConnector, SyncConnectorState> _state = new()
    {
        [SyncConnector.Jira] = new SyncConnectorState(SyncStatus.Idle, null, null),
        [SyncConnector.GitLab] = new SyncConnectorState(SyncStatus.Idle, null, null),
        [SyncConnector.Outlook] = new SyncConnectorState(SyncStatus.Idle, null, null),
    };

    private System.Threading.Timer? _periodicTimer;

    public SyncService(
        IJiraConnector? jiraConnector,
        IGitLabConnector? gitLabConnector,
        IOutlookConnector? outlookConnector,
        IJiraTicketRepository jiraTicketRepository,
        IVcsCommitRepository vcsCommitRepository,
        ICalendarEventRepository calendarEventRepository,
        IReadOnlyList<GitLabSyncTarget>? gitLabTargets = null,
        TimeSpan? gitLabLookback = null,
        TimeSpan? outlookLookback = null,
        int maxAttempts = 3,
        Func<RetryBackoff>? backoffFactory = null,
        Func<DateTimeOffset>? utcNow = null)
    {
        _jiraConnector = jiraConnector;
        _gitLabConnector = gitLabConnector;
        _outlookConnector = outlookConnector;
        _jiraTicketRepository = jiraTicketRepository;
        _vcsCommitRepository = vcsCommitRepository;
        _calendarEventRepository = calendarEventRepository;
        _gitLabTargets = gitLabTargets ?? [];
        _gitLabLookback = gitLabLookback ?? DefaultGitLabLookback;
        _outlookLookback = outlookLookback ?? DefaultOutlookLookback;
        _maxAttempts = maxAttempts;
        _backoffFactory = backoffFactory ?? (() => new RetryBackoff());
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>Levé (thread pool) après chaque connecteur synchronisé, consommé par la future UI de pastilles.</summary>
    public event EventHandler? StateChanged;

    public SyncConnectorState GetState(SyncConnector connector)
    {
        lock (_stateGate)
        {
            return _state[connector];
        }
    }

    /// <summary>
    /// Synchro manuelle (menu tray) ou tick périodique. No-op silencieux si une synchro est déjà en cours,
    /// pour ne jamais bloquer l'UI ni empiler des appels réseau concurrents.
    /// </summary>
    public async Task SyncAllAsync(CancellationToken cancellationToken = default)
    {
        if (!await _syncGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            await SyncJiraAsync(cancellationToken).ConfigureAwait(false);
            await SyncGitLabAsync(cancellationToken).ConfigureAwait(false);
            await SyncOutlookAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _syncGate.Release();
        }
    }

    public void StartPeriodicSync(TimeSpan interval)
    {
        StopPeriodicSync();
        _periodicTimer = new System.Threading.Timer(_ => _ = SyncAllAsync(), null, interval, interval);
    }

    public void StopPeriodicSync()
    {
        _periodicTimer?.Dispose();
        _periodicTimer = null;
    }

    public void Dispose()
    {
        StopPeriodicSync();
        _syncGate.Dispose();
    }

    private async Task SyncJiraAsync(CancellationToken cancellationToken)
    {
        if (_jiraConnector is null)
        {
            SetState(SyncConnector.Jira, SyncStatus.Unavailable, "Connecteur JIRA non configuré.");
            return;
        }

        SetState(SyncConnector.Jira, SyncStatus.Running, null);
        try
        {
            var tickets = await RunWithRetryAsync(
                () => _jiraConnector.FetchAssignedTicketsAsync(cancellationToken), cancellationToken).ConfigureAwait(false);

            // customfield_10047 (Imputations CATS) n'est jamais lu ici : JiraTicket ne l'expose pas
            // (lecture locale uniquement côté connecteur, jamais réémis — CLAUDE.md).
            var now = _utcNow();
            foreach (var ticket in tickets)
            {
                _jiraTicketRepository.Upsert(ticket, now.UtcDateTime);
            }

            SetState(SyncConnector.Jira, SyncStatus.Success, null, now);
        }
        catch (Exception ex)
        {
            SetState(SyncConnector.Jira, SyncStatus.Error, ex.Message);
        }
    }

    private async Task SyncGitLabAsync(CancellationToken cancellationToken)
    {
        if (_gitLabConnector is null || _gitLabTargets.Count == 0)
        {
            SetState(SyncConnector.GitLab, SyncStatus.Unavailable, "Connecteur GitLab non configuré.");
            return;
        }

        SetState(SyncConnector.GitLab, SyncStatus.Running, null);
        try
        {
            var since = _utcNow() - _gitLabLookback;

            foreach (var target in _gitLabTargets)
            {
                var commits = await RunWithRetryAsync(
                    () => _gitLabConnector.GetCommitsAsync(target.ProjectId, target.Branch, since, cancellationToken),
                    cancellationToken).ConfigureAwait(false);

                foreach (var commit in commits)
                {
                    _vcsCommitRepository.Upsert(commit);
                }
            }

            SetState(SyncConnector.GitLab, SyncStatus.Success, null, _utcNow());
        }
        catch (Exception ex)
        {
            SetState(SyncConnector.GitLab, SyncStatus.Error, ex.Message);
        }
    }

    private async Task SyncOutlookAsync(CancellationToken cancellationToken)
    {
        if (_outlookConnector is null)
        {
            SetState(SyncConnector.Outlook, SyncStatus.Unavailable, "Connecteur Outlook non configuré.");
            return;
        }

        SetState(SyncConnector.Outlook, SyncStatus.Running, null);
        try
        {
            var toUtc = _utcNow();
            var fromUtc = toUtc - _outlookLookback;

            var events = await RunWithRetryAsync(
                () => Task.Run(() => _outlookConnector.GetCalendarEvents(fromUtc.UtcDateTime, toUtc.UtcDateTime), cancellationToken),
                cancellationToken).ConfigureAwait(false);

            // ICalendarEventRepository.Insert n'a pas de contrainte d'unicité (docs/data-model.md) : une
            // resynchro de la même fenêtre insérerait des doublons sans cette déduplication applicative.
            var existingKeys = _calendarEventRepository
                .GetByDateRange(fromUtc.UtcDateTime, toUtc.UtcDateTime)
                .Select(EventKey)
                .ToHashSet();

            foreach (var calendarEvent in events)
            {
                if (existingKeys.Add(EventKey(calendarEvent)))
                {
                    _calendarEventRepository.Insert(calendarEvent);
                }
            }

            SetState(SyncConnector.Outlook, SyncStatus.Success, null, toUtc);
        }
        catch (Exception ex)
        {
            SetState(SyncConnector.Outlook, SyncStatus.Error, ex.Message);
        }
    }

    private static (DateTime, DateTime, string, string?) EventKey(CalendarEventData calendarEvent) =>
        (calendarEvent.StartUtc, calendarEvent.EndUtc, calendarEvent.Subject, calendarEvent.Organizer);

    private async Task<T> RunWithRetryAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        var backoff = _backoffFactory();

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await action().ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < _maxAttempts && IsTransient(ex, cancellationToken))
            {
                await Task.Delay(backoff.NextDelay(), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    // Ni les erreurs de configuration (token manquant : InvalidOperationException) ni une annulation
    // demandée par l'appelant ne doivent boucler en retry — seules les pannes réseau transitoires.
    private static bool IsTransient(Exception ex, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested
        && ex is HttpRequestException or TaskCanceledException or TimeoutException or OutlookUnavailableException;

    private void SetState(SyncConnector connector, SyncStatus status, string? error, DateTimeOffset? lastSyncUtc = null)
    {
        lock (_stateGate)
        {
            var previous = _state[connector];
            _state[connector] = new SyncConnectorState(status, lastSyncUtc ?? previous.LastSyncUtc, error);
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
