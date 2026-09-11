#nullable enable

using AbilityKit.Ability.Flow;
using AbilityKit.Ability.Flow.Blocks;
using AbilityKit.Ability.Flow.Nodes;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Flow
{
    /// <summary>
    /// 演示 Sequence、Race 和 ParallelAll 三种 Flow 组合器的真实执行行为。
    /// </summary>
    [Sample(22, "flow", "sequence", "race", "parallel", "web", "deterministic")]
    public sealed class SequenceAndRace : SampleBase
    {
        public override string Title => "Sequence and Race";
        public override string Description => "使用真实 FlowRunner 对比 SequenceNode、RaceNode 与 ParallelAllNode 的执行语义";
        public override SampleCategory Category => SampleCategory.Flow;

        protected override void OnRun()
        {
            Section("SequenceNode：按顺序完成三个阶段");
            RunFlow("Sequence", CreateSequenceFlow(), maxFrames: 8);

            Divider();
            Section("RaceNode：最快完成的分支获胜");
            RunFlow("Race", CreateRaceFlow(), maxFrames: 8);

            Divider();
            Section("ParallelAllNode：等待所有分支完成");
            RunFlow("ParallelAll", CreateParallelAllFlow(), maxFrames: 8);

            Divider();
            Section("组合器选择建议");
            Bullet("SequenceNode：用于技能检查 -> 前摇 -> 结算这类严格顺序流程。");
            Bullet("RaceNode：用于超时、取消、命中窗口等先到先赢流程。");
            Bullet("ParallelAllNode：用于动画、音效、命中特效等都结束后再继续的流程。");
        }

        private void RunFlow(string name, IFlowNode root, int maxFrames)
        {
            var flowContext = new FlowContext();
            flowContext.Set(new ComposerState(name));

            using var runner = new FlowRunner(flowContext);
            runner.Start(
                root,
                status => Log($"[{name}] Flow finished: {status}"),
                (previous, next) => Log($"[{name}] Status: {previous} -> {next}"));

            KeyValue($"{name}.Pattern", name);
            var frame = 0;
            while (runner.Status == FlowStatus.Running && frame < maxFrames)
            {
                frame++;
                const float delta = 0.1f;
                AdvanceTime(delta);
                var status = runner.Step(delta);
                var state = flowContext.Get<ComposerState>();
                KeyValue($"{name}.Frame[{frame}]", $"time={Time:F2}, status={status}, last={state.LastEvent}");
                KeyValue($"{name}.Frame", $"{frame}:time={Time:F2},status={status},last={state.LastEvent}");
                if (name == "Race" && state.LastEvent == "Fast branch won")
                {
                    KeyValue("Race.Winner", state.LastEvent);
                }
            }
        }

        private IFlowNode CreateSequenceFlow()
        {
            return new SequenceNode(
                Mark("Sequence", "Check"),
                new WaitSecondsNode(0.1f),
                Mark("Sequence", "Cast"),
                new WaitSecondsNode(0.1f),
                Mark("Sequence", "Apply"));
        }

        private IFlowNode CreateRaceFlow()
        {
            return new RaceNode(
                new SequenceNode(
                    new WaitSecondsNode(0.2f),
                    Mark("Race", "Fast branch won")),
                new SequenceNode(
                    new WaitSecondsNode(0.4f),
                    Mark("Race", "Slow branch completed")));
        }

        private IFlowNode CreateParallelAllFlow()
        {
            return new ParallelAllNode(
                new SequenceNode(
                    new WaitSecondsNode(0.1f),
                    Mark("ParallelAll", "Animation done")),
                new SequenceNode(
                    new WaitSecondsNode(0.2f),
                    Mark("ParallelAll", "Audio done")),
                new SequenceNode(
                    new WaitSecondsNode(0.3f),
                    Mark("ParallelAll", "Hit effect done")));
        }

        private IFlowNode Mark(string flowName, string eventName)
        {
            return new ActionNode(onEnter: ctx =>
            {
                var state = ctx.Get<ComposerState>();
                state.LastEvent = eventName;
                Log($"[{flowName}] {eventName}");
            });
        }

        private sealed class ComposerState
        {
            public ComposerState(string flowName)
            {
                FlowName = flowName;
                LastEvent = "Started";
            }

            public string FlowName { get; }
            public string LastEvent { get; set; }
        }
    }
}
