using System;
using System.Threading;
using System.Threading.Tasks;

namespace AbilityKit.Threading
{
    /// <summary>
    /// 通道（线程间通信）
    /// 类似于 Go 的 channel
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    public class Channel<T>
    {
        private readonly MpmcQueue<ChannelElement<T>> _queue;
        private readonly int _capacity;
        private volatile bool _completed;

        private struct ChannelElement<TElement>
        {
            public TElement Value;
            public bool IsValue;
            public Exception Exception;
            public bool IsCompleted;
        }

        /// <summary>
        /// 写入端
        /// </summary>
        public ChannelWriter<T> Writer { get; }

        /// <summary>
        /// 读取端
        /// </summary>
        public ChannelReader<T> Reader { get; }

        /// <summary>
        /// 通道是否已关闭
        /// </summary>
        public bool IsCompleted => _completed;

        /// <summary>
        /// 当前元素数量
        /// </summary>
        public int Count => _queue.Count;

        private Channel(int capacity)
        {
            _capacity = capacity;
            _queue = capacity > 0 ? new MpmcQueue<ChannelElement<T>>(capacity) : new MpmcQueue<ChannelElement<T>>();

            Writer = new ChannelWriter<T>(this);
            Reader = new ChannelReader<T>(this);
        }

        /// <summary>
        /// 创建无界通道
        /// </summary>
        public static Channel<T> CreateUnbounded()
        {
            return new Channel<T>(-1);
        }

        /// <summary>
        /// 创建有界通道
        /// </summary>
        public static Channel<T> CreateBounded(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            return new Channel<T>(capacity);
        }

        internal bool TryWrite(T item)
        {
            if (_completed)
                return false;

            _queue.Enqueue(new ChannelElement<T> { Value = item, IsValue = true });
            return true;
        }

        internal bool TryWriteComplete(Exception exception = null)
        {
            _completed = true;
            _queue.Enqueue(new ChannelElement<T> { IsCompleted = true, Exception = exception });
            return true;
        }

        internal bool TryRead(out T result)
        {
            if (_queue.TryDequeue(out var element))
            {
                if (element.IsCompleted)
                    throw new ChannelClosedException();
                result = element.Value;
                return true;
            }

            result = default;
            return false;
        }

        internal bool TryPeek(out T result)
        {
            if (_queue.TryPeek(out var element))
            {
                if (element.IsCompleted)
                {
                    result = default;
                    return false;
                }
                result = element.Value;
                return true;
            }

            result = default;
            return false;
        }

        internal bool IsEmpty => _queue.IsEmpty;
        internal bool IsFull => _capacity > 0 && _queue.Count >= _capacity;
    }

    /// <summary>
    /// 通道已关闭异常
    /// </summary>
    public class ChannelClosedException : Exception
    {
        public ChannelClosedException() : base("Channel has been closed.") { }
        public ChannelClosedException(string message) : base(message) { }
        public ChannelClosedException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// 通道写入器
    /// </summary>
    public class ChannelWriter<T>
    {
        private readonly Channel<T> _channel;

        internal ChannelWriter(Channel<T> channel)
        {
            _channel = channel;
        }

        /// <summary>
        /// 尝试写入（非阻塞）
        /// </summary>
        public bool TryWrite(T item)
        {
            return _channel.TryWrite(item);
        }

        /// <summary>
        /// 异步写入
        /// </summary>
        public async Task WriteAsync(T item, CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_channel.TryWrite(item))
                    return;

                // 等待有空间
                await Task.Delay(1, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        /// <summary>
        /// 尝试写入（带超时）
        /// </summary>
        public bool TryWrite(T item, TimeSpan timeout)
        {
            var endTime = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < endTime)
            {
                if (_channel.TryWrite(item))
                    return true;
                Thread.SpinWait(1);
            }
            return false;
        }

        /// <summary>
        /// 标记通道完成
        /// </summary>
        public void Complete(Exception exception = null)
        {
            _channel.TryWriteComplete(exception);
        }
    }

    /// <summary>
    /// 通道读取器
    /// </summary>
    public class ChannelReader<T>
    {
        private readonly Channel<T> _channel;

        internal ChannelReader(Channel<T> channel)
        {
            _channel = channel;
        }

        /// <summary>
        /// 通道是否已完成
        /// </summary>
        public Task Completion => _channel.IsCompleted ? System.Threading.Tasks.Task.CompletedTask : System.Threading.Tasks.Task.FromException(new ChannelClosedException());

        /// <summary>
        /// 尝试读取（非阻塞）
        /// </summary>
        public bool TryRead(out T item)
        {
            return _channel.TryRead(out item);
        }

        /// <summary>
        /// 异步读取
        /// </summary>
        public async Task<T> ReadAsync(CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_channel.TryRead(out var item))
                    return item;

                if (_channel.IsCompleted)
                    throw new ChannelClosedException();

                await Task.Delay(1, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return default;
        }

        /// <summary>
        /// 尝试读取（带超时）
        /// </summary>
        public bool TryRead(out T item, TimeSpan timeout)
        {
            var endTime = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < endTime)
            {
                if (_channel.TryRead(out item))
                    return true;
                Thread.SpinWait(1);
            }
            item = default;
            return false;
        }

        /// <summary>
        /// 等待可以读取
        /// </summary>
        public async Task<bool> WaitToReadAsync(TimeSpan timeout)
        {
            var endTime = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < endTime)
            {
                if (!_channel.IsEmpty)
                    return true;
                if (_channel.IsCompleted)
                    return false;
                await Task.Delay(1);
            }
            return false;
        }
    }

    /// <summary>
    /// 轻量级任务
    /// </summary>
    public class ActionTask : IDisposable
    {
        private readonly Action _action;
        private volatile bool _completed;
        private Exception _exception;
        private readonly ManualResetEvent _event = new(false);

        public bool IsCompleted => _completed;
        public Exception Exception => _exception;

        public ActionTask(Action action)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public void Start()
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    _action();
                }
                catch (Exception ex)
                {
                    _exception = ex;
                }
                finally
                {
                    _completed = true;
                    _event.Set();
                }
            });
        }

        public void Wait()
        {
            _event.Wait();
        }

        public bool Wait(int millisecondsTimeout)
        {
            return _event.Wait(millisecondsTimeout);
        }

        public void Dispose()
        {
            _event.Dispose();
        }
    }

    /// <summary>
    /// 轻量级任务（带返回值）
    /// </summary>
    public class ActionTask<TResult> : IDisposable
    {
        private readonly Func<TResult> _func;
        private volatile bool _completed;
        private Exception _exception;
        private TResult _result;
        private readonly ManualResetEvent _event = new(false);

        public bool IsCompleted => _completed;
        public TResult Result => _completed ? _result : default;
        public Exception Exception => _exception;

        public ActionTask(Func<TResult> func)
        {
            _func = func ?? throw new ArgumentNullException(nameof(func));
        }

        public void Start()
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    _result = _func();
                }
                catch (Exception ex)
                {
                    _exception = ex;
                }
                finally
                {
                    _completed = true;
                    _event.Set();
                }
            });
        }

        public TResult Wait()
        {
            _event.Wait();
            if (_exception != null)
                throw _exception;
            return _result;
        }

        public bool Wait(int millisecondsTimeout)
        {
            if (!_event.Wait(millisecondsTimeout))
                return false;
            if (_exception != null)
                throw _exception;
            return true;
        }

        public void Dispose()
        {
            _event.Dispose();
        }
    }
}
