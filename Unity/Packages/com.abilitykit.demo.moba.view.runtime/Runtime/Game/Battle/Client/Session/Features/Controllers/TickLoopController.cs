using System;

namespace AbilityKit.Game.Flow
{
    internal readonly struct FixedStepBudgetResult
    {
        internal FixedStepBudgetResult(
            float accumulatorSeconds,
            int steps,
            int backlogSteps,
            float droppedSeconds,
            bool overBudget,
            bool invalidDelta)
        {
            AccumulatorSeconds = accumulatorSeconds;
            Steps = steps;
            BacklogSteps = backlogSteps;
            DroppedSeconds = droppedSeconds;
            OverBudget = overBudget;
            InvalidDelta = invalidDelta;
        }

        internal float AccumulatorSeconds { get; }
        internal int Steps { get; }
        internal int BacklogSteps { get; }
        internal float DroppedSeconds { get; }
        internal bool OverBudget { get; }
        internal bool InvalidDelta { get; }
    }

    internal static class FixedStepBudgetPolicy
    {
        internal const int MaxStepsPerUpdate = 5;
        internal const int MaxRetainedSteps = 10;
        private const double StepRatioTolerance = 1e-6d;

        internal static FixedStepBudgetResult Evaluate(
            float accumulatorSeconds,
            float deltaTime,
            float fixedDeltaSeconds)
        {
            var invalidDelta = float.IsNaN(deltaTime) ||
                               float.IsInfinity(deltaTime) ||
                               deltaTime < 0f;
            var safeDelta = invalidDelta ? 0f : deltaTime;
            var safeAccumulator = float.IsNaN(accumulatorSeconds) ||
                                  float.IsInfinity(accumulatorSeconds) ||
                                  accumulatorSeconds < 0f
                ? 0f
                : accumulatorSeconds;
            if (fixedDeltaSeconds <= 0f ||
                float.IsNaN(fixedDeltaSeconds) ||
                float.IsInfinity(fixedDeltaSeconds))
            {
                return new FixedStepBudgetResult(
                    safeAccumulator,
                    0,
                    0,
                    0f,
                    false,
                    invalidDelta);
            }

            var accumulated = safeAccumulator + safeDelta;
            var maxAccumulator = fixedDeltaSeconds * MaxRetainedSteps;
            var droppedSeconds = Math.Max(0f, accumulated - maxAccumulator);
            accumulated = Math.Min(accumulated, maxAccumulator);

            var stepRatio = (double)accumulated / fixedDeltaSeconds;
            var availableSteps = Math.Min(
                MaxRetainedSteps,
                (int)Math.Floor(stepRatio + StepRatioTolerance));
            var steps = Math.Min(availableSteps, MaxStepsPerUpdate);
            var accumulatorAfterSteps = Math.Max(
                0f,
                accumulated - steps * fixedDeltaSeconds);
            var backlogSteps = Math.Max(0, availableSteps - steps);
            return new FixedStepBudgetResult(
                accumulatorAfterSteps,
                steps,
                backlogSteps,
                droppedSeconds,
                availableSteps > MaxStepsPerUpdate || droppedSeconds > 0f,
                invalidDelta);
        }
    }

    internal sealed class TickLoopController
    {
        private readonly BattleSessionState _state;
        private readonly BattleSessionHandles _handles;
        private readonly ITickLoopHost _host;

        public TickLoopController(BattleSessionState state, BattleSessionHandles handles, ITickLoopHost host)
        {
            _state = state;
            _handles = handles;
            _host = host;
        }

        public void MainTick(float deltaTime)
        {
            if (!HasSession()) return;

            var fixedDelta = _host.GetFixedDeltaSeconds();
            if (fixedDelta <= 0f) return;

            TickMainSession(deltaTime, fixedDelta);
            TickAuxiliaryWorlds(deltaTime);
        }

        public BattleSessionTickProjection CreateProjection()
        {
            return BattleSessionTickProjector.Create(
                _state.Tick.LastFrame,
                _state.Tick.TickAcc,
                _host.GetFixedDeltaSeconds(),
                _state.Tick.LastUpdateSteps,
                _state.Tick.BacklogSteps,
                _state.Tick.OverBudgetUpdateCount,
                _state.Tick.DroppedTimeSeconds,
                _state.Tick.InvalidDeltaCount);
        }

        private bool HasSession()
        {
            return _handles.Session != null;
        }

        private void TickMainSession(float deltaTime, float fixedDelta)
        {
            var budget = FixedStepBudgetPolicy.Evaluate(
                _state.Tick.TickAcc,
                deltaTime,
                fixedDelta);
            _state.Tick.TickAcc = budget.AccumulatorSeconds;
            _state.Tick.LastUpdateSteps = budget.Steps;
            _state.Tick.BacklogSteps = budget.BacklogSteps;
            _state.Tick.DroppedTimeSeconds += budget.DroppedSeconds;
            if (budget.OverBudget) _state.Tick.OverBudgetUpdateCount++;
            if (budget.InvalidDelta) _state.Tick.InvalidDeltaCount++;

            for (var i = 0; i < budget.Steps; i++)
            {
                TickNextFrame(fixedDelta);
            }
        }

        private void TickNextFrame(float fixedDelta)
        {
            var nextFrame = _state.Tick.LastFrame + 1;
            _handles.Session.Tick(fixedDelta);
            _state.Tick.LastFrame = nextFrame;
        }

        private void TickAuxiliaryWorlds(float deltaTime)
        {
            _host.TickRemoteDrivenLocalSim(deltaTime);
            _host.TickConfirmedAuthorityWorldSim(deltaTime);
            _host.TickRemoteInterpolation(deltaTime);
        }
    }
}
