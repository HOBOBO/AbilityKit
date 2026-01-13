using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Share.Common.AttributeSystem
{
    public sealed class AttributeContext : IAttributeProvider
    {
        private readonly Dictionary<string, AttributeGroup> _groups = new Dictionary<string, AttributeGroup>(StringComparer.Ordinal);

        public event Action<string, AttributeId, float, float> AttributeChanged;

        public AttributeGroup GetOrCreateGroup(string group)
        {
            group ??= string.Empty;
            if (_groups.TryGetValue(group, out var g) && g != null) return g;

            g = new AttributeGroup(group, this);
            g.AttributeChanged += (id, oldV, newV) =>
            {
                AttributeChanged?.Invoke(group, id, oldV, newV);
                OnAttributeValueChanged(id);
            };
            _groups[group] = g;
            return g;
        }

        private void OnAttributeValueChanged(AttributeId id)
        {
            var reg = AttributeRegistry.Instance;
            if (reg == null) return;

            var dependents = reg.GetDependents(id);
            if (dependents == null || dependents.Count == 0) return;

            for (int i = 0; i < dependents.Count; i++)
            {
                var dep = dependents[i];
                if (!dep.IsValid) continue;
                var g = GetGroupFor(dep);
                g?.MarkDirty(dep);
            }
        }

        public AttributeGroup GetGroupFor(AttributeId id)
        {
            var group = AttributeRegistry.Instance.GetGroup(id) ?? string.Empty;
            return GetOrCreateGroup(group);
        }

        public float GetValue(AttributeId id)
        {
            return GetGroupFor(id).GetValue(id);
        }

        public void SetBase(AttributeId id, float baseValue)
        {
            GetGroupFor(id).SetBase(id, baseValue);
        }

        public AttributeModifierHandle AddModifier(AttributeId id, AttributeModifier modifier)
        {
            return GetGroupFor(id).AddModifier(id, modifier);
        }

        public bool RemoveModifier(AttributeId id, AttributeModifierHandle handle)
        {
            return GetGroupFor(id).RemoveModifier(id, handle);
        }

        public AttributeEffectHandle ApplyEffect(AttributeEffect effect)
        {
            if (effect == null || effect.Entries == null || effect.Entries.Length == 0) return null;

            var list = new List<AttributeEffectHandle.Entry>(effect.Entries.Length);
            for (int i = 0; i < effect.Entries.Length; i++)
            {
                var e = effect.Entries[i];
                if (!e.Attribute.IsValid) continue;
                var h = AddModifier(e.Attribute, e.Modifier);
                if (h.IsValid)
                {
                    list.Add(new AttributeEffectHandle.Entry(e.Attribute, h));
                }
            }

            return list.Count == 0 ? null : new AttributeEffectHandle(this, list);
        }
    }
}
