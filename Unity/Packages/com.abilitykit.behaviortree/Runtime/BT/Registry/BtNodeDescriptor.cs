using System;
using System.Collections.Generic;

namespace AbilityKit.BehaviorTree
{
    /// <summary>属性字段的编辑语义：字面量 / 黑板 key 引用 / 枚举（Int64 索引）。</summary>
    public enum BtPropertyFieldKind
    {
        Literal = 0,
        BlackboardKeyRef = 1,
        Enum = 2,
    }

    /// <summary>
    /// 节点属性 schema 字段：编辑器据此生成类型化控件与校验。包外节点作者通过它声明编辑体验——
    /// 枚举下拉、黑板 key 下拉、数值范围、排序、提示，全部无需写编辑器代码。
    /// </summary>
    public sealed class BtPropertyField
    {
        public string Name { get; }
        public BtValueType Type { get; }
        public BtPropertyValue? Default { get; }
        public string Tooltip { get; }
        public BtPropertyFieldKind Kind { get; }
        /// <summary>Enum 字段的可选项（值 = 索引）；其余字段为 null。</summary>
        public IReadOnlyList<string> Options { get; }
        /// <summary>数值范围（Int64 用整数值，Fixed64 用 raw）。可空表示不限。</summary>
        public long? Min { get; }
        public long? Max { get; }
        /// <summary>Inspector 展示排序（升序）。</summary>
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

        /// <summary>枚举字段（Int64 值 = 选项索引）。</summary>
        public static BtPropertyField Enum(
            string name, IReadOnlyList<string> options, long defaultIndex = 0, string tooltip = "", int order = 0)
        {
            return new BtPropertyField(name, BtValueType.Int64,
                BtPropertyValue.Of(defaultIndex), tooltip, BtPropertyFieldKind.Enum, options, order: order);
        }

        /// <summary>黑板 key 引用字段（String 值 = key 名，编辑器渲染为 key 下拉，校验检查声明）。</summary>
        public static BtPropertyField KeyRef(string name, string tooltip = "", int order = 0)
        {
            return new BtPropertyField(name, BtValueType.String,
                BtPropertyValue.Of(""), tooltip, BtPropertyFieldKind.BlackboardKeyRef, order: order);
        }
    }

    /// <summary>节点声明的黑板访问（可选元数据，用于加载期校验 key 存在且类型一致）。</summary>
    public sealed class BtBlackboardKeyRef
    {
        public string Key { get; }
        public BtValueType Type { get; }

        public BtBlackboardKeyRef(string key, BtValueType type)
        {
            Key = key;
            Type = type;
        }
    }

    /// <summary>
    /// 节点描述符：注册中心的登记项。编辑器只认识描述符（菜单/端口/属性面板全部由
    /// PropertySchema 驱动生成），不认识 CLR 继承链——这是包外扩展零编辑器代码的基础。
    /// </summary>
    public sealed class BtNodeDescriptor
    {
        public string TypeId { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public BtNodeKind Kind { get; }
        public int MinChildren { get; }
        /// <summary>最大子节点数；-1 表示不限。</summary>
        public int MaxChildren { get; }
        public IReadOnlyList<BtPropertyField> PropertySchema { get; }
        public IReadOnlyList<BtBlackboardKeyRef> BlackboardKeys { get; }
        /// <summary>节点主题色（hex，如 "#4a90d9"）；null 时编辑器按 Kind 给默认色。</summary>
        public string? ColorHint { get; }
        /// <summary>菜单内同分类排序权重（升序）。</summary>
        public int MenuOrder { get; }
        public Func<BtNodeBase> Factory { get; }

        public BtNodeDescriptor(
            string typeId,
            string displayName,
            string category,
            BtNodeKind kind,
            int minChildren,
            int maxChildren,
            Func<BtNodeBase> factory,
            IReadOnlyList<BtPropertyField>? propertySchema = null,
            IReadOnlyList<BtBlackboardKeyRef>? blackboardKeys = null,
            string? colorHint = null,
            int menuOrder = 0)
        {
            TypeId = typeId;
            DisplayName = displayName;
            Category = category;
            Kind = kind;
            MinChildren = minChildren;
            MaxChildren = maxChildren;
            Factory = factory;
            PropertySchema = propertySchema ?? Array.Empty<BtPropertyField>();
            BlackboardKeys = blackboardKeys ?? Array.Empty<BtBlackboardKeyRef>();
            ColorHint = colorHint;
            MenuOrder = menuOrder;
        }
    }
}

