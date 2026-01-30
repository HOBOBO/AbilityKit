using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.HotReload
{
    public sealed class HotfixServiceOverlay : IWorldResolver
    {
        private readonly IWorldResolver _inner;
        private readonly Dictionary<Type, object> _overrides = new Dictionary<Type, object>();

        public HotfixServiceOverlay(IWorldResolver inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public void Set(Type serviceType, object instance)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            _overrides[serviceType] = instance;
        }

        public void Clear()
        {
            _overrides.Clear();
        }

        public object Resolve(Type serviceType)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));

            if (_overrides.TryGetValue(serviceType, out var obj) && obj != null)
            {
                return obj;
            }

            return _inner.Resolve(serviceType);
        }

        public T Resolve<T>()
        {
            return (T)Resolve(typeof(T));
        }

        public bool TryResolve(Type serviceType, out object instance)
        {
            if (serviceType == null)
            {
                instance = null;
                return false;
            }

            if (_overrides.TryGetValue(serviceType, out var obj) && obj != null)
            {
                instance = obj;
                return true;
            }

            return _inner.TryResolve(serviceType, out instance);
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
    }
}
