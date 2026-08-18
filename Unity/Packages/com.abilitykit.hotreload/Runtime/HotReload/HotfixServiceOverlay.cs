#nullable enable
using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.HotReload
{
    /// <summary>Resolves per-hotfix overrides before falling back to world services.</summary>
    public sealed class HotfixServiceOverlay : IWorldResolver
    {
        private readonly IWorldResolver _inner;
        private readonly Dictionary<Type, object> _overrides = new Dictionary<Type, object>();

        /// <summary>Creates an overlay over a world resolver.</summary>
        public HotfixServiceOverlay(IWorldResolver inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        /// <summary>Sets a non-null service override for an exact service type.</summary>
        public void Set(Type serviceType, object instance)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            _overrides[serviceType] = instance;
        }

        /// <summary>Removes one service override.</summary>
        public bool Remove(Type serviceType)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            return _overrides.Remove(serviceType);
        }

        /// <summary>Removes all overrides without disposing their instances.</summary>
        public void Clear()
        {
            _overrides.Clear();
        }

        /// <summary>Resolves an exact service type from overrides or the inner resolver.</summary>
        public object Resolve(Type serviceType)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));

            if (_overrides.TryGetValue(serviceType, out var obj))
            {
                return obj;
            }

            return _inner.Resolve(serviceType);
        }

        /// <summary>Resolves a service from overrides or the inner resolver.</summary>
        public T Resolve<T>()
        {
            return (T)Resolve(typeof(T));
        }

        /// <summary>Attempts to resolve an exact service type.</summary>
        public bool TryResolve(Type serviceType, out object instance)
        {
            if (serviceType == null)
            {
                instance = null!;
                return false;
            }

            if (_overrides.TryGetValue(serviceType, out var obj))
            {
                instance = obj;
                return true;
            }

            return _inner.TryResolve(serviceType, out instance);
        }

        /// <summary>Attempts to resolve a service.</summary>
        public bool TryResolve<T>(out T instance)
        {
            if (TryResolve(typeof(T), out var obj) && obj is T t)
            {
                instance = t;
                return true;
            }

            instance = default!;
            return false;
        }
    }
}
