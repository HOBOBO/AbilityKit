using System;
using System.Threading;

namespace AbilityKit.Threading
{
    /// <summary>
    /// 高性能读写锁
    /// 允许多个读线程或单个写线程
    /// </summary>
    public sealed class ReaderWriterLock : IDisposable
    {
        private int _readers;
        private int _writersWaiting;
        private int _writerId;
        private int _recursionCount;

        /// <summary>
        /// 获取读锁（可重入）
        /// </summary>
        public LockHandle AcquireReadLock()
        {
            var currentThreadId = Thread.CurrentThread.ManagedThreadId;

            // 可重入检查：同一写线程可以直接获取读锁
            if (_writerId == currentThreadId && _recursionCount > 0)
            {
                _recursionCount++;
                return new LockHandle(this, currentThreadId, isWriteLock: false);
            }

            // 等待没有写者
            while (_writersWaiting > 0 || _writerId != 0)
            {
                Thread.SpinWait(1);
            }

            // 增加读者计数
            Interlocked.Increment(ref _readers);
            return new LockHandle(this, currentThreadId, isWriteLock: false);
        }

        /// <summary>
        /// 获取读锁（带超时）
        /// </summary>
        public bool TryAcquireReadLock(int millisecondsTimeout, out LockHandle handle)
        {
            var currentThreadId = Thread.CurrentThread.ManagedThreadId;
            var startTime = millisecondsTimeout == Timeout.Infinite ? 0 : Environment.TickCount;

            // 可重入检查
            if (_writerId == currentThreadId && _recursionCount > 0)
            {
                _recursionCount++;
                handle = new LockHandle(this, currentThreadId, isWriteLock: false);
                return true;
            }

            // 等待没有写者
            while (_writersWaiting > 0 || _writerId != 0)
            {
                if (millisecondsTimeout != Timeout.Infinite)
                {
                    var elapsed = Environment.TickCount - startTime;
                    if (elapsed >= millisecondsTimeout)
                    {
                        handle = default;
                        return false;
                    }
                }
                Thread.SpinWait(1);
            }

            Interlocked.Increment(ref _readers);
            handle = new LockHandle(this, currentThreadId, isWriteLock: false);
            return true;
        }

        /// <summary>
        /// 获取写锁
        /// </summary>
        public LockHandle AcquireWriteLock(int millisecondsTimeout)
        {
            if (!TryAcquireWriteLock(millisecondsTimeout, out var handle))
            {
                throw new TimeoutException("Failed to acquire write lock within the specified timeout.");
            }
            return handle;
        }

        /// <summary>
        /// 获取写锁（带超时）
        /// </summary>
        public bool TryAcquireWriteLock(int millisecondsTimeout, out LockHandle handle)
        {
            var currentThreadId = Thread.CurrentThread.ManagedThreadId;
            var startTime = millisecondsTimeout == Timeout.Infinite ? 0 : Environment.TickCount;

            // 可重入检查
            if (_writerId == currentThreadId)
            {
                _recursionCount++;
                handle = new LockHandle(this, currentThreadId, isWriteLock: true);
                return true;
            }

            // 等待读者清空
            while (_readers > 0)
            {
                if (millisecondsTimeout != Timeout.Infinite)
                {
                    var elapsed = Environment.TickCount - startTime;
                    if (elapsed >= millisecondsTimeout)
                    {
                        handle = default;
                        return false;
                    }
                }
                Thread.SpinWait(1);
            }

            // 尝试成为写者
            Interlocked.Increment(ref _writersWaiting);
            try
            {
                while (_readers > 0)
                {
                    if (millisecondsTimeout != Timeout.Infinite)
                    {
                        var elapsed = Environment.TickCount - startTime;
                        if (elapsed >= millisecondsTimeout)
                        {
                            handle = default;
                            return false;
                        }
                    }
                    Thread.SpinWait(1);
                }

                if (Interlocked.CompareExchange(ref _writerId, currentThreadId, 0) == 0)
                {
                    _recursionCount = 1;
                    handle = new LockHandle(this, currentThreadId, isWriteLock: true);
                    return true;
                }
            }
            finally
            {
                Interlocked.Decrement(ref _writersWaiting);
            }

            handle = default;
            return false;
        }

        /// <summary>
        /// 升级读锁为写锁
        /// </summary>
        public bool UpgradeToWriteLock(out LockHandle writeHandle)
        {
            return TryAcquireWriteLock(Timeout.Infinite, out writeHandle);
        }

        internal void ReleaseReadLock(int threadId)
        {
            if (_writerId == threadId)
                return;

            Interlocked.Decrement(ref _readers);
        }

        internal void ReleaseWriteLock(int threadId)
        {
            if (_writerId != threadId)
                return;

            _recursionCount--;
            if (_recursionCount == 0)
            {
                Interlocked.Exchange(ref _writerId, 0);
            }
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// 锁句柄
    /// </summary>
    public readonly struct LockHandle : IDisposable
    {
        private readonly ReaderWriterLock _lock;
        private readonly int _threadId;
        private readonly bool _isWriteLock;

        internal LockHandle(ReaderWriterLock lock_, int threadId, bool isWriteLock)
        {
            _lock = lock_;
            _threadId = threadId;
            _isWriteLock = isWriteLock;
        }

        public void Dispose()
        {
            if (_isWriteLock)
            {
                _lock?.ReleaseWriteLock(_threadId);
            }
            else
            {
                _lock?.ReleaseReadLock(_threadId);
            }
        }
    }

    /// <summary>
    /// 读写锁扩展方法
    /// </summary>
    public static class ReaderWriterLockExtensions
    {
        /// <summary>
        /// 使用读锁读取值
        /// </summary>
        public static T Read<T>(this ReaderWriterLock lock_, ref T value)
        {
            using (lock_.AcquireReadLock())
            {
                return value;
            }
        }

        /// <summary>
        /// 使用写锁写入值
        /// </summary>
        public static void Write<T>(this ReaderWriterLock lock_, ref T value, T newValue)
        {
            using (var handle = lock_.AcquireWriteLock(Timeout.Infinite))
            {
                value = newValue;
            }
        }
    }
}
