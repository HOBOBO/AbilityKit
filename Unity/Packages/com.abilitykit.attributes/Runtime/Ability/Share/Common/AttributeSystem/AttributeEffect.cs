using System;
using AbilityKit.Modifiers;

namespace AbilityKit.Ability.Share.Common.AttributeSystem
{
    /// <summary>
    /// 属性效果。
    /// 表示一组对属性的修改。
    ///
    /// 支持两种格式：
    /// - AttributeModifier（旧版）
    /// - ModifierData（新版，通过 AbilityKit.Modifiers）
    /// </summary>
    public sealed class AttributeEffect
    {
        /// <summary>
        /// 效果条目（旧版，使用 AttributeModifier）
        /// </summary>
        public readonly Entry[] Entries;

        /// <summary>
        /// 效果条目（新版，使用 ModifierData）
        /// </summary>
        public readonly ModifierEntry[] ModifierEntries;

        #region 构造方法

        /// <summary>
        /// 使用 AttributeModifier 创建效果（旧版）
        /// </summary>
        public AttributeEffect(params Entry[] entries)
        {
            Entries = entries ?? Array.Empty<Entry>();
            ModifierEntries = Array.Empty<ModifierEntry>();
        }

        /// <summary>
        /// 使用 ModifierData 创建效果（新版）
        /// </summary>
        public static AttributeEffect FromModifiers(params ModifierEntry[] entries)
        {
            return new AttributeEffect(Array.Empty<Entry>(), entries ?? Array.Empty<ModifierEntry>());
        }

        private AttributeEffect()
        {
            Entries = Array.Empty<Entry>();
            ModifierEntries = Array.Empty<ModifierEntry>();
        }

        private AttributeEffect(Entry[] entries, ModifierEntry[] modifierEntries)
        {
            Entries = entries;
            ModifierEntries = modifierEntries;
        }

        #endregion

        #region 类型

        /// <summary>
        /// 效果条目（旧版，使用 AttributeModifier）
        /// </summary>
        [Serializable]
        public readonly struct Entry
        {
            public readonly AttributeId Attribute;
            public readonly AttributeModifier Modifier;

            public Entry(AttributeId attribute, AttributeModifier modifier)
            {
                Attribute = attribute;
                Modifier = modifier;
            }

            /// <summary>
            /// 转换为新版 ModifierEntry
            /// </summary>
            public ModifierEntry ToModifierEntry()
            {
                return new ModifierEntry(Attribute, Modifier.ToModifierData(ModifierKey.FromPacked((uint)Attribute.Id)));
            }
        }

        /// <summary>
        /// 效果条目（新版，使用 ModifierData）
        /// </summary>
        [Serializable]
        public readonly struct ModifierEntry
        {
            /// <summary>属性 ID</summary>
            public readonly AttributeId AttributeId;

            /// <summary>修改器数据</summary>
            public readonly ModifierData ModifierData;

            public ModifierEntry(AttributeId attributeId, ModifierData modifierData)
            {
                AttributeId = attributeId;
                ModifierData = modifierData;
            }

            /// <summary>
            /// 转换为旧版 Entry
            /// </summary>
            public Entry ToEntry()
            {
                return new Entry(AttributeId, AttributeModifier.FromModifierData(ModifierData));
            }
        }

        #endregion

        #region 便捷工厂方法

        /// <summary>
        /// 创建加法效果
        /// </summary>
        public static AttributeEffect Add(AttributeId attr, float value, int sourceId = 0)
        {
            return new AttributeEffect(new Entry(attr, AttributeModifier.Add(value, sourceId)));
        }

        /// <summary>
        /// 创建乘法效果
        /// </summary>
        public static AttributeEffect Mul(AttributeId attr, float value, int sourceId = 0)
        {
            return new AttributeEffect(new Entry(attr, AttributeModifier.Mul(value, sourceId)));
        }

        /// <summary>
        /// 创建覆盖效果
        /// </summary>
        public static AttributeEffect Override(AttributeId attr, float value, int sourceId = 0)
        {
            return new AttributeEffect(new Entry(attr, AttributeModifier.Override(value, sourceId)));
        }

        /// <summary>
        /// 创建最终加法效果
        /// </summary>
        public static AttributeEffect FinalAdd(AttributeId attr, float value, int sourceId = 0)
        {
            return new AttributeEffect(new Entry(attr, AttributeModifier.FinalAdd(value, sourceId)));
        }

        #endregion

        #region 扩展方法

        /// <summary>
        /// 获取所有条目数量
        /// </summary>
        public int Count => Entries.Length + ModifierEntries.Length;

        /// <summary>
        /// 是否有任何条目
        /// </summary>
        public bool HasEntries => Count > 0;

        /// <summary>
        /// 转换为 ModifierData 数组
        /// </summary>
        public ModifierData[] ToModifierDataArray()
        {
            var result = new ModifierData[Count];
            int index = 0;

            for (int i = 0; i < Entries.Length; i++)
            {
                result[index++] = Entries[i].ToModifierEntry().ModifierData;
            }

            for (int i = 0; i < ModifierEntries.Length; i++)
            {
                result[index++] = ModifierEntries[i].ModifierData;
            }

            return result;
        }

        #endregion
    }
}
