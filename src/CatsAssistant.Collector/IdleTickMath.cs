namespace CatsAssistant.Collector;

public static class IdleTickMath
{
    /// <summary>
    /// GetTickCount wraps around ~49.7 days; unchecked uint subtraction stays correct across the wrap.
    /// </summary>
    public static TimeSpan ComputeIdleDuration(uint currentTickCount, uint lastInputTickCount)
        => TimeSpan.FromMilliseconds(unchecked(currentTickCount - lastInputTickCount));
}
