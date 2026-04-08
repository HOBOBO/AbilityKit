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

        #region 时间属性

        /// <summary>当前时间（秒）</summary>
        public float CurrentTime { get; set; }

        /// <summary>增量时间（秒）</summary>
        public float DeltaTime { get; set; }

        /// <summary>距离修改器生效的已过时间（秒）</summary>
        public float ElapsedTime { get; set; }

        #endregion

        #region 数据槽

        private readonly Dictionary<string, object> _dataSlots = new Dictionary<string, object>();

        /// <summary>
        /// 获取数据（泛型）
        /// </summary>
        public T GetData<T>(string key) where T : class
        {
            return _dataSlots.TryGetValue(key, out var obj) ? obj as T : null;
        }

        /// <summary>
        /// 尝试获取数据
        /// </summary>
        public bool TryGetData<T>(string key, out T value) where T : class
        {
            value = GetData<T>(key);
            return value != null;
        }

        /// <summary>
        /// 获取浮点数据
        /// </summary>
        public float GetFloat(string key)
        {
            if (_dataSlots.TryGetValue(key, out var obj))
            {
                return obj switch
                {
                    float f => f,
                    int i => i,
                    double d => (float)d,
                    _ => 0f
                };
            }
            return 0f;
        }

        /// <summary>
        /// 尝试获取浮点数据
        /// </summary>
        public bool TryGetFloat(string key, out float value)
        {
            value = GetFloat(key);
            return _dataSlots.ContainsKey(key);
        }

        /// <summary>
        /// 获取整型数据
        /// </summary>
        public int GetInt(string key)
        {
            if (_dataSlots.TryGetValue(key, out var obj))
            {
                return obj switch
                {
                    int i => i,
                    float f => (int)f,
                    double d => (int)d,
                    _ => 0
                };
            }
            return 0;
        }

        /// <summary>
        /// 尝试获取整型数据
        /// </summary>
        public bool TryGetInt(string key, out int value)
        {
            value = GetInt(key);
            return _dataSlots.ContainsKey(key);
        }

        /// <summary>
        /// 设置数据
        /// </summary>
        public void SetData<T>(string key, T value) where T : class
        {
            _dataSlots[key] = value;
        }

        #endregion

        #region 元数据

        /// <summary>
        /// 修改器元数据
        /// </summary>
        public ModifierMetadata Metadata { get; set; }

        #endregion

        #region IModifierContext

        /// <summary>
        /// 获取属性值（实现 IModifierContext）
        /// </summary>
        float IModifierContext.GetAttribute(ModifierKey key)
        {
            var attrId = FindAttributeIdByKey(key);
            if (!attrId.IsValid) return 0f;
            return GetValue(attrId);
        }

        float IModifierContext.Level => Level;
        float IModifierContext.CurrentTime => CurrentTime;
        float IModifierContext.DeltaTime => DeltaTime;
        float IModifierContext.ElapsedTime => ElapsedTime;
        ModifierMetadata IModifierContext.Metadata => Metadata;

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
