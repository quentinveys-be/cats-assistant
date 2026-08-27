using CatsAssistant.Connectors;

namespace CatsAssistant.Tests.App;

internal sealed class FakeGitLabConnector : IGitLabConnector
{
    private readonly Func<int, IReadOnlyList<VcsCommit>> _resultFactory;

    public FakeGitLabConnector(Func<int, IReadOnlyList<VcsCommit>> resultFactory)
    {
        _resultFactory = resultFactory;
    }

    public FakeGitLabConnector(IReadOnlyList<VcsCommit> result)
        : this(_ => result)
    {
    }

    public int CallCount { get; private set; }

    public List<string> RequestedProjectIds { get; } = [];

    public Task<IReadOnlyList<GitLabBranch>> GetBranchesAsync(string projectId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Non utilisé par SyncService.");

    public Task<IReadOnlyList<VcsCommit>> GetCommitsAsync(
        string projectId,
        string branch,
        DateTimeOffset sinceUtc,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        RequestedProjectIds.Add(projectId);
        return Task.FromResult(_resultFactory(CallCount));
    }
}
