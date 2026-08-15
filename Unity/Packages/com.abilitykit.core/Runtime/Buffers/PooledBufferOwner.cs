#nullable enable

using System;
using System.Buffers;
using System.Threading;

namespace AbilityKit.Core.Buffers
{
    /// <summary>
    /// Controls when a rented buffer is cleared. Clearing applies to the full
    /// physical array supplied by the pool, not only to the logical buffer.
    /// </summary>
    [Flags]
    public enum PooledBufferClearMode
    {
        /// <summary>Does not clear the buffer.</summary>
        None = 0,

        /// <summary>Clears the buffer immediately after it is rented.</summary>
        OnRent = 1,

        /// <summary>Requests clearing when the buffer is returned.</summary>
        OnReturn = 2
    }

    /// <summary>
    /// Owns a logically sized region of an array rented from an
    /// <see cref="ArrayPool{T}"/>.
    /// </summary>
    /// <remarks>
    /// Views obtained from this owner are valid only until <see cref="Dispose"/>.
    /// Access must not race with disposal. Disposal itself is thread-safe and
    /// returns the array at most once.
    /// </remarks>
    public sealed class PooledBufferOwner<T> : IMemoryOwner<T>
    {
        private const PooledBufferClearMode ValidClearModes =
            PooledBufferClearMode.OnRent | PooledBufferClearMode.OnReturn;

        private readonly ArrayPool<T> _pool;
        private readonly int _length;
        private readonly bool _returnToPool;
        private readonly PooledBufferClearMode _clearMode;
        private T[]? _array;

        private PooledBufferOwner(
            ArrayPool<T> pool,
            T[] array,
            int length,
            bool returnToPool,
            PooledBufferClearMode clearMode)
        {
            _pool = pool;
            _array = array;
            _length = length;
            _returnToPool = returnToPool;
            _clearMode = clearMode;
        }

        /// <summary>Gets the logical number of elements owned by this buffer.</summary>
        /// <remarks>The logical length remains available after disposal.</remarks>
        public int Length => _length;

        /// <summary>Gets whether the rented array has been returned.</summary>
        public bool IsDisposed => Volatile.Read(ref _array) == null;

        /// <summary>Gets memory limited to the logical buffer length.</summary>
        /// <exception cref="ObjectDisposedException">The owner has been disposed.</exception>
        public Memory<T> Memory => GetArray().AsMemory(0, _length);

        /// <summary>Gets a span limited to the logical buffer length.</summary>
        /// <exception cref="ObjectDisposedException">The owner has been disposed.</exception>
        public Span<T> Span => GetArray().AsSpan(0, _length);

        /// <summary>Gets an array segment limited to the logical buffer length.</summary>
        /// <exception cref="ObjectDisposedException">The owner has been disposed.</exception>
        public ArraySegment<T> Segment => new ArraySegment<T>(GetArray(), 0, _length);

        /// <summary>Rents a buffer from <see cref="ArrayPool{T}.Shared"/>.</summary>
        /// <param name="length">Required logical length.</param>
        /// <param name="clearMode">Explicit buffer clearing policy.</param>
        /// <returns>An owner that must be disposed exactly once by its logical owner.</returns>
        public static PooledBufferOwner<T> Rent(
            int length,
            PooledBufferClearMode clearMode = PooledBufferClearMode.None)
        {
            return Rent(length, ArrayPool<T>.Shared, clearMode);
        }

        /// <summary>Rents a buffer from a caller-provided array pool.</summary>
        /// <param name="length">Required logical length.</param>
        /// <param name="pool">Pool that supplies and receives the physical array.</param>
        /// <param name="clearMode">Explicit buffer clearing policy.</param>
        /// <returns>An owner that must be disposed exactly once by its logical owner.</returns>
        public static PooledBufferOwner<T> Rent(
            int length,
            ArrayPool<T> pool,
            PooledBufferClearMode clearMode = PooledBufferClearMode.None)
        {
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            if (pool == null) throw new ArgumentNullException(nameof(pool));
            if ((clearMode & ~ValidClearModes) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(clearMode));
            }

            if (length == 0)
            {
                return new PooledBufferOwner<T>(
                    pool,
                    Array.Empty<T>(),
                    length: 0,
                    returnToPool: false,
                    clearMode);
            }

            var array = pool.Rent(length);
            if (array == null)
            {
                throw new InvalidOperationException("The array pool returned null.");
            }

            if (array.Length < length)
            {
                pool.Return(array, ShouldClearOnReturn(clearMode));
                throw new InvalidOperationException(
                    $"The array pool returned a buffer of length {array.Length} for a request of {length}.");
            }

            if ((clearMode & PooledBufferClearMode.OnRent) != 0)
            {
                Array.Clear(array, 0, array.Length);
            }

            return new PooledBufferOwner<T>(pool, array, length, returnToPool: true, clearMode);
        }

        /// <summary>Returns the rented array to its pool. Repeated calls are ignored.</summary>
        public void Dispose()
        {
            var array = Interlocked.Exchange(ref _array, null);
            if (array == null || !_returnToPool)
            {
                return;
            }

            _pool.Return(array, ShouldClearOnReturn(_clearMode));
        }

        private T[] GetArray()
        {
            var array = Volatile.Read(ref _array);
            if (array == null)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            return array;
        }

        private static bool ShouldClearOnReturn(PooledBufferClearMode clearMode)
        {
            return (clearMode & PooledBufferClearMode.OnReturn) != 0;
        }
    }
}
