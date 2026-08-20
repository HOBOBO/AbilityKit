namespace AbilityKit.Core.Pooling
{
    /// <summary>Receives synchronous lifecycle notifications from an <see cref="ObjectPool{T}"/>.</summary>
    public interface IPoolable
    {
        /// <summary>Runs when the instance is acquired, before the pool's configured acquisition callback.</summary>
        void OnPoolGet();
        /// <summary>Runs when the instance is returned or prewarmed, before the pool's configured release callback.</summary>
        void OnPoolRelease();
        /// <summary>Runs when the instance is permanently removed, before the pool's configured destruction callback.</summary>
        void OnPoolDestroy();
    }
}
