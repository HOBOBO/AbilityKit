using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

namespace AbilityKit.Core.Pooling
{
    /// <summary>
    /// Provides a thread-safe pool for reference-type elements. User factories and lifecycle callbacks run outside the pool lock.
    /// </summary>
    /// <typeparam name="T">The reference type stored by the pool.</typeparam>
    public sealed class ObjectPool<T> : IObjectPoolDebug, IObjectPoolControl where T : class
    {
        private readonly Func<T> _createFunc;
        private Action<T>? _onGet;
        private readonly Action<T>? _onRelease;
        private readonly Action<T>? _onDestroy;
        private readonly bool _collectionCheck;
        private readonly int _defaultCapacity;
        private readonly int _maxSize;
        private readonly PoolTrimPolicy _trimPolicy;
        private readonly bool _neverTrim;

        private readonly Stack<T> _stack;
        private readonly object _syncRoot = new object();
        private readonly HashSet<T>? _inactiveSet;
        private readonly HashSet<T>? _transitionSet;

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
        private int _pendingPrewarmSlots;
        private int _prewarmingCreatedCount;

        /// <summary>Creates a pool and prewarms its configured default capacity.</summary>
        /// <param name="options">The creation, lifecycle, capacity, and trimming options.</param>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">The factory or capacity settings are invalid.</exception>
        /// <remarks>If prewarming fails, every element already created by that prewarm attempt is destroyed before the exception is propagated.</remarks>
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
            if (_collectionCheck)
            {
                _inactiveSet = new HashSet<T>(ReferenceEqualityComparer.Instance);
                _transitionSet = new HashSet<T>(ReferenceEqualityComparer.Instance);
            }

            Prewarm(options.DefaultCapacity);
        }

        /// <summary>Gets the number of elements currently retained for reuse.</summary>
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

        /// <summary>Gets the number of acquired or lifecycle-transitioning elements.</summary>
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

        /// <summary>Gets the maximum number of inactive elements retained by this pool.</summary>
        public int MaxSize => _maxSize;

        /// <summary>Gets whether regular trim operations leave this pool unchanged.</summary>
        public bool NeverTrim => _neverTrim;

        /// <summary>Gets a thread-safe point-in-time snapshot of pool counters.</summary>
        public PoolStats Stats
        {
            get
            {
                lock (_syncRoot)
                {
                    return new PoolStats(
                        _createdTotal,
                        _getTotal,
                        _releaseTotal,
                        _stack.Count,
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
            lock (_syncRoot)
            {
                _onGet += onGet;
            }
        }

        /// <summary>Acquires an element, creating one when no inactive element is available.</summary>
        /// <returns>An initialized element owned by the caller until it is released.</returns>
        /// <exception cref="InvalidOperationException">The factory returns <see langword="null"/>.</exception>
        /// <remarks>If acquisition callbacks fail, the element is permanently destroyed before the original exception is propagated.</remarks>
        public T Get()
        {
            T? element;
            Action<T>? onGet;

            lock (_syncRoot)
            {
                _getTotal++;
                if (_stack.Count > 0)
                {
                    _hitCount++;
                    element = _stack.Pop();
                    _inactiveSet?.Remove(element);
                    BeginTransitionUnsafe(element);
                    UpdatePeakActiveCountUnsafe();
                    onGet = _onGet;
                }
                else
                {
                    _missCount++;
                    element = null;
                    onGet = null;
                }
            }

            if (element == null)
            {
                element = _createFunc();
                if (element == null)
                    throw new InvalidOperationException($"Pool createFunc returned null for type {typeof(T).FullName}");

                lock (_syncRoot)
                {
                    _createdTotal++;
                    BeginTransitionUnsafe(element);
                    UpdatePeakActiveCountUnsafe();
                    onGet = _onGet;
                }
            }

            try
            {
                element.TryOnPoolGet();
                onGet?.Invoke(element);
            }
            catch (Exception getException)
            {
                DestroyAfterFailedInitialization(element, getException);
                throw;
            }

            EndTransition(element);
            return element;
        }

        /// <summary>Acquires an element wrapped in an idempotent disposable return handle.</summary>
        /// <returns>A handle that returns the element to this pool when disposed.</returns>
        public PooledObject<T> GetPooled()
        {
            return new PooledObject<T>(this, Get());
        }

        /// <summary>Returns an element after running its release callbacks.</summary>
        /// <param name="element">The element to return.</param>
        /// <exception cref="ArgumentNullException"><paramref name="element"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">Collection checks detect a duplicate or reentrant release.</exception>
        /// <remarks>An element returned after the inactive capacity is full is permanently destroyed.</remarks>
        public void Release(T element)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));

            lock (_syncRoot)
            {
                _releaseTotal++;
                if (_collectionCheck && (_inactiveSet!.Contains(element) || _transitionSet!.Contains(element)))
                {
                    throw new InvalidOperationException($"Trying to release an object that is already in or transitioning through the pool: {typeof(T).FullName}");
                }

                BeginTransitionUnsafe(element);
            }

            try
            {
                element.TryOnPoolRelease();
                _onRelease?.Invoke(element);
            }
            catch
            {
                EndTransition(element);
                throw;
            }

            var destroy = false;
            lock (_syncRoot)
            {
                if (_stack.Count + _pendingPrewarmSlots >= _maxSize)
                {
                    _overflowDestroyCount++;
                    _destroyedTotal++;
                    destroy = true;
                }
                else
                {
                    _stack.Push(element);
                    _inactiveSet?.Add(element);
                }

                EndTransitionUnsafe(element);
            }

            if (destroy)
            {
                InvokeDestroyCallbacks(element);
            }
        }

        /// <summary>Removes all inactive elements.</summary>
        /// <param name="destroy">Whether permanent-destruction callbacks run for removed elements.</param>
        /// <remarks>All detached elements are processed even when a destruction callback throws; multiple failures are aggregated.</remarks>
        public void Clear(bool destroy = false)
        {
            List<T>? elements = null;
            lock (_syncRoot)
            {
                if (!destroy)
                {
                    _droppedInactiveCount += _stack.Count;
                    _stack.Clear();
                    _inactiveSet?.Clear();
                    return;
                }

                elements = DetachForDestructionUnsafe(_stack.Count, clearReason: true);
            }

            DestroyElements(elements);
        }

        /// <summary>Trims inactive elements using the configured policy.</summary>
        /// <returns>The number of inactive elements removed.</returns>
        public int Trim()
        {
            return Trim(_trimPolicy);
        }

        /// <summary>Trims inactive elements using the specified policy.</summary>
        /// <param name="policy">The policy that selects the retained inactive count.</param>
        /// <returns>The number of inactive elements removed, or zero when regular trimming is disabled.</returns>
        public int Trim(PoolTrimPolicy policy)
        {
            if (_neverTrim) return 0;
            return TrimCore(policy);
        }

        /// <summary>Creates up to the requested number of additional inactive elements without exceeding <see cref="MaxSize"/>.</summary>
        /// <param name="count">The maximum number of additional elements to create.</param>
        /// <remarks>Concurrent prewarm calls reserve capacity before invoking factories. Failed initialization destroys the affected element.</remarks>
        public void Prewarm(int count)
        {
            if (count <= 0) return;

            int reservedCount;
            lock (_syncRoot)
            {
                reservedCount = System.Math.Min(count, _maxSize - _stack.Count - _pendingPrewarmSlots);
                if (reservedCount <= 0) return;
                _pendingPrewarmSlots += reservedCount;
            }

            for (var index = 0; index < reservedCount; index++)
            {
                T element;
                try
                {
                    element = _createFunc();
                    if (element == null)
                        throw new InvalidOperationException($"Pool createFunc returned null for type {typeof(T).FullName}");
                }
                catch
                {
                    ReleasePrewarmReservations(reservedCount - index);
                    throw;
                }

                lock (_syncRoot)
                {
                    _createdTotal++;
                    _prewarmingCreatedCount++;
                    BeginTransitionUnsafe(element);
                }

                try
                {
                    element.TryOnPoolRelease();
                    _onRelease?.Invoke(element);
                }
                catch (Exception releaseException)
                {
                    lock (_syncRoot)
                    {
                        _prewarmingCreatedCount--;
                        _pendingPrewarmSlots -= reservedCount - index;
                        _destroyedTotal++;
                    }

                    DestroyAfterCommittedFailure(element, releaseException);
                    throw;
                }

                lock (_syncRoot)
                {
                    _prewarmingCreatedCount--;
                    _pendingPrewarmSlots--;
                    _stack.Push(element);
                    _inactiveSet?.Add(element);
                    EndTransitionUnsafe(element);
                }
            }
        }

        /// <summary>Trims inactive elements even when <see cref="NeverTrim"/> is set.</summary>
        /// <param name="policy">The policy that selects the retained inactive count.</param>
        /// <returns>The number of inactive elements removed.</returns>
        public int ForceTrim(PoolTrimPolicy policy)
        {
            return TrimCore(policy);
        }

        private int TrimCore(PoolTrimPolicy policy)
        {
            var targetInactiveCount = policy.ResolveTargetInactiveCount(_defaultCapacity);
            List<T>? elements;
            lock (_syncRoot)
            {
                elements = DetachForDestructionUnsafe(
                    System.Math.Max(0, _stack.Count - targetInactiveCount),
                    clearReason: false);
            }

            DestroyElements(elements);
            return elements?.Count ?? 0;
        }

        private List<T>? DetachForDestructionUnsafe(int count, bool clearReason)
        {
            if (count <= 0) return null;

            var elements = new List<T>(count);
            for (var index = 0; index < count; index++)
            {
                var element = _stack.Pop();
                _inactiveSet?.Remove(element);
                BeginTransitionUnsafe(element);
                elements.Add(element);
            }

            if (clearReason) _clearDestroyCount += elements.Count;
            else _trimDestroyCount += elements.Count;
            _destroyedTotal += elements.Count;
            return elements;
        }

        private void DestroyElements(List<T>? elements)
        {
            if (elements == null) return;

            List<Exception>? exceptions = null;
            for (var index = 0; index < elements.Count; index++)
            {
                try
                {
                    InvokeDestroyCallbacks(elements[index]);
                }
                catch (Exception exception)
                {
                    if (exceptions == null) exceptions = new List<Exception>();
                    exceptions.Add(exception);
                }
            }

            ThrowCapturedExceptions(exceptions);
        }

        private void DestroyAfterFailedInitialization(T element, Exception initializationException)
        {
            lock (_syncRoot)
            {
                _destroyedTotal++;
            }

            DestroyAfterCommittedFailure(element, initializationException);
        }

        private void DestroyAfterCommittedFailure(T element, Exception initializationException)
        {
            try
            {
                InvokeDestroyCallbacks(element);
            }
            catch (Exception destroyException)
            {
                throw new AggregateException(initializationException, destroyException);
            }
        }

        private void InvokeDestroyCallbacks(T element)
        {
            Exception? poolableException = null;
            Exception? callbackException = null;
            try
            {
                try
                {
                    element.TryOnPoolDestroy();
                }
                catch (Exception exception)
                {
                    poolableException = exception;
                }

                try
                {
                    _onDestroy?.Invoke(element);
                }
                catch (Exception exception)
                {
                    callbackException = exception;
                }
            }
            finally
            {
                EndTransition(element);
            }

            if (poolableException != null && callbackException != null)
                throw new AggregateException(poolableException, callbackException);
            if (poolableException != null) ExceptionDispatchInfo.Capture(poolableException).Throw();
            if (callbackException != null) ExceptionDispatchInfo.Capture(callbackException).Throw();
        }

        private void ReleasePrewarmReservations(int count)
        {
            lock (_syncRoot)
            {
                _pendingPrewarmSlots -= count;
            }
        }

        private void BeginTransitionUnsafe(T element)
        {
            _transitionSet?.Add(element);
        }

        private void EndTransition(T element)
        {
            lock (_syncRoot)
            {
                EndTransitionUnsafe(element);
            }
        }

        private void EndTransitionUnsafe(T element)
        {
            _transitionSet?.Remove(element);
        }

        private static void ThrowCapturedExceptions(List<Exception>? exceptions)
        {
            if (exceptions == null || exceptions.Count == 0) return;
            if (exceptions.Count == 1)
            {
                ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
                return;
            }

            throw new AggregateException(exceptions);
        }

        private int GetActiveCountUnsafe()
        {
            return System.Math.Max(
                0,
                _createdTotal - _destroyedTotal - _droppedInactiveCount - _stack.Count - _prewarmingCreatedCount);
        }

        private void UpdatePeakActiveCountUnsafe()
        {
            var active = GetActiveCountUnsafe();
            if (active > _peakActiveCount)
            {
                _peakActiveCount = active;
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
    }
}
