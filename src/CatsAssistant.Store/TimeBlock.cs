namespace CatsAssistant.Store;

public sealed record TimeBlock(
    DateOnly Date,
    DateTime StartUtc,
    DateTime EndUtc,
    string SourceSummary,
    string? JiraKey,
    string Posid,
    string Zwpid,
    string Note,
    double DurationHours,
    TimeBlockStatus Status,
    string? SapCounter);
