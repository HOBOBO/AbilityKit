#nullable enable

using System;
using System.Buffers;
using System.Threading;

namespace AbilityKit.Core.Buffers
{
    /// <summary>
    /// 定义租用数组的清理时机；清理范围覆盖池提供的整个物理数组，不限于逻辑长度，标志可以组合。
    /// </summary>
    [Flags]
    public enum PooledBufferClearMode
    {
        /// <summary>
        /// 不请求清理数组，租用的内容可能包含先前数据。
        /// </summary>
        None = 0,

        /// <summary>
        /// 租用成功后立即清理整个物理数组。
        /// </summary>
        OnRent = 1,

        /// <summary>
        /// 归还时向数组池请求清理整个物理数组；实际清理由池实现负责。
        /// </summary>
        OnReturn = 2
    }

    /// <summary>
    /// 持有数组池提供的物理数组，并公开指定逻辑长度的视图。由逻辑所有者负责释放，释放最多尝试归还一次。
    /// </summary>
    /// <remarks>
    /// 已取得的视图仅在释放前有效，访问不得与释放并发。
    /// 释放操作自身支持并发，先撤销访问权再向池归还；池的归还异常不会恢复访问权。
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

        /// <summary>
        /// 获取逻辑元素数量；释放后仍可读取，数值保持不变。
        /// </summary>
        public int Length => _length;

        /// <summary>
        /// 获取是否已撤销缓冲区访问权；该状态不代表自定义数组池已成功接收归还的数组。
        /// </summary>
        public bool IsDisposed => Volatile.Read(ref _array) == null;

        /// <summary>
        /// 获取限制在逻辑长度内的内存视图；所有者释放后不得继续使用该视图。
        /// </summary>
        /// <exception cref="ObjectDisposedException">所有者已释放。</exception>
        public Memory<T> Memory => GetArray().AsMemory(0, _length);

        /// <summary>
        /// 获取限制在逻辑长度内的跨度视图；所有者释放后不得继续使用该视图。
        /// </summary>
        /// <exception cref="ObjectDisposedException">所有者已释放。</exception>
        public Span<T> Span => GetArray().AsSpan(0, _length);

        /// <summary>
        /// 获取逻辑长度对应的数组段；底层数组可能更大，调用方不得越过逻辑范围或在释放后继续使用。
        /// </summary>
        /// <exception cref="ObjectDisposedException">所有者已释放。</exception>
        public ArraySegment<T> Segment => new ArraySegment<T>(GetArray(), 0, _length);

        /// <summary>
        /// 从共享数组池租用缓冲区并创建逻辑长度对应的所有者；零长度不会向池租用数组。
        /// </summary>
        /// <param name="length">逻辑元素数量，允许为零，不得为负数。</param>
        /// <param name="clearMode">显式清理策略，只接受已定义标志的组合。</param>
        /// <returns>由调用方负责释放的缓冲区所有者；重复释放不会重复归还。</returns>
        public static PooledBufferOwner<T> Rent(
            int length,
            PooledBufferClearMode clearMode = PooledBufferClearMode.None)
        {
            return Rent(length, ArrayPool<T>.Shared, clearMode);
        }

        /// <summary>
        /// 从指定数组池租用物理数组，并创建逻辑长度对应的所有者；零长度不会调用池的租用或归还方法。
        /// </summary>
        /// <param name="length">逻辑元素数量，允许为零，不得为负数。</param>
        /// <param name="pool">提供和接收物理数组的池，不得为空。</param>
        /// <param name="clearMode">显式清理策略，只接受已定义标志的组合。</param>
        /// <returns>由调用方负责释放的缓冲区所有者；重复释放不会重复归还。</returns>
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

        /// <summary>
        /// 先撤销所有权，再最多尝试归还数组一次；重复或并发释放不重复归还，数组池抛出的异常直接传播且后续不重试。
        /// </summary>
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
