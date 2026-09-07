using System.Collections.Generic;
using AbilityKit.Deterministic;
using Xunit;
using static AbilityKit.BehaviorTree.Tests.TestNodeTypes;

namespace AbilityKit.BehaviorTree.Tests
{
    /// <summary>
    /// 纭畾鎬э細鍚岀瀛愰€愬抚涓€鑷淬€侀殢鏈虹粍鍚堥涓墽琛屽瓙鑺傜偣鍙鐜般€佹鐜囨ā寮忚法绉嶅瓙鍙尯鍒嗐€?    /// 蹇収鎭㈠鍚庨殢鏈烘祦绮剧‘缁蛋銆?    /// </summary>
    public sealed class BtDeterminismTests
    {
        /// <summary>闅忔満椤哄簭鏍戯細棣栦釜鎵ц鐨勫瀛愬嵆娲楃墝棣栭€夛紙fail-fast 浣垮叾浣欏瀛愪笉鍚姩锛夈€?/summary>
        private static TreeDefinition RandomSequenceTree(int childCount)
        {
            var builder = new TreeBuilder().Blackboard("test.result", TreeValueType.Int64);
            var children = new string[childCount];
            for (var i = 0; i < childCount; i++) children[i] = "c" + i;

            builder.Node("root", BuiltInNodeTypes.RandomSequence, children);
            for (var i = 0; i < childCount; i++)
            {
                builder.Node("c" + i, CountingAction);
                builder.LastNode.Properties.Set("startCounterKey", PropertyValue.Of("c" + i + "Start"));
                builder.LastNode.Properties.Set("resultKey", PropertyValue.Of("test.result"));
                builder.Blackboard("c" + i + "Start", TreeValueType.Int64);
            }
            return builder.Root("root");
        }

        private static string FirstStartedChild(TreeRuntime runtime, int childCount)
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
            var a = TreeRuntime.Create(definition, CreateRegistry(), null, new TreeRunOptions { Seed = 777 });
            var b = TreeRuntime.Create(definition, CreateRegistry(), null, new TreeRunOptions { Seed = 777 });
            a.Enable();
            b.Enable();
            a.Blackboard.SetInt64("test.result", 0);   // 棣栦釜瀛╁瓙澶辫触鍗崇粓姝?            b.Blackboard.SetInt64("test.result", 0);

            for (var round = 0; round < 8; round++)
            {
                a.Update(round + 1, Fixed64.Zero);
                b.Update(round + 1, Fixed64.Zero);
                Assert.Equal(NodeState.Failure, a.TreeState);
            Assert.Equal(FirstStartedChild(a, 6), FirstStartedChild(b, 6));
                a.Restart();
                b.Restart();
            }
        }

        [Fact]
        public void SameSeed_IdenticalFrameByFrameStates()
        {
            var definition = RandomSequenceTree(5);
            var options = new TreeRunOptions { Seed = 42 };

            var a = TreeRuntime.Create(definition, CreateRegistry(), null, options);
            var b = TreeRuntime.Create(definition, CreateRegistry(), null, options);
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
                if (a.TreeState == NodeState.Failure)
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
                .Node("root", BuiltInNodeTypes.Probability)
                .Root("root");
            definition.Nodes[0].Properties.Set("percent", PropertyValue.Of(50L));

            Assert.NotEqual(DrawPattern(definition, 20260820), DrawPattern(definition, 20260821));
            Assert.Equal(DrawPattern(definition, 20260820), DrawPattern(definition, 20260820));
        }

        private static string DrawPattern(TreeDefinition definition, ulong seed)
        {
            var runtime = TreeRuntime.Create(definition, CreateRegistry(), null, new TreeRunOptions { Seed = seed });
            runtime.Enable();
            var pattern = new List<char>();
            for (var i = 0; i < 32; i++)
            {
                runtime.Update(i, Fixed64.Zero);
                pattern.Add(runtime.RootNodeState == NodeState.Success ? '1' : '0');
                runtime.Restart();
            }
            return new string(pattern.ToArray());
        }

        [Fact]
        public void SnapshotRestore_ResumesRandomStreamExactly()
        {
            var definition = RandomSequenceTree(5);
            var options = new TreeRunOptions { Seed = 99 };

            var original = TreeRuntime.Create(definition, CreateRegistry(), null, options);
            original.Enable();
            original.Blackboard.SetInt64("test.result", 0);
            for (var frame = 1; frame <= 3; frame++)
            {
                original.Update(frame, Fixed64.Zero);
                if (original.TreeState == NodeState.Failure) original.Restart();
            }

            var snapshot = original.CaptureState();
            var fork = TreeRuntime.Create(definition, CreateRegistry(), null, options);
            fork.Enable();
            fork.RestoreState(snapshot);
            fork.Blackboard.SetInt64("test.result", 0);

            for (var frame = 4; frame <= 12; frame++)
            {
                original.Update(frame, Fixed64.Zero);
                fork.Update(frame, Fixed64.Zero);
                Assert.Equal(original.RootNodeState, fork.RootNodeState);
                Assert.Equal(original.TreeState, fork.TreeState);
                if (original.TreeState == NodeState.Failure)
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
