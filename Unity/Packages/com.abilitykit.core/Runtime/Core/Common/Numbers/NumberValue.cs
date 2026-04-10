using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Common.Numbers
{
    public sealed class NumberValue
    {
        private float _baseValue;
        private float _cached;
        private bool _dirty;

        private int _nextHandle;
        private readonly Dictionary<int, NumberModifier> _modifiers;
        private readonly List<int> _tmpRemoveKeys;

        private float _add;
        private float _mul;
        private float _finalAdd;
        private float _override;
        private bool _hasOverride;

        public NumberValue(NumberValueMode mode, float baseValue = 0f, int initialCapacity = 8)
        {
            Mode = mode;
            _baseValue = baseValue;
            _dirty = true;
            _nextHandle = 1;
            _modifiers = new Dictionary<int, NumberModifier>(initialCapacity);
            _tmpRemoveKeys = new List<int>(initialCapacity);
        }

        public NumberValueMode Mode { get; }

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
                if (_dirty) Recompute();
                return _cached;
            }
        }

        public NumberModifierHandle Apply(NumberModifier modifier)
        {
            var handle = _nextHandle++;
            _modifiers[handle] = modifier;
            ApplyModifier(modifier);
            _dirty = true;
            return new NumberModifierHandle(handle);
        }

        public bool Remove(NumberModifierHandle handle)
        {
            if (!handle.IsValid) return false;
            if (!_modifiers.Remove(handle.Value)) return false;
            RebuildAggregates();
            _dirty = true;
            return true;
        }

        public NumberEffectHandle ApplyEffect(NumberEffect effect)
        {
            if (effect == null || effect.Entries == null || effect.Entries.Length == 0) return null;

            var handles = new List<NumberModifierHandle>(effect.Entries.Length);
            for (int i = 0; i < effect.Entries.Length; i++)
            {
                var h = Apply(effect.Entries[i].Modifier);
                if (h.IsValid) handles.Add(h);
            }

            return handles.Count == 0 ? null : new NumberEffectHandle(this, handles);
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
                _tmpRemoveKeys.Clear();
                foreach (var kv in _modifiers)
                {
                    if (kv.Value.SourceId == sourceId) _tmpRemoveKeys.Add(kv.Key);
                }

                for (int i = 0; i < _tmpRemoveKeys.Count; i++)
                {
                    _modifiers.Remove(_tmpRemoveKeys[i]);
                }
            }

            RebuildAggregates();
            _dirty = true;
        }

        public NumberModifierSet GetModifierSet()
        {
            return new NumberModifierSet(_add, _mul, _finalAdd, _override, _hasOverride);
        }

        public void Reset(float baseValue = 0f)
        {
            _baseValue = baseValue;
            _cached = 0f;
            _dirty = true;
            _nextHandle = 1;
            _modifiers.Clear();
            _add = 0f;
            _mul = 0f;
            _finalAdd = 0f;
            _override = 0f;
            _hasOverride = false;
        }

        private void Recompute()
        {
            var modifiers = GetModifierSet();
            var v = EvaluateInternal(_baseValue, in modifiers);

            if (float.IsNaN(v) || float.IsInfinity(v))
            {
                v = 0f;
            }

            _cached = v;
            _dirty = false;
        }

        private float EvaluateInternal(float baseValue, in NumberModifierSet modifiers)
        {
            switch (Mode)
            {
                case NumberValueMode.BaseOnly:
                    return baseValue;

                case NumberValueMode.OverrideOnly:
                    return modifiers.HasOverride ? modifiers.Override : baseValue;

                case NumberValueMode.BaseAdd:
                {
                    var v = baseValue + modifiers.Add + modifiers.FinalAdd;
                    if (modifiers.HasOverride) v = modifiers.Override;
                    return v;
                }

                case NumberValueMode.BaseAddMul:
                default:
                {
                    var v = (baseValue + modifiers.Add) * (1f + modifiers.Mul) + modifiers.FinalAdd;
                    if (modifiers.HasOverride) v = modifiers.Override;
                    return v;
                }
            }
        }

        private void ApplyModifier(NumberModifier modifier)
        {
            switch (modifier.Op)
            {
                case NumberModifierOp.Add:
                    _add += modifier.Value;
                    break;
                case NumberModifierOp.Mul:
                    _mul += modifier.Value;
                    break;
                case NumberModifierOp.FinalAdd:
                    _finalAdd += modifier.Value;
                    break;
                case NumberModifierOp.Override:
                    _override = modifier.Value;
                    _hasOverride = true;
                    break;
                case NumberModifierOp.Custom:
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
