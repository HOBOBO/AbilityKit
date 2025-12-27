using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.World.DI
{
    public sealed class WorldScope : IWorldScope
    {
        private readonly WorldContainer _root;
        private readonly Dictionary<Type, object> _scoped = new Dictionary<Type, object>();
        private bool _disposed;

        internal WorldScope(WorldContainer root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        public IWorldResolver Root => _root;

        public object Resolve(Type serviceType)
        {
            ThrowIfDisposed();
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));

            if (serviceType == typeof(IWorldResolver)) return this;
            if (serviceType == typeof(IWorldScope)) return this;
            if (serviceType == typeof(WorldScope)) return this;

            return _root.ResolveScoped(serviceType, this);
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

        internal object GetOrCreate(Type type, Func<object> factory)
        {
            if (_scoped.TryGetValue(type, out var cached)) return cached;
            var created = factory();
            _scoped[type] = created;
            return created;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(WorldScope));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var kv in _scoped)
            {
                if (kv.Value is IDisposable d) d.Dispose();
            }

            _scoped.Clear();
        }
    }
}
