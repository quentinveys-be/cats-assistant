namespace CatsAssistant.App.ViewModels;

/// <summary>État d'identifiant d'une carte "Connexions" (issue #24) : distinct de <see cref="SyncStatus"/>,
/// qui reflète l'exécution d'une synchro, pas la présence/validité d'un identifiant.</summary>
public enum ConnectionStatus
{
    NotConfigured,
    Connected,
    Expired,
}
