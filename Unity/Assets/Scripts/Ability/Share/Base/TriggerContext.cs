using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Triggering
{
    public sealed class TriggerContext
    {
        private readonly Dictionary<string, object> _vars;

        public TriggerContext(IServiceProvider services = null, object source = null, object target = null, IReadOnlyDictionary<string, object> vars = null)
        {
            Services = services;
            Source = source;
            Target = target;
            Event = default;

            if (vars == null)
            {
                _vars = new Dictionary<string, object>(StringComparer.Ordinal);
            }
            else
            {
                _vars = new Dictionary<string, object>(vars.Count, StringComparer.Ordinal);
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

            _vars = sharedLocalVars ?? new Dictionary<string, object>(StringComparer.Ordinal);
        }

        public IServiceProvider Services { get; }
        public object Source { get; }
        public object Target { get; }
        public TriggerEvent Event { get; internal set; }

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
                return GlobalVarStore.TryGet(key, out value);
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
                return GlobalVarStore.TryGet(key, out value);
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
                GlobalVarStore.Set(key, value);
                return;
            }

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
    }
}
