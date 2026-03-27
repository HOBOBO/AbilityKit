namespace UnityHFSM
{
    /// <summary>
    /// Timer interface for tracking elapsed time.
    /// Implementations can use Unity's Time.time or System.Diagnostics.Stopwatch.
    /// </summary>
    public interface ITimer
    {
        /// <summary>
        /// Gets the elapsed time in seconds.
        /// </summary>
        float Elapsed { get; }

        /// <summary>
        /// Resets the timer.
        /// </summary>
        void Reset();
    }
}
