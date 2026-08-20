using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace AbilityKit.Core.Pooling
{
    /// <summary>
    /// Owns keyed pools for one scope and coordinates thread-safe lookup, creation, trimming, clearing, and instance-based release.
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

        private sealed class ReleaseHandle
        {
            public IReleaseRegistration? Registration;
        }

        private interface IReleaseRegistration
        {
            object Pool { get; }

            void Release(object element);
        }

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

        /// <summary>Gets the existing keyed pool or atomically creates it from the supplied options.</summary>
        /// <typeparam name="T">The reference type stored by the pool.</typeparam>
        /// <param name="key">The pool key within <typeparamref name="T"/>.</param>
        /// <param name="options">Options used only when a pool must be created.</param>
        /// <returns>The shared pool registered for the type and normalized key.</returns>
        /// <remarks>Construction runs outside the manager lock. A failed construction is removed so a later call can retry.</remarks>
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

        /// <summary>Enables instance-based release for elements acquired from the specified pool.</summary>
        /// <typeparam name="T">The reference type stored by the pool.</typeparam>
        /// <param name="pool">The pool to register once by weak identity.</param>
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

        /// <summary>Attempts to return an instance to the most recent registered pool that acquired it.</summary>
        /// <param name="element">The instance to return; <see langword="null"/> is treated as already released.</param>
        /// <returns><see langword="true"/> when the instance is null or a release registration exists; otherwise <see langword="false"/>.</returns>
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

        /// <summary>Attempts to get a pool by element type and normalized key.</summary>
        /// <typeparam name="T">The reference type stored by the pool.</typeparam>
        /// <param name="key">The pool key.</param>
        /// <param name="pool">The matching pool on success. The failure value is unspecified for compatibility with the published signature.</param>
        /// <returns><see langword="true"/> when the pool exists; otherwise <see langword="false"/>.</returns>
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

        /// <summary>Unregisters a pool and clears its inactive elements.</summary>
        /// <typeparam name="T">The reference type stored by the pool.</typeparam>
        /// <param name="key">The pool key.</param>
        /// <param name="destroy">Whether destruction callbacks run for inactive elements.</param>
        /// <returns><see langword="true"/> when a pool was removed.</returns>
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

        /// <summary>Trims every currently registered pool using its configured policy.</summary>
        /// <returns>The total number of removed inactive elements.</returns>
        public int TrimAll()
        {
            return TrimAll(default(PoolTrimPolicy));
        }

        /// <summary>Trims every currently registered pool using the specified policy.</summary>
        /// <param name="policy">The trim policy applied to each pool.</param>
        /// <returns>The total number of removed inactive elements.</returns>
        /// <remarks>Operations run from a snapshot outside the manager lock. All pools are attempted before failures are propagated.</remarks>
        public int TrimAll(PoolTrimPolicy policy)
        {
            return ExecuteAll(control => control.Trim(policy));
        }

        /// <summary>Force-trims every currently registered pool using the specified policy.</summary>
        /// <param name="policy">The trim policy applied to each pool.</param>
        /// <returns>The total number of removed inactive elements.</returns>
        public int ForceTrimAll(PoolTrimPolicy policy)
        {
            return ExecuteAll(control => control.ForceTrim(policy));
        }

        /// <summary>Atomically unregisters all pools and then clears their inactive elements.</summary>
        /// <param name="destroy">Whether destruction callbacks run for inactive elements.</param>
        /// <remarks>All pools are attempted. One failure is rethrown unchanged; multiple failures produce an <see cref="AggregateException"/>.</remarks>
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
        /// <summary>Gets editor-only diagnostic snapshots for all registered pools.</summary>
        /// <returns>A point-in-time snapshot list.</returns>
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
