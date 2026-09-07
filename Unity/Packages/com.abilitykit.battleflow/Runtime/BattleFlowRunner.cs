using System.Collections.Generic;
using AbilityKit.Scenario;

namespace AbilityKit.BattleFlow
{
    /// <summary>运行结果（中性）：headless 跑完场景后的判定 + 摘要 + 可选 trace 树。项目把它的判定/覆盖率/trace 映射到这个中性形状。</summary>
    public sealed class BattleFlowRunResult
    {
        /// <summary>是否通过（verdict）。</summary>
        public bool Passed { get; init; }

        /// <summary>人类可读的判定摘要 / trace 概况。</summary>
        public string Summary { get; init; } = string.Empty;

        /// <summary>可选的溯源树（中性），供编辑器渲染命中链路；无 trace 时为 null。</summary>
        public IReadOnlyList<BattleFlowTraceNode>? Trace { get; init; }
    }

    /// <summary>中性溯源节点（编辑器渲染 trace 树用）。项目把它的 trace 记录（如 MobaAcceptanceTraceRecord）映射到这个形状。</summary>
    public sealed class BattleFlowTraceNode
    {
        /// <summary>节点 id。</summary>
        public long Id { get; set; }

        /// <summary>父节点 id（根为 0）。</summary>
        public long ParentId { get; set; }

        /// <summary>根节点 id。</summary>
        public long RootId { get; set; }

        /// <summary>trace 类别名（如 SkillCast / EffectExecution / DamageApply）。</summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>配置 id（技能/效果 id 等，0 表示无）。</summary>
        public int ConfigId { get; set; }

        /// <summary>触发帧。</summary>
        public int Frame { get; set; }
    }

    /// <summary>战斗流程运行钩子：项目实现它，把编译出的 <see cref="TestScenario"/> 在项目世界里 headless 跑出判定。</summary>
    public interface IBattleFlowRunner
    {
        /// <summary>跑一个已编译、已通过校验的场景，返回中性运行结果。</summary>
        BattleFlowRunResult Run(TestScenario scenario);
    }

    /// <summary>战斗流程批量运行钩子：项目实现它，批量跑一个目录下的 .battleflow 并返回可读报告文本。</summary>
    public interface IBattleFlowBatchRunner
    {
        /// <summary>批量跑一个目录下的 .battleflow，返回汇总报告文本（total/passed/failed + 每例结果）。</summary>
        string RunDirectory(string directory);
    }

    /// <summary>运行器注册表：项目在编辑器里注册自己的 runner（如 MOBA 的 MobaBattleFlowRunner）。</summary>
    public static class BattleFlowRunnerRegistry
    {
        /// <summary>当前注册的运行器；未注册时编辑器「运行」按钮会提示。</summary>
        public static IBattleFlowRunner? Runner { get; set; }

        /// <summary>当前注册的批量运行器；未注册时编辑器「批量运行」按钮会提示。</summary>
        public static IBattleFlowBatchRunner? BatchRunner { get; set; }
    }
}
