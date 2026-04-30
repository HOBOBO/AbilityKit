using System;
using System.Collections.Generic;
using System.Threading;

namespace AbilityKit.Threading
{
    /// <summary>
    /// 协程状态
    /// </summary>
    public enum FiberState
    {
        Ready,
        Running,
        Waiting,
        Completed,
        Faulted,
        Cancelled
    }

    /// <summary>
    /// 协程（轻量级线程）
    /// 在单线程内调度，无栈切换开销
    /// </summary>
    public class Fiber : IDisposable
    {
        private readonly FiberScheduler _scheduler;
        private readonly Action _action;
        private FiberState _state;
        private Exception _exception;
        private Func<bool> _waitCondition;
        private TimeSpan _waitDuration;
        private DateTime _waitEndTime;

        public FiberScheduler Scheduler => _scheduler;
        public FiberState State => _state;
        public Exception Exception => _exception;
        public bool IsCompleted => _state == FiberState.Completed || _state == FiberState.Faulted || _state == FiberState.Cancelled;

        internal Fiber(FiberScheduler scheduler, Action action)
        {
            _scheduler = scheduler;
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _state = FiberState.Ready;
        }

        /// <summary>
        /// 开始执行
        /// </summary>
        public void Start()
        {
            if (_state != FiberState.Ready)
                return;

            _state = FiberState.Running;
            _scheduler.AddRunningFiber(this);
        }

        /// <summary>
        /// 暂停执行（yield）
        /// </summary>
        public Fiber Yield()
        {
            _state = FiberState.Waiting;
            _scheduler.AddWaitingFiber(this);
            return this;
        }

        /// <summary>
        /// 等待另一个协程完成
        /// </summary>
        public Fiber Await(Fiber other)
        {
            if (other == null || other.IsCompleted)
                return this;

            _waitCondition = () => other.IsCompleted;
            return Yield();
        }

        /// <summary>
        /// 等待条件满足
        /// </summary>
        public Fiber WaitUntil(Func<bool> condition)
        {
            _waitCondition = condition ?? (() => true);
            return Yield();
        }

        /// <summary>
        /// 等待一段时间
        /// </summary>
        public Fiber Sleep(TimeSpan duration)
        {
            _waitDuration = duration;
            _waitEndTime = DateTime.UtcNow + duration;
            return Yield();
        }

        /// <summary>
        /// 执行一个步骤
        /// </summary>
        internal bool Step()
        {
            if (_state == FiberState.Completed || _state == FiberState.Faulted || _state == FiberState.Cancelled)
                return false;

            try
            {
                _action();
                _state = FiberState.Completed;
                return true;
            }
            catch (Exception ex)
            {
                _exception = ex;
                _state = FiberState.Faulted;
                return false;
            }
        }

        /// <summary>
        /// 检查等待条件
        /// </summary>
        internal bool CheckWaitCondition()
        {
            if (_waitCondition != null)
            {
                return _waitCondition();
            }

            if (_waitEndTime != default)
            {
                return DateTime.UtcNow >= _waitEndTime;
            }

            return false;
        }

        /// <summary>
        /// 设置等待条件（内部使用）
        /// </summary>
        internal void SetWaitCondition(Func<bool> condition)
        {
            _waitCondition = condition;
        }

        /// <summary>
        /// 取消协程
        /// </summary>
        public void Cancel()
        {
            if (!IsCompleted)
            {
                _state = FiberState.Cancelled;
            }
        }

        /// <summary>
        /// 设置状态（内部使用）
        /// </summary>
        internal void SetState(FiberState state)
        {
            _state = state;
        }

        public void Dispose()
        {
            Cancel();
        }

        public override string ToString()
        {
            return $"Fiber[{_state}]";
        }
    }

    /// <summary>
    /// 协程调度器
    /// </summary>
    public sealed class FiberScheduler : IDisposable
    {
        private readonly object _lock = new();
        private readonly List<Fiber> _runningFibers = new();
        private readonly List<Fiber> _waitingFibers = new();
        private readonly List<Fiber> _completedFibers = new();

        public int RunningCount => _runningFibers.Count;
        public int WaitingCount => _waitingFibers.Count;

        /// <summary>
        /// 创建新协程
        /// </summary>
        public Fiber NewFiber(Action action)
        {
            return new Fiber(this, action);
        }

        /// <summary>
        /// 创建并启动协程
        /// </summary>
        public Fiber StartFiber(Action action)
        {
            var fiber = NewFiber(action);
            fiber.Start();
            return fiber;
        }

        internal void AddRunningFiber(Fiber fiber)
        {
            lock (_lock)
            {
                _runningFibers.Add(fiber);
            }
        }

        internal void AddWaitingFiber(Fiber fiber)
        {
            lock (_lock)
            {
                _waitingFibers.Add(fiber);
            }
        }

        /// <summary>
        /// 执行一个时间片
        /// </summary>
        public void Update(TimeSpan deltaTime)
        {
            lock (_lock)
            {
                // 处理等待中的协程
                for (int i = _waitingFibers.Count - 1; i >= 0; i--)
                {
                    var fiber = _waitingFibers[i];
                    if (fiber.CheckWaitCondition())
                    {
                        _waitingFibers.RemoveAt(i);
                        fiber.SetState(FiberState.Running);
                        _runningFibers.Add(fiber);
                    }
                }

                // 执行运行中的协程
                for (int i = _runningFibers.Count - 1; i >= 0; i--)
                {
                    var fiber = _runningFibers[i];

                    var completed = !fiber.Step();

                    if (completed)
                    {
                        _runningFibers.RemoveAt(i);
                        _completedFibers.Add(fiber);
                    }
                }
            }
        }

        /// <summary>
        /// 执行一个时间片
        /// </summary>
        public void Update()
        {
            Update(TimeSpan.FromMilliseconds(16));
        }

        /// <summary>
        /// 执行直到所有协程完成
        /// </summary>
        public void RunToCompletion()
        {
            while (true)
            {
                lock (_lock)
                {
                    if (_runningFibers.Count == 0 && _waitingFibers.Count == 0)
                        break;
                }

                Update();
            }
        }

        /// <summary>
        /// 清除已完成的协程
        /// </summary>
        public void ClearCompleted()
        {
            lock (_lock)
            {
                _completedFibers.Clear();
            }
        }

        /// <summary>
        /// 清除所有协程
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                foreach (var fiber in _runningFibers)
                {
                    fiber.Cancel();
                }
                foreach (var fiber in _waitingFibers)
                {
                    fiber.Cancel();
                }

                _runningFibers.Clear();
                _waitingFibers.Clear();
                _completedFibers.Clear();
            }
        }

        public void Dispose()
        {
            Clear();
        }
    }
}
