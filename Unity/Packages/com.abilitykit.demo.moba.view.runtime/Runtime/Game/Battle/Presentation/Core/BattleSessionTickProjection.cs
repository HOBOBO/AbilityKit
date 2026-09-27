namespace AbilityKit.Game.Flow
{
    public readonly struct BattleSessionTickProjection
    {
        public BattleSessionTickProjection(
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

        public int LastFrame { get; }
        public double LogicTimeSeconds { get; }
        public int LastUpdateSteps { get; }
        public int BacklogSteps { get; }
        public long OverBudgetUpdateCount { get; }
        public double DroppedTimeSeconds { get; }
        public long InvalidDeltaCount { get; }
    }

    public static class BattleSessionTickProjector
    {
        public static BattleSessionTickProjection Create(
            int lastFrame,
            float tickAccumulator,
            float fixedDeltaSeconds) =>
            Create(
                lastFrame,
                tickAccumulator,
                fixedDeltaSeconds,
                0,
                0,
                0L,
                0d,
                0L);

        public static BattleSessionTickProjection Create(
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
