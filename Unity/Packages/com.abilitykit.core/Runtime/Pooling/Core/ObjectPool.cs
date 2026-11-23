using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

namespace AbilityKit.Core.Pooling
{
    /// <summary>
    /// 提供引用类型对象的后进先出复用池；内部存储受锁保护，工厂和生命周期回调在锁外同步执行，用户回调须自行保证并发安全。
    /// </summary>
    /// <typeparam name="T">池中保存的引用类型。</typeparam>
    public sealed partial class ObjectPool<T> : IObjectPoolDebug, IObjectPoolControl where T : class
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

        /// <summary>
        /// 创建对象池并按默认容量预热，预热数量不超过最大空闲容量。
        /// </summary>
        /// <param name="options">可变创建选项，仅在池创建时读取；不得并发修改。</param>
        /// <exception cref="ArgumentNullException">参数为空引用。</exception>
        /// <exception cref="ArgumentException">工厂或容量设置不合法。</exception>
        /// <remarks>构造预热失败时清理此前成功预热的对象，再传播原异常；清理也失败时聚合异常。</remarks>
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

            try
            {
                Prewarm(options.DefaultCapacity);
            }
            catch (Exception initializationException)
            {
                /*
                 * 构造未完成时调用方拿不到池，必须清理此前已成功预热的对象。
                 * 运行时显式预热仍保留已完成的部分，不使用这里的构造回滚。
                 */
                try
                {
                    Clear(destroy: true);
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException(initializationException, cleanupException);
                }

                throw;
            }
        }

        /// <summary>
        /// 获取当前留在空闲栈中可供复用的对象数量。
        /// </summary>
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

        /// <summary>
        /// 获取尚未归还或处于获取及归还转换中的对象计数；不包含预热中的对象，销毁承诺已提交的对象也不包含。
        /// </summary>
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

        /// <summary>
        /// 获取最大空闲保留数量；不是活动对象数量的上限。
        /// </summary>
        public int MaxSize => _maxSize;

        /// <summary>
        /// 获取是否跳过常规裁剪；不影响强制裁剪、溢出销毁及清空。
        /// </summary>
        public bool NeverTrim => _neverTrim;

        /// <summary>
        /// 在池锁内获取同一时点的计数快照；回调可能尚未完成，计数不表示回调成功次数。
        /// </summary>
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

        /// <summary>
        /// 优先从空闲栈获取对象，无空闲对象时创建；生命周期回调在池锁外同步执行。
        /// </summary>
        /// <returns>初始化成功的对象，调用方持有到归还结束。</returns>
        /// <exception cref="InvalidOperationException">工厂返回空引用。</exception>
        /// <remarks>获取回调失败先销毁对象，再传播原异常；销毁也失败时聚合两个异常。</remarks>
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

        /// <summary>
        /// 获取对象并包装为值类型归还句柄；句柄及其副本合计只能释放一次，不提供幂等释放。
        /// </summary>
        /// <returns>必须恰好归还一次的值类型句柄，不得通过多个副本重复释放。</returns>
        public PooledObject<T> GetPooled()
        {
            return new PooledObject<T>(this, Get());
        }

        /// <summary>
        /// 执行归还回调后保留对象；满容量则销毁。回调失败直接传播，对象不入空闲栈，调用方仍负责处理。
        /// </summary>
        /// <param name="element">要归还的非空对象；成功归还后调用方不得继续使用。</param>
        /// <exception cref="ArgumentNullException">参数为空引用。</exception>
        /// <exception cref="InvalidOperationException">引用检查发现重复或转换期间的归还。</exception>
        /// <remarks>空闲容量已满或被预热预留时，对象承诺销毁；回调异常不撤销销毁计数。</remarks>
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
                    EndTransitionUnsafe(element);
                }
                /*
                 * 溢出对象在销毁回调完成前仍处于转换中，防止回调将同一对象重新归还。
                 * 销毁路径统一由 InvokeDestroyCallbacks 的 finally 解除转换标记。
                 */
            }

            if (destroy)
            {
                InvokeDestroyCallbacks(element);
            }
        }

        /// <summary>
        /// 额外创建不超过请求数量的空闲对象；先预留容量，非正数不执行，已完成的预热在后续失败时仍保留。
        /// </summary>
        /// <param name="count">本次额外预热数量的上限，非正数不执行。</param>
        /// <remarks>并发预热先预留容量再执行工厂；初始化失败销毁当前对象并释放剩余预留，已完成部分继续保留。</remarks>
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

        /// <summary>
        /// 以对象引用身份执行转换及空闲集合检查，避免元素的值相等逻辑干扰重复归还检测。
        /// </summary>
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
