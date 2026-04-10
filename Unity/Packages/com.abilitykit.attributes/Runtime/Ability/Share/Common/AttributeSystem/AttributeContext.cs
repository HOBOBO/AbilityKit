using System;
using System.Collections.Generic;
using AbilityKit.Modifiers;

namespace AbilityKit.Ability.Share.Common.AttributeSystem
{
    /// <summary>
    /// 属性上下文。
    /// 实现 IModifierContext 接口，可以与 AbilityKit.Modifiers 配合使用。
    /// 
    /// 重构说明：
    /// - 简化 ApplyEffect 方法，使用 SourceId 追踪效果
    /// - 移除对 AttributeModifierHandle 和 AttributeEffectHandle 的依赖
    /// </summary>
    public sealed class AttributeContext : IModifierContext
    {
        private readonly Dictionary<string, AttributeGroup> _groups = new Dictionary<string, AttributeGroup>(StringComparer.Ordinal);
        private int _nextSourceId = 1;

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

        #region 组管理

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

        #endregion

        #region 属性值操作

        public float GetValue(AttributeId id)
        {
            return GetGroupFor(id).GetValue(id);
        }

        public void SetBase(AttributeId id, float baseValue)
        {
            GetGroupFor(id).SetBase(id, baseValue);
        }

        #endregion

        #region 修改器操作

        /// <summary>
        /// 添加修改器
        /// </summary>
        /// <param name="id">属性 ID</param>
        /// <param name="modifierData">修改器数据</param>
        /// <returns>修改器句柄</returns>
        public int AddModifier(AttributeId id, ModifierData modifierData)
        {
            return GetGroupFor(id).AddModifier(id, modifierData);
        }

        /// <summary>
        /// 添加修改器（便捷方法）
        /// </summary>
        public int AddModifier(AttributeId id, ModifierOp op, float value, int sourceId = 0)
        {
            return GetGroupFor(id).AddModifier(id, op, value, sourceId);
        }

        /// <summary>
        /// 移除修改器
        /// </summary>
        public bool RemoveModifier(AttributeId id, int handle)
        {
            return GetGroupFor(id).RemoveModifier(id, handle);
        }

        /// <summary>
        /// 清除指定来源的所有修改器
        /// </summary>
        /// <param name="sourceId">来源 ID</param>
        public void ClearModifiers(int sourceId)
        {
            foreach (var group in _groups.Values)
            {
                group.ClearModifiers(sourceId);
            }
        }

        #endregion

        #region 效果操作

        /// <summary>
        /// 应用属性效果
        /// </summary>
        /// <param name="effect">属性效果</param>
        /// <returns>效果对应的 SourceId，用于后续移除</returns>
        public int ApplyEffect(AttributeEffect effect)
        {
            if (effect == null || effect.Entries == null || effect.Entries.Length == 0) return 0;

            var sourceId = _nextSourceId++;

            for (int i = 0; i < effect.Entries.Length; i++)
            {
                var e = effect.Entries[i];
                if (!e.Attribute.IsValid) continue;

                var modifierData = e.ModifierData;
                modifierData.SourceId = sourceId;
                AddModifier(e.Attribute, modifierData);
            }

            return sourceId;
        }

        /// <summary>
        /// 移除指定效果
        /// </summary>
        /// <param name="sourceId">效果对应的 SourceId</param>
        public void RemoveEffect(int sourceId)
        {
            ClearModifiers(sourceId);
        }

        #endregion

        #region 内部方法

        /// <summary>
        /// 通过 ModifierKey 查找 AttributeId
        /// 默认实现：ModifierKey.Packed 与 AttributeId.Id 相同
        /// 业务层可继承后实现更复杂的映射
        /// </summary>
        internal AttributeId FindAttributeIdByKey(ModifierKey key)
        {
            if (key.IsEmpty) return default;
            return new AttributeId((int)key.Packed);
        }

        #endregion
    }
}
