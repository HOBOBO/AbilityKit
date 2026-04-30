using System;
using System.Threading;

namespace AbilityKit.Threading
{
    /// <summary>
    /// 高性能自旋锁
    /// 适用于锁持有时间非常短的场景
    /// </summary>
    public struct SpinLock
    {
        private int _isLocked;

        /// <summary>
        /// 是否已被锁定
        /// </summary>
        public bool IsLocked => _isLocked != 0;

        /// <summary>
        /// 获取锁（阻塞直到获得）
        /// </summary>
        public void Enter()
        {
            if (Interlocked.CompareExchange(ref _isLocked, 1, 0) == 0)
                return;

            var spin = 0;
            while (true)
            {
                Thread.SpinWait(spin);
                spin = spin < 10 ? spin + 1 : spin;

                if (Interlocked.CompareExchange(ref _isLocked, 1, 0) == 0)
                    return;

                if (spin > 10)
                {
                    Thread.Yield();
                }
            }
        }

        /// <summary>
        /// 释放锁
        /// </summary>
        public void Exit()
        {
            Interlocked.Exchange(ref _isLocked, 0);
        }

        /// <summary>
        /// 尝试获取锁（非阻塞）
        /// </summary>
        public bool TryEnter()
        {
            return Interlocked.CompareExchange(ref _isLocked, 1, 0) == 0;
        }
    }

    /// <summary>
    /// 可重入自旋锁
    /// </summary>
    public struct RecursiveSpinLock
    {
        private int _isLocked;
        private int _recursionCount;
        private int _ownerThreadId;

        public bool IsLocked => _isLocked != 0;

        public bool IsHeldByCurrentThread => _ownerThreadId == Thread.CurrentThread.ManagedThreadId;

        public void Enter()
        {
            var currentThreadId = Thread.CurrentThread.ManagedThreadId;

            if (_ownerThreadId == currentThreadId)
            {
                _recursionCount++;
                return;
            }

            var spin = 0;
            while (true)
            {
                if (Interlocked.CompareExchange(ref _isLocked, 1, 0) == 0)
                {
                    _ownerThreadId = currentThreadId;
                    _recursionCount = 1;
                    return;
                }

                Thread.SpinWait(spin);
                spin = spin < 10 ? spin + 1 : spin;

                if (spin > 10)
                {
                    Thread.Yield();
                }
            }
        }

        public void Exit()
        {
            var currentThreadId = Thread.CurrentThread.ManagedThreadId;
            if (_ownerThreadId != currentThreadId)
                return;

            _recursionCount--;
            if (_recursionCount == 0)
            {
                _ownerThreadId = 0;
                Interlocked.Exchange(ref _isLocked, 0);
            }
        }

        public bool TryEnter()
        {
            var currentThreadId = Thread.CurrentThread.ManagedThreadId;

            if (_ownerThreadId == currentThreadId)
            {
                _recursionCount++;
                return true;
            }

            if (Interlocked.CompareExchange(ref _isLocked, 1, 0) == 0)
            {
                _ownerThreadId = currentThreadId;
                _recursionCount = 1;
                return true;
            }

            return false;
        }
    }
}
