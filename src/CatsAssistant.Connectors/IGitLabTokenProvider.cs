namespace CatsAssistant.Connectors;

public interface IGitLabTokenProvider
{
    Task<string?> GetTokenAsync(CancellationToken cancellationToken = default);
}
