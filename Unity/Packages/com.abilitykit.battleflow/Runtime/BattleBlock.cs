using System;
using System.Collections.Generic;

namespace AbilityKit.BattleFlow
{
    /// <summary>积木所属的流程段：编辑器按它把积木分到对应区块（构建前置 / 流程驱动 / 验收断言）。</summary>
    public enum BattleBlockSection
    {
        /// <summary>构建前置：声明世界（环境/角色/障碍/setup 动作），非时间序。</summary>
        Setup = 0,

        /// <summary>流程驱动：真正随时间发生的动作序列。</summary>
        Timeline = 1,

        /// <summary>验收断言：跑完之后才判定的检查。</summary>
        Assertion = 2,

        /// <summary>Case-wide deterministic execution settings, not an ordered scenario action.</summary>
        Settings = 3,
    }

    /// <summary>积木基类：战斗流程的最小组合单元。粒度项目可选——框架给原子积木，项目用复合积木聚合。</summary>
    public abstract class BattleBlock
    {
        /// <summary>稳定 id（积木库注册与序列化用）。</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>显示名（编辑器调色板）。</summary>
        public string DisplayName { get; init; } = string.Empty;

        /// <summary>可选说明。</summary>
        public string? Description { get; init; }

        /// <summary>所属流程段（默认构建前置）。时间线积木覆写为 Timeline，断言积木覆写为 Assertion。</summary>
        public virtual BattleBlockSection Section => BattleBlockSection.Setup;

        /// <summary>浅克隆（供编辑器调色板从模板生成一个可编辑实例；原子积木字段为值类型/string，浅克隆足够）。</summary>
        public BattleBlock Clone() => (BattleBlock)MemberwiseClone();
    }

    /// <summary>原子积木（叶子）：编译成一个 IR 构件。框架提供映射 IR 的内置积木，项目可继承定义自定义积木。</summary>
    public abstract class BattleAtomicBlock : BattleBlock
    {
        /// <summary>把本积木编译进 <see cref="BattleFlowBuilder"/>。</summary>
        public abstract void Compile(BattleFlowBuilder builder);
    }

    /// <summary>
    /// Author-facing intent block. It expands once into internal atomic blocks before validation and compilation.
    /// One intent may contribute to multiple semantic sections.
    /// </summary>
    public abstract class BattleAuthorBlock : BattleBlock
    {
        /// <summary>Validates author-facing parameters before expansion.</summary>
        public virtual IReadOnlyList<string> Validate() => Array.Empty<string>();

        /// <summary>Expands this intent into internal blocks.</summary>
        public abstract IReadOnlyList<BattleBlock> Expand();
    }

    /// <summary>复合积木（容器）：一串子积木，可再嵌套。项目用它把原子积木聚合成「常用测试/预览套路」。</summary>
    public sealed class BattleCompositeBlock : BattleBlock
    {
        /// <summary>子积木（按序编译）。</summary>
        public IReadOnlyList<BattleBlock> Children { get; init; } = Array.Empty<BattleBlock>();
    }
}
