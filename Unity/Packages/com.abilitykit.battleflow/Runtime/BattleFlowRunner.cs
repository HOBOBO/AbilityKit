using AbilityKit.Scenario;

namespace AbilityKit.BattleFlow
{
    /// <summary>运行结果（中性）：headless 跑完场景后的判定 + 摘要。项目把它的判定/覆盖率映射到这个中性形状。</summary>
    public sealed class BattleFlowRunResult
    {
        /// <summary>是否通过（verdict）。</summary>
        public bool Passed { get; init; }

        /// <summary>人类可读的判定摘要 / trace 概况。</summary>
        public string Summary { get; init; } = string.Empty;
    }

    /// <summary>战斗流程运行钩子：项目实现它，把编译出的 <see cref="TestScenario"/> 在项目世界里 headless 跑出判定。</summary>
    public interface IBattleFlowRunner
    {
        /// <summary>跑一个已编译、已通过校验的场景，返回中性运行结果。</summary>
        BattleFlowRunResult Run(TestScenario scenario);
    }

    /// <summary>运行器注册表：项目在编辑器里注册自己的 runner（如 MOBA 的 MobaBattleFlowRunner）。</summary>
    public static class BattleFlowRunnerRegistry
    {
        /// <summary>当前注册的运行器；未注册时编辑器「运行」按钮会提示。</summary>
        public static IBattleFlowRunner? Runner { get; set; }
    }
}
