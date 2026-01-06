using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Share.Common.Pool
{
    public sealed class ObjectPool<T> where T : class
    {
        private readonly Func<T> _createFunc;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onRelease;
        private readonly Action<T> _onDestroy;
        private readonly bool _collectionCheck;
        private readonly int _maxSize;

        private readonly Stack<T> _stack;

        private int _createdTotal;
        private int _getTotal;
        private int _releaseTotal;

#if UNITY_EDITOR
        private readonly HashSet<T> _inactiveSet;
#endif

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
            _maxSize = options.MaxSize;

            _stack = new Stack<T>(options.DefaultCapacity);

#if UNITY_EDITOR
            _inactiveSet = _collectionCheck ? new HashSet<T>() : null;
#endif

            Prewarm(options.DefaultCapacity);
        }

        public int InactiveCount => _stack.Count;
        public int ActiveCount => _createdTotal - _stack.Count;

        public PoolStats Stats => new PoolStats(_createdTotal, _getTotal, _releaseTotal, InactiveCount, ActiveCount);

        public T Get()
        {
            _getTotal++;

            if (_stack.Count > 0)
            {
                var obj = _stack.Pop();

#if UNITY_EDITOR
                if (_collectionCheck) _inactiveSet.Remove(obj);
#endif

                obj.TryOnPoolGet();
                _onGet?.Invoke(obj);
                return obj;
            }

            var created = _createFunc();
            if (created == null) throw new InvalidOperationException($"Pool createFunc returned null for type {typeof(T).FullName}");

            _createdTotal++;
            created.TryOnPoolGet();
            _onGet?.Invoke(created);
            return created;
        }

        public PooledObject<T> GetPooled()
        {
            return new PooledObject<T>(this, Get());
        }

        public void Release(T element)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            _releaseTotal++;

#if UNITY_EDITOR
            if (_collectionCheck)
            {
                if (_inactiveSet.Contains(element))
                {
                    throw new InvalidOperationException($"Trying to release an object that is already in the pool: {typeof(T).FullName}");
                }
            }
#endif

            element.TryOnPoolRelease();
            _onRelease?.Invoke(element);

            if (_stack.Count >= _maxSize)
            {
                element.TryOnPoolDestroy();
                _onDestroy?.Invoke(element);
                return;
            }

            _stack.Push(element);

#if UNITY_EDITOR
            if (_collectionCheck) _inactiveSet.Add(element);
#endif
        }

        public void Clear(bool destroy = false)
        {
            if (!destroy)
            {
                _stack.Clear();
#if UNITY_EDITOR
                _inactiveSet?.Clear();
#endif
                return;
            }

            while (_stack.Count > 0)
            {
                var obj = _stack.Pop();
#if UNITY_EDITOR
                _inactiveSet?.Remove(obj);
#endif
                obj.TryOnPoolDestroy();
                _onDestroy?.Invoke(obj);
            }
        }

        public void Prewarm(int count)
        {
            if (count <= 0) return;

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

#if UNITY_EDITOR
                if (_collectionCheck) _inactiveSet.Add(obj);
#endif
            }
        }
    }
}
