#nullable enable

using AbilityKit.Ability.Flow;
using AbilityKit.Ability.Flow.Blocks;
using AbilityKit.Ability.Flow.Nodes;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Flow
{
    /// <summary>
    /// 演示技能前摇、取消请求和释放结算如何由 FlowRunner 按宿主帧推进。
    /// </summary>
    [Sample(21, "flow", "skill", "cast", "timing", "web", "deterministic")]
    public sealed class SkillCastTiming : SampleBase
    {
        public override string Title => "Skill Cast Timing";
        public override string Description => "使用 FlowRunner、SequenceNode、RaceNode、WaitSecondsNode 和 WaitUntilNode 表达前摇、取消与释放结算";
        public override SampleCategory Category => SampleCategory.Flow;

        protected override void OnRun()
        {
            Section("场景 A：前摇完成并释放技能");
            RunScenario("CastComplete", cancelAt: null);

            Divider();
            Section("场景 B：前摇期间收到取消请求");
            RunScenario("CancelDuringCast", cancelAt: 0.2f);

            Divider();
            Section("这个示例实际接入的包能力");
            Bullet("FlowRunner：由宿主逐帧 Step(delta)，sample 不拥有主循环。");
            Bullet("SequenceNode：串联检查、前摇竞态、结算或失败处理。");
            Bullet("RaceNode：让施法前摇与取消请求竞速，先完成者决定后续状态。");
            Bullet("WaitSecondsNode：表达确定性前摇时间。");
            Bullet("WaitUntilNode：把外部取消信号桥接进流程。");
        }

        private void RunScenario(string name, float? cancelAt)
        {
            var flowContext = new FlowContext();
            var state = new CastTimingState(name, castTime: 0.35f, manaCost: 30, damage: 45);
            flowContext.Set(state);
            KeyValue($"{name}.CastTime", state.CastTime.ToString("F2"));
            KeyValue($"{name}.ManaCost", state.ManaCost.ToString());
            KeyValue($"{name}.InitialMana", state.Mana.ToString());
            KeyValue($"{name}.InitialTargetHp", state.TargetHp.ToString());

            using var runner = new FlowRunner(flowContext);
            runner.Start(
                CreateCastFlow(),
                status => Log($"[{name}] Flow finished: {status}"),
                (previous, next) => Log($"[{name}] Status: {previous} -> {next}"));

            var frame = 0;
            var elapsed = 0f;
            while (runner.Status == FlowStatus.Running && frame < 8)
            {
                frame++;
                var delta = 0.1f;
                elapsed += delta;

                if (cancelAt.HasValue && !state.CancelRequested && elapsed >= cancelAt.Value)
                {
                    state.CancelRequested = true;
                    KeyValue($"{name}.CancelRequested", $"frame={frame}, time={elapsed:F2}");
                }

                AdvanceTime(delta);
                var status = runner.Step(delta);
                KeyValue($"{name}.Frame[{frame}]", $"time={elapsed:F2}, status={status}, phase={state.Phase}");
            }

            KeyValue($"{name}.Mana", state.Mana.ToString());
            KeyValue($"{name}.TargetHp", state.TargetHp.ToString());
            KeyValue($"{name}.Completed", state.Completed.ToString());
            KeyValue($"{name}.Canceled", state.Canceled.ToString());
        }

        private IFlowNode CreateCastFlow()
        {
            return new SequenceNode(
                new ActionNode(onEnter: ctx =>
                {
                    var state = ctx.Get<CastTimingState>();
                    state.Phase = "Check";
                    Log($"[{state.Name}] 检查资源：mana={state.Mana}, cost={state.ManaCost}");
                    if (state.Mana < state.ManaCost)
                    {
                        state.Canceled = true;
                        Warn($"[{state.Name}] 法力不足，标记失败");
                    }
                }),
                new ActionNode(onTick: (ctx, _) => ctx.Get<CastTimingState>().Canceled ? FlowStatus.Failed : FlowStatus.Succeeded),
                new ActionNode(onEnter: ctx =>
                {
                    var state = ctx.Get<CastTimingState>();
                    state.Mana -= state.ManaCost;
                    state.Phase = "Casting";
                    Log($"[{state.Name}] 消耗 {state.ManaCost} 法力，进入 {state.CastTime:F2}s 前摇");
                }),
                new RaceNode(
                    new SequenceNode(
                        new WaitSecondsNode(0.35f),
                        new ActionNode(onEnter: ctx =>
                        {
                            var state = ctx.Get<CastTimingState>();
                            state.CastReady = true;
                            state.Phase = "CastReady";
                            Log($"[{state.Name}] 前摇完成，可以释放技能");
                        })),
                    new SequenceNode(
                        new WaitUntilNode(ctx => ctx.Get<CastTimingState>().CancelRequested),
                        new ActionNode(onEnter: ctx =>
                        {
                            var state = ctx.Get<CastTimingState>();
                            state.Canceled = true;
                            state.Phase = "Canceled";
                            Log($"[{state.Name}] 收到取消请求，中断前摇");
                        }))),
                new ActionNode(onTick: (ctx, _) =>
                {
                    var state = ctx.Get<CastTimingState>();
                    return state.Canceled ? FlowStatus.Failed : FlowStatus.Succeeded;
                }),
                new ActionNode(onEnter: ctx =>
                {
                    var state = ctx.Get<CastTimingState>();
                    state.TargetHp -= state.Damage;
                    state.Completed = true;
                    state.Phase = "Applied";
                    Log($"[{state.Name}] 技能释放成功，造成 {state.Damage} 伤害");
                }));
        }

        private sealed class CastTimingState
        {
            public CastTimingState(string name, float castTime, int manaCost, int damage)
            {
                Name = name;
                CastTime = castTime;
                ManaCost = manaCost;
                Damage = damage;
                Mana = 100;
                TargetHp = 120;
                Phase = "Idle";
            }

            public string Name { get; }
            public float CastTime { get; }
            public int ManaCost { get; }
            public int Damage { get; }
            public int Mana { get; set; }
            public int TargetHp { get; set; }
            public string Phase { get; set; }
            public bool CancelRequested { get; set; }
            public bool CastReady { get; set; }
            public bool Completed { get; set; }
            public bool Canceled { get; set; }
        }
    }
}
