using CatsAssistant.App.Mvvm;

namespace CatsAssistant.App.ViewModels;

/// <summary>Une pastille de connecteur dans la barre d'état (issue #15). La couleur est dérivée de <see cref="Status"/> par la vue (DataTrigger), pas ici.</summary>
public sealed class ConnectorPillViewModel(string name) : ObservableObject
{
    private SyncStatus _status = SyncStatus.Unavailable;
    private string _tooltip = name;

    public string Name { get; } = name;

    public SyncStatus Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string Tooltip
    {
        get => _tooltip;
        private set => SetProperty(ref _tooltip, value);
    }

    public void Update(SyncStatus status, DateTimeOffset? lastSyncUtc, string? lastError, string? tooltipOverride = null)
    {
        Status = status;
        Tooltip = tooltipOverride ?? status switch
        {
            SyncStatus.Unavailable => $"{Name} : non configuré",
            SyncStatus.Running => $"{Name} : synchronisation en cours…",
            SyncStatus.Error => $"{Name} : erreur — {lastError}",
            SyncStatus.Success when lastSyncUtc is not null =>
                $"{Name} : synchronisé à {lastSyncUtc.Value.ToLocalTime():HH:mm}",
            _ => $"{Name} : en attente",
        };
    }
}
