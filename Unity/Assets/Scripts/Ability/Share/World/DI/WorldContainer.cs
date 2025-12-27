using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.World.DI
{
    public sealed class WorldContainer : IWorldResolver, IDisposable
    {
        private readonly Dictionary<Type, WorldServiceDescriptor> _map;
        private readonly Dictionary<Type, object> _singletons = new Dictionary<Type, object>();
        private bool _disposed;

        public WorldContainer(IEnumerable<WorldServiceDescriptor> descriptors)
        {
            _map = new Dictionary<Type, WorldServiceDescriptor>();
            foreach (var d in descriptors)
            {
                _map[d.ServiceType] = d;
            }
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

            if (serviceType == typeof(IWorldResolver)) return this;
            if (serviceType == typeof(WorldContainer)) return this;

            if (!_map.TryGetValue(serviceType, out var descriptor))
            {
                throw new InvalidOperationException($"Service not registered: {serviceType.FullName}");
            }

            if (descriptor.Lifetime == WorldLifetime.Singleton)
            {
                if (_singletons.TryGetValue(serviceType, out var cached)) return cached;
                var created = descriptor.Factory(this);
                _singletons[serviceType] = created;
                return created;
            }

            if (descriptor.Lifetime == WorldLifetime.Transient)
            {
                return descriptor.Factory(this);
            }

            throw new InvalidOperationException($"Cannot resolve scoped service from root container: {serviceType.FullName}");
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
            if (!_map.TryGetValue(serviceType, out var descriptor))
            {
                throw new InvalidOperationException($"Service not registered: {serviceType.FullName}");
            }

            switch (descriptor.Lifetime)
            {
                case WorldLifetime.Singleton:
                    return Resolve(serviceType);
                case WorldLifetime.Scoped:
                    return scope.GetOrCreate(serviceType, () => descriptor.Factory(scope));
                case WorldLifetime.Transient:
                    return descriptor.Factory(scope);
                default:
                    throw new InvalidOperationException($"Unknown lifetime: {descriptor.Lifetime}");
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(WorldContainer));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var kv in _singletons)
            {
                if (kv.Value is IDisposable d) d.Dispose();
            }

            _singletons.Clear();
            _map.Clear();
        }
    }
}
