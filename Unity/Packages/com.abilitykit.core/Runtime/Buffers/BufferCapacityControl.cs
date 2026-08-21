namespace AbilityKit.Core.Buffers
{
    /// <summary>
    /// Optional capability exposed by buffers whose retention capacity can change at runtime.
    /// </summary>
    public interface IBufferCapacityControl
    {
        /// <summary>Gets the active retention capacity.</summary>
        int Capacity { get; }

        /// <summary>
        /// Attempts to apply a positive capacity. Implementations may reject runtime changes.
        /// </summary>
        bool TrySetCapacity(int capacity);
    }
}
