namespace CatsAssistant.Secrets;

/// <summary>
/// Abstraction du challenge-response HMAC-SHA1 YubiKey (interop matérielle), pour permettre au coffre
/// d'être testé sans YubiKey physique (aucun appel réseau ni matériel dans les tests, cf. CLAUDE.md).
/// </summary>
public interface IYubiKeyChallengeResponseClient
{
    bool IsPresent();

    /// <summary>
    /// Envoie <paramref name="challenge"/> (64 octets) à la YubiKey et retourne la réponse HMAC-SHA1 brute (20 octets).
    /// </summary>
    byte[] CalculateHmacSha1Response(byte[] challenge);
}
