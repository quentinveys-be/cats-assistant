using CatsAssistant.Collector;

namespace CatsAssistant.Tests.Collector;

public class IdleDetectorTests
{
    [Fact]
    public void Evaluate_BelowThreshold_ReturnsNoneAndStaysActive()
    {
        var detector = new IdleDetector(TimeSpan.FromMinutes(5));

        var transition = detector.Evaluate(TimeSpan.FromMinutes(4));

        Assert.Equal(IdleTransition.None, transition);
        Assert.False(detector.IsIdle);
    }

    [Fact]
    public void Evaluate_ReachesThreshold_BecomesIdleOnce()
    {
        var detector = new IdleDetector(TimeSpan.FromMinutes(5));

        var first = detector.Evaluate(TimeSpan.FromMinutes(5));
        var second = detector.Evaluate(TimeSpan.FromMinutes(6));

        Assert.Equal(IdleTransition.BecameIdle, first);
        Assert.Equal(IdleTransition.None, second);
        Assert.True(detector.IsIdle);
    }

    [Fact]
    public void Evaluate_InputResumesBelowThreshold_BecomesActiveOnce()
    {
        var detector = new IdleDetector(TimeSpan.FromMinutes(5));
        detector.Evaluate(TimeSpan.FromMinutes(5));

        var transition = detector.Evaluate(TimeSpan.Zero);

        Assert.Equal(IdleTransition.BecameActive, transition);
        Assert.False(detector.IsIdle);
    }

    [Fact]
    public void Evaluate_DefaultThreshold_IsFiveMinutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(5), IdleDetector.DefaultThreshold);
    }
}
