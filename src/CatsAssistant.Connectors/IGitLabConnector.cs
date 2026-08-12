namespace CatsAssistant.Connectors;

public interface IGitLabConnector
{
    Task<IReadOnlyList<GitLabBranch>> GetBranchesAsync(string projectId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VcsCommit>> GetCommitsAsync(
        string projectId,
        string branch,
        DateTimeOffset sinceUtc,
        CancellationToken cancellationToken = default);
}
