using System;
using System.Threading;

namespace AbilityKit.Threading
{
    /// <summary>
    /// 多生产者单消费者队列（最高性能）
    /// 适用于：工作队列、日志队列、消息队列
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    public sealed class MpscQueue<T>
    {
        // 节点定义
        private class Node
        {
            public T Value;
            public Node Next;
        }

        // 虚拟头节点
        private readonly Node _stub;
        
        // 生产者端
        private Node _tail;
        
        // 消费者端
        private Node _head;

        /// <summary>
        /// 队列长度（非精确）
        /// </summary>
        public int Count
        {
            get
            {
                var count = 0;
                var current = _head.Next;
                while (current != null)
                {
                    count++;
                    current = current.Next;
                }
                return count;
            }
        }

        /// <summary>
        /// 队列是否为空
        /// </summary>
        public bool IsEmpty => _head.Next == null;

        /// <summary>
        /// 创建一个新的 MPSC 队列
        /// </summary>
        public MpscQueue()
        {
            _stub = new Node { Value = default };
            _tail = _stub;
            _head = _stub;
        }

        /// <summary>
        /// 生产者入队（多线程安全）
        /// </summary>
        public void Enqueue(T item)
        {
            var node = new Node { Value = item };
            
            var oldTail = _tail;
            while (true)
            {
                var next = oldTail.Next;
                if (next != null)
                {
                    Interlocked.CompareExchange(ref _tail, next, oldTail);
                    oldTail = _tail;
                    continue;
                }

                if (Interlocked.CompareExchange(ref oldTail.Next, node, null) == null)
                {
                    Interlocked.CompareExchange(ref _tail, node, oldTail);
                    return;
                }
            }
        }

        /// <summary>
        /// 消费者出队（单线程安全）
        /// </summary>
        public bool TryDequeue(out T result)
        {
            var head = _head;
            var next = head.Next;

            if (next == null)
            {
                result = default;
                return false;
            }

            result = next.Value;
            _head = next;
            return true;
        }

        /// <summary>
        /// 消费者出队（单线程安全）
        /// </summary>
        public T Dequeue()
        {
            T result;
            while (!TryDequeue(out result))
            {
                Thread.SpinWait(1);
            }
            return result;
        }

        /// <summary>
        /// 清空队列
        /// </summary>
        public void Clear()
        {
            while (TryDequeue(out _))
            {
            }
        }
    }

    /// <summary>
    /// 多生产者多消费者队列（公平调度）
    /// </summary>
    public sealed class MpmcQueue<T>
    {
        private readonly object _lock = new();
        private readonly Node _stub;
        private Node _tail;
        private Node _head;
        private readonly int _capacity;
        private int _count;

        private class Node
        {
            public T Value;
            public Node Next;
        }

        public int Count => _count;
        public int Capacity => _capacity;
        public bool IsFull => _capacity > 0 && _count >= _capacity;
        public bool IsEmpty => _count == 0;

        /// <summary>
        /// 创建 MPMC 队列
        /// </summary>
        /// <param name="boundedCapacity">有界容量，-1 表示无界</param>
        public MpmcQueue(int boundedCapacity = -1)
        {
            _capacity = boundedCapacity > 0 ? boundedCapacity : -1;
            _stub = new Node { Value = default };
            _tail = _stub;
            _head = _stub;
            _count = 0;
        }

        /// <summary>
        /// 入队
        /// </summary>
        public void Enqueue(T item)
        {
            if (_capacity > 0)
            {
                lock (_lock)
                {
                    while (IsFull)
                    {
                        System.Threading.Monitor.Wait(_lock, 1);
                    }

                    var node = new Node { Value = item };
                    _tail.Next = node;
                    _tail = node;
                    _count++;
                    System.Threading.Monitor.Pulse(_lock);
                }
            }
            else
            {
                lock (_lock)
                {
                    var node = new Node { Value = item };
                    _tail.Next = node;
                    _tail = node;
                    _count++;
                    System.Threading.Monitor.Pulse(_lock);
                }
            }
        }

        /// <summary>
        /// 尝试入队（非阻塞）
        /// </summary>
        public bool TryEnqueue(T item)
        {
            lock (_lock)
            {
                if (_capacity > 0 && IsFull)
                    return false;

                var node = new Node { Value = item };
                _tail.Next = node;
                _tail = node;
                _count++;
                System.Threading.Monitor.Pulse(_lock);
                return true;
            }
        }

        /// <summary>
        /// 出队（阻塞直到有元素）
        /// </summary>
        public T Dequeue()
        {
            lock (_lock)
            {
                while (_count == 0)
                {
                    System.Threading.Monitor.Wait(_lock);
                }

                var node = _head.Next;
                var value = node.Value;
                _head = node;
                _count--;
                System.Threading.Monitor.Pulse(_lock);
                return value;
            }
        }

        /// <summary>
        /// 尝试出队（非阻塞）
        /// </summary>
        public bool TryDequeue(out T result)
        {
            lock (_lock)
            {
                if (_count == 0)
                {
                    result = default;
                    return false;
                }

                var node = _head.Next;
                result = node.Value;
                _head = node;
                _count--;
                System.Threading.Monitor.Pulse(_lock);
                return true;
            }
        }

        /// <summary>
        /// 窥视队首元素
        /// </summary>
        public bool TryPeek(out T result)
        {
            lock (_lock)
            {
                if (_count == 0)
                {
                    result = default;
                    return false;
                }

                result = _head.Next.Value;
                return true;
            }
        }

        /// <summary>
        /// 清空队列
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _head = _stub;
                _tail = _stub;
                _count = 0;
            }
        }
    }
}
