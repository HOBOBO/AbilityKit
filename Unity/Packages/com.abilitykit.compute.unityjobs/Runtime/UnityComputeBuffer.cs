#nullable enable

using System;
using Unity.Collections;

namespace AbilityKit.Compute.UnityJobs
{
    /// <summary>
    /// Owns a persistent native array that grows geometrically. Complete all jobs using
    /// the current array before resizing or disposing this owner.
    /// </summary>
    public sealed class UnityComputeBuffer<T> : IDisposable where T : unmanaged
    {
        private NativeArray<T> _array;
        private bool _disposed;

        public bool IsCreated => _array.IsCreated;
        public int Capacity => _array.IsCreated ? _array.Length : 0;

        public NativeArray<T> Array
        {
            get
            {
                ThrowIfDisposed();
                return _array;
            }
        }

        public NativeArray<T> EnsureCapacity(int required)
        {
            ThrowIfDisposed();
            if (required < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(required));
            }

            if (_array.IsCreated && _array.Length >= required)
            {
                return _array;
            }

            if (_array.IsCreated)
            {
                _array.Dispose();
            }

            if (required == 0)
            {
                _array = default;
                return _array;
            }

            var capacity = 64;
            while (capacity < required)
            {
                capacity = checked(capacity * 2);
            }

            _array = new NativeArray<T>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            return _array;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_array.IsCreated)
            {
                _array.Dispose();
            }

            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }
    }
}
