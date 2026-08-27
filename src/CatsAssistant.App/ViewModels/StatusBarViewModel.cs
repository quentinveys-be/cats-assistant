using System.Windows.Threading;
using CatsAssistant.App.Mvvm;

namespace CatsAssistant.App.ViewModels;

/// <summary>
/// Barre d'état (issue #15) : pastilles SAP/JIRA/GitLab/Outlook/YubiKey + période. SAP et YubiKey n'ont
/// pas encore de service temps réel équivalent à <see cref="SyncService"/> — restent en "Unavailable"
/// statique (hors périmètre de ce shell).
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
    private readonly Dispatcher _dispatcher;

    public StatusBarViewModel(SyncService? syncService)
    {
        _syncService = syncService;
        _dispatcher = Dispatcher.CurrentDispatcher;

        var sap = new ConnectorPillViewModel("SAP");
        var jira = new ConnectorPillViewModel("JIRA");
        var gitLab = new ConnectorPillViewModel("GitLab");
        var outlook = new ConnectorPillViewModel("Outlook");
        var yubiKey = new ConnectorPillViewModel("YubiKey");
        Pills = [sap, jira, gitLab, outlook, yubiKey];

        RefreshSyncPills();

        if (_syncService is not null)
        {
            _syncService.StateChanged += OnSyncServiceStateChanged;
        }
    }

    public IReadOnlyList<ConnectorPillViewModel> Pills { get; }

    public string PeriodText { get; } = FormatCurrentWeek();

    private void OnSyncServiceStateChanged(object? sender, EventArgs e) =>
        _dispatcher.BeginInvoke(RefreshSyncPills);

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
    }
}
