using CatsAssistant.Connectors;

namespace CatsAssistant.Tests.App;

/// <summary>Aucun appel réseau (CLAUDE.md) : le résultat de chaque appel dépend uniquement du n° d'appel.</summary>
internal sealed class FakeJiraConnector : IJiraConnector
{
    private readonly Func<int, IReadOnlyList<JiraTicket>> _resultFactory;

    public FakeJiraConnector(Func<int, IReadOnlyList<JiraTicket>> resultFactory)
    {
        _resultFactory = resultFactory;
    }

    public FakeJiraConnector(IReadOnlyList<JiraTicket> result)
        : this(_ => result)
    {
    }

    public int CallCount { get; private set; }

    public Task<IReadOnlyList<JiraTicket>> FetchAssignedTicketsAsync(CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(_resultFactory(CallCount));
    }
}

/// <summary>Ne se résout jamais avant <see cref="Release"/> — pour tester le no-op sur synchro concurrente.</summary>
internal sealed class GatedJiraConnector : IJiraConnector
{
    private readonly TaskCompletionSource<IReadOnlyList<JiraTicket>> _gate = new();

    public int CallCount { get; private set; }

    public void Release(IReadOnlyList<JiraTicket> result) => _gate.SetResult(result);

    public Task<IReadOnlyList<JiraTicket>> FetchAssignedTicketsAsync(CancellationToken cancellationToken = default)
    {
        CallCount++;
        return _gate.Task;
    }
}
