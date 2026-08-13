namespace CatsAssistant.Secrets;

/// <summary>
/// Coffre de secrets (ADR D6) : réservé aux tokens JIRA et GitLab. Jamais de credential SAP (D4).
/// </summary>
public interface ISecretVault
{
    /// <summary>
    /// Reflète l'état matériel courant ; à utiliser par l'appelant pour décider entre inviter à reconnecter
    /// la YubiKey ou basculer en mode dégradé sans sync, plutôt que de bloquer l'application (docs/adr/D6).
    /// </summary>
    bool IsYubiKeyPresent { get; }

    /// <summary>Chiffre et persiste <paramref name="secretValue"/>. Lève <see cref="YubiKeyNotPresentException"/> si la YubiKey est absente.</summary>
    void Store(SecretName name, string secretValue);

    /// <summary>
    /// Retourne le secret en clair, ou null si rien n'est stocké pour ce nom.
    /// Lève <see cref="YubiKeyNotPresentException"/> si la YubiKey est absente et qu'un secret existe,
    /// ou <see cref="SecretVaultException"/> si le déchiffrement échoue (YubiKey différente, fichier corrompu).
    /// </summary>
    string? TryRead(SecretName name);

    /// <summary>Supprime le secret s'il existe ; ne lève pas s'il est déjà absent.</summary>
    void Delete(SecretName name);
}
