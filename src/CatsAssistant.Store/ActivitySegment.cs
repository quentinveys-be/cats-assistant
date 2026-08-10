namespace CatsAssistant.Store;

public sealed record ActivitySegment(
    DateTime StartUtc,
    DateTime EndUtc,
    string? Process,
    string? WindowTitle);
