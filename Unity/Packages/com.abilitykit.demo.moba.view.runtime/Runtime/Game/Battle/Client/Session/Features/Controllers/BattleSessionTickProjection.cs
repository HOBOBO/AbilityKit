namespace AbilityKit.Game.Flow
{
    internal readonly struct BattleSessionTickProjection
    {
        internal BattleSessionTickProjection(
            int lastFrame,
            double logicTimeSeconds)
        {
            LastFrame = lastFrame;
            LogicTimeSeconds = logicTimeSeconds;
        }

        internal int LastFrame { get; }

        internal double LogicTimeSeconds { get; }
    }

    internal static class BattleSessionTickProjector
    {
        internal static BattleSessionTickProjection Create(
            int lastFrame,
            float tickAccumulator,
            float fixedDeltaSeconds)
        {
            var logicTimeSeconds = fixedDeltaSeconds > 0f
                ? lastFrame * (double)fixedDeltaSeconds + tickAccumulator
                : 0d;
            return new BattleSessionTickProjection(
                lastFrame,
                logicTimeSeconds);
        }
    }
}
