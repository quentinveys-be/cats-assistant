namespace CatsAssistant.Correlator;

public sealed record CorrelationResult(
    IReadOnlyList<CorrelatedBlock> Blocks,
    IReadOnlyList<IdlePeriod> IdlePeriods,
    IReadOnlyList<string> RuleWarnings);
