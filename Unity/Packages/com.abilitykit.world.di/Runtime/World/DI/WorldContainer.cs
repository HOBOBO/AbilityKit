using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Logging;

namespace AbilityKit.Ability.World.DI
{
    public sealed class WorldContainer : IWorldResolver, IWorldServiceContainer, IDisposable
    {
        private readonly Dictionary<Type, WorldServiceDescriptor> _map;
        private readonly Dictionary<Type, object> _singletons = new Dictionary<Type, object>();
        private readonly HashSet<object> _initialized = new HashSet<object>(ReferenceEqualityComparer.Instance);
        private readonly List<object> _singletonDisposeOrder = new List<object>(32);
        private readonly HashSet<object> _singletonDisposeSet = new HashSet<object>(ReferenceEqualityComparer.Instance);
        [ThreadStatic] private static Stack<Type> _singletonCreationStack;
        [ThreadStatic] private static Stack<Type> _resolveStack;
        private bool _disposed;

        public WorldContainer(IEnumerable<WorldServiceDescriptor> descriptors)
        {
            _map = new Dictionary<Type, WorldServiceDescriptor>();
            foreach (var d in descriptors)
            {
                _map[d.ServiceType] = d;
            }
        }

        public IReadOnlyCollection<Type> RegisteredServiceTypes => _map.Keys;

        public bool IsRegistered(Type serviceType)
        {
            if (serviceType == null) return false;
            if (serviceType == typeof(IWorldServiceContainer)) return true;
            if (serviceType == typeof(WorldContainer)) return true;
            return _map.ContainsKey(serviceType);
        }

        public WorldServiceDescriptor GetDescriptor(Type serviceType)
        {
            if (serviceType == typeof(IWorldServiceContainer) || serviceType == typeof(WorldContainer))
            {
                return default;
            }
            if (_map.TryGetValue(serviceType, out var descriptor))
            {
                return descriptor;
            }
            return default;
        }

        public WorldScope CreateScope()
        {
            ThrowIfDisposed();
            return new WorldScope(this);
        }

        /// <summary>
        /// 创建一个 scope 并在返回前把跨阶段输入「播种」进去。
        ///
        /// 用于解决「容器构造期一次建好、但 per-scope 数据此时尚不存在」的矛盾：
        /// <paramref name="configure"/> 在 scope 创建后、首次解析前执行，可通过
        /// <see cref="IWorldScopeSeeder"/> 注入实例。播种实例的生命周期归调用方，
        /// <c>scope.Dispose()</c> 不接管其释放。
        /// </summary>
        public WorldScope CreateScope(Action<IWorldScopeSeeder> configure)
        {
            ThrowIfDisposed();
            var scope = new WorldScope(this);
            if (configure != null)
            {
                configure(new ScopeSeeder(scope));
            }
            return scope;
        }

        private sealed class ScopeSeeder : IWorldScopeSeeder
        {
            private readonly WorldScope _scope;

            public ScopeSeeder(WorldScope scope)
            {
                _scope = scope;
            }

            public IWorldScopeSeeder Seed(Type serviceType, object instance)
            {
                _scope.SeedInstance(serviceType, instance);
                return this;
            }

            public IWorldScopeSeeder Seed<TService>(TService instance)
            {
                _scope.SeedInstance(typeof(TService), instance);
                return this;
            }
        }

        public object Resolve(Type serviceType)
        {
            ThrowIfDisposed();
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));

            _resolveStack ??= new Stack<Type>(8);
            _resolveStack.Push(serviceType);
            try
            {
                if (serviceType == typeof(IWorldServiceContainer)) return this;
                if (serviceType == typeof(WorldContainer)) return this;

                if (!_map.TryGetValue(serviceType, out var descriptor))
                {
                    throw new InvalidOperationException($"Service not registered: {serviceType.FullName}. Resolve chain: {FormatResolveChain()}");
                }

                if (descriptor.Lifetime == WorldLifetime.Singleton)
                {
                    if (_singletons.TryGetValue(serviceType, out var cached)) return cached;

                    _singletonCreationStack ??= new Stack<Type>(4);
                    _singletonCreationStack.Push(serviceType);
                    try
                    {
                        var created = descriptor.Factory(this);
                        if (descriptor.Ownership == WorldServiceOwnership.Container)
                        {
                            TryInit(created, this);
                        }
                        _singletons[serviceType] = created;

                        if (descriptor.Ownership == WorldServiceOwnership.Container &&
                            created != null &&
                            _singletonDisposeSet.Add(created))
                        {
                            _singletonDisposeOrder.Add(created);
                        }

                        return created;
                    }
                    finally
                    {
                        _singletonCreationStack.Pop();
                    }
                }

                if (descriptor.Lifetime == WorldLifetime.Transient)
                {
                    var created = descriptor.Factory(this);
                    TryInit(created, this);
                    return created;
                }

                if (_singletonCreationStack != null && _singletonCreationStack.Count > 0)
                {
                    var singleton = _singletonCreationStack.Peek();
                    throw new InvalidOperationException($"Singleton service '{singleton.FullName}' cannot resolve scoped service '{serviceType.FullName}'. Scoped services must not be captured by singletons. Resolve chain: {FormatResolveChain()}");
                }

                throw new InvalidOperationException($"Cannot resolve scoped service from root container: {serviceType.FullName}");
            }
            finally
            {
                _resolveStack.Pop();
            }
        }

        public T Resolve<T>()
        {
            return (T)Resolve(typeof(T));
        }

        public bool TryResolve(Type serviceType, out object instance)
        {
            try
            {
                instance = Resolve(serviceType);
                return true;
            }
            catch
            {
                instance = null;
                return false;
            }
        }

        public bool TryResolve<T>(out T instance)
        {
            if (TryResolve(typeof(T), out var obj) && obj is T t)
            {
                instance = t;
                return true;
            }

            instance = default;
            return false;
        }

        internal object ResolveScoped(Type serviceType, WorldScope scope)
        {
            ThrowIfDisposed();

            _resolveStack ??= new Stack<Type>(8);

            if (!_map.TryGetValue(serviceType, out var descriptor))
            {
                throw new InvalidOperationException($"Service not registered: {serviceType.FullName}. Resolve chain: {FormatResolveChain()}");
            }

            // Attribute registration exposes contracts as aliases whose factories resolve the
            // shared implementation type. Tracking that implementation here would make every
            // alias resolve look like a self-cycle before its factory can create the instance.
            // Service contracts are sufficient to identify real dependency cycles.
            if (_resolveStack.Contains(serviceType))
            {
                throw new InvalidOperationException($"Circular dependency detected. Resolve chain: {FormatResolveChain()} -> {serviceType.FullName}");
            }

            _resolveStack.Push(serviceType);
            try
            {
                switch (descriptor.Lifetime)
                {
                    case WorldLifetime.Singleton:
                        return Resolve(serviceType);
                    case WorldLifetime.Scoped:
                        return scope.GetOrCreate(serviceType, () =>
                        {
                            var created = descriptor.Factory(scope);
                            TryInit(created, scope);
                            return created;
                        });
                    case WorldLifetime.Transient:
                        var created = descriptor.Factory(scope);
                        TryInit(created, scope);
                        return created;
                    default:
                        throw new InvalidOperationException($"Unknown lifetime: {descriptor.Lifetime}");
                }
            }
            finally
            {
                _resolveStack.Pop();
            }
        }

        private static string FormatResolveChain()
        {
            if (_resolveStack == null || _resolveStack.Count == 0) return "<empty>";

            // Stack 默认从栈顶到栈底枚举；反转后按 root -> leaf 展示。
            var arr = _resolveStack.ToArray();
            if (arr == null || arr.Length == 0) return "<empty>";

            var sb = new System.Text.StringBuilder(128);
            for (int i = arr.Length - 1; i >= 0; i--)
            {
                var t = arr[i];
                if (t == null) continue;
                if (sb.Length > 0) sb.Append(" -> ");
                sb.Append(t.FullName ?? t.Name);
            }
            return sb.ToString();
        }

        private void TryInit(object instance, IWorldResolver services)
        {
            if (instance == null) return;
            if (!_initialized.Add(instance)) return;

            if (instance is IWorldInitializable init)
            {
                init.OnInit(services);
            }
        }

        private void TryDeinit(object instance, IWorldResolver services)
        {
            if (instance == null) return;

            if (instance is IWorldDeinitializable deinit)
            {
                deinit.OnDeinit(services);
            }
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(WorldContainer));
        }

        public void Dispose()
        {
            if (_disposed) return;

            for (int i = _singletonDisposeOrder.Count - 1; i >= 0; i--)
            {
                var instance = _singletonDisposeOrder[i];
                try
                {
                    TryDeinit(instance, this);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, "[WorldContainer] singleton deinit failed");
                }

                try
                {
                    if (instance is IDisposable d) d.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, "[WorldContainer] singleton dispose failed");
                }
            }

            _disposed = true;
            _singletonDisposeOrder.Clear();
            _singletonDisposeSet.Clear();
            _singletons.Clear();
            _map.Clear();
        }
    }
}
