using System;
using System.Collections.Generic;

namespace AbilityKit.Effects.Core
{
    public sealed class EffectRegistry
    {
        private readonly Dictionary<EffectScopeKey, List<EffectInstance>> _instancesByScope = new();
        private static readonly List<EffectInstance> EmptyList = new List<EffectInstance>(0);

        public void Register(EffectInstance instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            if (!_instancesByScope.TryGetValue(instance.Scope, out var list))
            {
                list = new List<EffectInstance>();
                _instancesByScope.Add(instance.Scope, list);
            }

            list.Add(instance);
        }

        public bool Unregister(EffectInstance instance)
        {
            if (instance == null) return false;

            if (!_instancesByScope.TryGetValue(instance.Scope, out var list)) return false;

            return list.Remove(instance);
        }

        public IReadOnlyList<EffectInstance> GetInstances(in EffectScopeKey scope)
        {
            if (_instancesByScope.TryGetValue(scope, out var list))
            {
                return list;
            }

            return EmptyList;
        }
    }
}
