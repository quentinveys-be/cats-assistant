namespace CatsAssistant.Connectors;

/// <summary>
/// Abstraction locale en attendant le coffre de secrets de l'étape 2.1 (non encore mergé).
/// L'implémentation réelle (challenge-response YubiKey + DPAPI) sera câblée après coup.
/// </summary>
public interface IJiraTokenProvider
{
    Task<string?> GetTokenAsync(CancellationToken cancellationToken = default);
}
