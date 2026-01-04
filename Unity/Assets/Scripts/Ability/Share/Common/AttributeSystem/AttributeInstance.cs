using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Share.Common.AttributeSystem
{
    public sealed class AttributeInstance
    {
        private readonly AttributeId _id;

        private float _baseValue;
        private float _cached;
        private bool _dirty;

        private int _nextHandle;
        private readonly Dictionary<int, AttributeModifier> _modifiers = new Dictionary<int, AttributeModifier>(16);

        private float _add;
        private float _mul;
        private float _finalAdd;
        private float _override;
        private bool _hasOverride;

        public AttributeInstance(AttributeId id, float baseValue)
        {
            if (!id.IsValid) throw new ArgumentException("Invalid AttributeId", nameof(id));
            _id = id;
            _baseValue = baseValue;
            _dirty = true;
            _nextHandle = 1;
        }

        public AttributeId Id => _id;

        public float BaseValue
        {
            get => _baseValue;
            set
            {
                if (System.Math.Abs(_baseValue - value) < 0.00001f) return;
                _baseValue = value;
                _dirty = true;
            }
        }

        public float Value
        {
            get
            {
                if (_dirty)
                {
                    Recompute();
                }

                return _cached;
            }
        }

        public event Action<AttributeId, float, float> Changed;

        public AttributeModifierHandle AddModifier(AttributeModifier modifier)
        {
            var handle = _nextHandle++;
            _modifiers[handle] = modifier;
            ApplyModifier(modifier);
            MarkDirty();
            return new AttributeModifierHandle(handle);
        }

        public bool RemoveModifier(AttributeModifierHandle handle)
        {
            if (!handle.IsValid) return false;
            if (!_modifiers.TryGetValue(handle.Value, out var m)) return false;

            _modifiers.Remove(handle.Value);
            RebuildAggregates();
            MarkDirty();
            return true;
        }

        public void ClearModifiers(int sourceId = 0)
        {
            if (_modifiers.Count == 0) return;

            if (sourceId == 0)
            {
                _modifiers.Clear();
            }
            else
            {
                var toRemove = new List<int>();
                foreach (var kv in _modifiers)
                {
                    if (kv.Value.SourceId == sourceId) toRemove.Add(kv.Key);
                }
                for (int i = 0; i < toRemove.Count; i++)
                {
                    _modifiers.Remove(toRemove[i]);
                }
            }

            RebuildAggregates();
            MarkDirty();
        }

        public AttributeModifierSet GetModifierSet()
        {
            return new AttributeModifierSet(_add, _mul, _finalAdd, _override, _hasOverride);
        }

        private void MarkDirty()
        {
            _dirty = true;
        }

        private void Recompute()
        {
            var old = _cached;

            var formula = AttributeRegistry.Instance.GetFormula(_id);
            var v = formula.Evaluate(_baseValue, GetModifierSet());

            var constraint = AttributeRegistry.Instance.GetConstraint(_id);
            if (constraint != null)
            {
                v = constraint.Apply(_id, v);
            }

            _cached = v;
            _dirty = false;

            if (System.Math.Abs(old - v) > 0.00001f)
            {
                Changed?.Invoke(_id, old, v);
            }
        }

        private void ApplyModifier(AttributeModifier modifier)
        {
            switch (modifier.Op)
            {
                case AttributeModifierOp.Add:
                    _add += modifier.Value;
                    break;
                case AttributeModifierOp.Mul:
                    _mul += modifier.Value;
                    break;
                case AttributeModifierOp.FinalAdd:
                    _finalAdd += modifier.Value;
                    break;
                case AttributeModifierOp.Override:
                    _override = modifier.Value;
                    _hasOverride = true;
                    break;
                case AttributeModifierOp.Custom:
                default:
                    break;
            }
        }

        private void RebuildAggregates()
        {
            _add = 0f;
            _mul = 0f;
            _finalAdd = 0f;
            _override = 0f;
            _hasOverride = false;

            foreach (var kv in _modifiers)
            {
                ApplyModifier(kv.Value);
            }
        }
    }
}
