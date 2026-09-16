public static class StarGuideTimingPolicy
{
    public static bool IsTapVisible(float gameTime, float noteTime,
        bool resolved)
    {
        return !resolved && gameTime < noteTime;
    }

    public static bool IsChainVisible(float gameTime, float finalNodeTime)
    {
        return gameTime < finalNodeTime;
    }
}
