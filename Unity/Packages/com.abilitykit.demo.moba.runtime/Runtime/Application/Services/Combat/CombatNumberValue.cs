using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;

namespace AbilityKit.Demo.Moba
{
    public enum CombatNumberValueMode
    {
        BaseOnly = 0,
        BaseAdd = 1,
        BaseAddMul = 2,
        OverrideOnly = 3,
    }

    public enum CombatNumberModifierOp
    {
        Add = 0,
        Mul = 1,
        FinalAdd = 2,
        Override = 3,
        Custom = 4,
    }

    /// <summary>
    /// 伤害管线数值修饰符。数值以 Q32.32 定点存储；
    /// float 构造重载仅供配置/表现边界单次换算使用。
    /// </summary>
    public readonly struct CombatNumberModifier
    {
        public readonly CombatNumberModifierOp Op;
        public readonly Fixed64 Value;
        public readonly int SourceId;

        public CombatNumberModifier(CombatNumberModifierOp op, Fixed64 value, int sourceId = 0)
        {
            Op = op;
            Value = value;
            SourceId = sourceId;
        }

        public CombatNumberModifier(CombatNumberModifierOp op, float value, int sourceId = 0)
        {
            Op = op;
            Value = MobaResourceFixedConvert.ToFixed(value);
            SourceId = sourceId;
        }
    }

    public readonly struct CombatNumberModifierHandle
    {
        public readonly int Value;

        internal CombatNumberModifierHandle(int value)
        {
            Value = value;
        }

        public bool IsValid => Value != 0;
    }

    /// <summary>
    /// MOBA 战斗域的瞬态数值容器（旧名 DamageNumberValue，2026-08 改中性名）：
    /// 单次战斗数值计算（伤害/治疗/护盾等加成管线）用的 base+修饰器叠加，
    /// 持久属性加成走 attributes/modifiers 包。乘区为加法增量：0.5 表示 +50%。
    /// 内部全 Q32.32 定点算术；float 属性是表现边界的单次换算视图。
    /// </summary>
    public sealed class CombatNumberValue
    {
        private Fixed64 _baseValue;
        private Fixed64 _cached;
        private bool _dirty;
        private int _nextHandle;
        private readonly Dictionary<int, CombatNumberModifier> _modifiers;
        private readonly List<int> _orderedHandles;
        private Fixed64 _add;
        private Fixed64 _mul;
        private Fixed64 _finalAdd;
        private Fixed64 _override;
        private bool _hasOverride;

        public CombatNumberValue(CombatNumberValueMode mode, Fixed64 baseValue = default, int initialCapacity = 8)
        {
            Mode = mode;
            _baseValue = baseValue;
            _dirty = true;
            _nextHandle = 1;
            _modifiers = new Dictionary<int, CombatNumberModifier>(initialCapacity);
            _orderedHandles = new List<int>(initialCapacity);
        }

        /// <summary>float 边界构造重载（配置/测试用，单次换算）。</summary>
        public CombatNumberValue(CombatNumberValueMode mode, float baseValue, int initialCapacity = 8)
            : this(mode, MobaResourceFixedConvert.ToFixed(baseValue), initialCapacity)
        {
        }

        public CombatNumberValueMode Mode { get; }

        public Fixed64 FixedBaseValue
        {
            get => _baseValue;
            set
            {
                if (_baseValue == value) return;
                _baseValue = value;
                _dirty = true;
            }
        }

        public Fixed64 FixedValue
        {
            get
            {
                if (_dirty) Recompute();
                return _cached;
            }
        }

        public float BaseValue
        {
            get => MobaResourceFixedConvert.ToSingle(_baseValue);
            set => FixedBaseValue = MobaResourceFixedConvert.ToFixed(value);
        }

        public float Value => MobaResourceFixedConvert.ToSingle(FixedValue);

        public CombatNumberModifierHandle Apply(CombatNumberModifier modifier)
        {
            var handle = _nextHandle++;
            _modifiers[handle] = modifier;
            ApplyModifier(in modifier);
            _dirty = true;
            return new CombatNumberModifierHandle(handle);
        }

        public bool Remove(CombatNumberModifierHandle handle)
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

        public void Reset(Fixed64 baseValue = default)
        {
            _baseValue = baseValue;
            _cached = Fixed64.Zero;
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
                case CombatNumberValueMode.BaseOnly:
                    break;
                case CombatNumberValueMode.OverrideOnly:
                    if (_hasOverride) value = _override;
                    break;
                case CombatNumberValueMode.BaseAdd:
                    value += _add + _finalAdd;
                    if (_hasOverride) value = _override;
                    break;
                case CombatNumberValueMode.BaseAddMul:
                default:
                    value = (value + _add) * (Fixed64.One + _mul) + _finalAdd;
                    if (_hasOverride) value = _override;
                    break;
            }

            _cached = value;
            _dirty = false;
        }

        private void ApplyModifier(in CombatNumberModifier modifier)
        {
            switch (modifier.Op)
            {
                case CombatNumberModifierOp.Add:
                    _add += modifier.Value;
                    break;
                case CombatNumberModifierOp.Mul:
                    _mul += modifier.Value;
                    break;
                case CombatNumberModifierOp.FinalAdd:
                    _finalAdd += modifier.Value;
                    break;
                case CombatNumberModifierOp.Override:
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
            _add = Fixed64.Zero;
            _mul = Fixed64.Zero;
            _finalAdd = Fixed64.Zero;
            _override = Fixed64.Zero;
            _hasOverride = false;
        }
    }
}
