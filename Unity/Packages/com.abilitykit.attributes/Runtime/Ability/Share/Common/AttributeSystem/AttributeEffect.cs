using System;
using AbilityKit.Modifiers;

namespace AbilityKit.Ability.Share.Common.AttributeSystem
{
    // ============================================================================
    // 属性效果
    // ============================================================================

    /// <summary>
    /// 属性效果。
    /// 表示一组对属性的修改，使用 AbilityKit.Modifiers.ModifierData。
    /// </summary>
    public sealed class AttributeEffect
    {
        /// <summary>效果条目</summary>
        public readonly Entry[] Entries;

        #region 构造方法

        /// <summary>
        /// 使用效果条目创建效果
        /// </summary>
        public AttributeEffect(params Entry[] entries)
        {
            Entries = entries ?? Array.Empty<Entry>();
        }

        #endregion

        #region 类型

        /// <summary>
        /// 效果条目
        /// </summary>
        [Serializable]
        public readonly struct Entry
        {
            /// <summary>属性 ID</summary>
            public readonly AttributeId Attribute;

            /// <summary>修改器数据</summary>
            public readonly ModifierData ModifierData;

            public Entry(AttributeId attribute, ModifierData modifierData)
            {
                Attribute = attribute;
                ModifierData = modifierData;
            }

            #region 工厂方法

            /// <summary>
            /// 创建加法修改器
            /// </summary>
            public static Entry Add(AttributeId attribute, float value, int sourceId = 0)
            {
                return new Entry(attribute, ModifierData.Add(
                    ModifierKey.FromPacked((uint)attribute.Id),
                    value,
                    sourceId
                ));
            }

            /// <summary>
            /// 创建乘法修改器
            /// </summary>
            public static Entry Mul(AttributeId attribute, float value, int sourceId = 0)
            {
                return new Entry(attribute, ModifierData.Mul(
                    ModifierKey.FromPacked((uint)attribute.Id),
                    value,
                    sourceId
                ));
            }

            /// <summary>
            /// 创建百分比加成修改器
            /// </summary>
            public static Entry PercentAdd(AttributeId attribute, float percentValue, int sourceId = 0)
            {
                return new Entry(attribute, ModifierData.PercentAdd(
                    ModifierKey.FromPacked((uint)attribute.Id),
                    percentValue,
                    sourceId
                ));
            }

            /// <summary>
            /// 创建覆盖修改器
            /// </summary>
            public static Entry Override(AttributeId attribute, float value, int sourceId = 0)
            {
                return new Entry(attribute, ModifierData.Override(
                    ModifierKey.FromPacked((uint)attribute.Id),
                    value,
                    sourceId
                ));
            }

            #endregion
        }

        #endregion

        #region 便捷工厂方法

        /// <summary>
        /// 创建加法效果
        /// </summary>
        public static AttributeEffect Add(AttributeId attr, float value, int sourceId = 0)
        {
            return new AttributeEffect(Entry.Add(attr, value, sourceId));
        }

        /// <summary>
        /// 创建乘法效果
        /// </summary>
        public static AttributeEffect Mul(AttributeId attr, float value, int sourceId = 0)
        {
            return new AttributeEffect(Entry.Mul(attr, value, sourceId));
        }

        /// <summary>
        /// 创建百分比加成效果
        /// </summary>
        public static AttributeEffect PercentAdd(AttributeId attr, float percentValue, int sourceId = 0)
        {
            return new AttributeEffect(Entry.PercentAdd(attr, percentValue, sourceId));
        }

        /// <summary>
        /// 创建覆盖效果
        /// </summary>
        public static AttributeEffect Override(AttributeId attr, float value, int sourceId = 0)
        {
            return new AttributeEffect(Entry.Override(attr, value, sourceId));
        }

        #endregion

        #region 扩展方法

        /// <summary>
        /// 获取条目数量
        /// </summary>
        public int Count => Entries.Length;

        /// <summary>
        /// 是否有条目
        /// </summary>
        public bool HasEntries => Count > 0;

        #endregion
    }
}
