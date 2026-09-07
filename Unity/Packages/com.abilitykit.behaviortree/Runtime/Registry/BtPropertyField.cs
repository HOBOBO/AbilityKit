using System;
using System.Collections.Generic;

namespace AbilityKit.BehaviorTree
{
    /// <summary>
    /// 节点属schema 字段：编辑器据此生成类型化控件与校验。包外节点作者通过它声明编辑体验—
    /// 枚举下拉、黑key 下拉、数值范围、排序、提示，全部无需写编辑器代码
    /// </summary>
    [System.Obsolete("Use the unprefixed BehaviorTree API in segmented namespaces.", false)]
    public sealed class BtPropertyField
    {
        public string Name { get; }
        public BtValueType Type { get; }
        public BtPropertyValue? Default { get; }
        public string Tooltip { get; }
        public BtPropertyFieldKind Kind { get; }
        /// <summary>Enum 字段的可选项（= 索引）；其余字段null</summary>
        public IReadOnlyList<string> Options { get; }
        /// <summary>数值范围（Int64 用整数值，Fixed64 raw）。可空表示不限</summary>
        public long? Min { get; }
        public long? Max { get; }
        /// <summary>Inspector 展示排序（升序）</summary>
        public int Order { get; }

        public BtPropertyField(
            string name,
            BtValueType type,
            BtPropertyValue? @default = null,
            string tooltip = "",
            BtPropertyFieldKind kind = BtPropertyFieldKind.Literal,
            IReadOnlyList<string>? options = null,
            long? min = null,
            long? max = null,
            int order = 0)
        {
            Name = name;
            Type = type;
            Default = @default;
            Tooltip = tooltip ?? "";
            Kind = kind;
            Options = options ?? Array.Empty<string>();
            Min = min;
            Max = max;
            Order = order;
        }

        /// <summary>枚举字段（Int64 = 选项索引）</summary>
        public static BtPropertyField Enum(
            string name, IReadOnlyList<string> options, long defaultIndex = 0, string tooltip = "", int order = 0)
        {
            return new BtPropertyField(name, BtValueType.Int64,
                BtPropertyValue.Of(defaultIndex), tooltip, BtPropertyFieldKind.Enum, options, order: order);
        }

        /// <summary>黑板 key 引用字段（String = key 名，编辑器渲染为 key 下拉，校验检查声明）</summary>
        public static BtPropertyField KeyRef(string name, string tooltip = "", int order = 0)
        {
            return new BtPropertyField(name, BtValueType.String,
                BtPropertyValue.Of(""), tooltip, BtPropertyFieldKind.BlackboardKeyRef, order: order);
        }
    }
}
