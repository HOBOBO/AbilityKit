using AbilityKit.Deterministic;
using Xunit;
using static AbilityKit.BehaviorTree.Tests.TestNodeTypes;

namespace AbilityKit.BehaviorTree.Tests
{
    /// <summary>鎵ц璇箟锛氱粍鍚?瑁呴グ鎺ㄨ繘銆佸苟琛屽垎鏀€佹潯浠朵腑鏂紙Self/LowerPriority/Both锛夈€佽楗板櫒鎶㈠崰銆?/summary>
    public sealed class BtExecutionSemanticsTests
    {
        private static TreeRuntime Build(TreeDefinition definition, TreeRunOptions? options = null)
        {
            var runtime = TreeRuntime.Create(definition, CreateRegistry(), null, options);
            runtime.Enable(0, Fixed64.Zero);
            return runtime;
        }

        private static void SetResult(TreeRuntime runtime, string key, long value)
        {
            runtime.Blackboard.SetInt64(key, value);
        }

        [Fact]
        public void Sequence_CompletesOnlyWhenAllChildrenSucceed()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.result", TreeValueType.Int64)
                .Node("root", BuiltInNodeTypes.Sequence, "a", "b")
                .Node("a", ScriptedAction)
                .Node("b", ScriptedAction)
                .Root("root");

            var runtime = Build(definition);

            SetResult(runtime, "test.result", 2);
            runtime.Update(1, Fixed64.Zero);
            Assert.Equal(NodeState.Running, runtime.RootNodeState);

            SetResult(runtime, "test.result", 1);
            runtime.Update(2, Fixed64.Zero);
            Assert.Equal(NodeState.Success, runtime.RootNodeState);
            Assert.Equal(NodeState.Success, runtime.TreeState);
        }

        [Fact]
        public void Sequence_FailsFastOnChildFailure()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.result", TreeValueType.Int64)
                .Blackboard("test.startCount", TreeValueType.Int64)
                .Blackboard("test.stopCount", TreeValueType.Int64)
                .Node("root", BuiltInNodeTypes.Sequence, "a", "b")
                .Node("a", CountingAction)
                .Node("b", CountingAction)
                .Root("root");

            var runtime = Build(definition);
            SetResult(runtime, "test.result", 0);   // a 澶辫触

            runtime.Update(1, Fixed64.Zero);

            Assert.Equal(NodeState.Failure, runtime.RootNodeState);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.startCount"));
            }

        [Fact]
        public void Selector_SucceedsOnFirstSuccessAndFallsThroughOnFailure()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.result", TreeValueType.Int64)
                .Blackboard("test.aStart", TreeValueType.Int64)
                .Blackboard("test.bStart", TreeValueType.Int64)
                .Node("root", BuiltInNodeTypes.Selector, "a", "b")
                .Node("a", CountingAction)
                .Node("b", CountingAction)
                .Root("root");
            definition.Nodes[1].Properties.Set("startCounterKey", PropertyValue.Of("test.aStart"));
            definition.Nodes[2].Properties.Set("startCounterKey", PropertyValue.Of("test.bStart"));

            var runtime = Build(definition);
            SetResult(runtime, "test.result", 0);

            runtime.Update(1, Fixed64.Zero);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.aStart"));
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.bStart"));
            Assert.Equal(NodeState.Failure, runtime.RootNodeState);

            runtime.Restart();
            SetResult(runtime, "test.result", 1);
            runtime.Update(2, Fixed64.Zero);
            Assert.Equal(2, runtime.Blackboard.GetInt64("test.aStart"));
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.bStart"));
            Assert.Equal(NodeState.Success, runtime.RootNodeState);
        }

        [Fact]
        public void Parallel_AllBranchesRunPerTick_AndFailFast()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.result", TreeValueType.Int64)
                .Blackboard("test.aStart", TreeValueType.Int64)
                .Blackboard("test.bStart", TreeValueType.Int64)
                .Blackboard("test.stopCount", TreeValueType.Int64)
                .Node("root", BuiltInNodeTypes.Parallel, "a", "b")
                .Node("a", CountingAction)
                .Node("b", CountingAction)
                .Root("root");
            definition.Nodes[1].Properties.Set("startCounterKey", PropertyValue.Of("test.aStart"));
            definition.Nodes[2].Properties.Set("startCounterKey", PropertyValue.Of("test.bStart"));

            var runtime = Build(definition);
            runtime.Blackboard.SetInt64("test.result", 2);

            runtime.Update(1, Fixed64.Zero);
            runtime.Update(2, Fixed64.Zero);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.aStart"));
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.bStart"));
            Assert.Equal(NodeState.Running, runtime.RootNodeState);

            // 浠讳竴鍒嗘敮澶辫触 鈫?骞惰鏁翠綋 Failure
            runtime.Blackboard.SetInt64("test.result", 0);
            runtime.Update(3, Fixed64.Zero);
            Assert.Equal(NodeState.Failure, runtime.RootNodeState);
            Assert.Equal(2, runtime.Blackboard.GetInt64("test.stopCount"));
            }

        [Fact]
        public void Inverter_FlipsChildResult()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.result", TreeValueType.Int64)
                .Node("root", BuiltInNodeTypes.Inverter, "a")
                .Node("a", ScriptedAction)
                .Root("root");

            var runtime = Build(definition);
            SetResult(runtime, "test.result", 1);
            runtime.Update(1, Fixed64.Zero);
            Assert.Equal(NodeState.Failure, runtime.RootNodeState);

            runtime.Restart();
            SetResult(runtime, "test.result", 0);
            runtime.Update(2, Fixed64.Zero);
            Assert.Equal(NodeState.Success, runtime.RootNodeState);
        }

        [Fact]
        public void Repeater_RepeatsChildAcrossTicks_ThenSucceeds()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.result", TreeValueType.Int64)
                .Blackboard("test.startCount", TreeValueType.Int64)
                .Node("root", BuiltInNodeTypes.Repeater, "a")
                .Node("a", CountingAction)
                .Root("root");
            definition.Nodes[0].Properties.Set("count", PropertyValue.Of(3L));

            var runtime = Build(definition);
            SetResult(runtime, "test.result", 1);

            runtime.Update(1, Fixed64.Zero);
            Assert.Equal(NodeState.Running, runtime.RootNodeState);
            runtime.Update(2, Fixed64.Zero);
            runtime.Update(3, Fixed64.Zero);
            runtime.Update(4, Fixed64.Zero);
            Assert.Equal(3, runtime.Blackboard.GetInt64("test.startCount"));
            Assert.Equal(NodeState.Success, runtime.RootNodeState);
        }

        [Fact]
        public void Retry_RetriesOnFailureUpToCount()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.result", TreeValueType.Int64)
                .Blackboard("test.startCount", TreeValueType.Int64)
                .Node("root", BuiltInNodeTypes.Retry, "a")
                .Node("a", CountingAction)
                .Root("root");
            definition.Nodes[0].Properties.Set("count", PropertyValue.Of(2L));

            var runtime = Build(definition);
            SetResult(runtime, "test.result", 0);   // 鎸佺画澶辫触

            runtime.Update(1, Fixed64.Zero);
            runtime.Update(2, Fixed64.Zero);
            runtime.Update(3, Fixed64.Zero);
            runtime.Update(4, Fixed64.Zero);
            // 鍒濇 + 2 娆￠噸璇?= 3 娆″惎鍔紝涔嬪悗 Failure
            Assert.Equal(3, runtime.Blackboard.GetInt64("test.startCount"));
            Assert.Equal(NodeState.Failure, runtime.RootNodeState);
        }

        [Fact]
        public void Timeout_PreemptsRunningChildAfterDuration()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.result", TreeValueType.Int64)
                .Blackboard("test.startCount", TreeValueType.Int64)
                .Blackboard("test.stopCount", TreeValueType.Int64)
                .Node("root", BuiltInNodeTypes.Timeout, "a")
                .Node("a", CountingAction)
                .Root("root");
            definition.Nodes[0].Properties.Set("durationSeconds", PropertyValue.Of(Fixed64.One));

            var runtime = Build(definition);
            SetResult(runtime, "test.result", 2);   // 瀛愯妭鐐规寔缁?Running

            runtime.Update(1, Fixed64.FromInt32(0));
            Assert.Equal(NodeState.Running, runtime.RootNodeState);
            runtime.Update(2, Fixed64.FromInt32(1));
            Assert.Equal(NodeState.Failure, runtime.RootNodeState);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.stopCount"));   // 瀛愯妭鐐硅涓寮瑰嚭
        }

        [Fact]
        public void Cooldown_BlocksReentryWithinWindow()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.result", TreeValueType.Int64)
                .Blackboard("test.startCount", TreeValueType.Int64)
                .Node("root", BuiltInNodeTypes.Cooldown, "a")
                .Node("a", CountingAction)
                .Root("root");
            definition.Nodes[0].Properties.Set("cooldownSeconds", PropertyValue.Of(Fixed64.FromInt32(10)));

            var runtime = Build(definition);
            SetResult(runtime, "test.result", 1);

            runtime.Update(1, Fixed64.Zero);
            Assert.Equal(NodeState.Success, runtime.RootNodeState);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.startCount"));

            runtime.Restart();
            runtime.Update(2, Fixed64.FromInt32(5));   // 鍐峰嵈鏈熷唴
            Assert.Equal(NodeState.Failure, runtime.RootNodeState);   // resultOnCooldown 榛樿 Failure
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.startCount"));
            runtime.Restart();
            runtime.Update(3, Fixed64.FromInt32(11));  // 鍐峰嵈缁撴潫
            Assert.Equal(NodeState.Success, runtime.RootNodeState);
            Assert.Equal(2, runtime.Blackboard.GetInt64("test.startCount"));
        }

        [Fact]
        public void Once_RunsChildOnce_ThenGatesSubsequentEntries()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.result", TreeValueType.Int64)
                .Blackboard("test.startCount", TreeValueType.Int64)
                .Node("root", BuiltInNodeTypes.Once, "a")
                .Node("a", CountingAction)
                .Root("root");

            var runtime = Build(definition);
            SetResult(runtime, "test.result", 1);

            runtime.Update(1, Fixed64.Zero);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.startCount"));

            runtime.Restart();
            runtime.Update(2, Fixed64.Zero);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.startCount"));
            Assert.Equal(NodeState.Failure, runtime.RootNodeState);   // resultAfterFirst 榛樿 Failure
        }

        [Fact]
        public void ConditionalAbort_Self_InterruptsRunningSiblingWhenConditionFlips()
        {
            // Selector(Self)[cond, action]锛歝ond 澶辫触璁╅€夋嫨鍣ㄦ帹杩涘埌 action锛圧unning锛夛紝
            var definition = new TreeBuilder()
                .Blackboard("test.cond", TreeValueType.Bool)
                .Blackboard("test.result", TreeValueType.Int64)
                .Blackboard("test.aStart", TreeValueType.Int64)
                .Blackboard("test.aStop", TreeValueType.Int64)
                .Node("root", BuiltInNodeTypes.Selector, (long)AbortType.Self, "cond", "a")
                .Node("cond", ScriptedCondition)
                .Node("a", CountingAction)
                .Root("root");
            definition.Nodes[1].Properties.Set("condKey", PropertyValue.Of("test.cond"));
            definition.Nodes[2].Properties.Set("startCounterKey", PropertyValue.Of("test.aStart"));
            definition.Nodes[2].Properties.Set("stopCounterKey", PropertyValue.Of("test.aStop"));
            definition.Nodes[2].Properties.Set("resultKey", PropertyValue.Of("test.result"));

            var runtime = Build(definition);
            runtime.Blackboard.SetBool("test.cond", false);   // 鏉′欢澶辫触 鈫?鎺ㄨ繘鍒?action
            runtime.Blackboard.SetInt64("test.result", 2);    // action 鎸佺画 Running

            runtime.Update(1, Fixed64.Zero);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.aStart"));
            Assert.Equal(NodeState.Running, runtime.RootNodeState);

            // 鏉′欢缈荤湡锛歋elf 涓柇 鈫?action 琚?Stop锛岄€夋嫨鍣ㄩ噸鏂拌瘎浼板悗浠?Success 瀹屾垚
            runtime.Blackboard.SetBool("test.cond", true);
            runtime.Update(2, Fixed64.Zero);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.aStop"));
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.aStart"));   // 鏈噸鏂板惎鍔紙閫夋嫨鍣ㄧ洿鎺ュ畬鎴愶級
            Assert.Equal(NodeState.Success, runtime.RootNodeState);
        }

        [Fact]
        public void Wait_CompletesByInjectedTime()
        {
            var definition = new TreeBuilder()
                .Node("root", BuiltInNodeTypes.Wait)
                .Root("root");
            definition.Nodes[0].Properties.Set("durationSeconds", PropertyValue.Of(Fixed64.FromRatio(1, 2)));

            var runtime = Build(definition);
            runtime.Update(1, Fixed64.Zero);
            Assert.Equal(NodeState.Running, runtime.RootNodeState);
            runtime.Update(2, Fixed64.FromRatio(1, 4));
            Assert.Equal(NodeState.Running, runtime.RootNodeState);
            runtime.Update(3, Fixed64.FromRatio(1, 2));
            Assert.Equal(NodeState.Success, runtime.RootNodeState);
        }

        [Fact]
        public void Wait_CompletesByFrameCount()
        {
            var definition = new TreeBuilder()
                .Node("root", BuiltInNodeTypes.Wait)
                .Root("root");
            definition.Nodes[0].Properties.Set("mode", PropertyValue.Of(1L));
            definition.Nodes[0].Properties.Set("durationFrames", PropertyValue.Of(3L));

            var runtime = Build(definition);
            runtime.Update(1, Fixed64.Zero);
            runtime.Update(2, Fixed64.Zero);
            Assert.Equal(NodeState.Running, runtime.RootNodeState);
            runtime.Update(3, Fixed64.Zero);
            Assert.Equal(NodeState.Success, runtime.RootNodeState);
        }

        [Fact]
        public void ConditionalAbort_LowerPriority_InterruptsRunningLowerBranch()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.cond", TreeValueType.Bool)
                .Blackboard("test.result", TreeValueType.Int64)
                .Blackboard("test.highStart", TreeValueType.Int64)
                .Blackboard("test.lowStart", TreeValueType.Int64)
                .Blackboard("test.lowStop", TreeValueType.Int64)
                .Node("root", BuiltInNodeTypes.Selector, (long)AbortType.LowerPriority, "seq", "low")
                .Node("seq", BuiltInNodeTypes.Sequence, "cond", "high")
                .Node("cond", ScriptedCondition)
                .Node("high", CountingAction)
                .Node("low", CountingAction)
                .Root("root");
            definition.Nodes[2].Properties.Set("condKey", PropertyValue.Of("test.cond"));
            definition.Nodes[3].Properties.Set("startCounterKey", PropertyValue.Of("test.highStart"));
            definition.Nodes[3].Properties.Set("stopCounterKey", PropertyValue.Of("test.highStop"));
            definition.Nodes[3].Properties.Set("resultKey", PropertyValue.Of("test.result"));
            definition.Nodes[4].Properties.Set("startCounterKey", PropertyValue.Of("test.lowStart"));
            definition.Nodes[4].Properties.Set("stopCounterKey", PropertyValue.Of("test.lowStop"));
            definition.Nodes[4].Properties.Set("resultKey", PropertyValue.Of("test.result"));

            var runtime = Build(definition);
            runtime.Blackboard.SetBool("test.cond", false);
            runtime.Blackboard.SetInt64("test.result", 2);   // Running

            runtime.Update(1, Fixed64.Zero);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.lowStart"));
            Assert.Equal(0, runtime.Blackboard.GetInt64("test.highStart"));

            // 楂樹紭鍏堢骇鏉′欢缈荤湡 鈫?浣庡垎鏀涓锛岄珮鍒嗘敮鎺ユ墜
            runtime.Blackboard.SetBool("test.cond", true);
            runtime.Update(2, Fixed64.Zero);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.lowStop"));
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.highStart"));
        }

        [Fact]
        public void ConditionalAbort_LowerPriority_FlipFalse_DoesNotInterruptRunningBranch()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.cond", TreeValueType.Bool)
                .Blackboard("test.result", TreeValueType.Int64)
                .Blackboard("test.highStart", TreeValueType.Int64)
                .Blackboard("test.highStop", TreeValueType.Int64)
                .Node("root", BuiltInNodeTypes.Selector, (long)AbortType.LowerPriority, "seq", "low")
                .Node("seq", BuiltInNodeTypes.Sequence, "cond", "high")
                .Node("cond", ScriptedCondition)
                .Node("high", CountingAction)
                .Node("low", CountingAction)
                .Root("root");
            definition.Nodes[2].Properties.Set("condKey", PropertyValue.Of("test.cond"));
            definition.Nodes[3].Properties.Set("startCounterKey", PropertyValue.Of("test.highStart"));
            definition.Nodes[3].Properties.Set("stopCounterKey", PropertyValue.Of("test.highStop"));
            definition.Nodes[3].Properties.Set("resultKey", PropertyValue.Of("test.result"));
            definition.Nodes[4].Properties.Set("resultKey", PropertyValue.Of("test.result"));

            var runtime = Build(definition);
            runtime.Blackboard.SetBool("test.cond", true);
            runtime.Blackboard.SetInt64("test.result", 2);

            runtime.Update(1, Fixed64.Zero);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.highStart"));
            runtime.Blackboard.SetBool("test.cond", false);
            runtime.Update(2, Fixed64.Zero);
            Assert.Equal(0, runtime.Blackboard.GetInt64("test.highStop"));
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.highStart"));
        }

        [Fact]
        public void ConditionalAbort_Both_InterruptsInBothDirections()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.cond", TreeValueType.Bool)
                .Blackboard("test.result", TreeValueType.Int64)
                .Blackboard("test.highStart", TreeValueType.Int64)
                .Blackboard("test.highStop", TreeValueType.Int64)
                .Blackboard("test.lowStart", TreeValueType.Int64)
                .Blackboard("test.lowStop", TreeValueType.Int64)
                .Node("root", BuiltInNodeTypes.Selector, (long)AbortType.Both, "seq", "low")
                .Node("seq", BuiltInNodeTypes.Sequence, "cond", "high")
                .Node("cond", ScriptedCondition)
                .Node("high", CountingAction)
                .Node("low", CountingAction)
                .Root("root");
            definition.Nodes[2].Properties.Set("condKey", PropertyValue.Of("test.cond"));
            definition.Nodes[3].Properties.Set("startCounterKey", PropertyValue.Of("test.highStart"));
            definition.Nodes[3].Properties.Set("stopCounterKey", PropertyValue.Of("test.highStop"));
            definition.Nodes[3].Properties.Set("resultKey", PropertyValue.Of("test.result"));
            definition.Nodes[4].Properties.Set("startCounterKey", PropertyValue.Of("test.lowStart"));
            definition.Nodes[4].Properties.Set("stopCounterKey", PropertyValue.Of("test.lowStop"));
            definition.Nodes[4].Properties.Set("resultKey", PropertyValue.Of("test.result"));

            var runtime = Build(definition);
            runtime.Blackboard.SetBool("test.cond", false);
            runtime.Blackboard.SetInt64("test.result", 2);

            runtime.Update(1, Fixed64.Zero);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.lowStart"));

            // 缈荤湡 鈫?浣庡垎鏀涓
            runtime.Blackboard.SetBool("test.cond", true);
            runtime.Update(2, Fixed64.Zero);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.lowStop"));
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.highStart"));
            runtime.Blackboard.SetBool("test.cond", false);
            runtime.Update(3, Fixed64.Zero);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.highStop"));
            Assert.Equal(2, runtime.Blackboard.GetInt64("test.lowStart"));
        }

        [Fact]
        public void NestedComposites_ExecuteChildrenInPreorder()
        {
            // root = Sequence[ inner, c ]; inner = Sequence[ a, b ] 鈥斺€?宓屽缁撴瀯涓嬪瓙鑺傜偣绱㈠紩蹇呴』姝ｇ‘
            var definition = new TreeBuilder()
                .Blackboard("test.result", TreeValueType.Int64)
                .Blackboard("test.aStart", TreeValueType.Int64)
                .Blackboard("test.bStart", TreeValueType.Int64)
                .Blackboard("test.cStart", TreeValueType.Int64)
                .Node("root", BuiltInNodeTypes.Sequence, "inner", "c")
                .Node("inner", BuiltInNodeTypes.Sequence, "a", "b")
                .Node("a", CountingAction)
                .Node("b", CountingAction)
                .Node("c", CountingAction)
                .Root("root");
            definition.Nodes[2].Properties.Set("startCounterKey", PropertyValue.Of("test.aStart"));
            definition.Nodes[3].Properties.Set("startCounterKey", PropertyValue.Of("test.bStart"));
            definition.Nodes[4].Properties.Set("startCounterKey", PropertyValue.Of("test.cStart"));

            var runtime = Build(definition);
            SetResult(runtime, "test.result", 1);

            runtime.Update(1, Fixed64.Zero);

            Assert.Equal(NodeState.Success, runtime.RootNodeState);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.aStart"));
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.bStart"));
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.cStart"));
        }

        [Fact]
        public void RestartWhenComplete_OptionRestartsTreeAutomatically()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.result", TreeValueType.Int64)
                .Blackboard("test.startCount", TreeValueType.Int64)
                .Node("root", BuiltInNodeTypes.Sequence, "a")
                .Node("a", CountingAction)
                .Root("root");

            var runtime = Build(definition, new TreeRunOptions { RestartWhenComplete = true });
            SetResult(runtime, "test.result", 1);

            runtime.Update(1, Fixed64.Zero);
            runtime.Update(2, Fixed64.Zero);
            Assert.Equal(2, runtime.Blackboard.GetInt64("test.startCount"));
        }
    }
}
