namespace CatsAssistant.Secrets;

/// <summary>
/// Levée quand une opération sur le coffre exige la YubiKey (store/read) et qu'aucune n'est détectée.
/// Décision comportementale (docs/adr/D6-credentials-yubikey.md) : l'appelant (App/Connectors) doit
/// intercepter cette exception pour basculer en mode dégradé sans sync, jamais bloquer l'app entière.
/// </summary>
public sealed class YubiKeyNotPresentException : Exception
{
    public YubiKeyNotPresentException()
        : base("Aucune YubiKey détectée : le coffre de secrets est inaccessible.")
    {
    }
}
