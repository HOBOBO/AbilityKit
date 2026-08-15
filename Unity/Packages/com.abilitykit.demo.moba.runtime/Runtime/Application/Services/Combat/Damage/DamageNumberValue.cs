using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba
{
    public enum DamageNumberValueMode
    {
        BaseOnly = 0,
        BaseAdd = 1,
        BaseAddMul = 2,
        OverrideOnly = 3,
    }

    public enum DamageNumberModifierOp
    {
        Add = 0,
        Mul = 1,
        FinalAdd = 2,
        Override = 3,
        Custom = 4,
    }

    public readonly struct DamageNumberModifier
    {
        public readonly DamageNumberModifierOp Op;
        public readonly float Value;
        public readonly int SourceId;

        public DamageNumberModifier(DamageNumberModifierOp op, float value, int sourceId = 0)
        {
            Op = op;
            Value = value;
            SourceId = sourceId;
        }
    }

    public readonly struct DamageNumberModifierHandle
    {
        public readonly int Value;

        internal DamageNumberModifierHandle(int value)
        {
            Value = value;
        }

        public bool IsValid => Value != 0;
    }

    public readonly struct DamageNumberModifierSet
    {
        public readonly float Add;
        public readonly float Mul;
        public readonly float FinalAdd;
        public readonly float Override;
        public readonly bool HasOverride;

        public DamageNumberModifierSet(float add, float mul, float finalAdd, float @override, bool hasOverride)
        {
            Add = add;
            Mul = mul;
            FinalAdd = finalAdd;
            Override = @override;
            HasOverride = hasOverride;
        }
    }

    /// <summary>
    /// Mutable damage-pipeline number owned by the MOBA combat domain.
    /// Multipliers are additive deltas: 0.5 means a 50 percent increase.
    /// </summary>
    public sealed class DamageNumberValue
    {
        private float _baseValue;
        private float _cached;
        private bool _dirty;
        private int _nextHandle;
        private readonly Dictionary<int, DamageNumberModifier> _modifiers;
        private readonly List<int> _orderedHandles;
        private float _add;
        private float _mul;
        private float _finalAdd;
        private float _override;
        private bool _hasOverride;

        public DamageNumberValue(DamageNumberValueMode mode, float baseValue = 0f, int initialCapacity = 8)
        {
            Mode = mode;
            _baseValue = baseValue;
            _dirty = true;
            _nextHandle = 1;
            _modifiers = new Dictionary<int, DamageNumberModifier>(initialCapacity);
            _orderedHandles = new List<int>(initialCapacity);
        }

        public DamageNumberValueMode Mode { get; }

        public float BaseValue
        {
            get => _baseValue;
            set
            {
                if (Math.Abs(_baseValue - value) < 0.00001f) return;
                _baseValue = value;
                _dirty = true;
            }
        }

        public float Value
        {
            get
            {
                if (_dirty) Recompute();
                return _cached;
            }
        }

        public DamageNumberModifierHandle Apply(DamageNumberModifier modifier)
        {
            var handle = _nextHandle++;
            _modifiers[handle] = modifier;
            ApplyModifier(in modifier);
            _dirty = true;
            return new DamageNumberModifierHandle(handle);
        }

        public bool Remove(DamageNumberModifierHandle handle)
        {
            if (!handle.IsValid || !_modifiers.Remove(handle.Value)) return false;
            RebuildAggregates();
            _dirty = true;
            return true;
        }

        public void Clear(int sourceId = 0)
        {
            if (_modifiers.Count == 0) return;

            if (sourceId == 0)
            {
                _modifiers.Clear();
            }
            else
            {
                _orderedHandles.Clear();
                foreach (var pair in _modifiers)
                {
                    if (pair.Value.SourceId == sourceId) _orderedHandles.Add(pair.Key);
                }

                for (var i = 0; i < _orderedHandles.Count; i++)
                {
                    _modifiers.Remove(_orderedHandles[i]);
                }
            }

            RebuildAggregates();
            _dirty = true;
        }

        public DamageNumberModifierSet GetModifierSet()
        {
            return new DamageNumberModifierSet(_add, _mul, _finalAdd, _override, _hasOverride);
        }

        public void Reset(float baseValue = 0f)
        {
            _baseValue = baseValue;
            _cached = 0f;
            _dirty = true;
            _nextHandle = 1;
            _modifiers.Clear();
            ResetAggregates();
        }

        private void Recompute()
        {
            var value = _baseValue;
            switch (Mode)
            {
                case DamageNumberValueMode.BaseOnly:
                    break;
                case DamageNumberValueMode.OverrideOnly:
                    if (_hasOverride) value = _override;
                    break;
                case DamageNumberValueMode.BaseAdd:
                    value += _add + _finalAdd;
                    if (_hasOverride) value = _override;
                    break;
                case DamageNumberValueMode.BaseAddMul:
                default:
                    value = (value + _add) * (1f + _mul) + _finalAdd;
                    if (_hasOverride) value = _override;
                    break;
            }

            _cached = float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
            _dirty = false;
        }

        private void ApplyModifier(in DamageNumberModifier modifier)
        {
            switch (modifier.Op)
            {
                case DamageNumberModifierOp.Add:
                    _add += modifier.Value;
                    break;
                case DamageNumberModifierOp.Mul:
                    _mul += modifier.Value;
                    break;
                case DamageNumberModifierOp.FinalAdd:
                    _finalAdd += modifier.Value;
                    break;
                case DamageNumberModifierOp.Override:
                    _override = modifier.Value;
                    _hasOverride = true;
                    break;
            }
        }

        private void RebuildAggregates()
        {
            ResetAggregates();
            _orderedHandles.Clear();
            foreach (var handle in _modifiers.Keys) _orderedHandles.Add(handle);
            _orderedHandles.Sort();
            for (var i = 0; i < _orderedHandles.Count; i++)
            {
                var modifier = _modifiers[_orderedHandles[i]];
                ApplyModifier(in modifier);
            }
        }

        private void ResetAggregates()
        {
            _add = 0f;
            _mul = 0f;
            _finalAdd = 0f;
            _override = 0f;
            _hasOverride = false;
        }
    }
}
