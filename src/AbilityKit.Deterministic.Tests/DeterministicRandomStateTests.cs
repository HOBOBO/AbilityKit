using System.Collections.Generic;
using AbilityKit.Deterministic;
using Xunit;

namespace AbilityKit.Deterministic.Tests
{
    /// <summary>CaptureState/RestoreState：随机流可精确续走（行为树等快照回滚依赖）。</summary>
    public sealed class DeterministicRandomStateTests
    {
        [Fact]
        public void CaptureThenRestore_ContinuesStreamExactly()
        {
            var a = new DeterministicRandom(20260820UL);
            var prefix = new List<ulong>();
            for (var i = 0; i < 5; i++) prefix.Add(a.NextUInt64());

            a.CaptureState(out var s0, out var s1, out var sequence);

            var fork = new DeterministicRandom(20260820UL);
            fork.RestoreState(s0, s1, sequence);

            for (var i = 0; i < 10; i++)
            {
                Assert.Equal(a.NextUInt64(), fork.NextUInt64());
                Assert.Equal(a.NextInt32(0, 100), fork.NextInt32(0, 100));
            }
        }

        [Fact]
        public void RestoreState_AdvancesSequenceCounter()
        {
            var random = new DeterministicRandom(7UL);
            for (var i = 0; i < 3; i++) random.NextUInt64();
            random.CaptureState(out var s0, out var s1, out var sequence);
            Assert.Equal(3UL, sequence);

            var restored = new DeterministicRandom(7UL);
            restored.RestoreState(s0, s1, sequence);
            restored.NextUInt64();
            restored.CaptureState(out _, out _, out var after);
            Assert.Equal(4UL, after);
        }

        [Fact]
        public void SameSeed_ProducesIdenticalStreams()
        {
            var a = new DeterministicRandom(12345UL);
            var b = new DeterministicRandom(12345UL);
            for (var i = 0; i < 32; i++)
            {
                Assert.Equal(a.NextUInt64(), b.NextUInt64());
            }
        }
    }
}
