using CatsAssistant.Connectors;

namespace CatsAssistant.Store;

public interface IJiraTicketRepository
{
    void Upsert(JiraTicket ticket, DateTime lastSyncUtc);

    JiraTicketRow? GetByKey(string key);

    IReadOnlyList<JiraTicketRow> GetAll();
}
