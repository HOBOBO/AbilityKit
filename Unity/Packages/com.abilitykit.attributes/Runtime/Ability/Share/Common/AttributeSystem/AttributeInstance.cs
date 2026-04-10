using System;
using System.Runtime.CompilerServices;
using AbilityKit.Modifiers;

namespace AbilityKit.Core.Common.AttributeSystem
{
    /// <summary>
    /// 属性实例。
    /// 管理单个属性的基础值和修改器。
    /// 
    /// 重构说明：
    /// - 统一使用 AbilityKit.Modifiers.ModifierCalculator 进行计算
    /// - 修改器数据直接使用 ModifierData
    /// - 移除双分支计算逻辑
    /// </summary>
    public sealed class AttributeInstance
    {
        private readonly AttributeGroup _group;
        private readonly AttributeId _id;
        private readonly int _rawId;
        private readonly AttributeContext _ctx;
        private readonly ModifierCalculator _calculator;
        private readonly ModifierKey _modifierKey;

        private int _nextHandle;

        private struct ModifierSlot
        {
            public int Handle;
            public ModifierData ModifierData;
            public int NextFree;
            public bool Active;
        }

        private ModifierSlot[] _modifierSlots = new ModifierSlot[0];
        private int _modifierSlotCount;
        private int _modifierFreeHead;
        private int[] _handleToSlotIndex = new int[0];

        public AttributeInstance(AttributeGroup group, AttributeId id, AttributeContext ctx)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));
            if (!id.IsValid) throw new ArgumentException("Invalid AttributeId", nameof(id));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            _group = group;
            _id = id;
            _rawId = id.Id;
            _ctx = ctx;
            _calculator = new ModifierCalculator();
            _modifierKey = ModifierKey.FromPacked((uint)id.Id);
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

        /// <summary>
        /// 添加修改器
        /// </summary>
        /// <param name="modifierData">修改器数据</param>
        /// <returns>修改器句柄</returns>
        public int AddModifier(ModifierData modifierData)
        {
            var handle = _nextHandle++;

            var slotIndex = AllocateModifierSlot(handle, modifierData);
            if (slotIndex < 0)
            {
                throw new InvalidOperationException("AllocateModifierSlot failed");
            }

            EnsureHandleMapCapacity(handle + 1);
            _handleToSlotIndex[handle] = slotIndex;

            MarkDirty();
            return handle;
        }

        /// <summary>
        /// 添加修改器（便捷方法，自动设置目标键）
        /// </summary>
        public int AddModifier(ModifierOp op, float value, int sourceId = 0)
        {
            var modifierData = op switch
            {
                ModifierOp.Add => ModifierData.Add(_modifierKey, value, sourceId),
                ModifierOp.Mul => ModifierData.Mul(_modifierKey, value, sourceId),
                ModifierOp.PercentAdd => ModifierData.PercentAdd(_modifierKey, value, sourceId),
                ModifierOp.Override => ModifierData.Override(_modifierKey, value, sourceId),
                _ => ModifierData.Add(_modifierKey, value, sourceId)
            };
            return AddModifier(modifierData);
        }

        /// <summary>
        /// 移除修改器
        /// </summary>
        /// <param name="handle">修改器句柄</param>
        /// <returns>是否成功移除</returns>
        public bool RemoveModifier(int handle)
        {
            if (handle <= 0) return false;

            if (!TryDeactivateModifierSlot(handle)) return false;

            MarkDirty();
            return true;
        }

        /// <summary>
        /// 清除所有修改器或指定来源的修改器
        /// </summary>
        /// <param name="sourceId">来源 ID，0 表示清除所有</param>
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

            MarkDirty();
        }

        /// <summary>
        /// 获取活跃修改器数量
        /// </summary>
        public int ActiveModifierCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _modifierSlotCount; i++)
                {
                    if (_modifierSlots[i].Active) count++;
                }
                return count;
            }
        }

        /// <summary>
        /// 获取活跃修改器数据数组
        /// </summary>
        public ReadOnlySpan<ModifierData> GetActiveModifierData()
        {
            int count = ActiveModifierCount;
            if (count == 0) return ReadOnlySpan<ModifierData>.Empty;

            var buffer = new ModifierData[count];
            int idx = 0;
            for (int i = 0; i < _modifierSlotCount; i++)
            {
                if (_modifierSlots[i].Active)
                {
                    buffer[idx++] = _modifierSlots[i].ModifierData;
                }
            }
            return buffer;
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

            // 获取活跃修改器数据
            var modifierData = GetActiveModifierData();

            // 使用 ModifierCalculator 计算
            var modifierResult = _calculator.Calculate(modifierData, slot.BaseValue);

            // 调用公式计算
            var formula = AttributeRegistry.Instance.GetFormula(_id);
            var v = formula.Evaluate(_ctx, _id, slot.BaseValue, modifierResult);

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

        private bool HasAnyActiveModifiers()
        {
            for (int i = 0; i < _modifierSlotCount; i++)
            {
                if (_modifierSlots[i].Active) return true;
            }
            return false;
        }

        private int AllocateModifierSlot(int handle, ModifierData modifierData)
        {
            if (_modifierFreeHead >= 0)
            {
                var idx = _modifierFreeHead;
                _modifierFreeHead = _modifierSlots[idx].NextFree;

                _modifierSlots[idx].Handle = handle;
                _modifierSlots[idx].ModifierData = modifierData;
                _modifierSlots[idx].NextFree = -1;
                _modifierSlots[idx].Active = true;
                return idx;
            }

            EnsureModifierSlotCapacity(_modifierSlotCount + 1);
            var newIdx = _modifierSlotCount++;
            _modifierSlots[newIdx].Handle = handle;
            _modifierSlots[newIdx].ModifierData = modifierData;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                if (_modifierSlots[i].ModifierData.SourceId != sourceId) continue;

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
