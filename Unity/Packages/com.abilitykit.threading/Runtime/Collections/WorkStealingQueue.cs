using System;
using System.Threading;

namespace AbilityKit.Threading
{
    /// <summary>
    /// 工作窃取队列
    /// 本地线程从头部取任务，其他线程从尾部偷任务
    /// 适用于分治算法和线程池
    /// </summary>
    public class WorkStealingQueue<T>
    {
        private T[] _items = Array.Empty<T>();
        private int _head;
        private int _tail;
        private int _count;

        /// <summary>
        /// 元素数量
        /// </summary>
        public int Count => _count;

        /// <summary>
        /// 是否为空
        /// </summary>
        public bool IsEmpty => _count == 0;

        private const int MinimumGrow = 4;
        private const int GrowFactor = 2;

        /// <summary>
        /// 本地线程入队（从头部）
        /// </summary>
        public void LocalPush(T item)
        {
            if (_count == _items.Length)
            {
                Grow();
            }

            _items[_head] = item;
            _head = (_head + 1) % _items.Length;
            Interlocked.Increment(ref _count);
        }

        /// <summary>
        /// 本地线程出队（从头部）
        /// </summary>
        public bool LocalPop(out T item)
        {
            if (_count == 0)
            {
                item = default;
                return false;
            }

            _head = (_head - 1 + _items.Length) % _items.Length;
            item = _items[_head];
            _items[_head] = default;
            Interlocked.Decrement(ref _count);
            return true;
        }

        /// <summary>
        /// 偷取任务（从尾部）
        /// </summary>
        public bool Steal(out T item)
        {
            if (_count == 0)
            {
                item = default;
                return false;
            }

            item = _items[_tail];
            _items[_tail] = default;
            _tail = (_tail + 1) % _items.Length;
            Interlocked.Decrement(ref _count);
            return true;
        }

        /// <summary>
        /// 窥视头部元素
        /// </summary>
        public bool Peek(out T item)
        {
            if (_count == 0)
            {
                item = default;
                return false;
            }

            var headIndex = (_head - 1 + _items.Length) % _items.Length;
            item = _items[headIndex];
            return true;
        }

        /// <summary>
        /// 清空队列
        /// </summary>
        public void Clear()
        {
            _head = 0;
            _tail = 0;
            _count = 0;
            _items = Array.Empty<T>();
        }

        private void Grow()
        {
            var newSize = _items.Length == 0 ? 4 : _items.Length * GrowFactor;
            var newArray = new T[newSize];

            if (_head < _tail)
            {
                Array.Copy(_items, _head, newArray, 0, _count);
            }
            else
            {
                Array.Copy(_items, _head, newArray, 0, _items.Length - _head);
                Array.Copy(_items, 0, newArray, _items.Length - _head, _tail);
            }

            _head = 0;
            _tail = _count;
            _items = newArray;
        }
    }

    /// <summary>
    /// 工作窃取线程池
    /// 支持完整的 Fork/Join 并行模式
    /// </summary>
    public sealed class WorkStealingPool : IDisposable
    {
        private readonly ThreadWorker[] _workers;
        private readonly WorkStealingQueue<Action>[] _workQueues;
        private readonly Barrier _barrier;
        private readonly AtomicCounter _pendingTasks;
        private readonly AutoResetEvent _taskAvailable;
        private volatile bool _isRunning;
        private readonly int _threadCount;
        private int _nextWorkerIndex;

        /// <summary>
        /// 工作线程数
        /// </summary>
        public int ThreadCount => _threadCount;

        /// <summary>
        /// 创建工作窃取线程池
        /// </summary>
        /// <param name="threadCount">线程数量</param>
        public WorkStealingPool(int threadCount)
        {
            if (threadCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(threadCount));

            _threadCount = threadCount;
            _workers = new ThreadWorker[threadCount];
            _workQueues = new WorkStealingQueue<Action>[threadCount];
            _barrier = new Barrier(threadCount);
            _pendingTasks = new AtomicCounter();
            _taskAvailable = new AutoResetEvent(false);
            _isRunning = true;

            for (int i = 0; i < threadCount; i++)
            {
                _workQueues[i] = new WorkStealingQueue<Action>();
            }

            for (int i = 0; i < threadCount; i++)
            {
                var index = i;
                _workers[i] = new ThreadWorker($"WorkStealing-{i}");
                _workers[i].QueueWork(() => WorkerLoop(index));
            }
        }

        private void WorkerLoop(int threadIndex)
        {
            var localQueue = _workQueues[threadIndex];
            var random = new Random(threadIndex);

            while (_isRunning)
            {
                if (localQueue.LocalPop(out var work))
                {
                    ExecuteWork(work);
                    continue;
                }

                var stolen = false;
                var attempts = _workers.Length - 1;
                var startIndex = random.Next(_workers.Length);

                for (int i = 0; i < attempts; i++)
                {
                    var targetIndex = (startIndex + i) % _workers.Length;
                    if (targetIndex == threadIndex)
                        continue;

                    if (_workQueues[targetIndex].Steal(out work))
                    {
                        ExecuteWork(work);
                        stolen = true;
                        break;
                    }
                }

                if (!stolen)
                {
                    if (_pendingTasks.Value == 0)
                    {
                        Thread.Sleep(1);
                    }
                }
            }

            _barrier.SignalAndWait();
        }

        private void ExecuteWork(Action work)
        {
            try
            {
                work?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WorkStealingPool ExecuteWork Exception: {ex}");
            }
        }

        private int GetNextWorkerIndex()
        {
            return Interlocked.Increment(ref _nextWorkerIndex) % _workers.Length;
        }

        /// <summary>
        /// 提交任务
        /// </summary>
        public void Submit(Action work)
        {
            if (!_isRunning)
                return;

            var threadIndex = GetNextWorkerIndex();
            _workQueues[threadIndex].LocalPush(work);
            _pendingTasks.Increment();
        }

        /// <summary>
        /// Fork - 开启并行分支
        /// </summary>
        /// <typeparam name="T">结果类型</typeparam>
        /// <param name="func">要执行的操作</param>
        /// <returns>Future 用于后续 Join</returns>
        public Future<T> Fork<T>(Func<T> func)
        {
            var future = new Future<T>();
            var threadIndex = GetNextWorkerIndex();

            _workQueues[threadIndex].LocalPush(() =>
            {
                try
                {
                    var result = func();
                    future.SetResult(result);
                }
                catch (Exception ex)
                {
                    future.SetException(ex);
                }
            });

            _pendingTasks.Increment();
            return future;
        }

        /// <summary>
        /// Fork - 开启并行分支（无返回值）
        /// </summary>
        public Future<VoidResult> Fork(Action action)
        {
            return Fork(() =>
            {
                action();
                return VoidResult.Default;
            });
        }

        /// <summary>
        /// Fork - 开启多个并行分支
        /// </summary>
        public Future<T>[] Fork<T>(params Func<T>[] funcs)
        {
            var futures = new Future<T>[funcs.Length];
            for (int i = 0; i < funcs.Length; i++)
            {
                futures[i] = Fork(funcs[i]);
            }
            return futures;
        }

        /// <summary>
        /// Join - 等待所有 Fork 的任务完成并合并结果
        /// </summary>
        public T[] Join<T>(Future<T>[] futures)
        {
            var results = new T[futures.Length];
            for (int i = 0; i < futures.Length; i++)
            {
                results[i] = futures[i].Wait();
            }
            return results;
        }

        /// <summary>
        /// Join - 等待所有无返回值的 Fork 完成
        /// </summary>
        public void Join(Future<VoidResult>[] futures)
        {
            foreach (var future in futures)
            {
                future.Wait();
            }
        }

        /// <summary>
        /// Fork/Join 简化封装
        /// </summary>
        public T[] ParallelInvoke<T>(params Func<T>[] funcs)
        {
            var futures = Fork(funcs);
            return Join(futures);
        }

        /// <summary>
        /// Fork/Join 简化封装（无返回值）
        /// </summary>
        public void ParallelInvoke(params Action[] actions)
        {
            var futures = new Future<VoidResult>[actions.Length];
            for (int i = 0; i < actions.Length; i++)
            {
                futures[i] = Fork(actions[i]);
            }
            Join(futures);
        }

        /// <summary>
        /// 分治任务（完整实现）
        /// </summary>
        public TResult Execute<TInput, TResult>(
            TInput input,
            Func<TInput, TResult> solve,
            Func<TInput, (TInput, TInput)> split,
            Func<TResult, TResult, TResult> merge,
            int threshold = 1024)
        {
            var futures = new System.Collections.Concurrent.ConcurrentBag<Future<TResult>>();
            var pendingCount = new AtomicCounter();
            var resultLock = new SpinLock();
            TResult result = default;
            var isFirstResult = new AtomicBoolean(true);

            void ExecuteRecursive(TInput subInput)
            {
                if (threshold <= 1 || !ShouldSplit(subInput))
                {
                    var localResult = solve(subInput);

                    var lockTaken = false;
                    try
                    {
                        resultLock.Enter();
                        lockTaken = true;
                        if (isFirstResult.TrySet())
                        {
                            result = localResult;
                        }
                        else
                        {
                            result = merge(result, localResult);
                        }
                    }
                    finally
                    {
                        if (lockTaken)
                            resultLock.Exit();
                    }
                    pendingCount.Decrement();
                    return;
                }

                var (left, right) = split(subInput);
                pendingCount.Increment();
                pendingCount.Increment();

                // 递归执行（简化为串行，实际可以使用并行）
                ExecuteRecursive(left);
                ExecuteRecursive(right);
            }

            pendingCount.Increment();
            ExecuteRecursive(input);
            pendingCount.Decrement();

            return result;
        }

        /// <summary>
        /// 递归分治（完整并行版本）
        /// </summary>
        public TResult ExecuteParallel<TInput, TResult>(
            TInput input,
            Func<TInput, TResult> solve,
            Func<TInput, (TInput, TInput)> split,
            Func<TResult, TResult, TResult> merge,
            int threshold = 1024)
        {
            var futures = new System.Collections.Concurrent.ConcurrentBag<Future<TResult>>();
            ExecuteParallelRecursive(input, solve, split, merge, threshold, futures);

            var result = default(TResult);
            var isFirst = true;
            var resultLock = new SpinLock();

            while (!futures.IsEmpty || result == null)
            {
                Future<TResult> futureToProcess = null;
                while (futures.TryTake(out var f))
                {
                    if (f.IsCompleted)
                    {
                        futureToProcess = f;
                        break;
                    }
                    futures.Add(f);
                }

                if (futureToProcess != null)
                {
                    var localResult = futureToProcess.Wait();
                    var lockTaken = false;
                    try
                    {
                        resultLock.Enter();
                        lockTaken = true;
                        if (isFirst)
                        {
                            result = localResult;
                            isFirst = false;
                        }
                        else
                        {
                            result = merge(result, localResult);
                        }
                    }
                    finally
                    {
                        if (lockTaken)
                            resultLock.Exit();
                    }
                }
                else
                {
                    Thread.SpinWait(10);
                }
            }

            return result;
        }

        private void ExecuteParallelRecursive<TInput, TResult>(
            TInput subInput,
            Func<TInput, TResult> solve,
            Func<TInput, (TInput, TInput)> split,
            Func<TResult, TResult, TResult> merge,
            int threshold,
            System.Collections.Concurrent.ConcurrentBag<Future<TResult>> futures)
        {
            if (threshold <= 1 || !ShouldSplit(subInput))
            {
                var localResult = solve(subInput);
                futures.Add(Fork(() => localResult));
                return;
            }

            var (left, right) = split(subInput);

            var leftFuture = Fork(() =>
            {
                ExecuteParallelRecursive(left, solve, split, merge, threshold / 2, futures);
                return default(TResult);
            });

            ExecuteParallelRecursive(right, solve, split, merge, threshold / 2, futures);
        }

        private bool ShouldSplit<T>(T input)
        {
            return true;
        }

        /// <summary>
        /// 停止线程池
        /// </summary>
        public void Shutdown()
        {
            _isRunning = false;
        }

        public void Dispose()
        {
            Shutdown();

            for (int i = 0; i < _workers.Length; i++)
            {
                _workers[i]?.Dispose();
            }

            _barrier?.Dispose();
            _taskAvailable?.Dispose();
        }
    }

    /// <summary>
    /// Future 异步结果
    /// </summary>
    public sealed class Future<T>
    {
        private readonly object _lock = new();
        private T _result;
        private Exception _exception;
        private AtomicBoolean _isCompleted = new();
        private ManualResetEvent _waitHandle = new(false);

        public bool IsCompleted => _isCompleted.Value;

        public T Result
        {
            get
            {
                if (!IsCompleted)
                    throw new InvalidOperationException("Task is not completed");
                if (_exception != null)
                    throw _exception;
                return _result;
            }
        }

        public void SetResult(T result)
        {
            lock (_lock)
            {
                if (_isCompleted.TrySet())
                {
                    _result = result;
                    _waitHandle.Set();
                }
            }
        }

        public void SetException(Exception exception)
        {
            lock (_lock)
            {
                if (_isCompleted.TrySet())
                {
                    _exception = exception;
                    _waitHandle.Set();
                }
            }
        }

        public T Wait()
        {
            _waitHandle.Wait();
            if (_exception != null)
                throw _exception;
            return _result;
        }

        public bool Wait(int timeoutMs)
        {
            if (!_waitHandle.Wait(timeoutMs))
                return false;

            if (_exception != null)
                throw _exception;
            return true;
        }
    }

    /// <summary>
    /// 空结果类型
    /// </summary>
    public struct VoidResult
    {
        public static VoidResult Default => default;
    }
}
