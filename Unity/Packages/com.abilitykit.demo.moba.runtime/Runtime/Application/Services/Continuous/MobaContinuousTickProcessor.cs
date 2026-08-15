using System;
using System.Collections.Generic;
using AbilityKit.Continuous;

namespace AbilityKit.Demo.Moba.Services
{
    internal sealed class MobaContinuousTickProcessor : IMobaContinuousTickProcessor
    {
        internal const int MaxIntervalExecutionsPerTick = 32;

        private readonly IReadOnlyList<IMobaContinuousIntervalHandler> _intervalHandlers;

        public MobaContinuousTickProcessor(IReadOnlyList<IMobaContinuousIntervalHandler> intervalHandlers)
        {
            _intervalHandlers = intervalHandlers;
        }

        public void Tick(IContinuous continuous, float deltaTimeSeconds)
        {
            if (continuous == null || !IsFinitePositive(deltaTimeSeconds)) return;
            if (continuous.IsTerminated || !continuous.IsActive || continuous.IsPaused) return;
            if (!(continuous.Config is IMobaContinuousPeriodicConfig periodicConfig)) return;
            if (!(continuous is IMobaContinuousIntervalState intervalState)) return;
            if (periodicConfig.IntervalEffectIds == null || periodicConfig.IntervalEffectIds.Count == 0) return;

            var intervalSeconds = periodicConfig.IntervalSeconds;
            if (!IsFinitePositive(intervalSeconds))
            {
                // Invalid intervals are disabled and normalized instead of ticking every frame.
                intervalState.IntervalRemainingSeconds = 0f;
                return;
            }

            if (!HasIntervalHandler(continuous)) return;

            var remainingSeconds = intervalState.IntervalRemainingSeconds;
            if (float.IsNaN(remainingSeconds) || float.IsInfinity(remainingSeconds))
                remainingSeconds = intervalSeconds;

            remainingSeconds -= deltaTimeSeconds;
            if (remainingSeconds > 0f)
            {
                intervalState.IntervalRemainingSeconds = remainingSeconds;
                return;
            }

            if (!(continuous is IMobaContinuousExecutionContextProvider contextProvider)
                || !contextProvider.TryGetCombatExecutionContext(out var executionContext))
            {
                intervalState.IntervalRemainingSeconds = NormalizeRemainder(remainingSeconds, intervalSeconds);
                return;
            }

            var executionCount = 0;
            while (remainingSeconds <= 0f && executionCount < MaxIntervalExecutionsPerTick)
            {
                remainingSeconds += intervalSeconds;
                intervalState.IntervalRemainingSeconds = remainingSeconds;
                executionCount++;

                DispatchInterval(continuous, periodicConfig, in executionContext);
                if (continuous.IsTerminated || !continuous.IsActive || continuous.IsPaused)
                    break;
            }
        }

        private bool HasIntervalHandler(IContinuous continuous)
        {
            if (_intervalHandlers == null) return false;

            for (var i = 0; i < _intervalHandlers.Count; i++)
            {
                var handler = _intervalHandlers[i];
                if (handler != null && handler.CanHandle(continuous))
                    return true;
            }

            return false;
        }

        private void DispatchInterval(
            IContinuous continuous,
            IMobaContinuousPeriodicConfig periodicConfig,
            in MobaCombatExecutionContext executionContext)
        {
            for (var i = 0; i < _intervalHandlers.Count; i++)
            {
                var handler = _intervalHandlers[i];
                if (handler == null || !handler.CanHandle(continuous)) continue;

                handler.OnInterval(continuous, periodicConfig, in executionContext);
                if (continuous.IsTerminated || !continuous.IsActive || continuous.IsPaused)
                    return;
            }
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float NormalizeRemainder(float remainingSeconds, float intervalSeconds)
        {
            var skippedIntervals = Math.Floor(-(double)remainingSeconds / intervalSeconds) + 1d;
            var normalized = remainingSeconds + skippedIntervals * intervalSeconds;
            if (normalized <= 0d || normalized > intervalSeconds || double.IsNaN(normalized) || double.IsInfinity(normalized))
                return intervalSeconds;

            return (float)normalized;
        }
    }
}
