using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Share.Common.AttributeSystem
{
    public sealed class AttributeGroup
    {
        internal struct AttributeSlot
        {
            public float BaseValue;
            public float Cached;
            public bool Dirty;

            public float Add;
            public float Mul;
            public float FinalAdd;
            public float Override;
            public bool HasOverride;
        }

        private readonly string _name;
        private readonly AttributeContext _ctx;
        private readonly Dictionary<int, AttributeInstance> _attrs = new Dictionary<int, AttributeInstance>(64);
        private AttributeInstance[] _byId = new AttributeInstance[64];
        private AttributeSlot[] _slots = new AttributeSlot[64];

        public AttributeGroup(string name, AttributeContext ctx)
        {
            _name = name;
            _ctx = ctx;
        }

        public string Name => _name;

        public IReadOnlyDictionary<int, AttributeInstance> Attributes => _attrs;

        public event Action<AttributeId, float, float> AttributeChanged;

        public AttributeInstance GetOrCreate(AttributeId id)
        {
            if (!id.IsValid) throw new ArgumentException("Invalid AttributeId", nameof(id));

            EnsureCapacity(id.Id);
            var inst = _byId[id.Id];
            if (inst != null)
            {
                return inst;
            }

            if (_attrs.TryGetValue(id.Id, out inst) && inst != null)
            {
                _byId[id.Id] = inst;
                return inst;
            }

            var baseValue = AttributeRegistry.Instance.GetDefaultBaseValue(id);
            ref var slot = ref _slots[id.Id];
            slot.BaseValue = baseValue;
            slot.Cached = 0f;
            slot.Dirty = true;
            slot.Add = 0f;
            slot.Mul = 0f;
            slot.FinalAdd = 0f;
            slot.Override = 0f;
            slot.HasOverride = false;

            inst = new AttributeInstance(this, id, _ctx);
            inst.Changed += (a, oldV, newV) => AttributeChanged?.Invoke(a, oldV, newV);
            _attrs[id.Id] = inst;
            _byId[id.Id] = inst;
            return inst;
        }

        public bool TryGet(AttributeId id, out AttributeInstance inst)
        {
            inst = null;
            if (!id.IsValid) return false;

            if (id.Id >= 0 && id.Id < _byId.Length)
            {
                inst = _byId[id.Id];
                if (inst != null) return true;
            }

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

        internal void MarkDirty(AttributeId id)
        {
            if (!id.IsValid) return;

            AttributeInstance inst = null;
            if (id.Id >= 0 && id.Id < _byId.Length)
            {
                inst = _byId[id.Id];
            }
            if (inst == null)
            {
                _attrs.TryGetValue(id.Id, out inst);
                if (inst != null)
                {
                    EnsureCapacity(id.Id);
                    _byId[id.Id] = inst;
                }
            }

            if (inst != null)
            {
                inst.MarkDirtyByDependency();
            }
        }

        internal ref AttributeSlot GetSlotRef(int rawId)
        {
            return ref _slots[rawId];
        }

        private void EnsureCapacity(int rawId)
        {
            if (rawId < 0) return;
            if (rawId < _byId.Length && rawId < _slots.Length) return;

            var newSize = _byId.Length;
            if (newSize <= 0) newSize = 4;
            while (rawId >= newSize)
            {
                newSize *= 2;
            }
            Array.Resize(ref _byId, newSize);
            Array.Resize(ref _slots, newSize);
        }
    }
}
