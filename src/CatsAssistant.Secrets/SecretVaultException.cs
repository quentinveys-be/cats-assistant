namespace CatsAssistant.Secrets;

/// <summary>
/// Levée quand un secret existe sur disque mais ne peut pas être déchiffré : YubiKey différente de celle
/// ayant servi au chiffrement, ou fichier corrompu/altéré. Ne jamais inclure de contenu de secret dans le message.
/// </summary>
public sealed class SecretVaultException : Exception
{
    public SecretVaultException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
