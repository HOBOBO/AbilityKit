using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.World.DI
{
    public sealed class WorldContainer : IWorldServices, IWorldServiceContainer, IDisposable
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
            if (serviceType == typeof(IWorldServices)) return true;
            if (serviceType == typeof(IWorldServiceContainer)) return true;
            if (serviceType == typeof(WorldContainer)) return true;
            return _map.ContainsKey(serviceType);
        }

        public WorldScope CreateScope()
        {
            ThrowIfDisposed();
            return new WorldScope(this);
        }

        public object Resolve(Type serviceType)
        {
            ThrowIfDisposed();
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));

            _resolveStack ??= new Stack<Type>(8);
            _resolveStack.Push(serviceType);
            try
            {
                if (serviceType == typeof(IWorldServices)) return this;
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
                        TryInit(created, this);
                        _singletons[serviceType] = created;

                        if (created != null && _singletonDisposeSet.Add(created))
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

        public T Get<T>()
        {
            return Resolve<T>();
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

        public bool TryGet<T>(out T instance)
        {
            return TryResolve(out instance);
        }

        internal object ResolveScoped(Type serviceType, WorldScope scope)
        {
            ThrowIfDisposed();

            _resolveStack ??= new Stack<Type>(8);
            _resolveStack.Push(serviceType);
            try
            {
                if (!_map.TryGetValue(serviceType, out var descriptor))
                {
                    throw new InvalidOperationException($"Service not registered: {serviceType.FullName}. Resolve chain: {FormatResolveChain()}");
                }

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

            // Stack enumerates from top to bottom; reverse to show root -> leaf.
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

        private void TryInit(object instance, IWorldServices services)
        {
            if (instance == null) return;
            if (!_initialized.Add(instance)) return;

            if (instance is IWorldInitializable init)
            {
                init.OnInit(services);
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
            _disposed = true;

            for (int i = _singletonDisposeOrder.Count - 1; i >= 0; i--)
            {
                try
                {
                    if (_singletonDisposeOrder[i] is IDisposable d) d.Dispose();
                }
                catch
                {
                }
            }

            _singletonDisposeOrder.Clear();
            _singletonDisposeSet.Clear();
            _singletons.Clear();
            _map.Clear();
        }
    }
}
