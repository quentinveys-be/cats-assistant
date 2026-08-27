namespace CatsAssistant.App;

/// <summary>Les 3 connecteurs orchestrés par <see cref="SyncService"/> (docs/phases.md, étape 2.6).</summary>
public enum SyncConnector
{
    Jira,
    GitLab,
    Outlook,
}

public enum SyncStatus
{
    Idle,
    Running,
    Success,
    Error,

    /// <summary>Connecteur non câblé (non configuré) ou coffre verrouillé (YubiKey absente) — pas une erreur réseau.</summary>
    Unavailable,
}

/// <summary>
/// État consommé par la future UI de pastilles de la barre d'état (hors périmètre de cette étape) —
/// exposé ici via <see cref="SyncService.GetState"/>/<see cref="SyncService.StateChanged"/>.
/// </summary>
public sealed record SyncConnectorState(SyncStatus Status, DateTimeOffset? LastSyncUtc, string? LastError);
