using CatsAssistant.Secrets;

namespace CatsAssistant.App;

/// <summary>État UI du coffre métier (issue #26), distinct de la dérivation de clé elle-même (issue business.db).</summary>
public enum YubiKeyVaultState
{
    Locked,
    Unlocked,
    Degraded,
}

/// <summary>
/// Combine <see cref="BusinessMasterKeyProvider"/> (dérivation de clé, hors périmètre de #26) avec l'état
/// UI du dialogue de déverrouillage. Le <see cref="BusinessMasterKeyProvider"/> passé au constructeur doit
/// vivre pour toute la durée du process : sa clé dérivée reste en cache en mémoire, donc réutiliser la même
/// instance entre les appels à <see cref="TryUnlock"/> (démarrage, "Tester la clé", synchro déclenchée
/// coffre verrouillé) garantit un seul appui YubiKey par session.
/// </summary>
public sealed class YubiKeyVaultCoordinator
{
    private readonly BusinessMasterKeyProvider _keyProvider;

    public YubiKeyVaultCoordinator(BusinessMasterKeyProvider keyProvider)
    {
        _keyProvider = keyProvider;
    }

    public YubiKeyVaultState State { get; private set; } = YubiKeyVaultState.Locked;

    public string? CachedKey { get; private set; }

    public bool IsYubiKeyPresent => _keyProvider.IsYubiKeyPresent;

    public event EventHandler? StateChanged;

    /// <summary>
    /// Bloquant : attend le touch physique (ou le timeout/refus du SDK). À appeler hors thread UI.
    /// Retourne false sur clé absente, touch refusé/expiré ou coffre illisible — jamais d'exception.
    /// </summary>
    public bool TryUnlock()
    {
        var key = _keyProvider.TryGetOrDeriveKey();
        if (key is null)
        {
            return false;
        }

        CachedKey = key;
        SetState(YubiKeyVaultState.Unlocked);
        return true;
    }

    public void ContinueWithoutVault() => SetState(YubiKeyVaultState.Degraded);

    private void SetState(YubiKeyVaultState state)
    {
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
