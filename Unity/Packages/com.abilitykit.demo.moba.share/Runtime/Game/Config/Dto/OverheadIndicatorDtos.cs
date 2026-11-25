using System;

namespace AbilityKit.Demo.Moba.Share.Config
{
    /// <summary>
    /// 头顶指示器允许的只读来源；数值与正式 Luban 枚举保持一致。
    /// </summary>
    public enum IndicatorSourceKind { None = 0, Resource = 1, ActorCounter = 2, HfsmParameter = 3, BuffStack = 4, BuffPresent = 5 }

    /// <summary>
    /// 数据语义与渲染类型独立，新增内容不得按英雄身份选择算法。
    /// </summary>
    public enum IndicatorSemantic { Health = 1, Mana = 2, Energy = 3, Rage = 4, Ammo = 5, Counter = 6 }

    /// <summary>
    /// 通用指示器渲染类型。
    /// </summary>
    public enum IndicatorRendererKind { ContinuousBar = 1, Segments = 2, Number = 3 }

    /// <summary>
    /// 枚举值映射；声明映射后未覆盖的输入视为不可用。
    /// </summary>
    [Serializable]
    public sealed class IndicatorValueMappingDTO { public float Input; public float Output; }

    /// <summary>
    /// 有限的条件值覆盖，按配置顺序匹配第一项，不执行任意表达式。
    /// </summary>
    [Serializable]
    public sealed class IndicatorValueOverrideDTO
    {
        public IndicatorSourceKind SourceKind;
        public int SourceKey;
        public float Expected;
        public float Value;
    }

    /// <summary>
    /// 指示器定义；Max 为零的 Resource 读取当前属性上限，负值表示不显示上限。
    /// </summary>
    [Serializable]
    public sealed class IndicatorDefinitionDTO
    {
        public int Id;
        public string Name;
        public IndicatorSemantic Semantic;
        public IndicatorSourceKind SourceKind;
        public int SourceKey;
        public bool MissingAsZero;
        public bool AggregateMax;
        public float Scale = 1;
        public float Offset;
        public float Max;
        public float ReadyValue = -1;
        public bool ReadyAtLeast;
        public IndicatorValueMappingDTO[] Mappings;
        public IndicatorValueOverrideDTO[] Overrides;
        public int VisibilityBuffId;
        public bool VisibilityBuffPresent;
    }

    /// <summary>
    /// 布局中的一行；Slot 相同的互斥行共用位置，局部计数可只对本地角色显示。
    /// </summary>
    [Serializable]
    public sealed class OverheadIndicatorRowDTO
    {
        public int IndicatorId;
        public IndicatorRendererKind Renderer;
        public int Slot;
        public float Height;
        public bool LocalOnly;
        public string Style;
        public string Label;
        public string EmptyLabel;
    }

    /// <summary>
    /// 头顶面板布局；所有行共享世界锚点并使用屏幕空间间距。
    /// </summary>
    [Serializable]
    public sealed class OverheadLayoutDTO
    {
        public int Id;
        public string Name;
        public float Width;
        public float Spacing;
        public float WorldOffsetY;
        public OverheadIndicatorRowDTO[] Rows;
    }

    /// <summary>
    /// Actor 内容绑定；ContentKind 为 Character/Summon/UnitSubType（1/2/3），键零为分类缺省。
    /// </summary>
    [Serializable]
    public sealed class ActorOverheadBindingDTO
    {
        public int Id;
        public int ContentKind;
        public int ContentId;
        public int LayoutId;
    }
}
