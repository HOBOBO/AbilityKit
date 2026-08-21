using AbilityKit.Deterministic;
using Xunit;
using static AbilityKit.BehaviorTree.Tests.TestNodeTypes;

namespace AbilityKit.BehaviorTree.Tests
{
    /// <summary>执行语义：组合/装饰推进、并行分支、条件中断（Self/LowerPriority/Both）、装饰器抢占。</summary>
    public sealed class BtExecutionSemanticsTests
    {
        private static BtTreeRuntime Build(BtTreeDefinition definition, BtTreeRunOptions? options = null)
        {
            var runtime = BtTreeRuntime.Create(definition, CreateRegistry(), null, options);
            runtime.Enable(0, Fixed64.Zero);
            return runtime;
        }

        private static void SetResult(BtTreeRuntime runtime, string key, long value)
        {
            runtime.Blackboard.SetInt64(key, value);
        }

        [Fact]
        public void Sequence_CompletesOnlyWhenAllChildrenSucceed()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.result", BtValueType.Int64)
                .Node("root", BtBuiltInNodeTypes.Sequence, "a", "b")
                .Node("a", ScriptedAction)
                .Node("b", ScriptedAction)
                .Root("root");

            var runtime = Build(definition);

            SetResult(runtime, "test.result", 2);
            runtime.Update(1, Fixed64.Zero);
            Assert.Equal(BtNodeState.Running, runtime.RootNodeState);

            SetResult(runtime, "test.result", 1);
            runtime.Update(2, Fixed64.Zero);
            Assert.Equal(BtNodeState.Success, runtime.RootNodeState);
            Assert.Equal(BtNodeState.Success, runtime.TreeState);
        }

        [Fact]
        public void Sequence_FailsFastOnChildFailure()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.result", BtValueType.Int64)
                .Blackboard("test.startCount", BtValueType.Int64)
                .Blackboard("test.stopCount", BtValueType.Int64)
                .Node("root", BtBuiltInNodeTypes.Sequence, "a", "b")
                .Node("a", CountingAction)
                .Node("b", CountingAction)
                .Root("root");

            var runtime = Build(definition);
            SetResult(runtime, "test.result", 0);   // a 失败

            runtime.Update(1, Fixed64.Zero);

            Assert.Equal(BtNodeState.Failure, runtime.RootNodeState);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.startCount"));   // b 未启动
        }

        [Fact]
        public void Selector_SucceedsOnFirstSuccessAndFallsThroughOnFailure()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.result", BtValueType.Int64)
                .Blackboard("test.aStart", BtValueType.Int64)
                .Blackboard("test.bStart", BtValueType.Int64)
                .Node("root", BtBuiltInNodeTypes.Selector, "a", "b")
                .Node("a", CountingAction)
                .Node("b", CountingAction)
                .Root("root");
            definition.Nodes[1].Properties.Set("startCounterKey", BtPropertyValue.Of("test.aStart"));
            definition.Nodes[2].Properties.Set("startCounterKey", BtPropertyValue.Of("test.bStart"));

            var runtime = Build(definition);
            SetResult(runtime, "test.result", 0);

            runtime.Update(1, Fixed64.Zero);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.aStart"));
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.bStart"));
            Assert.Equal(BtNodeState.Failure, runtime.RootNodeState);

            runtime.Restart();
            SetResult(runtime, "test.result", 1);
            runtime.Update(2, Fixed64.Zero);
            Assert.Equal(2, runtime.Blackboard.GetInt64("test.aStart"));
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.bStart"));   // b 未再次启动
            Assert.Equal(BtNodeState.Success, runtime.RootNodeState);
        }

        [Fact]
        public void Parallel_AllBranchesRunPerTick_AndFailFast()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.result", BtValueType.Int64)
                .Blackboard("test.aStart", BtValueType.Int64)
                .Blackboard("test.bStart", BtValueType.Int64)
                .Blackboard("test.stopCount", BtValueType.Int64)
                .Node("root", BtBuiltInNodeTypes.Parallel, "a", "b")
                .Node("a", CountingAction)
                .Node("b", CountingAction)
                .Root("root");
            definition.Nodes[1].Properties.Set("startCounterKey", BtPropertyValue.Of("test.aStart"));
            definition.Nodes[2].Properties.Set("startCounterKey", BtPropertyValue.Of("test.bStart"));

            var runtime = Build(definition);
            runtime.Blackboard.SetInt64("test.result", 2);

            runtime.Update(1, Fixed64.Zero);
            runtime.Update(2, Fixed64.Zero);
            // 两个分支每 tick 各自保持运行（首次启动后不再重复 Start）
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.aStart"));
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.bStart"));
            Assert.Equal(BtNodeState.Running, runtime.RootNodeState);

            // 任一分支失败 → 并行整体 Failure
            runtime.Blackboard.SetInt64("test.result", 0);
            runtime.Update(3, Fixed64.Zero);
            Assert.Equal(BtNodeState.Failure, runtime.RootNodeState);
            Assert.Equal(2, runtime.Blackboard.GetInt64("test.stopCount"));   // 两分支均被弹出
        }

        [Fact]
        public void Inverter_FlipsChildResult()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.result", BtValueType.Int64)
                .Node("root", BtBuiltInNodeTypes.Inverter, "a")
                .Node("a", ScriptedAction)
                .Root("root");

            var runtime = Build(definition);
            SetResult(runtime, "test.result", 1);
            runtime.Update(1, Fixed64.Zero);
            Assert.Equal(BtNodeState.Failure, runtime.RootNodeState);

            runtime.Restart();
            SetResult(runtime, "test.result", 0);
            runtime.Update(2, Fixed64.Zero);
            Assert.Equal(BtNodeState.Success, runtime.RootNodeState);
        }

        [Fact]
        public void Repeater_RepeatsChildAcrossTicks_ThenSucceeds()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.result", BtValueType.Int64)
                .Blackboard("test.startCount", BtValueType.Int64)
                .Node("root", BtBuiltInNodeTypes.Repeater, "a")
                .Node("a", CountingAction)
                .Root("root");
            definition.Nodes[0].Properties.Set("count", BtPropertyValue.Of(3L));

            var runtime = Build(definition);
            SetResult(runtime, "test.result", 1);

            runtime.Update(1, Fixed64.Zero);
            Assert.Equal(BtNodeState.Running, runtime.RootNodeState);
            runtime.Update(2, Fixed64.Zero);
            runtime.Update(3, Fixed64.Zero);
            runtime.Update(4, Fixed64.Zero);
            Assert.Equal(3, runtime.Blackboard.GetInt64("test.startCount"));   // 3 次完整执行
            Assert.Equal(BtNodeState.Success, runtime.RootNodeState);
        }

        [Fact]
        public void Retry_RetriesOnFailureUpToCount()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.result", BtValueType.Int64)
                .Blackboard("test.startCount", BtValueType.Int64)
                .Node("root", BtBuiltInNodeTypes.Retry, "a")
                .Node("a", CountingAction)
                .Root("root");
            definition.Nodes[0].Properties.Set("count", BtPropertyValue.Of(2L));

            var runtime = Build(definition);
            SetResult(runtime, "test.result", 0);   // 持续失败

            runtime.Update(1, Fixed64.Zero);
            runtime.Update(2, Fixed64.Zero);
            runtime.Update(3, Fixed64.Zero);
            runtime.Update(4, Fixed64.Zero);
            // 初次 + 2 次重试 = 3 次启动，之后 Failure
            Assert.Equal(3, runtime.Blackboard.GetInt64("test.startCount"));
            Assert.Equal(BtNodeState.Failure, runtime.RootNodeState);
        }

        [Fact]
        public void Timeout_PreemptsRunningChildAfterDuration()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.result", BtValueType.Int64)
                .Blackboard("test.startCount", BtValueType.Int64)
                .Blackboard("test.stopCount", BtValueType.Int64)
                .Node("root", BtBuiltInNodeTypes.Timeout, "a")
                .Node("a", CountingAction)
                .Root("root");
            definition.Nodes[0].Properties.Set("durationSeconds", BtPropertyValue.Of(Fixed64.One));

            var runtime = Build(definition);
            SetResult(runtime, "test.result", 2);   // 子节点持续 Running

            runtime.Update(1, Fixed64.FromInt32(0));   // 0s：进入运行
            Assert.Equal(BtNodeState.Running, runtime.RootNodeState);
            runtime.Update(2, Fixed64.FromInt32(1));   // 1s：到达超时
            Assert.Equal(BtNodeState.Failure, runtime.RootNodeState);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.stopCount"));   // 子节点被中止弹出
        }

        [Fact]
        public void Cooldown_BlocksReentryWithinWindow()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.result", BtValueType.Int64)
                .Blackboard("test.startCount", BtValueType.Int64)
                .Node("root", BtBuiltInNodeTypes.Cooldown, "a")
                .Node("a", CountingAction)
                .Root("root");
            definition.Nodes[0].Properties.Set("cooldownSeconds", BtPropertyValue.Of(Fixed64.FromInt32(10)));

            var runtime = Build(definition);
            SetResult(runtime, "test.result", 1);

            runtime.Update(1, Fixed64.Zero);
            Assert.Equal(BtNodeState.Success, runtime.RootNodeState);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.startCount"));

            runtime.Restart();
            runtime.Update(2, Fixed64.FromInt32(5));   // 冷却期内
            Assert.Equal(BtNodeState.Failure, runtime.RootNodeState);   // resultOnCooldown 默认 Failure
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.startCount"));   // 子树未运行

            runtime.Restart();
            runtime.Update(3, Fixed64.FromInt32(11));  // 冷却结束
            Assert.Equal(BtNodeState.Success, runtime.RootNodeState);
            Assert.Equal(2, runtime.Blackboard.GetInt64("test.startCount"));
        }

        [Fact]
        public void Once_RunsChildOnce_ThenGatesSubsequentEntries()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.result", BtValueType.Int64)
                .Blackboard("test.startCount", BtValueType.Int64)
                .Node("root", BtBuiltInNodeTypes.Once, "a")
                .Node("a", CountingAction)
                .Root("root");

            var runtime = Build(definition);
            SetResult(runtime, "test.result", 1);

            runtime.Update(1, Fixed64.Zero);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.startCount"));

            runtime.Restart();
            runtime.Update(2, Fixed64.Zero);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.startCount"));   // 未重复执行
            Assert.Equal(BtNodeState.Failure, runtime.RootNodeState);   // resultAfterFirst 默认 Failure
        }

        [Fact]
        public void ConditionalAbort_Self_InterruptsRunningSiblingWhenConditionFlips()
        {
            // Selector(Self)[cond, action]：cond 失败让选择器推进到 action（Running），
            // cond 翻真后 Self 中断中止 action，选择器重新评估并因 cond 成功而完成。
            var definition = new TreeBuilder()
                .Blackboard("test.cond", BtValueType.Bool)
                .Blackboard("test.result", BtValueType.Int64)
                .Blackboard("test.aStart", BtValueType.Int64)
                .Blackboard("test.aStop", BtValueType.Int64)
                .Node("root", BtBuiltInNodeTypes.Selector, (long)BtAbortType.Self, "cond", "a")
                .Node("cond", ScriptedCondition)
                .Node("a", CountingAction)
                .Root("root");
            definition.Nodes[1].Properties.Set("condKey", BtPropertyValue.Of("test.cond"));
            definition.Nodes[2].Properties.Set("startCounterKey", BtPropertyValue.Of("test.aStart"));
            definition.Nodes[2].Properties.Set("stopCounterKey", BtPropertyValue.Of("test.aStop"));
            definition.Nodes[2].Properties.Set("resultKey", BtPropertyValue.Of("test.result"));

            var runtime = Build(definition);
            runtime.Blackboard.SetBool("test.cond", false);   // 条件失败 → 推进到 action
            runtime.Blackboard.SetInt64("test.result", 2);    // action 持续 Running

            runtime.Update(1, Fixed64.Zero);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.aStart"));
            Assert.Equal(BtNodeState.Running, runtime.RootNodeState);

            // 条件翻真：Self 中断 → action 被 Stop，选择器重新评估后以 Success 完成
            runtime.Blackboard.SetBool("test.cond", true);
            runtime.Update(2, Fixed64.Zero);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.aStop"));
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.aStart"));   // 未重新启动（选择器直接完成）
            Assert.Equal(BtNodeState.Success, runtime.RootNodeState);
        }

        [Fact]
        public void Wait_CompletesByInjectedTime()
        {
            var definition = new TreeBuilder()
                .Node("root", BtBuiltInNodeTypes.Wait)
                .Root("root");
            definition.Nodes[0].Properties.Set("durationSeconds", BtPropertyValue.Of(Fixed64.FromRatio(1, 2)));

            var runtime = Build(definition);
            runtime.Update(1, Fixed64.Zero);
            Assert.Equal(BtNodeState.Running, runtime.RootNodeState);
            runtime.Update(2, Fixed64.FromRatio(1, 4));
            Assert.Equal(BtNodeState.Running, runtime.RootNodeState);
            runtime.Update(3, Fixed64.FromRatio(1, 2));
            Assert.Equal(BtNodeState.Success, runtime.RootNodeState);
        }

        [Fact]
        public void Wait_CompletesByFrameCount()
        {
            var definition = new TreeBuilder()
                .Node("root", BtBuiltInNodeTypes.Wait)
                .Root("root");
            definition.Nodes[0].Properties.Set("mode", BtPropertyValue.Of(1L));
            definition.Nodes[0].Properties.Set("durationFrames", BtPropertyValue.Of(3L));

            var runtime = Build(definition);
            runtime.Update(1, Fixed64.Zero);
            runtime.Update(2, Fixed64.Zero);
            Assert.Equal(BtNodeState.Running, runtime.RootNodeState);
            runtime.Update(3, Fixed64.Zero);
            Assert.Equal(BtNodeState.Success, runtime.RootNodeState);
        }

        [Fact]
        public void ConditionalAbort_LowerPriority_InterruptsRunningLowerBranch()
        {
            // Selector[LowerPriority]( Sequence(cond, high), low )：高优先级条件翻真 → 中断运行中的低分支
            var definition = new TreeBuilder()
                .Blackboard("test.cond", BtValueType.Bool)
                .Blackboard("test.result", BtValueType.Int64)
                .Blackboard("test.highStart", BtValueType.Int64)
                .Blackboard("test.lowStart", BtValueType.Int64)
                .Blackboard("test.lowStop", BtValueType.Int64)
                .Node("root", BtBuiltInNodeTypes.Selector, (long)BtAbortType.LowerPriority, "seq", "low")
                .Node("seq", BtBuiltInNodeTypes.Sequence, "cond", "high")
                .Node("cond", ScriptedCondition)
                .Node("high", CountingAction)
                .Node("low", CountingAction)
                .Root("root");
            definition.Nodes[2].Properties.Set("condKey", BtPropertyValue.Of("test.cond"));
            definition.Nodes[3].Properties.Set("startCounterKey", BtPropertyValue.Of("test.highStart"));
            definition.Nodes[3].Properties.Set("stopCounterKey", BtPropertyValue.Of("test.highStop"));
            definition.Nodes[3].Properties.Set("resultKey", BtPropertyValue.Of("test.result"));
            definition.Nodes[4].Properties.Set("startCounterKey", BtPropertyValue.Of("test.lowStart"));
            definition.Nodes[4].Properties.Set("stopCounterKey", BtPropertyValue.Of("test.lowStop"));
            definition.Nodes[4].Properties.Set("resultKey", BtPropertyValue.Of("test.result"));

            var runtime = Build(definition);
            runtime.Blackboard.SetBool("test.cond", false);
            runtime.Blackboard.SetInt64("test.result", 2);   // Running

            runtime.Update(1, Fixed64.Zero);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.lowStart"));
            Assert.Equal(0, runtime.Blackboard.GetInt64("test.highStart"));

            // 高优先级条件翻真 → 低分支被中止，高分支接手
            runtime.Blackboard.SetBool("test.cond", true);
            runtime.Update(2, Fixed64.Zero);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.lowStop"));
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.highStart"));
        }

        [Fact]
        public void ConditionalAbort_LowerPriority_FlipFalse_DoesNotInterruptRunningBranch()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.cond", BtValueType.Bool)
                .Blackboard("test.result", BtValueType.Int64)
                .Blackboard("test.highStart", BtValueType.Int64)
                .Blackboard("test.highStop", BtValueType.Int64)
                .Node("root", BtBuiltInNodeTypes.Selector, (long)BtAbortType.LowerPriority, "seq", "low")
                .Node("seq", BtBuiltInNodeTypes.Sequence, "cond", "high")
                .Node("cond", ScriptedCondition)
                .Node("high", CountingAction)
                .Node("low", CountingAction)
                .Root("root");
            definition.Nodes[2].Properties.Set("condKey", BtPropertyValue.Of("test.cond"));
            definition.Nodes[3].Properties.Set("startCounterKey", BtPropertyValue.Of("test.highStart"));
            definition.Nodes[3].Properties.Set("stopCounterKey", BtPropertyValue.Of("test.highStop"));
            definition.Nodes[3].Properties.Set("resultKey", BtPropertyValue.Of("test.result"));
            definition.Nodes[4].Properties.Set("resultKey", BtPropertyValue.Of("test.result"));

            var runtime = Build(definition);
            runtime.Blackboard.SetBool("test.cond", true);
            runtime.Blackboard.SetInt64("test.result", 2);

            runtime.Update(1, Fixed64.Zero);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.highStart"));

            // 条件翻假：LowerPriority 不中断已运行分支（Self 才会）
            runtime.Blackboard.SetBool("test.cond", false);
            runtime.Update(2, Fixed64.Zero);
            Assert.Equal(0, runtime.Blackboard.GetInt64("test.highStop"));
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.highStart"));
        }

        [Fact]
        public void ConditionalAbort_Both_InterruptsInBothDirections()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.cond", BtValueType.Bool)
                .Blackboard("test.result", BtValueType.Int64)
                .Blackboard("test.highStart", BtValueType.Int64)
                .Blackboard("test.highStop", BtValueType.Int64)
                .Blackboard("test.lowStart", BtValueType.Int64)
                .Blackboard("test.lowStop", BtValueType.Int64)
                .Node("root", BtBuiltInNodeTypes.Selector, (long)BtAbortType.Both, "seq", "low")
                .Node("seq", BtBuiltInNodeTypes.Sequence, "cond", "high")
                .Node("cond", ScriptedCondition)
                .Node("high", CountingAction)
                .Node("low", CountingAction)
                .Root("root");
            definition.Nodes[2].Properties.Set("condKey", BtPropertyValue.Of("test.cond"));
            definition.Nodes[3].Properties.Set("startCounterKey", BtPropertyValue.Of("test.highStart"));
            definition.Nodes[3].Properties.Set("stopCounterKey", BtPropertyValue.Of("test.highStop"));
            definition.Nodes[3].Properties.Set("resultKey", BtPropertyValue.Of("test.result"));
            definition.Nodes[4].Properties.Set("startCounterKey", BtPropertyValue.Of("test.lowStart"));
            definition.Nodes[4].Properties.Set("stopCounterKey", BtPropertyValue.Of("test.lowStop"));
            definition.Nodes[4].Properties.Set("resultKey", BtPropertyValue.Of("test.result"));

            var runtime = Build(definition);
            runtime.Blackboard.SetBool("test.cond", false);
            runtime.Blackboard.SetInt64("test.result", 2);

            runtime.Update(1, Fixed64.Zero);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.lowStart"));

            // 翻真 → 低分支被中止
            runtime.Blackboard.SetBool("test.cond", true);
            runtime.Update(2, Fixed64.Zero);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.lowStop"));
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.highStart"));

            // 翻假 → 高分支被中止，低分支再次接手（Both 双向）
            runtime.Blackboard.SetBool("test.cond", false);
            runtime.Update(3, Fixed64.Zero);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.highStop"));
            Assert.Equal(2, runtime.Blackboard.GetInt64("test.lowStart"));
        }

        [Fact]
        public void NestedComposites_ExecuteChildrenInPreorder()
        {
            // root = Sequence[ inner, c ]; inner = Sequence[ a, b ] —— 嵌套结构下子节点索引必须正确
            var definition = new TreeBuilder()
                .Blackboard("test.result", BtValueType.Int64)
                .Blackboard("test.aStart", BtValueType.Int64)
                .Blackboard("test.bStart", BtValueType.Int64)
                .Blackboard("test.cStart", BtValueType.Int64)
                .Node("root", BtBuiltInNodeTypes.Sequence, "inner", "c")
                .Node("inner", BtBuiltInNodeTypes.Sequence, "a", "b")
                .Node("a", CountingAction)
                .Node("b", CountingAction)
                .Node("c", CountingAction)
                .Root("root");
            definition.Nodes[2].Properties.Set("startCounterKey", BtPropertyValue.Of("test.aStart"));
            definition.Nodes[3].Properties.Set("startCounterKey", BtPropertyValue.Of("test.bStart"));
            definition.Nodes[4].Properties.Set("startCounterKey", BtPropertyValue.Of("test.cStart"));

            var runtime = Build(definition);
            SetResult(runtime, "test.result", 1);

            runtime.Update(1, Fixed64.Zero);

            Assert.Equal(BtNodeState.Success, runtime.RootNodeState);
            // 前序依次 a → b → c，三者各执行一次
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.aStart"));
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.bStart"));
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.cStart"));
        }

        [Fact]
        public void RestartWhenComplete_OptionRestartsTreeAutomatically()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.result", BtValueType.Int64)
                .Blackboard("test.startCount", BtValueType.Int64)
                .Node("root", BtBuiltInNodeTypes.Sequence, "a")
                .Node("a", CountingAction)
                .Root("root");

            var runtime = Build(definition, new BtTreeRunOptions { RestartWhenComplete = true });
            SetResult(runtime, "test.result", 1);

            runtime.Update(1, Fixed64.Zero);
            runtime.Update(2, Fixed64.Zero);
            Assert.Equal(2, runtime.Blackboard.GetInt64("test.startCount"));
        }
    }
}
