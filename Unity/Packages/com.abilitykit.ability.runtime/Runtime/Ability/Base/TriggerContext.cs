using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Common.Pool;

namespace AbilityKit.Ability.Triggering
{
    public sealed class TriggerContext : IPoolable, IDisposable
    {
        private static readonly ObjectPool<TriggerContext> _pool = Pools.GetPool(
            createFunc: () => new TriggerContext(),
            defaultCapacity: 64,
            maxSize: 1024,
            collectionCheck: false);

        private static readonly ObjectPool<Dictionary<string, object>> _varsPool = Pools.GetPool(
            createFunc: () => new Dictionary<string, object>(StringComparer.Ordinal),
            onRelease: dict => dict.Clear(),
            defaultCapacity: 32,
            maxSize: 1024,
            collectionCheck: false);

        private Dictionary<string, object> _vars;

        private bool _ownsVars;

        private IVarStore _globalVars;

        private bool _fromPool;

        private TriggerContext()
        {
            _fromPool = false;
            _ownsVars = false;
        }

        public TriggerContext(IServiceProvider services = null, object source = null, object target = null, IReadOnlyDictionary<string, object> vars = null)
        {
            Services = services;
            Source = source;
            Target = target;
            Event = default;
            _fromPool = false;

            _ownsVars = true;

            if (vars == null)
            {
                _vars = _varsPool.Get();
            }
            else
            {
                _vars = _varsPool.Get();
                foreach (var kv in vars)
                {
                    _vars[kv.Key] = kv.Value;
                }
            }
        }

        internal TriggerContext(IServiceProvider services, object source, object target, Dictionary<string, object> sharedLocalVars)
        {
            Services = services;
            Source = source;
            Target = target;
            Event = default;
            _fromPool = false;

            if (sharedLocalVars != null)
            {
                _vars = sharedLocalVars;
                _ownsVars = false;
            }
            else
            {
                _vars = _varsPool.Get();
                _ownsVars = true;
            }
        }

        public IServiceProvider Services { get; private set; }
        public object Source { get; private set; }
        public object Target { get; private set; }
        public TriggerEvent Event { get; internal set; }

        public static TriggerContext Rent()
        {
            return _pool.Get();
        }

        public static void Return(TriggerContext context)
        {
            if (context == null) return;
            if (!context._fromPool) return;
            _pool.Release(context);
        }

        public void Init(IServiceProvider services, object source, object target, Dictionary<string, object> sharedLocalVars)
        {
            Services = services;
            Source = source;
            Target = target;
            Event = default;
            _globalVars = null;

            if (_ownsVars && _vars != null)
            {
                _varsPool.Release(_vars);
            }

            if (sharedLocalVars != null)
            {
                _vars = sharedLocalVars;
                _ownsVars = false;
            }
            else
            {
                _vars = _varsPool.Get();
                _ownsVars = true;
            }
        }

        private IVarStore GetGlobalVars()
        {
            if (_globalVars != null) return _globalVars;

            if (Services != null)
            {
                try
                {
                    var s = Services.GetService(typeof(IVarStore));
                    if (s is IVarStore store)
                    {
                        _globalVars = store;
                        return _globalVars;
                    }
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, "[TriggerContext] resolve IVarStore failed");
                }
            }

            _globalVars = GlobalVarStoreAdapter.Instance;
            return _globalVars;
        }

        public bool TryGetVar<T>(string key, out T value)
        {
            return TryGetVar(VarScope.Local, key, out value);
        }

        public bool TryGetVar<T>(VarScope scope, string key, out T value)
        {
            if (key == null)
            {
                value = default;
                return false;
            }

            if (scope == VarScope.Global)
            {
                return GetGlobalVars().TryGet(key, out value);
            }

            if (_vars == null)
            {
                value = default;
                return false;
            }

            if (_vars.TryGetValue(key, out var obj) && obj is T t)
            {
                value = t;
                return true;
            }

            value = default;
            return false;
        }

        public bool TryGetVar(VarScope scope, string key, out object value)
        {
            if (key == null)
            {
                value = null;
                return false;
            }

            if (scope == VarScope.Global)
            {
                return GetGlobalVars().TryGet(key, out value);
            }

            if (_vars == null)
            {
                value = null;
                return false;
            }

            if (_vars.TryGetValue(key, out var obj))
            {
                value = obj;
                return true;
            }

            value = null;
            return false;
        }

        public void SetVar(string key, object value)
        {
            SetVar(VarScope.Local, key, value);
        }

        public void SetVar(VarScope scope, string key, object value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            if (scope == VarScope.Global)
            {
                GetGlobalVars().Set(key, value);
                return;
            }

            if (_vars == null) throw new InvalidOperationException("Local vars store is not initialized.");

            _vars[key] = value;
        }

        public bool TryGetArg<T>(string key, out T value)
        {
            var args = Event.Args;
            if (args != null && key != null && args.TryGetValue(key, out var obj) && obj is T t)
            {
                value = t;
                return true;
            }

            value = default;
            return false;
        }

        public void OnPoolGet()
        {
            _fromPool = true;
        }

        public void OnPoolRelease()
        {
            Services = null;
            Source = null;
            Target = null;
            Event = default;

            if (_ownsVars && _vars != null)
            {
                _varsPool.Release(_vars);
            }

            _vars = null;
            _ownsVars = false;
            _globalVars = null;
        }

        public void OnPoolDestroy()
        {
            OnPoolRelease();
        }

        public void Dispose()
        {
            if (_ownsVars && _vars != null)
            {
                _varsPool.Release(_vars);
                _vars = null;
                _ownsVars = false;
            }

            _globalVars = null;

            if (_fromPool)
            {
                Return(this);
            }
        }
    }
}
