namespace CatsAssistant.Store;

public sealed record Rule(
    RuleMatcherKind MatcherKind,
    string MatcherValue,
    string Target,
    int Priority,
    RuleOrigin Origin);
