using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace AbilityKit.Core.Pooling
{
    /// <summary>
    /// 持有一个作用域内按类型和键区分的池，支持同步查找、延迟创建和快照式批量控制；不提供关闭状态。
    /// </summary>
    public sealed class PoolManager
    {
        private readonly Dictionary<(Type type, PoolKey key), Lazy<object>> _pools =
            new Dictionary<(Type, PoolKey), Lazy<object>>();
        private readonly ConditionalWeakTable<object, Lazy<bool>> _releaseRegistrations =
            new ConditionalWeakTable<object, Lazy<bool>>();
        private readonly object _gate = new object();
        private readonly ConditionalWeakTable<object, ReleaseHandle> _releaseHandles =
            new ConditionalWeakTable<object, ReleaseHandle>();

        /// <summary>
        /// 保存实例最近一次获取时登记的池归还入口，不消耗登记也不证明实例当前仍借出。
        /// </summary>
        private sealed class ReleaseHandle
        {
            public IReleaseRegistration? Registration;
        }

        /// <summary>
        /// 为已登记的具体泛型池提供按实例归还和引用身份查询。
        /// </summary>
        private interface IReleaseRegistration
        {
            object Pool { get; }

            void Release(object element);
        }

        /// <summary>
        /// 将非泛型实例归还调用转发至已知的泛型对象池，保持其校验及异常语义。
        /// </summary>
        private sealed class ReleaseRegistration<T> : IReleaseRegistration where T : class
        {
            private readonly ObjectPool<T> _pool;

            public ReleaseRegistration(ObjectPool<T> pool)
            {
                _pool = pool;
            }

            public object Pool => _pool;

            public void Release(object element)
            {
                _pool.Release((T)element);
            }
        }

        /// <summary>
        /// 按类型及规范化键原子登记延迟构造的池；同键共享实例，构造和用户回调不持有管理器锁。
        /// </summary>
        /// <typeparam name="T">池中保存的引用类型。</typeparam>
        /// <param name="key">元素类型及作用域内的池键，管理器入口会规范化默认结构值。</param>
        /// <param name="options">可变创建选项，仅在池创建时读取；不得并发修改。</param>
        /// <returns>指定元素类型及规范化键共享的对象池。</returns>
        /// <remarks>构造在管理器锁外执行；构造失败时仅移除对应旧登记，允许后续重试。</remarks>
        public ObjectPool<T> GetOrCreate<T>(PoolKey key, ObjectPoolOptions<T> options) where T : class
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            key = PoolKey.Normalize(key);
            var poolKey = (typeof(T), key);
            Lazy<object> entry;
            lock (_gate)
            {
                if (!_pools.TryGetValue(poolKey, out entry!))
                {
                    entry = new Lazy<object>(
                        () => new ObjectPool<T>(options),
                        LazyThreadSafetyMode.ExecutionAndPublication);
                    _pools.Add(poolKey, entry);
                }
            }

            ObjectPool<T> pool;
            try
            {
                pool = (ObjectPool<T>)entry.Value;
            }
            catch
            {
                RemoveFaultedEntry(poolKey, entry);
                throw;
            }

            RegisterForObjectRelease(pool);
            return pool;
        }

        /// <summary>
        /// 为指定池登记按实例归还的获取回调；按弱引用身份只登记一次，不追溯之前已取出的对象。
        /// </summary>
        /// <typeparam name="T">池中保存的引用类型。</typeparam>
        /// <param name="pool">需要登记按实例归还回调的对象池。</param>
        public void RegisterForObjectRelease<T>(ObjectPool<T> pool) where T : class
        {
            if (pool == null) throw new ArgumentNullException(nameof(pool));

            Lazy<bool> registration;
            lock (_gate)
            {
                registration = _releaseRegistrations.GetValue(
                    pool,
                    _ => new Lazy<bool>(
                        () =>
                        {
                            pool.AppendOnGet(obj => RegisterReleaseHandle(pool, obj));
                            return true;
                        },
                        LazyThreadSafetyMode.ExecutionAndPublication));
            }

            try
            {
                _ = registration.Value;
            }
            catch
            {
                lock (_gate)
                {
                    if (_releaseRegistrations.TryGetValue(pool, out var current) && ReferenceEquals(current, registration))
                    {
                        _releaseRegistrations.Remove(pool);
                    }
                }

                throw;
            }
        }

        /// <summary>
        /// 尝试按实例归还到最近一次获取它的已登记池；空对象返回 true，回调异常直接传播。
        /// </summary>
        /// <param name="element">需要归还的对象；空值行为由具体入口的摘要说明。</param>
        /// <returns>空对象或找到归还记录且归还未抛异常时返回 true，否则返回 false。</returns>
        public bool TryRelease(object element)
        {
            if (element == null) return true;
            if (_releaseHandles.TryGetValue(element, out var handle))
            {
                var registration = Volatile.Read(ref handle.Registration);
                if (registration != null)
                {
                    registration.Release(element);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 按元素类型和规范化键查询池；已有的延迟构造会在锁外执行或等待，构造异常直接传播。
        /// </summary>
        /// <typeparam name="T">池中保存的引用类型。</typeparam>
        /// <param name="key">元素类型及作用域内的池键，管理器入口会规范化默认结构值。</param>
        /// <param name="pool">成功时输出对应池；失败时不得使用输出，当前实现为空引用。</param>
        /// <returns>找到池且构造成功时返回 true，未登记时返回 false。</returns>
        public bool TryGet<T>(PoolKey key, out ObjectPool<T> pool) where T : class
        {
            key = PoolKey.Normalize(key);
            Lazy<object> entry;
            lock (_gate)
            {
                if (!_pools.TryGetValue((typeof(T), key), out entry!))
                {
                    pool = null!;
                    return false;
                }
            }

            pool = (ObjectPool<T>)entry.Value;
            return true;
        }

        /// <summary>
        /// 先注销池，再清理其空闲对象；回调失败不会重新登记池，也不使旧池引用失效。
        /// </summary>
        /// <typeparam name="T">池中保存的引用类型。</typeparam>
        /// <param name="key">元素类型及作用域内的池键，管理器入口会规范化默认结构值。</param>
        /// <param name="destroy">是否对被移除的空闲对象执行销毁回调。</param>
        /// <returns>找到并注销池且清理未抛异常时返回 true，否则返回 false。</returns>
        public bool Remove<T>(PoolKey key, bool destroy = false) where T : class
        {
            key = PoolKey.Normalize(key);
            Lazy<object> entry;
            lock (_gate)
            {
                if (!_pools.TryGetValue((typeof(T), key), out entry!)) return false;
                _pools.Remove((typeof(T), key));
            }

            var pool = (ObjectPool<T>)entry.Value;
            pool.Clear(destroy);
            return true;
        }

        /// <summary>
        /// 按各池配置的策略裁剪当前池快照；禁止常规裁剪的池保持不变，所有池均尝试后再传播异常。
        /// </summary>
        /// <returns>移除的空闲对象总数；发生异常时抛出而不返回部分数量。</returns>
        public int TrimAll()
        {
            return ExecuteAll(control => control.Trim());
        }

        /// <summary>
        /// 按指定策略裁剪当前池快照；禁止常规裁剪的池保持不变，所有池均尝试后再传播异常。
        /// </summary>
        /// <param name="policy">决定空闲保留目标的策略。</param>
        /// <returns>移除的空闲对象总数；发生异常时抛出而不返回部分数量。</returns>
        /// <remarks>从登记快照在管理器锁外执行操作，先尝试所有池再传播异常；不保证跨池原子性。</remarks>
        public int TrimAll(PoolTrimPolicy policy)
        {
            return ExecuteAll(control => control.Trim(policy));
        }

        /// <summary>
        /// 按指定策略强制裁剪当前池快照，忽略禁止常规裁剪标志；先尝试所有池再传播异常。
        /// </summary>
        /// <param name="policy">决定空闲保留目标的策略。</param>
        /// <returns>移除的空闲对象总数；发生异常时抛出而不返回部分数量。</returns>
        public int ForceTrimAll(PoolTrimPolicy policy)
        {
            return ExecuteAll(control => control.ForceTrim(policy));
        }

        /// <summary>
        /// 先在锁内注销全部池，再在锁外清理其空闲对象；不回收调用方仍持有的对象。
        /// </summary>
        /// <param name="destroy">是否对被移除的空闲对象执行销毁回调。</param>
        /// <remarks>先尝试所有池；一个异常原样传播，多个异常聚合。</remarks>
        public void ClearAll(bool destroy = false)
        {
            List<Lazy<object>> entries;
            lock (_gate)
            {
                entries = new List<Lazy<object>>(_pools.Values);
                _pools.Clear();
            }

            List<Exception>? exceptions = null;
            for (var index = 0; index < entries.Count; index++)
            {
                try
                {
                    if (entries[index].Value is IObjectPoolControl control)
                    {
                        control.Clear(destroy);
                    }
                }
                catch (Exception exception)
                {
                    AddException(ref exceptions, exception);
                }
            }

            ThrowCapturedExceptions(exceptions);
        }

#if UNITY_EDITOR
        /// <summary>
        /// 获取已注册池的编辑器诊断快照；可能触发延迟构造，各池分别取样。
        /// </summary>
        /// <returns>诊断快照列表，各池分别取样。</returns>
        public IReadOnlyList<PoolDebugSnapshot> GetDebugSnapshots()
        {
            var entries = SnapshotEntries();
            if (entries.Count == 0) return Array.Empty<PoolDebugSnapshot>();

            var list = new List<PoolDebugSnapshot>(entries.Count);
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                var value = entry.Value.Value;
                if (value is IObjectPoolDebug debug)
                {
                    list.Add(new PoolDebugSnapshot(entry.Key.type, entry.Key.key, debug.Stats, debug.MaxSize, debug.NeverTrim));
                }
                else
                {
                    list.Add(new PoolDebugSnapshot(entry.Key.type, entry.Key.key, default, maxSize: 0));
                }
            }

            return list;
        }
#endif

        private void RegisterReleaseHandle<T>(ObjectPool<T> pool, T element) where T : class
        {
            if (element == null) return;
            var handle = _releaseHandles.GetValue(element, static _ => new ReleaseHandle());
            var registration = Volatile.Read(ref handle.Registration);
            if (ReferenceEquals(registration?.Pool, pool)) return;

            lock (handle)
            {
                registration = handle.Registration;
                if (ReferenceEquals(registration?.Pool, pool)) return;
                Volatile.Write(ref handle.Registration, new ReleaseRegistration<T>(pool));
            }
        }

        private int ExecuteAll(Func<IObjectPoolControl, int> operation)
        {
            var entries = SnapshotValues();
            var result = 0;
            List<Exception>? exceptions = null;
            for (var index = 0; index < entries.Count; index++)
            {
                try
                {
                    if (entries[index].Value is IObjectPoolControl control)
                    {
                        result += operation(control);
                    }
                }
                catch (Exception exception)
                {
                    AddException(ref exceptions, exception);
                }
            }

            ThrowCapturedExceptions(exceptions);
            return result;
        }

        private List<Lazy<object>> SnapshotValues()
        {
            lock (_gate)
            {
                return new List<Lazy<object>>(_pools.Values);
            }
        }

#if UNITY_EDITOR
        private List<KeyValuePair<(Type type, PoolKey key), Lazy<object>>> SnapshotEntries()
        {
            lock (_gate)
            {
                return new List<KeyValuePair<(Type type, PoolKey key), Lazy<object>>>(_pools);
            }
        }
#endif

        private void RemoveFaultedEntry((Type type, PoolKey key) poolKey, Lazy<object> entry)
        {
            lock (_gate)
            {
                if (_pools.TryGetValue(poolKey, out var current) && ReferenceEquals(current, entry))
                {
                    _pools.Remove(poolKey);
                }
            }
        }

        private static void AddException(ref List<Exception>? exceptions, Exception exception)
        {
            if (exceptions == null) exceptions = new List<Exception>();
            exceptions.Add(exception);
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
    }
}
