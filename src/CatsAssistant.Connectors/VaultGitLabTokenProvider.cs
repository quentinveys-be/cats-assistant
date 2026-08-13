using CatsAssistant.Secrets;

namespace CatsAssistant.Connectors;

/// <summary>
/// Câblage réel de <see cref="IGitLabTokenProvider"/> sur le coffre (ADR D6, étape 2.1). YubiKey absente :
/// dégrade en mode sans sync (retourne null) plutôt que de bloquer l'appelant, comme documenté sur
/// <see cref="YubiKeyNotPresentException"/>. Les autres erreurs du coffre (secret illisible, corrompu)
/// remontent telles quelles : ce ne sont pas des cas de dégradation silencieuse.
/// </summary>
public sealed class VaultGitLabTokenProvider : IGitLabTokenProvider
{
    private readonly ISecretVault _vault;

    public VaultGitLabTokenProvider(ISecretVault vault)
    {
        _vault = vault;
    }

    public Task<string?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return Task.FromResult(_vault.TryRead(SecretName.GitLabPersonalToken));
        }
        catch (YubiKeyNotPresentException)
        {
            return Task.FromResult<string?>(null);
        }
    }
}
