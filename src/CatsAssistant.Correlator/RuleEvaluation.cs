namespace CatsAssistant.Correlator;

public sealed record RuleEvaluation(string? Target, IReadOnlyList<string> InvalidRuleWarnings);
