using System;
using AbilityKit.Modifiers;

namespace AbilityKit.Ability.Share.Common.AttributeSystem
{
    /// <summary>
    /// 属性实例。
    /// 管理单个属性的基础值和修改器。
    ///
    /// 内部使用 AbilityKit.Modifiers.ModifierCalculator 进行计算。
    /// </summary>
    public sealed class AttributeInstance
    {
        private readonly AttributeGroup _group;
        private readonly AttributeId _id;
        private readonly int _rawId;
        private readonly AttributeContext _ctx;

        /// <summary>
        /// 修改器计算器（可选使用）
        /// 如果不为 null，则使用 ModifierCalculator 进行计算
        /// </summary>
        private ModifierCalculator _calculator;

        private int _nextHandle;

        private struct ModifierSlot
        {
            public int Handle;
            public AttributeModifier Modifier;
            public ModifierData ModifierData;  // 转换为 ModifierData
            public int NextFree;
            public bool Active;
        }

        private ModifierSlot[] _modifierSlots = new ModifierSlot[0];
        private int _modifierSlotCount;
        private int _modifierFreeHead;
        private int[] _handleToSlotIndex = new int[0];

        /// <summary>
        /// 是否使用新的 ModifierCalculator 计算模式
        /// </summary>
        public bool UseModifierCalculator => _calculator != null;

        public AttributeInstance(AttributeGroup group, AttributeId id, AttributeContext ctx)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));
            if (!id.IsValid) throw new ArgumentException("Invalid AttributeId", nameof(id));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            _group = group;
            _id = id;
            _rawId = id.Id;
            _ctx = ctx;
            _nextHandle = 1;

            _modifierFreeHead = -1;
        }

        public AttributeId Id => _id;

        public float BaseValue
        {
            get => _group.GetSlotRef(_rawId).BaseValue;
            set
            {
                ref var slot = ref _group.GetSlotRef(_rawId);
                if (System.Math.Abs(slot.BaseValue - value) < 0.00001f) return;
                slot.BaseValue = value;
                slot.Dirty = true;
            }
        }

        public float Value
        {
            get
            {
                ref var slot = ref _group.GetSlotRef(_rawId);
                if (slot.Dirty)
                {
                    Recompute();
                }

                return slot.Cached;
            }
        }

        public event Action<AttributeId, float, float> Changed;

        #region 修改器管理

        public AttributeModifierHandle AddModifier(AttributeModifier modifier)
        {
            var handle = _nextHandle++;

            var slotIndex = AllocateModifierSlot(handle, modifier);
            if (slotIndex < 0)
            {
                throw new InvalidOperationException("AllocateModifierSlot failed");
            }

            EnsureHandleMapCapacity(handle + 1);
            _handleToSlotIndex[handle] = slotIndex;

            ApplyModifier(modifier);
            MarkDirty();
            return new AttributeModifierHandle(handle);
        }

        public bool RemoveModifier(AttributeModifierHandle handle)
        {
            if (!handle.IsValid) return false;

            if (!TryDeactivateModifierSlot(handle.Value)) return false;

            RebuildAggregates();
            MarkDirty();
            return true;
        }

        public void ClearModifiers(int sourceId = 0)
        {
            if (!HasAnyActiveModifiers()) return;

            if (sourceId == 0)
            {
                ClearAllModifierSlots();
            }
            else
            {
                ClearModifierSlotsBySource(sourceId);
            }

            RebuildAggregates();
            MarkDirty();
        }

        public AttributeModifierSet GetModifierSet()
        {
            ref var slot = ref _group.GetSlotRef(_rawId);
            return new AttributeModifierSet(slot.Add, slot.Mul, slot.FinalAdd, slot.Override, slot.HasOverride);
        }

        /// <summary>
        /// 获取当前生效的修改器数组（用于调试/UI）
        /// </summary>
        public ReadOnlySpan<AttributeModifier> GetActiveModifiers()
        {
            var active = new System.Collections.Generic.List<AttributeModifier>(_modifierSlotCount);
            for (int i = 0; i < _modifierSlotCount; i++)
            {
                if (_modifierSlots[i].Active)
                {
                    active.Add(_modifierSlots[i].Modifier);
                }
            }
            return active.ToArray();
        }

        #endregion

        #region 启用 ModifierCalculator

        /// <summary>
        /// 启用 ModifierCalculator 计算模式
        /// 这将使用 AbilityKit.Modifiers 进行更通用的计算
        /// </summary>
        public void EnableModifierCalculator()
        {
            if (_calculator == null)
            {
                _calculator = new ModifierCalculator();
            }
        }

        /// <summary>
        /// 禁用 ModifierCalculator 计算模式
        /// 这将使用旧的聚合计算方式
        /// </summary>
        public void DisableModifierCalculator()
        {
            _calculator = null;
        }

        #endregion

        #region 内部方法

        private void MarkDirty()
        {
            ref var slot = ref _group.GetSlotRef(_rawId);
            slot.Dirty = true;
        }

        internal void MarkDirtyByDependency()
        {
            ref var slot = ref _group.GetSlotRef(_rawId);
            slot.Dirty = true;
        }

        private void Recompute()
        {
            ref var slot = ref _group.GetSlotRef(_rawId);
            var old = slot.Cached;

            // 获取修改器聚合结果
            var modifiers = GetModifierSet();

            // 调用公式计算
            var formula = AttributeRegistry.Instance.GetFormula(_id);

            float v;
            if (_calculator != null)
            {
                // 使用 ModifierCalculator 模式
                var modifierResult = ToModifierResult(modifiers);
                v = formula.Evaluate(_ctx, _id, slot.BaseValue, modifierResult);
            }
            else
            {
                // 使用旧版聚合计算模式
                v = formula.Evaluate(_ctx, _id, slot.BaseValue, in modifiers);
            }

            // 应用约束
            var constraint = AttributeRegistry.Instance.GetConstraint(_id);
            if (constraint != null)
            {
                v = constraint.Apply(_id, v);
            }

            slot.Cached = v;
            slot.Dirty = false;

            if (System.Math.Abs(old - v) > 0.00001f)
            {
                Changed?.Invoke(_id, old, v);
            }
        }

        /// <summary>
        /// 将 AttributeModifierSet 转换为 ModifierResult
        /// </summary>
        private ModifierResult ToModifierResult(AttributeModifierSet set)
        {
            if (set.HasOverride)
            {
                return new ModifierResult
                {
                    BaseValue = 0f,  // 会被公式覆盖
                    AddSum = set.Add,
                    MulProduct = 1f + set.Mul,
                    OverrideValue = set.Override,
                    Count = 0
                };
            }

            return new ModifierResult
            {
                BaseValue = 0f,
                AddSum = set.Add,
                MulProduct = 1f + set.Mul,
                OverrideValue = null,
                Count = 0
            };
        }

        private void ApplyModifier(AttributeModifier modifier)
        {
            ref var slot = ref _group.GetSlotRef(_rawId);
            switch (modifier.Op)
            {
                case AttributeModifierOp.Add:
                    slot.Add += modifier.Value;
                    break;

                case AttributeModifierOp.Mul:
                    slot.Mul += modifier.Value;
                    break;

                case AttributeModifierOp.FinalAdd:
                    slot.FinalAdd += modifier.Value;
                    break;

                case AttributeModifierOp.Override:
                    slot.Override = modifier.Value;
                    slot.HasOverride = true;
                    break;

                case AttributeModifierOp.Custom:
                default:
                    break;
            }
        }

        private void RebuildAggregates()
        {
            ref var slot = ref _group.GetSlotRef(_rawId);
            slot.Add = 0f;
            slot.Mul = 0f;
            slot.FinalAdd = 0f;
            slot.Override = 0f;
            slot.HasOverride = false;

            for (int i = 0; i < _modifierSlotCount; i++)
            {
                if (!_modifierSlots[i].Active) continue;
                ApplyModifier(_modifierSlots[i].Modifier);
            }
        }

        private bool HasAnyActiveModifiers()
        {
            for (int i = 0; i < _modifierSlotCount; i++)
            {
                if (_modifierSlots[i].Active) return true;
            }

            return false;
        }

        private int AllocateModifierSlot(int handle, AttributeModifier modifier)
        {
            if (_modifierFreeHead >= 0)
            {
                var idx = _modifierFreeHead;
                _modifierFreeHead = _modifierSlots[idx].NextFree;

                _modifierSlots[idx].Handle = handle;
                _modifierSlots[idx].Modifier = modifier;
                _modifierSlots[idx].ModifierData = modifier.ToModifierData(ModifierKey.FromPacked((uint)_id.Id));
                _modifierSlots[idx].NextFree = -1;
                _modifierSlots[idx].Active = true;
                return idx;
            }

            EnsureModifierSlotCapacity(_modifierSlotCount + 1);
            var newIdx = _modifierSlotCount++;
            _modifierSlots[newIdx].Handle = handle;
            _modifierSlots[newIdx].Modifier = modifier;
            _modifierSlots[newIdx].ModifierData = modifier.ToModifierData(ModifierKey.FromPacked((uint)_id.Id));
            _modifierSlots[newIdx].NextFree = -1;
            _modifierSlots[newIdx].Active = true;
            return newIdx;
        }

        private void EnsureHandleMapCapacity(int required)
        {
            if (_handleToSlotIndex.Length >= required) return;

            var oldLen = _handleToSlotIndex.Length;
            var newSize = oldLen;
            if (newSize <= 0) newSize = 4;
            while (newSize < required)
            {
                newSize *= 2;
            }

            Array.Resize(ref _handleToSlotIndex, newSize);

            for (int i = oldLen; i < newSize; i++)
            {
                _handleToSlotIndex[i] = -1;
            }
        }

        private void EnsureModifierSlotCapacity(int required)
        {
            if (_modifierSlots.Length >= required) return;

            var newSize = _modifierSlots.Length;
            if (newSize <= 0) newSize = 4;
            while (newSize < required)
            {
                newSize *= 2;
            }

            Array.Resize(ref _modifierSlots, newSize);
        }

        private bool TryDeactivateModifierSlot(int handle)
        {
            if (handle <= 0) return false;
            if (handle >= _handleToSlotIndex.Length) return false;

            var idx = _handleToSlotIndex[handle];
            if (idx < 0 || idx >= _modifierSlotCount) return false;
            if (!_modifierSlots[idx].Active) return false;
            if (_modifierSlots[idx].Handle != handle) return false;

            _modifierSlots[idx].Active = false;
            _modifierSlots[idx].NextFree = _modifierFreeHead;
            _modifierFreeHead = idx;
            _handleToSlotIndex[handle] = -1;
            return true;
        }

        private void ClearAllModifierSlots()
        {
            for (int i = 0; i < _modifierSlotCount; i++)
            {
                if (!_modifierSlots[i].Active) continue;
                _modifierSlots[i].Active = false;
                _modifierSlots[i].NextFree = _modifierFreeHead;
                _modifierFreeHead = i;

                var handle = _modifierSlots[i].Handle;
                if (handle > 0 && handle < _handleToSlotIndex.Length)
                {
                    _handleToSlotIndex[handle] = -1;
                }
            }
        }

        private void ClearModifierSlotsBySource(int sourceId)
        {
            for (int i = 0; i < _modifierSlotCount; i++)
            {
                if (!_modifierSlots[i].Active) continue;
                if (_modifierSlots[i].Modifier.SourceId != sourceId) continue;

                _modifierSlots[i].Active = false;
                _modifierSlots[i].NextFree = _modifierFreeHead;
                _modifierFreeHead = i;

                var handle = _modifierSlots[i].Handle;
                if (handle > 0 && handle < _handleToSlotIndex.Length)
                {
                    _handleToSlotIndex[handle] = -1;
                }
            }
        }

        #endregion
    }
}
