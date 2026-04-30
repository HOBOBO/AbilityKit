using System;
using System.Collections.Generic;
using System.Threading;

namespace AbilityKit.Threading
{
    /// <summary>
    /// 并发字典
    /// 基于细粒度锁实现
    /// </summary>
    public sealed class ConcurrentDictionary<TKey, TValue> where TKey : notnull
    {
        private readonly int _bucketCount;
        private readonly Bucket<TKey, TValue>[] _buckets;
        private readonly IEqualityComparer<TKey> _comparer;
        private int _count;

        private class Bucket<K, V>
        {
            public readonly object Lock = new();
            public Dictionary<K, V> Items = new();
        }

        public int Count => _count;
        public bool IsEmpty => _count == 0;

        public ConcurrentDictionary() : this(31, EqualityComparer<TKey>.Default) { }

        public ConcurrentDictionary(int bucketCount) : this(bucketCount, EqualityComparer<TKey>.Default) { }

        public ConcurrentDictionary(IEqualityComparer<TKey> comparer) : this(31, comparer) { }

        public ConcurrentDictionary(int bucketCount, IEqualityComparer<TKey> comparer)
        {
            _bucketCount = bucketCount;
            _buckets = new Bucket<TKey, TValue>[bucketCount];
            _comparer = comparer ?? EqualityComparer<TKey>.Default;

            for (int i = 0; i < bucketCount; i++)
            {
                _buckets[i] = new Bucket<TKey, TValue>();
            }
        }

        private int GetBucketIndex(TKey key)
        {
            var hash = _comparer.GetHashCode(key);
            return (hash & 0x7FFFFFFF) % _bucketCount;
        }

        /// <summary>
        /// 尝试获取值
        /// </summary>
        public bool TryGet(TKey key, out TValue value)
        {
            var index = GetBucketIndex(key);
            var bucket = _buckets[index];

            lock (bucket.Lock)
            {
                return bucket.Items.TryGetValue(key, out value);
            }
        }

        /// <summary>
        /// 添加或更新值
        /// </summary>
        public void Set(TKey key, TValue value)
        {
            var index = GetBucketIndex(key);
            var bucket = _buckets[index];

            lock (bucket.Lock)
            {
                if (bucket.Items.ContainsKey(key))
                {
                    bucket.Items[key] = value;
                }
                else
                {
                    bucket.Items[key] = value;
                    Interlocked.Increment(ref _count);
                }
            }
        }

        /// <summary>
        /// 添加值（如果不存在）
        /// </summary>
        public bool TryAdd(TKey key, TValue value)
        {
            var index = GetBucketIndex(key);
            var bucket = _buckets[index];

            lock (bucket.Lock)
            {
                if (bucket.Items.ContainsKey(key))
                {
                    return false;
                }

                bucket.Items[key] = value;
                Interlocked.Increment(ref _count);
                return true;
            }
        }

        /// <summary>
        /// 添加或更新值
        /// </summary>
        public TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
        {
            var index = GetBucketIndex(key);
            var bucket = _buckets[index];

            lock (bucket.Lock)
            {
                if (bucket.Items.TryGetValue(key, out var existing))
                {
                    return existing;
                }

                var value = factory(key);
                bucket.Items[key] = value;
                Interlocked.Increment(ref _count);
                return value;
            }
        }

        /// <summary>
        /// 尝试移除值
        /// </summary>
        public bool TryRemove(TKey key, out TValue value)
        {
            var index = GetBucketIndex(key);
            var bucket = _buckets[index];

            lock (bucket.Lock)
            {
                if (bucket.Items.TryGetValue(key, out value))
                {
                    bucket.Items.Remove(key);
                    Interlocked.Decrement(ref _count);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 移除所有项
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < _bucketCount; i++)
            {
                var bucket = _buckets[i];
                lock (bucket.Lock)
                {
                    bucket.Items.Clear();
                }
            }
            Interlocked.Exchange(ref _count, 0);
        }

        /// <summary>
        /// 获取所有键
        /// </summary>
        public TKey[] GetKeys()
        {
            var keys = new List<TKey>(_count);
            for (int i = 0; i < _bucketCount; i++)
            {
                var bucket = _buckets[i];
                lock (bucket.Lock)
                {
                    foreach (var kvp in bucket.Items)
                    {
                        keys.Add(kvp.Key);
                    }
                }
            }
            return keys.ToArray();
        }

        /// <summary>
        /// 获取所有值
        /// </summary>
        public TValue[] GetValues()
        {
            var values = new List<TValue>(_count);
            for (int i = 0; i < _bucketCount; i++)
            {
                var bucket = _buckets[i];
                lock (bucket.Lock)
                {
                    foreach (var kvp in bucket.Items)
                    {
                        values.Add(kvp.Value);
                    }
                }
            }
            return values.ToArray();
        }

        /// <summary>
        /// 获取所有键值对
        /// </summary>
        public KeyValuePair<TKey, TValue>[] ToArray()
        {
            var items = new List<KeyValuePair<TKey, TValue>>(_count);
            for (int i = 0; i < _bucketCount; i++)
            {
                var bucket = _buckets[i];
                lock (bucket.Lock)
                {
                    foreach (var kvp in bucket.Items)
                    {
                        items.Add(new KeyValuePair<TKey, TValue>(kvp.Key, kvp.Value));
                    }
                }
            }
            return items.ToArray();
        }

        /// <summary>
        /// 是否包含键
        /// </summary>
        public bool ContainsKey(TKey key)
        {
            var index = GetBucketIndex(key);
            var bucket = _buckets[index];

            lock (bucket.Lock)
            {
                return bucket.Items.ContainsKey(key);
            }
        }

        /// <summary>
        /// 尝试获取或添加
        /// </summary>
        public bool TryGetOrAdd(TKey key, TValue value, out TValue existingValue)
        {
            var index = GetBucketIndex(key);
            var bucket = _buckets[index];

            lock (bucket.Lock)
            {
                if (bucket.Items.TryGetValue(key, out existingValue))
                {
                    return true;
                }

                bucket.Items[key] = value;
                Interlocked.Increment(ref _count);
                existingValue = value;
                return false;
            }
        }
    }

    /// <summary>
    /// 并发列表
    /// </summary>
    public sealed class ConcurrentList<T>
    {
        private readonly object _lock = new();
        private T[] _items = Array.Empty<T>();
        private int _count;

        public int Count => _count;

        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= _count)
                    throw new IndexOutOfRangeException();

                lock (_lock)
                {
                    return _items[index];
                }
            }
            set
            {
                if (index < 0 || index >= _count)
                    throw new IndexOutOfRangeException();

                lock (_lock)
                {
                    _items[index] = value;
                }
            }
        }

        public void Add(T item)
        {
            lock (_lock)
            {
                if (_count >= _items.Length)
                {
                    var newItems = new T[Math.Max(_items.Length * 2, 4)];
                    Array.Copy(_items, newItems, _count);
                    _items = newItems;
                }
                _items[_count++] = item;
            }
        }

        public bool TryAdd(T item)
        {
            lock (_lock)
            {
                if (_count >= _items.Length)
                {
                    return false;
                }
                _items[_count++] = item;
                return true;
            }
        }

        public void AddRange(IEnumerable<T> items)
        {
            lock (_lock)
            {
                foreach (var item in items)
                {
                    if (_count >= _items.Length)
                    {
                        var newItems = new T[Math.Max(_items.Length * 2, 4)];
                        Array.Copy(_items, newItems, _count);
                        _items = newItems;
                    }
                    _items[_count++] = item;
                }
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _items = Array.Empty<T>();
                _count = 0;
            }
        }

        public bool Contains(T item)
        {
            lock (_lock)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (EqualityComparer<T>.Default.Equals(_items[i], item))
                        return true;
                }
                return false;
            }
        }

        public T[] ToArray()
        {
            lock (_lock)
            {
                var result = new T[_count];
                Array.Copy(_items, result, _count);
                return result;
            }
        }

        public bool TryRemove(T item)
        {
            lock (_lock)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (EqualityComparer<T>.Default.Equals(_items[i], item))
                    {
                        Array.Copy(_items, i + 1, _items, i, _count - i - 1);
                        _count--;
                        return true;
                    }
                }
                return false;
            }
        }

        public void RemoveAt(int index)
        {
            lock (_lock)
            {
                if (index < 0 || index >= _count)
                    throw new IndexOutOfRangeException();

                Array.Copy(_items, index + 1, _items, index, _count - index - 1);
                _count--;
            }
        }
    }

    /// <summary>
    /// 并发 HashSet
    /// </summary>
    public sealed class ConcurrentHashSet<T>
    {
        private readonly int _bucketCount;
        private readonly Bucket<T>[] _buckets;
        private readonly IEqualityComparer<T> _comparer;
        private int _count;

        private class Bucket<TItem>
        {
            public readonly object Lock = new();
            public HashSet<TItem> Items = new();
        }

        public int Count => _count;

        public ConcurrentHashSet() : this(31, EqualityComparer<T>.Default) { }

        public ConcurrentHashSet(int bucketCount) : this(bucketCount, EqualityComparer<T>.Default) { }

        public ConcurrentHashSet(IEqualityComparer<T> comparer) : this(31, comparer) { }

        public ConcurrentHashSet(int bucketCount, IEqualityComparer<T> comparer)
        {
            _bucketCount = bucketCount;
            _buckets = new Bucket<T>[bucketCount];
            _comparer = comparer ?? EqualityComparer<T>.Default;

            for (int i = 0; i < bucketCount; i++)
            {
                _buckets[i] = new Bucket<T>();
            }
        }

        private int GetBucketIndex(T item)
        {
            var hash = _comparer.GetHashCode(item);
            return (hash & 0x7FFFFFFF) % _bucketCount;
        }

        public bool Add(T item)
        {
            var index = GetBucketIndex(item);
            var bucket = _buckets[index];

            lock (bucket.Lock)
            {
                if (bucket.Items.Contains(item))
                {
                    return false;
                }

                bucket.Items.Add(item);
                Interlocked.Increment(ref _count);
                return true;
            }
        }

        public bool Contains(T item)
        {
            var index = GetBucketIndex(item);
            var bucket = _buckets[index];

            lock (bucket.Lock)
            {
                return bucket.Items.Contains(item);
            }
        }

        public bool TryRemove(T item)
        {
            var index = GetBucketIndex(item);
            var bucket = _buckets[index];

            lock (bucket.Lock)
            {
                if (bucket.Items.Remove(item))
                {
                    Interlocked.Decrement(ref _count);
                    return true;
                }
                return false;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _bucketCount; i++)
            {
                var bucket = _buckets[i];
                lock (bucket.Lock)
                {
                    bucket.Items.Clear();
                }
            }
            Interlocked.Exchange(ref _count, 0);
        }
    }

    /// <summary>
    /// 并发栈（LIFO）
    /// </summary>
    public sealed class ConcurrentStack<T>
    {
        private volatile Node _head;
        private int _count;

        private class Node
        {
            public T Value;
            public Node Next;
        }

        public int Count => _count;

        public void Push(T item)
        {
            var node = new Node { Value = item };
            node.Next = _head;
            while (Interlocked.CompareExchange(ref _head, node, node.Next) != node.Next)
            {
                // CAS 失败，重试
            }
            Interlocked.Increment(ref _count);
        }

        public bool TryPop(out T result)
        {
            var head = _head;
            if (head == null)
            {
                result = default;
                return false;
            }

            if (Interlocked.CompareExchange(ref _head, head.Next, head) == head)
            {
                Interlocked.Decrement(ref _count);
                result = head.Value;
                return true;
            }

            result = default;
            return false;
        }

        public T Pop()
        {
            if (!TryPop(out var result))
            {
                throw new InvalidOperationException("Stack is empty");
            }
            return result;
        }

        public void Clear()
        {
            while (TryPop(out _)) { }
        }
    }
}
