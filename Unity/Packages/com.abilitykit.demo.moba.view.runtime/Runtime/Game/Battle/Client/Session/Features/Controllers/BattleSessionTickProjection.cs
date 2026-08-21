namespace AbilityKit.Game.Flow
{
    internal readonly struct BattleSessionTickProjection
    {
        internal BattleSessionTickProjection(
            int lastFrame,
            double logicTimeSeconds,
            int lastUpdateSteps,
            int backlogSteps,
            long overBudgetUpdateCount,
            double droppedTimeSeconds,
            long invalidDeltaCount)
        {
            LastFrame = lastFrame;
            LogicTimeSeconds = logicTimeSeconds;
            LastUpdateSteps = lastUpdateSteps;
            BacklogSteps = backlogSteps;
            OverBudgetUpdateCount = overBudgetUpdateCount;
            DroppedTimeSeconds = droppedTimeSeconds;
            InvalidDeltaCount = invalidDeltaCount;
        }

        internal int LastFrame { get; }
        internal double LogicTimeSeconds { get; }
        internal int LastUpdateSteps { get; }
        internal int BacklogSteps { get; }
        internal long OverBudgetUpdateCount { get; }
        internal double DroppedTimeSeconds { get; }
        internal long InvalidDeltaCount { get; }
    }

    internal static class BattleSessionTickProjector
    {
        internal static BattleSessionTickProjection Create(
            int lastFrame,
            float tickAccumulator,
            float fixedDeltaSeconds) =>
            Create(lastFrame, tickAccumulator, fixedDeltaSeconds, 0, 0, 0L, 0d, 0L);

        internal static BattleSessionTickProjection Create(
            int lastFrame,
            float tickAccumulator,
            float fixedDeltaSeconds,
            int lastUpdateSteps,
            int backlogSteps,
            long overBudgetUpdateCount,
            double droppedTimeSeconds,
            long invalidDeltaCount)
        {
            var logicTimeSeconds = fixedDeltaSeconds > 0f
                ? lastFrame * (double)fixedDeltaSeconds + tickAccumulator
                : 0d;
            return new BattleSessionTickProjection(
                lastFrame,
                logicTimeSeconds,
                lastUpdateSteps,
                backlogSteps,
                overBudgetUpdateCount,
                droppedTimeSeconds,
                invalidDeltaCount);
        }
    }
}
