using System;

namespace AbilityKit.Game.Flow
{
    public readonly struct FixedStepBudgetResult
    {
        public FixedStepBudgetResult(
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

        public float AccumulatorSeconds { get; }

        public int Steps { get; }

        public int BacklogSteps { get; }

        public float DroppedSeconds { get; }

        public bool OverBudget { get; }

        public bool InvalidDelta { get; }
    }

    public static class FixedStepBudgetPolicy
    {
        public const int MaxStepsPerUpdate = 5;
        public const int MaxRetainedSteps = 10;
        private const double StepRatioTolerance = 1e-6d;

        public static FixedStepBudgetResult Evaluate(
            float accumulatorSeconds,
            float deltaTime,
            float fixedDeltaSeconds)
        {
            var invalidDelta = !IsFiniteNonNegative(deltaTime);
            var safeDelta = invalidDelta ? 0f : deltaTime;
            var safeAccumulator = IsFiniteNonNegative(accumulatorSeconds)
                ? accumulatorSeconds
                : 0f;
            if (!IsFinitePositive(fixedDeltaSeconds))
            {
                return new FixedStepBudgetResult(
                    safeAccumulator,
                    0,
                    0,
                    0f,
                    false,
                    invalidDelta);
            }

            var accumulated =
                (double)safeAccumulator + safeDelta;
            var maxAccumulator =
                (double)fixedDeltaSeconds * MaxRetainedSteps;
            var droppedSeconds = Math.Max(
                0d,
                accumulated - maxAccumulator);
            accumulated = Math.Min(accumulated, maxAccumulator);

            var stepRatio = accumulated / fixedDeltaSeconds;
            var availableSteps = stepRatio >= MaxRetainedSteps
                ? MaxRetainedSteps
                : (int)Math.Floor(stepRatio + StepRatioTolerance);
            var steps = Math.Min(
                availableSteps,
                MaxStepsPerUpdate);
            var accumulatorAfterSteps = Math.Max(
                0d,
                accumulated - steps * fixedDeltaSeconds);
            var backlogSteps = Math.Max(
                0,
                availableSteps - steps);
            return new FixedStepBudgetResult(
                (float)accumulatorAfterSteps,
                steps,
                backlogSteps,
                (float)Math.Min(float.MaxValue, droppedSeconds),
                availableSteps > MaxStepsPerUpdate ||
                droppedSeconds > 0f,
                invalidDelta);
        }

        internal static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value) &&
                   value > 0f;
        }

        internal static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value) &&
                   value >= 0f;
        }
    }

    public class SessionTickLoopState
    {
        public int LastFrame { get; set; }

        public float AccumulatorSeconds { get; set; }

        public int LastUpdateSteps { get; set; }

        public int BacklogSteps { get; set; }

        public long OverBudgetUpdateCount { get; set; }

        public double DroppedTimeSeconds { get; set; }

        public long InvalidDeltaCount { get; set; }

        public virtual void Reset()
        {
            LastFrame = 0;
            AccumulatorSeconds = 0f;
            LastUpdateSteps = 0;
            BacklogSteps = 0;
            OverBudgetUpdateCount = 0L;
            DroppedTimeSeconds = 0d;
            InvalidDeltaCount = 0L;
        }

        internal void Apply(in FixedStepBudgetResult budget)
        {
            AccumulatorSeconds = budget.AccumulatorSeconds;
            LastUpdateSteps = budget.Steps;
            BacklogSteps = budget.BacklogSteps;
            DroppedTimeSeconds += budget.DroppedSeconds;
            if (budget.OverBudget)
            {
                OverBudgetUpdateCount++;
            }
            if (budget.InvalidDelta)
            {
                InvalidDeltaCount++;
            }
        }
    }

    public interface ISessionTickClock
    {
        double NowSeconds { get; }
    }

    public interface ISessionTickLoopPort
    {
        bool HasSession { get; }

        float FixedDeltaSeconds { get; }

        void TickTransport(float elapsedSeconds);

        void TickSimulationFrame(float fixedDeltaSeconds);

        void TickRemoteDrivenSimulation(float deltaTime);

        void TickConfirmedSimulation(float deltaTime);

        void TickPresentation(float deltaTime);
    }

    public sealed class SessionTickLoopController
    {
        private readonly SessionTickLoopState _state;
        private readonly ISessionTickLoopPort _port;
        private readonly ISessionTickClock _clock;
        private double _lastTransportTimestamp;
        private bool _hasTransportTimestamp;

        public SessionTickLoopController(
            SessionTickLoopState state,
            ISessionTickLoopPort port,
            ISessionTickClock clock)
        {
            _state = state ??
                throw new ArgumentNullException(nameof(state));
            _port = port ??
                throw new ArgumentNullException(nameof(port));
            _clock = clock ??
                throw new ArgumentNullException(nameof(clock));
        }

        public SessionTickLoopState State => _state;

        public void MainTick(float deltaTime)
        {
            if (!_port.HasSession)
            {
                ResetTransportClock();
                return;
            }

            TickTransport();

            var fixedDelta = _port.FixedDeltaSeconds;
            if (!FixedStepBudgetPolicy.IsFinitePositive(fixedDelta))
            {
                return;
            }

            var budget = FixedStepBudgetPolicy.Evaluate(
                _state.AccumulatorSeconds,
                deltaTime,
                fixedDelta);
            _state.Apply(in budget);

            for (var i = 0; i < budget.Steps; i++)
            {
                _port.TickSimulationFrame(fixedDelta);
                _state.LastFrame++;
            }

            var safeDelta = budget.InvalidDelta ? 0f : deltaTime;
            _port.TickRemoteDrivenSimulation(safeDelta);
            _port.TickConfirmedSimulation(safeDelta);
            _port.TickPresentation(safeDelta);
        }

        public BattleSessionTickProjection CreateProjection()
        {
            return BattleSessionTickProjector.Create(
                _state.LastFrame,
                _state.AccumulatorSeconds,
                _port.FixedDeltaSeconds,
                _state.LastUpdateSteps,
                _state.BacklogSteps,
                _state.OverBudgetUpdateCount,
                _state.DroppedTimeSeconds,
                _state.InvalidDeltaCount);
        }

        public void ResetTransportClock()
        {
            _lastTransportTimestamp = 0d;
            _hasTransportTimestamp = false;
        }

        private void TickTransport()
        {
            var timestamp = _clock.NowSeconds;
            var validTimestamp =
                !double.IsNaN(timestamp) &&
                !double.IsInfinity(timestamp);
            var elapsed = _hasTransportTimestamp && validTimestamp
                ? Math.Max(0d, timestamp - _lastTransportTimestamp)
                : 0d;

            if (validTimestamp)
            {
                _lastTransportTimestamp = timestamp;
                _hasTransportTimestamp = true;
            }
            else
            {
                ResetTransportClock();
            }

            var boundedElapsed = Math.Min(
                elapsed,
                float.MaxValue);
            _port.TickTransport((float)boundedElapsed);
        }
    }
}
