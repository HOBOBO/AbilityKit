using System;
using System.Collections.Generic;
using AbilityKit.Modifiers;

namespace AbilityKit.Ability.Share.Common.AttributeSystem
{
    /// <summary>
    /// 属性上下文。
    /// 实现 IModifierContext 接口，可以与 AbilityKit.Modifiers 配合使用。
    /// </summary>
    public sealed class AttributeContext : IModifierContext
    {
        private readonly Dictionary<string, AttributeGroup> _groups = new Dictionary<string, AttributeGroup>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, AttributeGroup> Groups => _groups;

        /// <summary>
        /// 当前等级（用于 ScalableFloat 曲线插值）
        /// </summary>
        public float Level { get; set; } = 1f;

        public event Action<string, AttributeId, float, float> AttributeChanged;

        #region IModifierContext

        /// <summary>
        /// 获取属性值（实现 IModifierContext）
        /// </summary>
        float IModifierContext.GetAttribute(ModifierKey key)
        {
            // 将 ModifierKey 转换回 AttributeId
            // 这里需要通过 ModifierKey 的 Packed 值来查找
            var attrId = FindAttributeIdByKey(key);
            if (!attrId.IsValid) return 0f;
            return GetValue(attrId);
        }

        #endregion

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

        #region 内部方法

        /// <summary>
        /// 通过 ModifierKey 查找 AttributeId
        /// 默认实现：ModifierKey.Packed 与 AttributeId.Id 相同
        /// 业务层可继承后实现更复杂的映射
        /// </summary>
        internal AttributeId FindAttributeIdByKey(ModifierKey key)
        {
            // 默认映射：ModifierKey.Packed 的低 8 位作为 AttributeId
            // 业务层可以在子类中实现更复杂的映射逻辑
            if (key.IsEmpty) return default;
            return new AttributeId((int)key.Packed);
        }

        #endregion
    }
}
