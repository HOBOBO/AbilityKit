#nullable enable

using AbilityKit.Ability.Flow;
using AbilityKit.Ability.Flow.Nodes;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Flow
{
    /// <summary>
    /// Flow 基础示例：真实运行 FlowRunner 和节点。
    /// </summary>
    [Sample(20, "flow", "package-api")]
    public sealed class FlowBasics : SampleBase
    {
        public override string Title => "Flow Basics";
        public override string Description => "使用 AbilityKit.Flow 的 FlowRunner、SequenceNode、WaitSecondsNode 和 ActionNode 编排施法流程";
        public override SampleCategory Category => SampleCategory.Flow;

        protected override void OnRun()
        {
            var flowContext = new FlowContext();
            flowContext.Set(new CastState(mana: 100, damage: 45));
            KeyValue("Flow.InitialMana", "100");
            KeyValue("Flow.Damage", "45");
            KeyValue("Flow.InitialTargetHp", "120");

            using var runner = new FlowRunner(flowContext);
            runner.Start(
                CreateFireballFlow(),
                status => Log($"Flow finished: {status}"),
                (previous, next) => Log($"Status: {previous} -> {next}"));

            Section("运行 Flow");
            var frame = 0;
            while (runner.Status == FlowStatus.Running && frame < 8)
            {
                frame++;
                var delta = 0.1f;
                AdvanceTime(delta);
                var status = runner.Step(delta);
                KeyValue($"Frame {frame}", $"time={Time:F2}, status={status}");
                KeyValue("Flow.Frame", $"{frame}:time={Time:F2},status={status}");
            }

            Divider();
            Section("结果状态");
            var state = flowContext.Get<CastState>();
            KeyValue("Mana", state.Mana.ToString());
            KeyValue("TargetHp", state.TargetHp.ToString());
            KeyValue("CastCompleted", state.CastCompleted.ToString());

            Divider();
            Section("这个示例实际接入的包能力");
            Bullet("FlowRunner：负责启动、Step、完成回调和异常边界。");
            Bullet("FlowContext：在节点之间共享 CastState。");
            Bullet("SequenceNode：顺序执行检查、等待、结算。");
            Bullet("WaitSecondsNode：由宿主推进 deltaTime，表达跨帧等待。");
            Bullet("ActionNode：把具体业务动作挂到流程节点上。");
        }

        private IFlowNode CreateFireballFlow()
        {
            return new SequenceNode(
                new ActionNode(onEnter: ctx =>
                {
                    var state = ctx.Get<CastState>();
                    Log("[Check] 检查法力和目标状态");
                    if (state.Mana < 30)
                    {
                        state.CastCompleted = false;
                        Warn("法力不足，本示例会继续展示节点运行，但真实技能可在这里返回 Failed。");
                    }
                }),
                new ActionNode(onEnter: ctx =>
                {
                    var state = ctx.Get<CastState>();
                    state.Mana -= 30;
                    Log("[Consume] 消耗 30 法力");
                }),
                new WaitSecondsNode(0.25f),
                new ActionNode(onEnter: ctx =>
                {
                    var state = ctx.Get<CastState>();
                    state.TargetHp -= state.Damage;
                    state.CastCompleted = true;
                    Log($"[Apply] 火球命中，造成 {state.Damage} 伤害");
                }));
        }

        private sealed class CastState
        {
            public CastState(int mana, int damage)
            {
                Mana = mana;
                Damage = damage;
                TargetHp = 120;
            }

            public int Mana { get; set; }
            public int Damage { get; }
            public int TargetHp { get; set; }
            public bool CastCompleted { get; set; }
        }
    }
}
