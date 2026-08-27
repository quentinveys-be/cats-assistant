using CatsAssistant.Connectors;
using CatsAssistant.Correlator;
using CatsAssistant.Store;

namespace CatsAssistant.Tests.Correlator;

public class RuleEvaluatorTests
{
    private static readonly DateTimeOffset Day = new(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Evaluate_NoRules_ReturnsNullTarget()
    {
        var evaluation = RuleEvaluator.Evaluate(
            Array.Empty<ActivitySegment>(),
            Array.Empty<string>(),
            Array.Empty<VcsCommit>(),
            Array.Empty<RuleRow>());

        Assert.Null(evaluation.Target);
        Assert.Empty(evaluation.InvalidRuleWarnings);
    }

    [Fact]
    public void Evaluate_ProcessMatcher_MatchesCaseInsensitively()
    {
        var segments = new[] { new ActivitySegment(default, default, "OUTLOOK.EXE", null) };
        var rules = new[] { new RuleRow(1, new Rule(RuleMatcherKind.Process, "outlook.exe", "ULISTROIS-1", 10, RuleOrigin.Manual)) };

        var evaluation = RuleEvaluator.Evaluate(segments, Array.Empty<string>(), Array.Empty<VcsCommit>(), rules);

        Assert.Equal("ULISTROIS-1", evaluation.Target);
    }

    [Fact]
    public void Evaluate_UrlRegexMatcher_MatchesAgainstEventUrls()
    {
        var rules = new[] { new RuleRow(1, new Rule(RuleMatcherKind.UrlRegex, @"confluence\.uliege\.be", "ULISTROIS-2", 10, RuleOrigin.Learned)) };

        var evaluation = RuleEvaluator.Evaluate(
            Array.Empty<ActivitySegment>(),
            new[] { "https://confluence.uliege.be/pages/123" },
            Array.Empty<VcsCommit>(),
            rules);

        Assert.Equal("ULISTROIS-2", evaluation.Target);
    }

    [Fact]
    public void Evaluate_JiraProjectMatcher_MatchesViaWindowTitleOrBranch()
    {
        var commits = new[] { new VcsCommit("sha", Day, "repo", "ULISTROIS/general", "wip", null) };
        var rules = new[] { new RuleRow(1, new Rule(RuleMatcherKind.JiraProject, "ULISTROIS", "ULISTROIS-3", 10, RuleOrigin.Manual)) };

        var evaluation = RuleEvaluator.Evaluate(Array.Empty<ActivitySegment>(), Array.Empty<string>(), commits, rules);

        Assert.Equal("ULISTROIS-3", evaluation.Target);
    }

    [Fact]
    public void Evaluate_InvalidTitleRegex_SkipsRuleAndFallsThroughToNextOne()
    {
        var segments = new[] { new ActivitySegment(default, default, null, "peu importe") };
        var rules = new[]
        {
            new RuleRow(1, new Rule(RuleMatcherKind.TitleRegex, "(unclosed[", "ULISTROIS-4", 10, RuleOrigin.Manual)),
            new RuleRow(2, new Rule(RuleMatcherKind.TitleRegex, "peu importe", "ULISTROIS-5", 20, RuleOrigin.Manual)),
        };

        var evaluation = RuleEvaluator.Evaluate(segments, Array.Empty<string>(), Array.Empty<VcsCommit>(), rules);

        Assert.Equal("ULISTROIS-5", evaluation.Target);
        Assert.Single(evaluation.InvalidRuleWarnings);
    }

    [Fact]
    public void Evaluate_NoMatchingRule_ReturnsNullTargetWithoutWarning()
    {
        var segments = new[] { new ActivitySegment(default, default, "chrome.exe", "Gmail") };
        var rules = new[] { new RuleRow(1, new Rule(RuleMatcherKind.Process, "idea64.exe", "ULISTROIS-6", 10, RuleOrigin.Manual)) };

        var evaluation = RuleEvaluator.Evaluate(segments, Array.Empty<string>(), Array.Empty<VcsCommit>(), rules);

        Assert.Null(evaluation.Target);
        Assert.Empty(evaluation.InvalidRuleWarnings);
    }

    [Fact]
    public void Evaluate_RulesOutOfOrder_StillHonorsPriorityAscending()
    {
        var segments = new[] { new ActivitySegment(default, default, "idea64.exe", null) };
        var rules = new[]
        {
            new RuleRow(1, new Rule(RuleMatcherKind.Process, "idea64.exe", "ULISTROIS-LOW", 50, RuleOrigin.Manual)),
            new RuleRow(2, new Rule(RuleMatcherKind.Process, "idea64.exe", "ULISTROIS-HIGH", 5, RuleOrigin.Manual)),
        };

        var evaluation = RuleEvaluator.Evaluate(segments, Array.Empty<string>(), Array.Empty<VcsCommit>(), rules);

        Assert.Equal("ULISTROIS-HIGH", evaluation.Target);
    }
}
