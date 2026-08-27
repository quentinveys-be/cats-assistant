using System.Windows.Threading;
using CatsAssistant.App.Mvvm;

namespace CatsAssistant.App.ViewModels;

/// <summary>
/// Barre d'état (issue #15) : pastilles SAP/JIRA/GitLab/Outlook/YubiKey + période. SAP n'a pas encore de
/// service temps réel équivalent à <see cref="SyncService"/> — reste en "Unavailable" statique (hors
/// périmètre de ce shell). La pastille YubiKey reflète <see cref="YubiKeyVaultCoordinator"/> (issue #26) ;
/// si aucun coordinateur n'est fourni (tests, écrans hors app réelle), elle reste "Unavailable" par défaut.
/// </summary>
public sealed class StatusBarViewModel : ObservableObject, IDisposable
{
    private static readonly (SyncConnector Connector, string Name)[] SyncPills =
    [
        (SyncConnector.Jira, "JIRA"),
        (SyncConnector.GitLab, "GitLab"),
        (SyncConnector.Outlook, "Outlook"),
    ];

    private readonly SyncService? _syncService;
    private readonly YubiKeyVaultCoordinator? _vaultCoordinator;
    private readonly Dispatcher _dispatcher;

    public StatusBarViewModel(SyncService? syncService, YubiKeyVaultCoordinator? vaultCoordinator = null)
    {
        _syncService = syncService;
        _vaultCoordinator = vaultCoordinator;
        _dispatcher = Dispatcher.CurrentDispatcher;

        var sap = new ConnectorPillViewModel("SAP");
        var jira = new ConnectorPillViewModel("JIRA");
        var gitLab = new ConnectorPillViewModel("GitLab");
        var outlook = new ConnectorPillViewModel("Outlook");
        var yubiKey = new ConnectorPillViewModel("YubiKey");
        Pills = [sap, jira, gitLab, outlook, yubiKey];

        RefreshSyncPills();
        RefreshYubiKeyPill();

        if (_syncService is not null)
        {
            _syncService.StateChanged += OnSyncServiceStateChanged;
        }

        if (_vaultCoordinator is not null)
        {
            _vaultCoordinator.StateChanged += OnVaultStateChanged;
        }
    }

    public IReadOnlyList<ConnectorPillViewModel> Pills { get; }

    public string PeriodText { get; } = FormatCurrentWeek();

    private void OnSyncServiceStateChanged(object? sender, EventArgs e) =>
        _dispatcher.BeginInvoke(RefreshSyncPills);

    private void OnVaultStateChanged(object? sender, EventArgs e) =>
        _dispatcher.BeginInvoke(RefreshYubiKeyPill);

    private void RefreshYubiKeyPill()
    {
        if (_vaultCoordinator is null)
        {
            return;
        }

        var (status, tooltip) = _vaultCoordinator.State switch
        {
            YubiKeyVaultState.Unlocked => (SyncStatus.Success, "YubiKey : coffre déverrouillé"),
            YubiKeyVaultState.Degraded => (SyncStatus.Unavailable, "YubiKey : mode dégradé (sans coffre)"),
            _ => (SyncStatus.Error, "YubiKey : coffre verrouillé"),
        };

        Pills.Single(p => p.Name == "YubiKey").Update(status, null, null, tooltip);
    }

    private void RefreshSyncPills()
    {
        if (_syncService is null)
        {
            return;
        }

        foreach (var (connector, name) in SyncPills)
        {
            var state = _syncService.GetState(connector);
            var pill = Pills.Single(p => p.Name == name);
            pill.Update(state.Status, state.LastSyncUtc, state.LastError);
        }
    }

    private static string FormatCurrentWeek()
    {
        var today = DateTime.Now;
        var startOfWeek = today.AddDays(-((int)today.DayOfWeek + 6) % 7);
        return $"Semaine du {startOfWeek:dd/MM}";
    }

    public void Dispose()
    {
        if (_syncService is not null)
        {
            _syncService.StateChanged -= OnSyncServiceStateChanged;
        }

        if (_vaultCoordinator is not null)
        {
            _vaultCoordinator.StateChanged -= OnVaultStateChanged;
        }
    }
}
