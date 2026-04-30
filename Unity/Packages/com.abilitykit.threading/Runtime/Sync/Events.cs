using System;
using System.Threading;

namespace AbilityKit.Threading
{
    /// <summary>
    /// 倒计时事件
    /// 等待计数器归零
    /// </summary>
    public sealed class CountdownEvent : IDisposable
    {
        private int _count;
        private readonly object _lock = new();
        private readonly ManualResetEventSlim _event;

        /// <summary>
        /// 当前计数
        /// </summary>
        public int CurrentCount => _count;

        /// <summary>
        /// 初始计数
        /// </summary>
        public int InitialCount { get; }

        /// <summary>
        /// 是否已触发（计数为0）
        /// </summary>
        public bool IsSet => _count == 0;

        /// <summary>
        /// 创建一个倒计时事件
        /// </summary>
        /// <param name="initialCount">初始计数</param>
        public CountdownEvent(int initialCount)
        {
            if (initialCount < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCount));

            InitialCount = initialCount;
            _count = initialCount;
            _event = new ManualResetEventSlim(_count == 0);
        }

        /// <summary>
        /// 递减计数
        /// </summary>
        public bool Signal()
        {
            lock (_lock)
            {
                if (_count <= 0)
                    return false;

                _count--;
                if (_count == 0)
                {
                    _event.Set();
                }
                return true;
            }
        }

        /// <summary>
        /// 递减多个计数
        /// </summary>
        public bool Signal(int signalCount)
        {
            if (signalCount <= 0)
                return false;

            lock (_lock)
            {
                if (_count < signalCount)
                    return false;

                _count -= signalCount;
                if (_count == 0)
                {
                    _event.Set();
                }
                return true;
            }
        }

        /// <summary>
        /// 添加计数
        /// </summary>
        public void AddCount()
        {
            AddCount(1);
        }

        /// <summary>
        /// 添加多个计数
        /// </summary>
        public void AddCount(int signalCount)
        {
            if (signalCount <= 0)
                return;

            lock (_lock)
            {
                if (_count == 0)
                    _event.Reset();

                _count += signalCount;
            }
        }

        /// <summary>
        /// 等待计数归零
        /// </summary>
        public void Wait()
        {
            _event.Wait();
        }

        /// <summary>
        /// 等待计数归零（带超时）
        /// </summary>
        public bool Wait(int millisecondsTimeout)
        {
            return _event.Wait(millisecondsTimeout);
        }

        /// <summary>
        /// 等待计数归零（带超时）
        /// </summary>
        public bool Wait(TimeSpan timeout)
        {
            return _event.Wait(timeout);
        }

        /// <summary>
        /// 重置计数
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _count = InitialCount;
                _event.Reset();
            }
        }

        /// <summary>
        /// 重置为指定计数
        /// </summary>
        public void Reset(int count)
        {
            lock (_lock)
            {
                _count = count;
                _event.Reset();
            }
        }

        public void Dispose()
        {
            _event.Dispose();
        }
    }

    /// <summary>
    /// 栅栏
    /// 等待所有线程到达后同时继续
    /// </summary>
    public sealed class Barrier : IDisposable
    {
        private int _participants;
        private int _arrived;
        private int _currentGeneration;
        private readonly object _lock = new();
        private readonly ManualResetEventSlim _event;

        /// <summary>
        /// 参与方数量
        /// </summary>
        public int ParticipantCount => _participants;

        /// <summary>
        /// 当前已到达的参与方数量
        /// </summary>
        public int CurrentCount => _arrived;

        /// <summary>
        /// 创建一个栅栏
        /// </summary>
        /// <param name="participantCount">参与方数量</param>
        public Barrier(int participantCount)
        {
            if (participantCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(participantCount));

            _participants = participantCount;
            _arrived = 0;
            _event = new ManualResetEventSlim(false);
        }

        /// <summary>
        /// 信号到达并等待所有参与方
        /// </summary>
        public void SignalAndWait()
        {
            SignalAndWait(Timeout.Infinite);
        }

        /// <summary>
        /// 信号到达并等待所有参与方（带超时）
        /// </summary>
        public bool SignalAndWait(int millisecondsTimeout)
        {
            return SignalAndWait(TimeSpan.FromMilliseconds(millisecondsTimeout));
        }

        /// <summary>
        /// 信号到达并等待所有参与方（带超时）
        /// </summary>
        public bool SignalAndWait(TimeSpan timeout)
        {
            var generation = _currentGeneration;

            lock (_lock)
            {
                _arrived++;
                if (_arrived == _participants)
                {
                    // 所有参与方都到达
                    _arrived = 0;
                    _currentGeneration++;
                    _event.Set();
                    return true;
                }
            }

            // 等待信号
            var result = _event.Wait(timeout);

            // 检查是否是正确的generation
            lock (_lock)
            {
                if (_currentGeneration != generation)
                {
                    return true;
                }

                // 如果超时或者generation变化，可能需要重试
                if (!result && _arrived == _participants)
                {
                    _arrived = 0;
                    _currentGeneration++;
                    _event.Set();
                    return true;
                }

                return result;
            }
        }

        /// <summary>
        /// 重置栅栏
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _arrived = 0;
                _currentGeneration++;
                _event.Reset();
            }
        }

        /// <summary>
        /// 添加参与方
        /// </summary>
        public void AddParticipants(int additionalParticipants)
        {
            if (additionalParticipants <= 0)
                return;

            lock (_lock)
            {
                _participants += additionalParticipants;
            }
        }

        /// <summary>
        /// 移除参与方
        /// </summary>
        public void RemoveParticipants(int participantsToRemove)
        {
            if (participantsToRemove <= 0)
                return;

            lock (_lock)
            {
                if (_participants <= participantsToRemove)
                    throw new InvalidOperationException("Cannot remove more participants than exist.");

                _participants -= participantsToRemove;
            }
        }

        public void Dispose()
        {
            _event.Dispose();
        }
    }

    /// <summary>
    /// 信号量
    /// 控制对资源的并发访问
    /// </summary>
    public sealed class Semaphore : IDisposable
    {
        private int _count;
        private readonly int _maxCount;
        private readonly object _lock = new();
        private readonly ManualResetEventSlim _event;

        /// <summary>
        /// 当前计数
        /// </summary>
        public int CurrentCount => _count;

        /// <summary>
        /// 最大计数
        /// </summary>
        public int MaxCount => _maxCount;

        /// <summary>
        /// 创建一个信号量
        /// </summary>
        /// <param name="initialCount">初始计数</param>
        /// <param name="maxCount">最大计数</param>
        public Semaphore(int initialCount, int maxCount)
        {
            if (initialCount < 0 || initialCount > maxCount)
                throw new ArgumentOutOfRangeException(nameof(initialCount));
            if (maxCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxCount));

            _count = initialCount;
            _maxCount = maxCount;
            _event = new ManualResetEventSlim(_count > 0);
        }

        /// <summary>
        /// 等待信号
        /// </summary>
        public void Wait()
        {
            Wait(Timeout.Infinite);
        }

        /// <summary>
        /// 等待信号（带超时）
        /// </summary>
        public bool Wait(int millisecondsTimeout)
        {
            return Wait(TimeSpan.FromMilliseconds(millisecondsTimeout));
        }

        /// <summary>
        /// 等待信号（带超时）
        /// </summary>
        public bool Wait(TimeSpan timeout)
        {
            lock (_lock)
            {
                while (_count == 0)
                {
                    if (!_event.Wait(timeout))
                        return false;
                }

                _count--;
                if (_count > 0)
                {
                    _event.Reset();
                }
                return true;
            }
        }

        /// <summary>
        /// 释放信号
        /// </summary>
        public void Release()
        {
            Release(1);
        }

        /// <summary>
        /// 释放多个信号
        /// </summary>
        public void Release(int releaseCount)
        {
            if (releaseCount <= 0)
                return;

            lock (_lock)
            {
                var previousCount = _count;
                _count = Math.Min(_count + releaseCount, _maxCount);

                // 如果之前是0，现在有信号了
                if (previousCount == 0 && _count > 0)
                {
                    _event.Set();
                }
            }
        }

        public void Dispose()
        {
            _event.Dispose();
        }
    }

    /// <summary>
    /// 自动重置事件
    /// </summary>
    public sealed class AutoResetEvent : IDisposable
    {
        private volatile bool _signaled;
        private readonly object _lock = new();
        private readonly ManualResetEventSlim _event;

        /// <summary>
        /// 创建一个自动重置事件
        /// </summary>
        /// <param name="initialState">初始状态</param>
        public AutoResetEvent(bool initialState)
        {
            _signaled = initialState;
            _event = new ManualResetEventSlim(initialState);
        }

        /// <summary>
        /// 等待信号
        /// </summary>
        public bool Wait()
        {
            return Wait(Timeout.Infinite);
        }

        /// <summary>
        /// 等待信号（带超时）
        /// </summary>
        public bool Wait(int millisecondsTimeout)
        {
            return Wait(TimeSpan.FromMilliseconds(millisecondsTimeout));
        }

        /// <summary>
        /// 等待信号（带超时）
        /// </summary>
        public bool Wait(TimeSpan timeout)
        {
            lock (_lock)
            {
                if (_signaled)
                {
                    _signaled = false;
                    return true;
                }

                var result = _event.Wait(timeout);
                if (result)
                {
                    _signaled = false;
                }
                return result;
            }
        }

        /// <summary>
        /// 设置信号（自动重置）
        /// </summary>
        public void Set()
        {
            lock (_lock)
            {
                _signaled = true;
                _event.Set();
            }
        }

        /// <summary>
        /// 重置为无信号状态
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _signaled = false;
                _event.Reset();
            }
        }

        public void Dispose()
        {
            _event.Dispose();
        }
    }

    /// <summary>
    /// 手动重置事件
    /// </summary>
    public sealed class ManualResetEvent : IDisposable
    {
        private volatile bool _signaled;
        private readonly object _lock = new();
        private readonly ManualResetEventSlim _event;

        /// <summary>
        /// 创建一个手动重置事件
        /// </summary>
        /// <param name="initialState">初始状态</param>
        public ManualResetEvent(bool initialState)
        {
            _signaled = initialState;
            _event = new ManualResetEventSlim(initialState);
        }

        /// <summary>
        /// 等待信号
        /// </summary>
        public bool Wait()
        {
            return Wait(Timeout.Infinite);
        }

        /// <summary>
        /// 等待信号（带超时）
        /// </summary>
        public bool Wait(int millisecondsTimeout)
        {
            return Wait(TimeSpan.FromMilliseconds(millisecondsTimeout));
        }

        /// <summary>
        /// 等待信号（带超时）
        /// </summary>
        public bool Wait(TimeSpan timeout)
        {
            return _event.Wait(timeout);
        }

        /// <summary>
        /// 设置为有信号状态
        /// </summary>
        public void Set()
        {
            lock (_lock)
            {
                _signaled = true;
                _event.Set();
            }
        }

        /// <summary>
        /// 重置为无信号状态
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _signaled = false;
                _event.Reset();
            }
        }

        public void Dispose()
        {
            _event.Dispose();
        }
    }
}
