namespace CatsAssistant.Connectors;

public sealed record VcsCommit(
    string Sha,
    DateTimeOffset TimestampUtc,
    string Repo,
    string Branch,
    string Message,
    string? JiraKey);
