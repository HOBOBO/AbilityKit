using System;

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

    /// <summary>
    /// Converts an application-defined sample into a desired buffer capacity.
    /// </summary>
    public interface IBufferCapacityPolicy<in TSample>
    {
        /// <summary>Returns a desired capacity for the supplied sample.</summary>
        int GetTargetCapacity(TSample sample, int currentCapacity);
    }

    /// <summary>
    /// Applies policy output to an optional buffer capability with configured bounds.
    /// </summary>
    public sealed class BufferCapacityController<TSample>
    {
        private readonly IBufferCapacityControl _capacityControl;
        private readonly IBufferCapacityPolicy<TSample> _policy;

        /// <summary>Creates a bounded controller for one adjustable buffer.</summary>
        public BufferCapacityController(
            IBufferCapacityControl capacityControl,
            IBufferCapacityPolicy<TSample> policy,
            int minCapacity = 1,
            int maxCapacity = int.MaxValue)
        {
            _capacityControl = capacityControl ?? throw new ArgumentNullException(nameof(capacityControl));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            if (minCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(minCapacity));
            if (maxCapacity < minCapacity) throw new ArgumentOutOfRangeException(nameof(maxCapacity));
            var currentCapacity = _capacityControl.Capacity;
            if (currentCapacity <= 0)
                throw new ArgumentException("Capacity control must report a positive capacity.", nameof(capacityControl));

            MinCapacity = minCapacity;
            MaxCapacity = maxCapacity;
            LastTargetCapacity = Clamp(currentCapacity);
        }

        /// <summary>Gets the lowest capacity the controller may request.</summary>
        public int MinCapacity { get; }

        /// <summary>Gets the highest capacity the controller may request.</summary>
        public int MaxCapacity { get; }

        /// <summary>Gets the capacity currently reported by the buffer.</summary>
        public int CurrentCapacity => _capacityControl.Capacity;

        /// <summary>Gets the most recent policy target after bounds were applied.</summary>
        public int LastTargetCapacity { get; private set; }

        /// <summary>Evaluates the policy and attempts to apply its bounded target.</summary>
        public bool Update(TSample sample)
        {
            var requested = _policy.GetTargetCapacity(sample, _capacityControl.Capacity);
            return TrySetTargetCapacity(requested);
        }

        /// <summary>Attempts to apply a directly supplied target after bounds are applied.</summary>
        public bool TrySetTargetCapacity(int capacity)
        {
            var target = Clamp(capacity);
            LastTargetCapacity = target;
            if (target == _capacityControl.Capacity) return false;
            return _capacityControl.TrySetCapacity(target);
        }

        private int Clamp(int capacity)
        {
            if (capacity < MinCapacity) return MinCapacity;
            return capacity > MaxCapacity ? MaxCapacity : capacity;
        }
    }
}
