using CatsAssistant.App.Mvvm;

namespace CatsAssistant.App.ViewModels;

/// <summary>Carte "Connexions" Outlook (issue #24) : reflète l'état de <see cref="SyncService"/> pour ce connecteur, mis à jour par le VM parent (<see cref="ConnectionsViewModel"/>).</summary>
public sealed class OutlookConnectionCardViewModel : ObservableObject
{
    private SyncStatus _status = SyncStatus.Unavailable;

    public void Update(SyncConnectorState state)
    {
        if (SetProperty(ref _status, state.Status, nameof(Status)))
        {
            OnPropertyChanged(nameof(StatusLabel));
        }
    }

    public SyncStatus Status => _status;

    public string StatusLabel => Status switch
    {
        SyncStatus.Success or SyncStatus.Idle or SyncStatus.Running => "connecté",
        SyncStatus.Error => "erreur",
        _ => "non configuré",
    };
}
