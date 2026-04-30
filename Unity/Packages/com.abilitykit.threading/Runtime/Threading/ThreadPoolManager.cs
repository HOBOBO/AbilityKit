using System;
using System.Collections.Generic;
using System.Threading;

namespace AbilityKit.Threading
{
    /// <summary>
    /// 线程池管理器
    /// </summary>
    public sealed class ThreadPoolManager
    {
        private static readonly Lazy<ThreadPoolManager> _instance = new(() => new ThreadPoolManager());
        public static ThreadPoolManager Instance => _instance.Value;

        private readonly Dictionary<int, ThreadWorker> _namedThreads;
        private readonly object _lock = new();
        private ThreadLocal<Random> _localRandom;

        public int MinWorkerThreads { get; private set; }
        public int MaxWorkerThreads { get; private set; }

        public Random LocalRandom => _localRandom.Value;

        private ThreadPoolManager()
        {
            _namedThreads = new Dictionary<int, ThreadWorker>();
            _localRandom = new ThreadLocal<Random>(() => new Random());
            
            ThreadPool.GetMinThreads(out var minWorker, out var minCompletion);
            ThreadPool.GetMaxThreads(out var maxWorker, out var maxCompletion);
            MinWorkerThreads = minWorker;
            MaxWorkerThreads = maxWorker;
        }

        /// <summary>
        /// 配置线程池大小
        /// </summary>
        public bool SetMinThreads(int workerThreads, int completionPortThreads)
        {
            return ThreadPool.SetMinThreads(workerThreads, completionPortThreads);
        }

        /// <summary>
        /// 配置线程池大小
        /// </summary>
        public bool SetMaxThreads(int workerThreads, int completionPortThreads)
        {
            return ThreadPool.SetMaxThreads(workerThreads, completionPortThreads);
        }

        /// <summary>
        /// 获取当前线程信息
        /// </summary>
        public ThreadInfo GetCurrentThreadInfo()
        {
            var thread = Thread.CurrentThread;
            return new ThreadInfo
            {
                Id = thread.ManagedThreadId,
                Name = thread.Name,
                IsBackground = thread.IsBackground,
                Priority = thread.Priority,
                ThreadState = thread.ThreadState
            };
        }

        /// <summary>
        /// 注册命名线程
        /// </summary>
        public ThreadWorker RegisterThread(string name = null)
        {
            lock (_lock)
            {
                var thread = new ThreadWorker(name);
                _namedThreads[thread.Id] = thread;
                return thread;
            }
        }

        /// <summary>
        /// 获取命名线程
        /// </summary>
        public ThreadWorker GetThread(int threadId)
        {
            lock (_lock)
            {
                return _namedThreads.TryGetValue(threadId, out var worker) ? worker : null;
            }
        }

        /// <summary>
        /// 在线程池执行任务
        /// </summary>
        public void QueueTask(Action action)
        {
            if (action == null) return;
            System.Threading.Tasks.Task.Run(action);
        }

        /// <summary>
        /// 在指定线程执行任务
        /// </summary>
        public bool QueueToThread(int threadId, Action work)
        {
            lock (_lock)
            {
                if (_namedThreads.TryGetValue(threadId, out var worker))
                {
                    worker.QueueWork(work);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 获取活跃的工作线程数量
        /// </summary>
        public int GetActiveWorkerCount()
        {
            ThreadPool.GetAvailableThreads(out var workerThreads, out var completionPortThreads);
            return MaxWorkerThreads - workerThreads;
        }
    }

    /// <summary>
    /// 工作线程封装
    /// </summary>
    public sealed class ThreadWorker : IDisposable
    {
        private readonly Thread _thread;
        private readonly MpscQueue<Action> _workQueue;
        private readonly ManualResetEvent _wakeEvent;
        private volatile bool _shouldStop;
        private volatile bool _isRunning;

        public int Id => _thread.ManagedThreadId;
        public string Name => _thread.Name ?? $"Thread-{Id}";
        public bool IsRunning => _isRunning;

        internal ThreadWorker(string name)
        {
            _workQueue = new MpscQueue<Action>();
            _wakeEvent = new ManualResetEvent(false);

            _thread = new Thread(WorkLoop)
            {
                Name = name ?? $"Worker-{Guid.NewGuid():N}",
                IsBackground = true,
                Priority = ThreadPriority.Normal
            };

            _thread.Start();
        }

        internal void QueueWork(Action work)
        {
            _workQueue.Enqueue(work);
            _wakeEvent.Set();
        }

        private void WorkLoop()
        {
            _isRunning = true;

            while (!_shouldStop)
            {
                if (_workQueue.TryDequeue(out var work))
                {
                    try
                    {
                        work?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ThreadWorker[{Name}] Exception: {ex}");
                    }
                }
                else
                {
                    Thread.Sleep(1);
                }
            }

            _isRunning = false;
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
        }
    }

    /// <summary>
    /// 线程信息
    /// </summary>
    public sealed class ThreadInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool IsBackground { get; set; }
        public ThreadPriority Priority { get; set; }
        public ThreadState ThreadState { get; set; }
    }
}
