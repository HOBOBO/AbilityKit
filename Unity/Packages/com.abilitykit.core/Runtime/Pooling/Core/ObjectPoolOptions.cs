using System;

namespace AbilityKit.Core.Pooling
{
    /// <summary>
    /// Configures object creation, lifecycle callbacks, capacity, validation, and trimming for an <see cref="ObjectPool{T}"/>.
    /// </summary>
    /// <typeparam name="T">The reference type stored by the pool.</typeparam>
    public sealed class ObjectPoolOptions<T> where T : class
    {
        /// <summary>Creates a new element when no inactive element is available.</summary>
        public Func<T> CreateFunc;

        /// <summary>Runs after an element is acquired and its <see cref="IPoolable.OnPoolGet"/> callback completes.</summary>
        public Action<T>? OnGet;

        /// <summary>Runs after <see cref="IPoolable.OnPoolRelease"/> and before the element becomes available again.</summary>
        public Action<T>? OnRelease;

        /// <summary>Runs when an element is permanently removed from the pool.</summary>
        public Action<T>? OnDestroy;

        /// <summary>Gets or sets the number of elements created during pool construction.</summary>
        public int DefaultCapacity = 0;

        /// <summary>Gets or sets the maximum number of inactive elements retained by the pool.</summary>
        public int MaxSize = 1024;

        /// <summary>Gets or sets the default policy used by <see cref="ObjectPool{T}.Trim()"/>.</summary>
        public PoolTrimPolicy TrimPolicy = PoolTrimPolicy.KeepDefaultCapacity;

        /// <summary>Gets or sets whether duplicate releases are detected by reference identity.</summary>
        public bool CollectionCheck = true;

        /// <summary>Gets or sets whether regular trim operations must leave this pool unchanged.</summary>
        public bool NeverTrim;

        /// <summary>Creates options that use the specified element factory.</summary>
        /// <param name="createFunc">A factory that must return a non-null element.</param>
        /// <exception cref="ArgumentNullException"><paramref name="createFunc"/> is <see langword="null"/>.</exception>
        public ObjectPoolOptions(Func<T> createFunc)
        {
            CreateFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
        }
    }
}
