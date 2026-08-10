namespace CatsAssistant.Store;

public sealed record ActivityEvent(
    long Id,
    DateTime TimestampUtc,
    ActivityEventKind Kind,
    string? Process,
    string? WindowTitle,
    string? Url);
