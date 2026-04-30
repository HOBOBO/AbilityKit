using System;
using System.Runtime.InteropServices;

namespace AbilityKit.Threading
{
    /// <summary>
    /// 数组池（减少 GC）
    /// 适用于需要频繁分配临时数组的场景
    /// </summary>
    public sealed class ArrayPool<T>
    {
        private const int MaxArrayLength = 0x7FEFFFFF;
        private const int MaxBuffersPerBucket = 16;

        private readonly Bucket[] _buckets;

        private sealed class Bucket
        {
            internal T[][] Buffers = Array.Empty<T[]>();
            internal int Count;
        }

        private ArrayPool()
        {
            var numBuckets = 0;
            var length = 1;
            while (length < 4096)
            {
                length <<= 1;
                numBuckets++;
            }

            _buckets = new Bucket[numBuckets];
            for (int i = 0; i < numBuckets; i++)
            {
                _buckets[i] = new Bucket();
            }
        }

        private static readonly Lazy<ArrayPool<T>> _shared = new(() => new ArrayPool<T>());

        /// <summary>
        /// 共享池
        /// </summary>
        public static ArrayPool<T> Shared => _shared.Value;

        private static int GetBucketIndex(int length)
        {
            var index = 0;
            var size = 1;
            while (size < length)
            {
                size <<= 1;
                index++;
            }
            return index;
        }

        /// <summary>
        /// 租用数组
        /// </summary>
        public T[] Rent(int minimumLength)
        {
            if (minimumLength < 0)
                throw new ArgumentOutOfRangeException(nameof(minimumLength));

            if (minimumLength == 0)
                return Array.Empty<T>();

            var index = GetBucketIndex(minimumLength);
            if (index >= _buckets.Length)
                index = _buckets.Length - 1;

            var bucket = _buckets[index];

            T[] buffer;
            lock (bucket)
            {
                if (bucket.Count > 0)
                {
                    buffer = bucket.Buffers[--bucket.Count];
                    bucket.Buffers[bucket.Count] = null;
                }
                else
                {
                    var size = 1 << (index + 1);
                    buffer = new T[size];
                }
            }

            return buffer;
        }

        /// <summary>
        /// 归还数组
        /// </summary>
        public void Return(T[] array, bool clearArray = false)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            if (array.Length == 0)
                return;

            var index = GetBucketIndex(array.Length);
            if (index >= _buckets.Length)
            {
                return;
            }

            if (clearArray)
            {
                Array.Clear(array, 0, array.Length);
            }

            var bucket = _buckets[index];
            lock (bucket)
            {
                if (bucket.Count < MaxBuffersPerBucket)
                {
                    if (bucket.Buffers.Length == bucket.Count)
                    {
                        var newBuffers = new T[bucket.Count * 2][];
                        Array.Copy(bucket.Buffers, newBuffers, bucket.Count);
                        bucket.Buffers = newBuffers;
                    }
                    bucket.Buffers[bucket.Count++] = array;
                }
            }
        }
    }

    /// <summary>
    /// 池化数组（using 语法糖）
    /// </summary>
    public struct PooledArray<T> : IDisposable
    {
        private readonly T[] _array;
        private readonly ArrayPool<T> _pool;
        private readonly bool _clearArray;

        /// <summary>
        /// 数组长度
        /// </summary>
        public int Length => _array?.Length ?? 0;

        /// <summary>
        /// 数组引用
        /// </summary>
        public T[] Array => _array;

        /// <summary>
        /// 隐式转换为数组
        /// </summary>
        public static implicit operator T[](PooledArray<T> pooled) => pooled._array;

        internal PooledArray(T[] array, ArrayPool<T> pool, bool clearArray)
        {
            _array = array;
            _pool = pool;
            _clearArray = clearArray;
        }

        /// <summary>
        /// 归还数组到池
        /// </summary>
        public void Dispose()
        {
            if (_array != null && _pool != null)
            {
                _pool.Return(_array, _clearArray);
            }
        }
    }

    /// <summary>
    /// 数组池扩展方法
    /// </summary>
    public static class ArrayPoolExtensions
    {
        /// <summary>
        /// 租用数组并自动归还
        /// </summary>
        public static PooledArray<T> RentArray<T>(this ArrayPool<T> pool, int length, bool clearOnReturn = true)
        {
            var array = pool.Rent(length);
            return new PooledArray<T>(array, pool, clearOnReturn);
        }

        /// <summary>
        /// 使用池化数组执行操作
        /// </summary>
        public static void UseArray<T>(this ArrayPool<T> pool, int length, Action<T[]> action, bool clearOnReturn = true)
        {
            var array = pool.Rent(length);
            try
            {
                action(array);
            }
            finally
            {
                pool.Return(array, clearOnReturn);
            }
        }
    }

    /// <summary>
    /// 内存池（裸内存）
    /// </summary>
    public sealed class MemoryPool
    {
        private static readonly Lazy<MemoryPool> _shared = new(() => new MemoryPool());
        private readonly ArrayPool<byte> _arrayPool;

        /// <summary>
        /// 共享池
        /// </summary>
        public static MemoryPool Shared => _shared.Value;

        private MemoryPool()
        {
            _arrayPool = ArrayPool<byte>.Shared;
        }

        /// <summary>
        /// 租用内存块
        /// </summary>
        public byte[] Rent(int minimumLength)
        {
            return _arrayPool.Rent(minimumLength);
        }

        /// <summary>
        /// 归还内存块
        /// </summary>
        public void Return(byte[] memory, bool clearArray = true)
        {
            if (memory != null)
            {
                _arrayPool.Return(memory, clearArray);
            }
        }
    }

    /// <summary>
    /// 池化内存（using 语法糖）
    /// </summary>
    public struct PooledMemory : IDisposable
    {
        private readonly byte[] _memory;
        private readonly MemoryPool _pool;
        private readonly bool _clearArray;
        private readonly int _length;

        /// <summary>
        /// 内存长度
        /// </summary>
        public int Length => _length;

        /// <summary>
        /// 内存引用
        /// </summary>
        public byte[] Memory => _memory;

        internal PooledMemory(byte[] memory, MemoryPool pool, bool clearArray)
        {
            _memory = memory;
            _pool = pool;
            _clearArray = clearArray;
            _length = memory?.Length ?? 0;
        }

        /// <summary>
        /// 归还内存到池
        /// </summary>
        public void Dispose()
        {
            if (_memory != null && _pool != null)
            {
                _pool.Return(_memory, _clearArray);
            }
        }
    }
}
