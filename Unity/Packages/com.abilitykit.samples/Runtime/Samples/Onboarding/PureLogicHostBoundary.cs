#nullable enable

using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Onboarding
{
    /// <summary>
    /// 新手导览：理解纯逻辑示例和宿主环境的边界。
    /// </summary>
    [Sample(1, "onboarding", "host", "logic")]
    public sealed class PureLogicHostBoundary : SampleBase
    {
        public override string Title => "Pure Logic And Host Boundary";
        public override string Description => "同一份逻辑通过 SampleRuntimeContext 适配控制台、文件输出和未来游戏宿主";
        public override SampleCategory Category => SampleCategory.Onboarding;

        protected override void OnRun()
        {
            Section("当前宿主上下文");
            KeyValue("HostKind", Context.HostKind.ToString());
            KeyValue("ExecutionMode", Environment.GetType().Name);
            KeyValue("OutputDirectory", string.IsNullOrWhiteSpace(Context.OutputDirectory) ? "(未指定)" : Context.OutputDirectory);
            KeyValue("HasConfigProvider", Config != null ? "true" : "false");
            KeyValue("HasResourceProvider", Resources != null ? "true" : "false");

            Divider();
            Section("同一份逻辑依赖的只是抽象");
            Bullet("ILogger：输出到控制台、文件、Unity Console 或 MonoGame overlay 都可以由宿主决定。");
            Bullet("ISampleEnvironment：时间由宿主推进，sample 不直接依赖 Thread.Sleep 或引擎帧循环。");
            Bullet("IConfigProvider / IResourceProvider：配置来源可以是文件、内存、Addressables 或远端服务。");

            Divider();
            Section("时间与帧回调演示");
            var ticks = 0;
            Environment.OnTick += delta =>
            {
                ticks++;
                Log($"  Tick {ticks}: delta={delta:F3}, time={Time:F3}");
            };

            Log($"初始 time={Time:F3}");
            AdvanceTime(0.100f);
            KeyValue("AdvanceTime 后", Time.ToString("F3"));
            Environment.Tick();
            KeyValue("Tick 回调次数", ticks.ToString());

            Pause();
            KeyValue("Pause 后 IsPaused", Environment.IsPaused.ToString());
            AdvanceTime(1.000f);
            KeyValue("Pause 后尝试 AdvanceTime", Time.ToString("F3"));
            Resume();
            KeyValue("Resume 后 IsPaused", Environment.IsPaused.ToString());

            Line();
            Bullet("Instant 模式偏向立即执行，Pause 可能只是宿主层的空操作。");
            Bullet("使用 --mode simulated 运行本示例，可以观察更接近游戏帧循环的暂停和 Tick 行为。");

            Divider();
            Log("结论：sample 只表达玩法逻辑，真正的输出、资源和帧推进都交给宿主环境。");
        }
    }
}
