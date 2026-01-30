using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.World.DI
{
    public sealed class WorldScope : IWorldScope, IWorldServices
    {
        private readonly WorldContainer _root;
        private readonly Dictionary<Type, object> _scoped = new Dictionary<Type, object>();
        private readonly List<object> _disposeOrder = new List<object>(32);
        private bool _disposed;

        internal WorldScope(WorldContainer root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        public IWorldServices Root => _root;

        public object Resolve(Type serviceType)
        {
            ThrowIfDisposed();
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));

            if (serviceType == typeof(IWorldServices)) return this;
            if (serviceType == typeof(IWorldServiceContainer)) return _root;
            if (serviceType == typeof(IWorldScope)) return this;
            if (serviceType == typeof(WorldScope)) return this;

            return _root.ResolveScoped(serviceType, this);
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

        internal object GetOrCreate(Type type, Func<object> factory)
        {
            if (_scoped.TryGetValue(type, out var cached)) return cached;
            var created = factory();
            _scoped[type] = created;
            _disposeOrder.Add(created);
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

            for (int i = _disposeOrder.Count - 1; i >= 0; i--)
            {
                try
                {
                    if (_disposeOrder[i] is IDisposable d) d.Dispose();
                }
                catch
                {
                }
            }

            _disposeOrder.Clear();
            _scoped.Clear();
        }
    }
}
