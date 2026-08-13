namespace CatsAssistant.Connectors;

public interface IJiraConnector
{
    Task<IReadOnlyList<JiraTicket>> FetchAssignedTicketsAsync(CancellationToken cancellationToken = default);
}
