using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Share.Common.AttributeSystem
{
    public sealed class AttributeGroup
    {
        private readonly string _name;
        private readonly Dictionary<int, AttributeInstance> _attrs = new Dictionary<int, AttributeInstance>(64);

        public AttributeGroup(string name)
        {
            _name = name;
        }

        public string Name => _name;

        public event Action<AttributeId, float, float> AttributeChanged;

        public AttributeInstance GetOrCreate(AttributeId id)
        {
            if (!id.IsValid) throw new ArgumentException("Invalid AttributeId", nameof(id));

            if (_attrs.TryGetValue(id.Id, out var inst) && inst != null)
            {
                return inst;
            }

            var baseValue = AttributeRegistry.Instance.GetDefaultBaseValue(id);
            inst = new AttributeInstance(id, baseValue);
            inst.Changed += (a, oldV, newV) => AttributeChanged?.Invoke(a, oldV, newV);
            _attrs[id.Id] = inst;
            return inst;
        }

        public bool TryGet(AttributeId id, out AttributeInstance inst)
        {
            inst = null;
            if (!id.IsValid) return false;
            return _attrs.TryGetValue(id.Id, out inst) && inst != null;
        }

        public float GetValue(AttributeId id)
        {
            return GetOrCreate(id).Value;
        }

        public void SetBase(AttributeId id, float baseValue)
        {
            GetOrCreate(id).BaseValue = baseValue;
        }

        public AttributeModifierHandle AddModifier(AttributeId id, AttributeModifier modifier)
        {
            return GetOrCreate(id).AddModifier(modifier);
        }

        public bool RemoveModifier(AttributeId id, AttributeModifierHandle handle)
        {
            if (!TryGet(id, out var inst) || inst == null) return false;
            return inst.RemoveModifier(handle);
        }

        public void ClearModifiers(AttributeId id, int sourceId = 0)
        {
            if (!TryGet(id, out var inst) || inst == null) return;
            inst.ClearModifiers(sourceId);
        }
    }
}
