#nullable enable

using System;

namespace AbilityKit.Game.Battle.Agent
{
    public enum MobaSynchronizationHealthLevel
    {
        Healthy,
        Recovering,
        Degraded,
        Critical
    }

    public readonly struct MobaSynchronizationHealthSample
    {
        public MobaSynchronizationHealthSample(
            bool isRecoveringState,
            int unacknowledgedInputFrames,
            int snapshotFrameLag,
            bool interpolationStarved,
            int bufferedSnapshots,
            long playbackDelayTicks,
            float predictionBacklog,
            bool predictionWindowStalled,
            bool predictionIdealFrameStalled,
            bool replaying,
            long totalRollbacks,
            long totalRollbackRestoreFailures,
            long totalReplayTimeouts,
            long totalReconcileMismatches,
            int maxPredictionAheadFrames,
            int minPredictionWindow,
            float backlogEwmaAlpha)
        {
            IsRecoveringState = isRecoveringState;
            UnacknowledgedInputFrames = Math.Max(0, unacknowledgedInputFrames);
            SnapshotFrameLag = Math.Max(0, snapshotFrameLag);
            InterpolationStarved = interpolationStarved;
            BufferedSnapshots = Math.Max(0, bufferedSnapshots);
            PlaybackDelayTicks = Math.Max(0L, playbackDelayTicks);
            PredictionBacklog = Math.Max(0f, predictionBacklog);
            PredictionWindowStalled = predictionWindowStalled;
            PredictionIdealFrameStalled = predictionIdealFrameStalled;
            Replaying = replaying;
            TotalRollbacks = Math.Max(0L, totalRollbacks);
            TotalRollbackRestoreFailures = Math.Max(0L, totalRollbackRestoreFailures);
            TotalReplayTimeouts = Math.Max(0L, totalReplayTimeouts);
            TotalReconcileMismatches = Math.Max(0L, totalReconcileMismatches);
            MaxPredictionAheadFrames = Math.Max(1, maxPredictionAheadFrames);
            MinPredictionWindow = Math.Max(1, minPredictionWindow);
            BacklogEwmaAlpha = Clamp(backlogEwmaAlpha, 0.01f, 1f);
        }

        public bool IsRecoveringState { get; }
        public int UnacknowledgedInputFrames { get; }
        public int SnapshotFrameLag { get; }
        public bool InterpolationStarved { get; }
        public int BufferedSnapshots { get; }
        public long PlaybackDelayTicks { get; }
        public float PredictionBacklog { get; }
        public bool PredictionWindowStalled { get; }
        public bool PredictionIdealFrameStalled { get; }
        public bool Replaying { get; }
        public long TotalRollbacks { get; }
        public long TotalRollbackRestoreFailures { get; }
        public long TotalReplayTimeouts { get; }
        public long TotalReconcileMismatches { get; }
        public int MaxPredictionAheadFrames { get; }
        public int MinPredictionWindow { get; }
        public float BacklogEwmaAlpha { get; }

        private static float Clamp(float value, float min, float max)
            => value < min ? min : value > max ? max : value;
    }

    public readonly struct MobaPredictionTuningRecommendation
    {
        public MobaPredictionTuningRecommendation(
            bool shouldApply,
            bool resetDefaults,
            int maxPredictionAheadFrames,
            int minPredictionWindow,
            float backlogEwmaAlpha)
        {
            ShouldApply = shouldApply;
            ResetDefaults = resetDefaults;
            MaxPredictionAheadFrames = maxPredictionAheadFrames;
            MinPredictionWindow = minPredictionWindow;
            BacklogEwmaAlpha = backlogEwmaAlpha;
        }

        public bool ShouldApply { get; }
        public bool ResetDefaults { get; }
        public int MaxPredictionAheadFrames { get; }
        public int MinPredictionWindow { get; }
        public float BacklogEwmaAlpha { get; }
    }

    public readonly struct MobaSynchronizationHealthSnapshot
    {
        public MobaSynchronizationHealthSnapshot(
            MobaSynchronizationHealthLevel level,
            int pressureScore,
            int consecutiveUnhealthySamples,
            int consecutiveHealthySamples,
            long rollbackDelta,
            long restoreFailureDelta,
            long replayTimeoutDelta,
            long mismatchDelta,
            MobaPredictionTuningRecommendation tuning)
        {
            Level = level;
            PressureScore = pressureScore;
            ConsecutiveUnhealthySamples = consecutiveUnhealthySamples;
            ConsecutiveHealthySamples = consecutiveHealthySamples;
            RollbackDelta = rollbackDelta;
            RestoreFailureDelta = restoreFailureDelta;
            ReplayTimeoutDelta = replayTimeoutDelta;
            MismatchDelta = mismatchDelta;
            Tuning = tuning;
        }

        public MobaSynchronizationHealthLevel Level { get; }
        public int PressureScore { get; }
        public int ConsecutiveUnhealthySamples { get; }
        public int ConsecutiveHealthySamples { get; }
        public long RollbackDelta { get; }
        public long RestoreFailureDelta { get; }
        public long ReplayTimeoutDelta { get; }
        public long MismatchDelta { get; }
        public MobaPredictionTuningRecommendation Tuning { get; }
    }

    /// <summary>
    /// Aggregates synchronization pressure with state hysteresis and emits tuning only on level transitions.
    /// </summary>
    public sealed class MobaSynchronizationHealthEvaluator
    {
        private const int UnhealthySamplesToDegrade = 3;
        private const int CriticalSamplesToEscalate = 2;
        private const int HealthySamplesToRecover = 4;

        private MobaSynchronizationHealthLevel _level;
        private int _unhealthySamples;
        private int _healthySamples;
        private int _criticalSamples;
        private bool _hasCounters;
        private long _lastRollbacks;
        private long _lastRestoreFailures;
        private long _lastReplayTimeouts;
        private long _lastMismatches;

        public MobaSynchronizationHealthSnapshot Current { get; private set; }

        public MobaSynchronizationHealthSnapshot Evaluate(in MobaSynchronizationHealthSample sample)
        {
            var rollbackDelta = Delta(sample.TotalRollbacks, ref _lastRollbacks);
            var restoreFailureDelta = Delta(sample.TotalRollbackRestoreFailures, ref _lastRestoreFailures);
            var replayTimeoutDelta = Delta(sample.TotalReplayTimeouts, ref _lastReplayTimeouts);
            var mismatchDelta = Delta(sample.TotalReconcileMismatches, ref _lastMismatches);
            _hasCounters = true;

            var pressure = CalculatePressure(in sample, rollbackDelta, restoreFailureDelta, replayTimeoutDelta, mismatchDelta);
            var critical = sample.IsRecoveringState || restoreFailureDelta > 0 || replayTimeoutDelta > 0 || pressure >= 6;
            var unhealthy = critical || pressure >= 2;
            UpdateStreaks(unhealthy, critical);

            var previousLevel = _level;
            if (sample.IsRecoveringState || _criticalSamples >= CriticalSamplesToEscalate)
                _level = MobaSynchronizationHealthLevel.Critical;
            else if (_unhealthySamples >= UnhealthySamplesToDegrade && _level != MobaSynchronizationHealthLevel.Critical)
                _level = MobaSynchronizationHealthLevel.Degraded;
            else if (!unhealthy && (_level == MobaSynchronizationHealthLevel.Degraded || _level == MobaSynchronizationHealthLevel.Critical))
                _level = MobaSynchronizationHealthLevel.Recovering;
            else if (!unhealthy && _level == MobaSynchronizationHealthLevel.Recovering && _healthySamples >= HealthySamplesToRecover)
                _level = MobaSynchronizationHealthLevel.Healthy;

            var tuning = BuildTuning(in sample, previousLevel);
            Current = new MobaSynchronizationHealthSnapshot(
                _level, pressure, _unhealthySamples, _healthySamples,
                rollbackDelta, restoreFailureDelta, replayTimeoutDelta, mismatchDelta, tuning);
            return Current;
        }

        public void Reset()
        {
            _level = MobaSynchronizationHealthLevel.Healthy;
            _unhealthySamples = 0;
            _healthySamples = 0;
            _criticalSamples = 0;
            _hasCounters = false;
            _lastRollbacks = 0;
            _lastRestoreFailures = 0;
            _lastReplayTimeouts = 0;
            _lastMismatches = 0;
            Current = default;
        }

        private void UpdateStreaks(bool unhealthy, bool critical)
        {
            if (unhealthy)
            {
                _unhealthySamples++;
                _healthySamples = 0;
                _criticalSamples = critical ? _criticalSamples + 1 : 0;
                return;
            }

            _unhealthySamples = 0;
            _criticalSamples = 0;
            _healthySamples++;
        }

        private MobaPredictionTuningRecommendation BuildTuning(
            in MobaSynchronizationHealthSample sample,
            MobaSynchronizationHealthLevel previousLevel)
        {
            if (previousLevel == _level)
                return default;

            MobaPredictionTuningRecommendation result;
            if (_level == MobaSynchronizationHealthLevel.Critical)
            {
                result = new MobaPredictionTuningRecommendation(
                    true, false,
                    Math.Max(sample.MaxPredictionAheadFrames, 12),
                    Math.Max(sample.MinPredictionWindow, 4),
                    Math.Max(sample.BacklogEwmaAlpha, 0.35f));
            }
            else if (_level == MobaSynchronizationHealthLevel.Degraded)
            {
                result = new MobaPredictionTuningRecommendation(
                    true, false,
                    Math.Max(sample.MaxPredictionAheadFrames, 8),
                    Math.Max(sample.MinPredictionWindow, 3),
                    Math.Max(sample.BacklogEwmaAlpha, 0.25f));
            }
            else if (_level == MobaSynchronizationHealthLevel.Healthy)
            {
                result = new MobaPredictionTuningRecommendation(true, true, 0, 0, 0f);
            }
            else
            {
                return default;
            }

            return result;
        }

        private static int CalculatePressure(
            in MobaSynchronizationHealthSample sample,
            long rollbackDelta,
            long restoreFailureDelta,
            long replayTimeoutDelta,
            long mismatchDelta)
        {
            var score = 0;
            if (sample.UnacknowledgedInputFrames >= 12) score += 2;
            else if (sample.UnacknowledgedInputFrames >= 6) score++;
            if (sample.SnapshotFrameLag >= 12) score += 2;
            else if (sample.SnapshotFrameLag >= 6) score++;
            if (sample.InterpolationStarved) score += 2;
            if (sample.BufferedSnapshots == 0) score++;
            if (sample.PlaybackDelayTicks >= 12) score++;
            if (sample.PredictionBacklog >= 10f) score += 2;
            else if (sample.PredictionBacklog >= 5f) score++;
            if (sample.PredictionWindowStalled) score++;
            if (sample.PredictionIdealFrameStalled) score++;
            if (sample.Replaying) score++;
            if (rollbackDelta > 0) score++;
            if (mismatchDelta > 0) score++;
            if (restoreFailureDelta > 0) score += 4;
            if (replayTimeoutDelta > 0) score += 4;
            return score;
        }

        private long Delta(long current, ref long previous)
        {
            if (!_hasCounters)
            {
                previous = current;
                return 0L;
            }

            var delta = current >= previous ? current - previous : 0L;
            previous = current;
            return delta;
        }
    }
}
