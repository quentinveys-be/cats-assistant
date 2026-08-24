using CatsAssistant.Connectors;

namespace CatsAssistant.Store;

public sealed record JiraTicketRow(JiraTicket Ticket, DateTime LastSyncUtc);
