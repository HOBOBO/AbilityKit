using System;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Runtime.Sync;

namespace AbilityKit.Game.Flow
{
    internal readonly struct GatewayTimeSyncRuntimeOptions
    {
        public readonly uint OpCode;
        public readonly int IntervalMs;
        public readonly double Alpha;
        public readonly int TimeoutMs;

        public GatewayTimeSyncRuntimeOptions(uint opCode, int intervalMs, double alpha, int timeoutMs)
        {
            OpCode = opCode;
            IntervalMs = intervalMs;
            Alpha = alpha;
            TimeoutMs = timeoutMs;
        }
    }

    internal readonly struct GatewayTimeSyncSample
    {
        public readonly double RttSeconds;
        public readonly double OffsetSeconds;

        public GatewayTimeSyncSample(double rttSeconds, double offsetSeconds)
        {
            RttSeconds = rttSeconds;
            OffsetSeconds = offsetSeconds;
        }
    }

    internal readonly struct GatewayTimeSyncEwma
    {
        public readonly bool HasClockSync;
        public readonly double ClockOffsetSecondsEwma;
        public readonly double RttSecondsEwma;
        public readonly int Samples;

        public GatewayTimeSyncEwma(bool hasClockSync, double clockOffsetSecondsEwma, double rttSecondsEwma, int samples)
        {
            HasClockSync = hasClockSync;
            ClockOffsetSecondsEwma = clockOffsetSecondsEwma;
            RttSecondsEwma = rttSecondsEwma;
            Samples = samples;
        }
    }

    internal static class GatewayTimeSyncHelper
    {
        public static GatewayTimeSyncRuntimeOptions ResolveRuntimeOptions(in BattleStartPlanTimeSyncOptions timeSync)
        {
            var alpha = timeSync.Alpha;
            if (alpha < 0) alpha = 0;
            if (alpha > 1) alpha = 1;

            var intervalMs = timeSync.IntervalMs;
            if (intervalMs <= 0) intervalMs = 1000;

            var timeoutMs = timeSync.TimeoutMs;
            if (timeoutMs <= 0) timeoutMs = 2000;

            return new GatewayTimeSyncRuntimeOptions(timeSync.OpCode, intervalMs, alpha, timeoutMs);
        }

        public static GatewayTimeSyncSample CalculateSample(
            long clientSendTicks,
            long clientReceiveTicks,
            long serverNowTicks,
            long serverTickFrequency,
            double localTickFrequency)
        {
            ClockSynchronizationSample sample;
            try
            {
                sample = ClockSynchronizationSample.FromRoundTrip(
                    clientSendTicks,
                    clientReceiveTicks,
                    localTickFrequency,
                    serverNowTicks,
                    serverTickFrequency);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                throw new InvalidOperationException("Gateway time sync requires positive local and server tick frequencies.", exception);
            }

            return new GatewayTimeSyncSample(
                sample.RoundTripSeconds,
                sample.LocalMinusServerOffsetSeconds);
        }

        public static GatewayTimeSyncEwma ApplySample(
            bool hasClockSync,
            double currentClockOffsetSecondsEwma,
            double currentRttSecondsEwma,
            int currentSamples,
            in GatewayTimeSyncSample sample,
            double alpha)
        {
            var current = new ClockSynchronizationEstimate(
                hasClockSync,
                currentClockOffsetSecondsEwma,
                currentRttSecondsEwma,
                currentSamples);
            var frameworkSample = new ClockSynchronizationSample(
                sample.RttSeconds,
                sample.OffsetSeconds);
            var estimate = ClockSynchronizationCoordinator.Smooth(
                in current,
                in frameworkSample,
                alpha);
            return new GatewayTimeSyncEwma(
                estimate.HasSample,
                estimate.LocalMinusServerOffsetSeconds,
                estimate.RoundTripSeconds,
                estimate.SampleCount);
        }
    }
}
