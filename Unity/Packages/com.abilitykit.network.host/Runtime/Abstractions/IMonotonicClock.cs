namespace AbilityKit.Network.Host
{
    /// <summary>
    /// Compatibility alias for the shared Core monotonic clock contract.
    /// New cross-package APIs should depend on <see cref="AbilityKit.Core.Timing.IMonotonicClock"/>.
    /// </summary>
    public interface IMonotonicClock : AbilityKit.Core.Timing.IMonotonicClock
    {
    }

    /// <summary>Compatibility wrapper over the shared Core Stopwatch clock.</summary>
    public sealed class StopwatchMonotonicClock : IMonotonicClock
    {
        public static readonly StopwatchMonotonicClock Instance = new StopwatchMonotonicClock();

        private StopwatchMonotonicClock()
        {
        }

        public long Timestamp => AbilityKit.Core.Timing.StopwatchMonotonicClock.Instance.Timestamp;
        public long Frequency => AbilityKit.Core.Timing.StopwatchMonotonicClock.Instance.Frequency;
    }
}
