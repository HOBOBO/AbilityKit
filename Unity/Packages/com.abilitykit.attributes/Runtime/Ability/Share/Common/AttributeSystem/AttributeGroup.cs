using System;
using System.Collections.Generic;
using AbilityKit.Modifiers;

namespace AbilityKit.Ability.Share.Common.AttributeSystem
{
    /// <summary>
    /// 属性组。
    /// 管理和组织一组相关的属性实例。
    /// 
    /// 重构说明：
    /// - 简化修改器管理方法，使用 ModifierData 和 SourceId
    /// - 移除对 AttributeModifierHandle 的依赖
    /// </summary>
    public sealed class AttributeGroup
    {
        internal struct AttributeSlot
        {
            public float BaseValue;
            public float Cached;
            public bool Dirty;
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

        #region 属性获取

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

        #endregion

        #region 修改器操作

        /// <summary>
        /// 添加修改器
        /// </summary>
        /// <returns>修改器句柄</returns>
        public int AddModifier(AttributeId id, ModifierData modifierData)
        {
            return GetOrCreate(id).AddModifier(modifierData);
        }

        /// <summary>
        /// 添加修改器（便捷方法）
        /// </summary>
        public int AddModifier(AttributeId id, ModifierOp op, float value, int sourceId = 0)
        {
            return GetOrCreate(id).AddModifier(op, value, sourceId);
        }

        /// <summary>
        /// 移除修改器
        /// </summary>
        public bool RemoveModifier(AttributeId id, int handle)
        {
            if (!TryGet(id, out var inst) || inst == null) return false;
            return inst.RemoveModifier(handle);
        }

        /// <summary>
        /// 清除指定来源的所有修改器
        /// </summary>
        /// <param name="sourceId">来源 ID，0 表示清除所有</param>
        public void ClearModifiers(int sourceId)
        {
            foreach (var inst in _attrs.Values)
            {
                inst.ClearModifiers(sourceId);
            }
        }

        /// <summary>
        /// 清除所有修改器
        /// </summary>
        public void ClearAllModifiers()
        {
            ClearModifiers(sourceId: 0);
        }

        #endregion

        #region 内部方法

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

            inst?.MarkDirtyByDependency();
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

        #endregion
    }
}
