using System;
using System.Collections.Generic;
using System.Threading;

namespace AbilityKit.Threading
{
    /// <summary>
    /// 动态线程池配置
    /// </summary>
    public sealed class DynamicThreadPoolConfig
    {
        /// <summary>
        /// 最小线程数
        /// </summary>
        public int MinThreads { get; set; } = 1;

        /// <summary>
        /// 最大线程数
        /// </summary>
        public int MaxThreads { get; set; } = Environment.ProcessorCount * 2;

        /// <summary>
        /// 线程空闲超时（毫秒）
        /// </summary>
        public int IdleTimeoutMs { get; set; } = 30000;

        /// <summary>
        /// 目标延迟阈值（毫秒）- 超过此延迟则增加线程
        /// </summary>
        public int TargetLatencyMs { get; set; } = 100;

        /// <summary>
        /// 负载评估间隔（毫秒）
        /// </summary>
        public int LoadCheckIntervalMs { get; set; } = 500;

        /// <summary>
        /// 每次增加/减少的线程数
        /// </summary>
        public int ThreadAdjustStep { get; set; } = 1;

        /// <summary>
        /// 线程名称前缀
        /// </summary>
        public string ThreadNamePrefix { get; set; } = "DynamicPool";
    }

    /// <summary>
    /// 动态线程池
    /// 根据负载自动调整线程数量
    /// </summary>
    public sealed class DynamicThreadPool : IDisposable
    {
        private readonly DynamicThreadPoolConfig _config;
        private readonly PriorityWorkQueue<Action> _workQueue;
        private readonly ConcurrentDictionary<int, WorkerThread> _workers;
        private readonly AtomicCounter64 _pendingTasks;
        private readonly AtomicCounter64 _activeTasks;
        private readonly AtomicCounter64 _completedTasks;
        private readonly AtomicCounter64 _totalLatencyMs;
        private readonly AutoResetEvent _taskAvailable;
        private readonly Timer _loadBalancerTimer;
        private volatile bool _isRunning;
        private int _currentThreadCount;
        private int _threadIdCounter;
        private readonly object _threadLock = new();

        /// <summary>
        /// 当前线程数
        /// </summary>
        public int CurrentThreadCount => _currentThreadCount;

        /// <summary>
        /// 最小线程数
        /// </summary>
        public int MinThreads => _config.MinThreads;

        /// <summary>
        /// 最大线程数
        /// </summary>
        public int MaxThreads => _config.MaxThreads;

        /// <summary>
        /// 挂起的任务数
        /// </summary>
        public long PendingTaskCount => _pendingTasks.Value;

        /// <summary>
        /// 活跃的任务数
        /// </summary>
        public long ActiveTaskCount => _activeTasks.Value;

        /// <summary>
        /// 已完成的任务数
        /// </summary>
        public long CompletedTaskCount => _completedTasks.Value;

        /// <summary>
        /// 平均任务延迟（毫秒）
        /// </summary>
        public double AverageLatencyMs => _completedTasks.Value > 0
            ? (double)_totalLatencyMs.Value / _completedTasks.Value
            : 0;

        /// <summary>
        /// 创建动态线程池
        /// </summary>
        public DynamicThreadPool() : this(new DynamicThreadPoolConfig())
        {
        }

        /// <summary>
        /// 创建动态线程池（带配置）
        /// </summary>
        public DynamicThreadPool(DynamicThreadPoolConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _workQueue = new PriorityWorkQueue<Action>(4);
            _workers = new ConcurrentDictionary<int, WorkerThread>();
            _pendingTasks = new AtomicCounter64();
            _activeTasks = new AtomicCounter64();
            _completedTasks = new AtomicCounter64();
            _totalLatencyMs = new AtomicCounter64();
            _taskAvailable = new AutoResetEvent(false);
            _isRunning = true;

            // 确保最小线程数
            for (int i = 0; i < _config.MinThreads; i++)
            {
                CreateWorker();
            }

            // 启动负载均衡器
            _loadBalancerTimer = new Timer(LoadBalance, null,
                _config.LoadCheckIntervalMs, _config.LoadCheckIntervalMs);
        }

        /// <summary>
        /// 提交任务（普通优先级）
        /// </summary>
        public void Submit(Action work)
        {
            Submit(work, WorkPriority.Normal);
        }

        /// <summary>
        /// 提交任务（指定优先级）
        /// </summary>
        public void Submit(Action work, WorkPriority priority)
        {
            if (!_isRunning || work == null)
                return;

            _workQueue.Enqueue(work, priority);
            _pendingTasks.Increment();
            _taskAvailable.Set();
        }

        /// <summary>
        /// 提交任务（自定义优先级数值）
        /// </summary>
        public void Submit(Action work, int priority)
        {
            if (!_isRunning || work == null)
                return;

            _workQueue.Enqueue(work, priority);
            _pendingTasks.Increment();
            _taskAvailable.Set();
        }

        /// <summary>
        /// 提交高优先级任务
        /// </summary>
        public void SubmitHighPriority(Action work)
        {
            Submit(work, WorkPriority.High);
        }

        /// <summary>
        /// 提交关键任务（最高优先级）
        /// </summary>
        public void SubmitCritical(Action work)
        {
            Submit(work, WorkPriority.Critical);
        }

        /// <summary>
        /// 提交低优先级任务
        /// </summary>
        public void SubmitLowPriority(Action work)
        {
            Submit(work, WorkPriority.Low);
        }

        /// <summary>
        /// 获取当前负载指标
        /// </summary>
        public LoadMetrics GetMetrics()
        {
            return new LoadMetrics
            {
                CurrentThreadCount = _currentThreadCount,
                PendingTaskCount = _pendingTasks.Value,
                ActiveTaskCount = _activeTasks.Value,
                CompletedTaskCount = _completedTasks.Value,
                AverageLatencyMs = AverageLatencyMs,
                QueueLengthByPriority = _workQueue.GetPriorityCounts()
            };
        }

        private void CreateWorker()
        {
            lock (_threadLock)
            {
                if (_currentThreadCount >= _config.MaxThreads)
                    return;

                var threadId = Interlocked.Increment(ref _threadIdCounter);
                var worker = new WorkerThread(
                    $"{_config.ThreadNamePrefix}-{threadId}",
                    threadId,
                    ProcessWork,
                    _taskAvailable);

                _workers.Set(threadId, worker);
                _currentThreadCount++;
            }
        }

        private void ProcessWork()
        {
            while (_isRunning)
            {
                if (_workQueue.TryDequeue(out var work))
                {
                    _pendingTasks.Decrement();
                    _activeTasks.Increment();

                    var startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    try
                    {
                        work?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"DynamicThreadPool[{Thread.CurrentThread.Name}] Exception: {ex}");
                    }
                    finally
                    {
                        var latency = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime;
                        _totalLatencyMs.Add(latency);
                        _completedTasks.Increment();
                        _activeTasks.Decrement();
                    }
                }
                else
                {
                    // 没有任务时等待
                    _taskAvailable.Wait(100);
                }
            }
        }

        private void LoadBalance(object state)
        {
            if (!_isRunning)
                return;

            var metrics = GetMetrics();
            var pending = metrics.PendingTaskCount;
            var avgLatency = metrics.AverageLatencyMs;

            // 负载过高 - 增加线程
            if (avgLatency > _config.TargetLatencyMs && pending > _currentThreadCount)
            {
                if (_currentThreadCount < _config.MaxThreads)
                {
                    CreateWorker();
                }
            }

            // 负载过低 - 减少线程
            if (avgLatency < _config.TargetLatencyMs * 0.3 && pending == 0)
            {
                // 保持最小线程数
                if (_currentThreadCount > _config.MinThreads)
                {
                    // 减少线程的逻辑需要更复杂的实现
                    // 当前简化版本暂不减少线程
                }
            }
        }

        /// <summary>
        /// 优雅关闭
        /// </summary>
        public void Shutdown()
        {
            _isRunning = false;
        }

        /// <summary>
        /// 等待所有任务完成并关闭
        /// </summary>
        public void ShutdownAndWait(int timeoutMs = Timeout.Infinite)
        {
            Shutdown();
            var deadline = timeoutMs == Timeout.Infinite ? long.MaxValue :
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + timeoutMs;

            while (_pendingTasks.Value > 0 || _activeTasks.Value > 0)
            {
                if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > deadline)
                    break;
                Thread.Sleep(10);
            }
        }

        public void Dispose()
        {
            Shutdown();

            _loadBalancerTimer?.Dispose();

            foreach (var worker in _workers.GetValues())
            {
                worker.Dispose();
            }

            _workers.Clear();
            _taskAvailable?.Dispose();
        }

        private sealed class WorkerThread : IDisposable
        {
            private readonly Thread _thread;
            private readonly ManualResetEvent _wakeEvent;
            private volatile bool _shouldStop;

            public int Id { get; }

            internal WorkerThread(string name, int id, Action processWork, AutoResetEvent wakeEvent)
            {
                Id = id;
                _wakeEvent = new ManualResetEvent(false);

                _thread = new Thread(() =>
                {
                    while (!_shouldStop)
                    {
                        if (processWork != null)
                        {
                            processWork();
                        }
                        Thread.Sleep(1);
                    }
                })
                {
                    Name = name,
                    IsBackground = true,
                    Priority = ThreadPriority.Normal
                };

                _thread.Start();
            }

            public void Stop()
            {
                _shouldStop = true;
                _wakeEvent.Set();
                if (_thread.IsAlive)
                {
                    _thread.Join(5000);
                }
            }

            public void Dispose()
            {
                Stop();
                _wakeEvent?.Dispose();
            }
        }
    }

    /// <summary>
    /// 负载指标
    /// </summary>
    public struct LoadMetrics
    {
        /// <summary>
        /// 当前线程数
        /// </summary>
        public int CurrentThreadCount;

        /// <summary>
        /// 挂起的任务数
        /// </summary>
        public long PendingTaskCount;

        /// <summary>
        /// 正在执行的任务数
        /// </summary>
        public long ActiveTaskCount;

        /// <summary>
        /// 已完成的任务数
        /// </summary>
        public long CompletedTaskCount;

        /// <summary>
        /// 平均任务延迟（毫秒）
        /// </summary>
        public double AverageLatencyMs;

        /// <summary>
        /// 各优先级的队列长度
        /// </summary>
        public int[] QueueLengthByPriority;

        public override string ToString()
        {
            return $"Threads={CurrentThreadCount}, Pending={PendingTaskCount}, " +
                   $"Active={ActiveTaskCount}, Completed={CompletedTaskCount}, " +
                   $"AvgLatency={AverageLatencyMs:F2}ms";
        }
    }
}
