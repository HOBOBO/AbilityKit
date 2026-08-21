using System.Collections.Generic;
using AbilityKit.Deterministic;
using Xunit;
using static AbilityKit.BehaviorTree.Tests.TestNodeTypes;

namespace AbilityKit.BehaviorTree.Tests
{
    /// <summary>
    /// 确定性：同种子逐帧一致、随机组合首个执行子节点可复现、概率模式跨种子可区分、
    /// 快照恢复后随机流精确续走。
    /// </summary>
    public sealed class BtDeterminismTests
    {
        /// <summary>随机顺序树：首个执行的孩子即洗牌首选（fail-fast 使其余孩子不启动）。</summary>
        private static BtTreeDefinition RandomSequenceTree(int childCount)
        {
            var builder = new TreeBuilder().Blackboard("test.result", BtValueType.Int64);
            var children = new string[childCount];
            for (var i = 0; i < childCount; i++) children[i] = "c" + i;

            builder.Node("root", BtBuiltInNodeTypes.RandomSequence, children);
            for (var i = 0; i < childCount; i++)
            {
                builder.Node("c" + i, CountingAction);
                builder.LastNode.Properties.Set("startCounterKey", BtPropertyValue.Of("c" + i + "Start"));
                builder.LastNode.Properties.Set("resultKey", BtPropertyValue.Of("test.result"));
                builder.Blackboard("c" + i + "Start", BtValueType.Int64);
            }
            return builder.Root("root");
        }

        private static string FirstStartedChild(BtTreeRuntime runtime, int childCount)
        {
            for (var i = 0; i < childCount; i++)
            {
                if (runtime.Blackboard.GetInt64("c" + i + "Start") > 0) return "c" + i;
            }
            return "none";
        }

        [Fact]
        public void SameSeed_SameFirstShuffledChild_AcrossRestarts()
        {
            var definition = RandomSequenceTree(6);
            var a = BtTreeRuntime.Create(definition, CreateRegistry(), null, new BtTreeRunOptions { Seed = 777 });
            var b = BtTreeRuntime.Create(definition, CreateRegistry(), null, new BtTreeRunOptions { Seed = 777 });
            a.Enable();
            b.Enable();
            a.Blackboard.SetInt64("test.result", 0);   // 首个孩子失败即终止
            b.Blackboard.SetInt64("test.result", 0);

            for (var round = 0; round < 8; round++)
            {
                a.Update(round + 1, Fixed64.Zero);
                b.Update(round + 1, Fixed64.Zero);
                Assert.Equal(BtNodeState.Failure, a.TreeState);
                // 同种子、同轮次：随机流位置一致 → 洗牌首选一致
                Assert.Equal(FirstStartedChild(a, 6), FirstStartedChild(b, 6));
                a.Restart();
                b.Restart();
            }
        }

        [Fact]
        public void SameSeed_IdenticalFrameByFrameStates()
        {
            var definition = RandomSequenceTree(5);
            var options = new BtTreeRunOptions { Seed = 42 };

            var a = BtTreeRuntime.Create(definition, CreateRegistry(), null, options);
            var b = BtTreeRuntime.Create(definition, CreateRegistry(), null, options);
            a.Enable();
            b.Enable();
            a.Blackboard.SetInt64("test.result", 0);
            b.Blackboard.SetInt64("test.result", 0);

            for (var frame = 1; frame <= 10; frame++)
            {
                a.Update(frame, Fixed64.Zero);
                b.Update(frame, Fixed64.Zero);
                Assert.Equal(a.RootNodeState, b.RootNodeState);
                Assert.Equal(a.TreeState, b.TreeState);
                if (a.TreeState == BtNodeState.Failure)
                {
                    a.Restart();
                    b.Restart();
                }
            }

            for (var i = 0; i < 5; i++)
            {
                Assert.Equal(a.Blackboard.GetInt64("c" + i + "Start"), b.Blackboard.GetInt64("c" + i + "Start"));
            }
        }

        [Fact]
        public void ProbabilityPatterns_DifferAcrossSeeds_AndRepeatWithinSeed()
        {
            var definition = new TreeBuilder()
                .Node("root", BtBuiltInNodeTypes.Probability)
                .Root("root");
            definition.Nodes[0].Properties.Set("percent", BtPropertyValue.Of(50L));

            Assert.NotEqual(DrawPattern(definition, 20260820), DrawPattern(definition, 20260821));
            Assert.Equal(DrawPattern(definition, 20260820), DrawPattern(definition, 20260820));
        }

        private static string DrawPattern(BtTreeDefinition definition, ulong seed)
        {
            var runtime = BtTreeRuntime.Create(definition, CreateRegistry(), null, new BtTreeRunOptions { Seed = seed });
            runtime.Enable();
            var pattern = new List<char>();
            for (var i = 0; i < 32; i++)
            {
                runtime.Update(i, Fixed64.Zero);
                pattern.Add(runtime.RootNodeState == BtNodeState.Success ? '1' : '0');
                runtime.Restart();
            }
            return new string(pattern.ToArray());
        }

        [Fact]
        public void SnapshotRestore_ResumesRandomStreamExactly()
        {
            var definition = RandomSequenceTree(5);
            var options = new BtTreeRunOptions { Seed = 99 };

            var original = BtTreeRuntime.Create(definition, CreateRegistry(), null, options);
            original.Enable();
            original.Blackboard.SetInt64("test.result", 0);
            for (var frame = 1; frame <= 3; frame++)
            {
                original.Update(frame, Fixed64.Zero);
                if (original.TreeState == BtNodeState.Failure) original.Restart();
            }

            var snapshot = original.CaptureState();
            var fork = BtTreeRuntime.Create(definition, CreateRegistry(), null, options);
            fork.Enable();
            fork.RestoreState(snapshot);
            fork.Blackboard.SetInt64("test.result", 0);

            for (var frame = 4; frame <= 12; frame++)
            {
                original.Update(frame, Fixed64.Zero);
                fork.Update(frame, Fixed64.Zero);
                Assert.Equal(original.RootNodeState, fork.RootNodeState);
                Assert.Equal(original.TreeState, fork.TreeState);
                if (original.TreeState == BtNodeState.Failure)
                {
                    original.Restart();
                    fork.Restart();
                }
            }

            for (var i = 0; i < 5; i++)
            {
                Assert.Equal(original.Blackboard.GetInt64("c" + i + "Start"), fork.Blackboard.GetInt64("c" + i + "Start"));
            }
        }
    }
}
