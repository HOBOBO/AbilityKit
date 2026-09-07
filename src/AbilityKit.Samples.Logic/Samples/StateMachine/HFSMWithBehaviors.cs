using System;
using System.Collections.Generic;
using AbilityKit.HFSM;
using AbilityKit.HFSM.Extension;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.StateMachine
{
    /// <summary>
    /// HFSMWithBehaviors - 演示状态中的行为执行
    /// 类似于 Unity Node Canvas，状态可以内嵌行为树来执行持续性行为
    /// </summary>
    [Sample]
    public sealed class HFSMWithBehaviors : SampleBase
    {
        public override string Title => "HFSM with Behaviors";
        public override string Description => "状态中的行为执行、行为状态机 CompositeActionState";
        public override SampleCategory Category => SampleCategory.StateMachine;

        protected override void OnRun()
        {
            Log("=== HFSM 行为状态示例 ===");
            Output.Divider();

            // 1. 核心概念
            Log("【1】核心概念");
            Output.Bullet("IActionBehaviour - 行为接口，每帧 Tick 执行");
            Output.Bullet("CompositeActionState - 支持行为执行的状态");
            Output.Bullet("SequenceBehaviour - 顺序执行子行为");
            Output.Bullet("ParallelBehaviour - 并行执行子行为");
            Output.Bullet("CallbackBehaviour - 回调行为");
            Output.Bullet("DelayBehaviour - 延迟行为");
            Log("");

            // 2. 基础行为执行
            Log("【2】基础行为执行 - 使用 CallbackBehaviour");
            var basicBehaviorFsm = CreateBasicBehaviorFsm();
            SimulateFrames(5);
            Log("");

            // 3. 顺序行为序列
            Log("【3】顺序行为序列 - SequenceBehaviour");
            var sequenceFsm = CreateSequenceBehaviorFsm();
            Log("序列行为: [延迟 0.1s] -> [回调 A] -> [延迟 0.1s] -> [回调 B]");
            SimulateFrames(10);
            Log("");

            // 4. 并行行为
            Log("【4】并行行为 - ParallelBehaviour");
            var parallelFsm = CreateParallelBehaviorFsm();
            Log("并行行为: 同时执行 [倒计时] 和 [状态更新]");
            SimulateFrames(5);
            Log("");

            // 5. 复合行为状态
            Log("【5】复合行为状态 - CompositeActionState");
            Log("CompositeActionState 将行为树嵌入到状态中");
            var compositeFsm = CreateCompositeActionStateFsm();
            Log("复合状态: 等待 3 秒，期间每秒输出一条消息");
            SimulateFrames(15);
            Log("");

            // 6. 循环行为
            Log("【6】循环行为 - 重复执行子行为");
            var loopFsm = CreateLoopBehaviorFsm();
            Log("循环行为: 每 0.05s 执行一次 [计数递增]，共 3 次");
            SimulateFrames(10);
            Log("");

            // 7. 条件行为
            Log("【7】条件行为组合");
            var conditionalFsm = CreateConditionalBehaviorFsm();
            Log("条件行为: 根据计数器决定执行哪个分支");
            SimulateFrames(10);

            Output.Divider();

            Log("【8】总结");
            Output.Bullet("IActionBehaviour.Tick() - 每帧执行，返回 Running/Success/Failure");
            Output.Bullet("ActionBehaviourContext - 包含 deltaTime、timeScale、speed 等");
            Output.Bullet("CompositeActionState - 将行为树嵌入到状态生命周期");
            Output.Bullet("BehaviorStatus vs ActionBehaviourStatus - 两种状态枚举");
        }

        private void SimulateFrames(int frameCount)
        {
            Log($"--- 模拟 {frameCount} 帧 ---");
        }

        #region 示例 1: 基础回调行为

        private StateMachine<string> CreateBasicBehaviorFsm()
        {
            var fsm = new StateMachine<string>(needsExitTime: true);

            var counter = 0;
            var state = new CompositeActionState<string, string>(
                needsExitTime: true,
                isGhostState: false
            );

            state.SetRoot(new CallbackBehaviour(() =>
            {
                counter++;
                Log($"  [Callback] 执行次数: {counter}");
            }));

            state.SetLoop(true);

            fsm.AddState("Counter", state);
            fsm.SetStartState("Counter");
            fsm.Init();

            Log("  初始计数器值: 0");
            return fsm;
        }

        #endregion

        #region 示例 2: 顺序行为序列

        private StateMachine<string> CreateSequenceBehaviorFsm()
        {
            var fsm = new StateMachine<string>(needsExitTime: true);

            var step = 0;
            var state = new CompositeActionState<string, string>(needsExitTime: true);

            state.SetRoot(
                new SequenceBehaviour()
                    .Add(new DelayBehaviour(0.1f))
                    .Add(new CallbackBehaviour(() =>
                    {
                        step++;
                        Log($"  [步骤 A] 完成 (步骤 {step})");
                    }))
                    .Add(new DelayBehaviour(0.1f))
                    .Add(new CallbackBehaviour(() =>
                    {
                        step++;
                        Log($"  [步骤 B] 完成 (步骤 {step})");
                    }))
            );

            state.SetLoop(true);

            fsm.AddState("Sequence", state);
            fsm.SetStartState("Sequence");
            fsm.Init();

            Log("  初始步骤: 0");
            return fsm;
        }

        #endregion

        #region 示例 3: 并行行为

        private StateMachine<string> CreateParallelBehaviorFsm()
        {
            var fsm = new StateMachine<string>(needsExitTime: true);

            var countdown = 2;
            var updateCount = 0;

            var state = new CompositeActionState<string, string>(needsExitTime: true);

            state.SetRoot(
                new ParallelBehaviour()
                    // 倒计时分支
                    .Add(new SequenceBehaviour()
                        .Add(new CallbackBehaviour(() => Log($"  [倒计时] 剩余 {countdown} 秒")))
                        .Add(new DelayBehaviour(1f))
                        .Add(new CallbackBehaviour(() => countdown--))
                    )
                    // 状态更新分支
                    .Add(new SequenceBehaviour()
                        .Add(new DelayBehaviour(0.5f))
                        .Add(new CallbackBehaviour(() =>
                        {
                            updateCount++;
                            Log($"  [更新] 状态已更新 {updateCount} 次");
                        }))
                    )
            );

            fsm.AddState("Parallel", state);
            fsm.SetStartState("Parallel");
            fsm.Init();

            Log("  初始倒计时: 2 秒");
            return fsm;
        }

        #endregion

        #region 示例 4: 复合行为状态

        private StateMachine<string> CreateCompositeActionStateFsm()
        {
            var fsm = new StateMachine<string>(needsExitTime: true);

            var messageIndex = 0;
            var messages = new[] { "初始化...", "加载资源...", "准备完成!" };

            var state = new CompositeActionState<string, string>(needsExitTime: true);

            state.SetRoot(
                new SequenceBehaviour()
                    .Add(new CallbackBehaviour(() => Log($"  [Loading] 开始加载")))
                    .Add(new DelayBehaviour(1f))
                    .Add(new CallbackBehaviour(() =>
                    {
                        messageIndex = 0;
                        Log($"  [Loading] {messages[messageIndex]}");
                    }))
                    .Add(new DelayBehaviour(1f))
                    .Add(new CallbackBehaviour(() =>
                    {
                        messageIndex = 1;
                        Log($"  [Loading] {messages[messageIndex]}");
                    }))
                    .Add(new DelayBehaviour(1f))
                    .Add(new CallbackBehaviour(() =>
                    {
                        messageIndex = 2;
                        Log($"  [Loading] {messages[messageIndex]}");
                    }))
                    .Add(new CallbackBehaviour(() => Log($"  [Loading] 加载完成，3 秒已过")))
            );

            fsm.AddState("Loading", state);
            fsm.SetStartState("Loading");
            fsm.Init();

            return fsm;
        }

        #endregion

        #region 示例 5: 循环行为

        private StateMachine<string> CreateLoopBehaviorFsm()
        {
            var fsm = new StateMachine<string>(needsExitTime: true);

            var loopCount = 0;
            var totalIterations = 0;

            var state = new CompositeActionState<string, string>(needsExitTime: true);

            // 循环执行 3 次，每次间隔 0.05s
            state.SetRoot(
                new SequenceBehaviour()
                    .Add(new CallbackBehaviour(() => loopCount = 0))
                    .Add(new LoopBehaviour(3, new SequenceBehaviour()
                        .Add(new DelayBehaviour(0.05f))
                        .Add(new CallbackBehaviour(() =>
                        {
                            loopCount++;
                            totalIterations++;
                            Log($"  [循环] 第 {loopCount} 次迭代 (总计 {totalIterations})");
                        }))
                    ))
                    .Add(new CallbackBehaviour(() => Log($"  [循环] 3 次迭代完成")))
            );

            fsm.AddState("Loop", state);
            fsm.SetStartState("Loop");
            fsm.Init();

            return fsm;
        }

        #endregion

        #region 示例 6: 条件行为组合

        private StateMachine<string> CreateConditionalBehaviorFsm()
        {
            var fsm = new StateMachine<string>(needsExitTime: true);

            var condition = true;
            var branch = 0;

            var state = new CompositeActionState<string, string>(needsExitTime: true);

            state.SetRoot(
                new SequenceBehaviour()
                    .Add(new CallbackBehaviour(() =>
                    {
                        branch++;
                        Log($"  [分支选择] 第 {branch} 轮");
                        condition = !condition; // 交替切换条件
                    }))
                    .Add(new DelayBehaviour(0.05f))
                    .Add(new CallbackBehaviour(() =>
                    {
                        if (condition)
                            Log($"  [分支 A] 条件为 True，执行 A 分支");
                        else
                            Log($"  [分支 B] 条件为 False，执行 B 分支");
                    }))
            );

            state.SetLoop(true);

            fsm.AddState("Conditional", state);
            fsm.SetStartState("Conditional");
            fsm.Init();

            return fsm;
        }

        #endregion
    }

    /// <summary>
    /// 循环行为 - 重复执行子行为指定次数
    /// </summary>
    public sealed class LoopBehaviour : IActionBehaviour
    {
        private readonly int _count;
        private readonly IActionBehaviour _child;
        private int _currentIteration;

        public LoopBehaviour(int count, IActionBehaviour child)
        {
            _count = count;
            _child = child;
        }

        public void Reset()
        {
            _currentIteration = 0;
            _child?.Reset();
        }

        public ActionBehaviourStatus Tick(in ActionBehaviourContext ctx)
        {
            if (_child == null) return ActionBehaviourStatus.Success;
            if (_count <= 0) return ActionBehaviourStatus.Success;

            while (_currentIteration < _count)
            {
                var status = _child.Tick(ctx);

                if (status == ActionBehaviourStatus.Running)
                    return ActionBehaviourStatus.Running;

                _currentIteration++;
                _child.Reset();
            }

            return ActionBehaviourStatus.Success;
        }
    }
}
