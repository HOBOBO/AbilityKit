using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace AbilityKit.Core.Pooling
{
    public sealed class ObjectPool<T> : IObjectPoolDebug, IObjectPoolControl where T : class
    {
        private readonly Func<T> _createFunc;
        private Action<T> _onGet;
        private readonly Action<T> _onRelease;
        private readonly Action<T> _onDestroy;
        private readonly bool _collectionCheck;
        private readonly int _defaultCapacity;
        private readonly int _maxSize;
        private readonly PoolTrimPolicy _trimPolicy;
        private readonly bool _neverTrim;

        private readonly Stack<T> _stack;
        private readonly object _syncRoot = new object();
        private readonly HashSet<T> _inactiveSet;

        private int _createdTotal;
        private int _destroyedTotal;
        private int _getTotal;
        private int _releaseTotal;
        private int _hitCount;
        private int _missCount;
        private int _peakActiveCount;
        private int _overflowDestroyCount;
        private int _clearDestroyCount;
        private int _droppedInactiveCount;
        private int _trimDestroyCount;

        public ObjectPool(ObjectPoolOptions<T> options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (options.CreateFunc == null) throw new ArgumentException("CreateFunc is required", nameof(options));
            if (options.MaxSize <= 0) throw new ArgumentException("MaxSize must be > 0", nameof(options));
            if (options.DefaultCapacity < 0) throw new ArgumentException("DefaultCapacity must be >= 0", nameof(options));

            _createFunc = options.CreateFunc;
            _onGet = options.OnGet;
            _onRelease = options.OnRelease;
            _onDestroy = options.OnDestroy;
            _collectionCheck = options.CollectionCheck;
            _defaultCapacity = options.DefaultCapacity;
            _maxSize = options.MaxSize;
            _trimPolicy = options.TrimPolicy;
            _neverTrim = options.NeverTrim;

            _stack = new Stack<T>(options.DefaultCapacity);

            _inactiveSet = _collectionCheck
                ? new HashSet<T>(ReferenceEqualityComparer.Instance)
                : null;

            Prewarm(options.DefaultCapacity);
        }

        public int InactiveCount
        {
            get
            {
                lock (_syncRoot)
                {
                    return _stack.Count;
                }
            }
        }

        public int ActiveCount
        {
            get
            {
                lock (_syncRoot)
                {
                    return GetActiveCountUnsafe();
                }
            }
        }

        public int MaxSize => _maxSize;

        public bool NeverTrim => _neverTrim;

        public PoolStats Stats
        {
            get
            {
                lock (_syncRoot)
                {
                    var inactive = _stack.Count;
                    return new PoolStats(
                        _createdTotal,
                        _getTotal,
                        _releaseTotal,
                        inactive,
                        GetActiveCountUnsafe(),
                        _peakActiveCount,
                        _hitCount,
                        _missCount,
                        _overflowDestroyCount,
                        _clearDestroyCount,
                        _droppedInactiveCount,
                        _trimDestroyCount);
                }
            }
        }

        Type IObjectPoolDebug.ElementType => typeof(T);
        PoolStats IObjectPoolDebug.Stats => Stats;
        int IObjectPoolDebug.MaxSize => _maxSize;
        bool IObjectPoolDebug.NeverTrim => _neverTrim;

        internal void AppendOnGet(Action<T> onGet)
        {
            if (onGet == null) return;
            _onGet += onGet;
        }

        public T Get()
        {
            lock (_syncRoot)
            {
                _getTotal++;

                if (_stack.Count > 0)
                {
                    _hitCount++;
                    var obj = _stack.Pop();

                    if (_collectionCheck) _inactiveSet.Remove(obj);

                    UpdatePeakActiveCountUnsafe();
                    obj.TryOnPoolGet();
                    _onGet?.Invoke(obj);
                    return obj;
                }

                _missCount++;
                var created = _createFunc();
                if (created == null) throw new InvalidOperationException($"Pool createFunc returned null for type {typeof(T).FullName}");

                _createdTotal++;
                UpdatePeakActiveCountUnsafe();
                created.TryOnPoolGet();
                _onGet?.Invoke(created);
                return created;
            }
        }

        public PooledObject<T> GetPooled()
        {
            return new PooledObject<T>(this, Get());
        }

        public void Release(T element)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));

            lock (_syncRoot)
            {
                _releaseTotal++;

                if (_collectionCheck)
                {
                    if (_inactiveSet.Contains(element))
                    {
                        throw new InvalidOperationException($"Trying to release an object that is already in the pool: {typeof(T).FullName}");
                    }
                }

                element.TryOnPoolRelease();
                _onRelease?.Invoke(element);

                if (_stack.Count >= _maxSize)
                {
                    DestroyElementUnsafe(element);
                    _overflowDestroyCount++;
                    return;
                }

                _stack.Push(element);

                if (_collectionCheck) _inactiveSet.Add(element);
            }
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<T>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            private ReferenceEqualityComparer()
            {
            }

            public bool Equals([AllowNull] T x, [AllowNull] T y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(T obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }

        public void Clear(bool destroy = false)
        {
            lock (_syncRoot)
            {
                if (!destroy)
                {
                    _droppedInactiveCount += _stack.Count;
                    _stack.Clear();
                    _inactiveSet?.Clear();
                    return;
                }

                while (_stack.Count > 0)
                {
                    var obj = _stack.Pop();
                    _inactiveSet?.Remove(obj);
                    DestroyElementUnsafe(obj);
                    _clearDestroyCount++;
                }
            }
        }

        public int Trim()
        {
            return Trim(_trimPolicy);
        }

        public int Trim(PoolTrimPolicy policy)
        {
            if (_neverTrim) return 0;

            var targetInactiveCount = policy.ResolveTargetInactiveCount(_defaultCapacity);

            lock (_syncRoot)
            {
                var trimmedCount = 0;
                while (_stack.Count > targetInactiveCount)
                {
                    var obj = _stack.Pop();
                    _inactiveSet?.Remove(obj);
                    DestroyElementUnsafe(obj);
                    _trimDestroyCount++;
                    trimmedCount++;
                }

                return trimmedCount;
            }
        }

        public void Prewarm(int count)
        {
            if (count <= 0) return;

            lock (_syncRoot)
            {
                if (_stack.Count + count > _maxSize)
                {
                    count = System.Math.Max(0, _maxSize - _stack.Count);
                }

                for (int i = 0; i < count; i++)
                {
                    var obj = _createFunc();
                    if (obj == null) throw new InvalidOperationException($"Pool createFunc returned null for type {typeof(T).FullName}");

                    _createdTotal++;
                    obj.TryOnPoolRelease();
                    _onRelease?.Invoke(obj);
                    _stack.Push(obj);

                    if (_collectionCheck) _inactiveSet.Add(obj);
                }
            }
        }

        public int ForceTrim(PoolTrimPolicy policy)
        {
            var targetInactiveCount = policy.ResolveTargetInactiveCount(_defaultCapacity);

            lock (_syncRoot)
            {
                var trimmedCount = 0;
                while (_stack.Count > targetInactiveCount)
                {
                    var obj = _stack.Pop();
                    _inactiveSet?.Remove(obj);
                    DestroyElementUnsafe(obj);
                    _trimDestroyCount++;
                    trimmedCount++;
                }

                return trimmedCount;
            }
        }

        private void DestroyElementUnsafe(T element)
        {
            _destroyedTotal++;
            element.TryOnPoolDestroy();
            _onDestroy?.Invoke(element);
        }

        private int GetActiveCountUnsafe()
        {
            return System.Math.Max(0, _createdTotal - _destroyedTotal - _droppedInactiveCount - _stack.Count);
        }

        private void UpdatePeakActiveCountUnsafe()
        {
            var active = GetActiveCountUnsafe();
            if (active > _peakActiveCount)
            {
                _peakActiveCount = active;
            }
        }
    }
}
