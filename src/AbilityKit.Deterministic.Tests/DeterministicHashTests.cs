using Xunit;

namespace AbilityKit.Deterministic.Tests;

public sealed class DeterministicHashTests
{
        [Fact]
        public void Hash_IsStableAcrossCalls()
        {
            var vector = new FixedVec3(Fixed64.FromRatio(1, 3), Fixed64.FromInt64(-7), Fixed64.Zero);

            Assert.Equal(DeterministicHash.Hash(vector), DeterministicHash.Hash(vector));
            Assert.Equal(DeterministicHash.Hash(Fixed64.One), DeterministicHash.Hash(Fixed64.One));
        }

        [Fact]
        public void Hash_DistinguishesDifferentValues()
        {
            Assert.NotEqual(DeterministicHash.Hash(Fixed64.Zero), DeterministicHash.Hash(Fixed64.One));
            Assert.NotEqual(DeterministicHash.Hash(Fixed64.FromInt64(1)), DeterministicHash.Hash(Fixed64.FromInt64(256)));

            var left = new FixedVec2(Fixed64.One, Fixed64.Zero);
            var right = new FixedVec2(Fixed64.Zero, Fixed64.One);
            Assert.NotEqual(DeterministicHash.Hash(left), DeterministicHash.Hash(right));
        }

        [Fact]
        public void Hash_OrderMattersWhenCombining()
        {
            var first = DeterministicHash.Combine(DeterministicHash.OffsetBasis, Fixed64.One);
            var second = DeterministicHash.Combine(first, Fixed64.Zero);
            var reversed = DeterministicHash.Combine(
                DeterministicHash.Combine(DeterministicHash.OffsetBasis, Fixed64.Zero),
                Fixed64.One);

            Assert.NotEqual(second, reversed);
        }

        [Fact]
        public void Combine_Vectors_FoldComponentsInOrder()
        {
            var vector = new FixedVec3(Fixed64.FromRatio(3, 4), Fixed64.FromInt64(-2), Fixed64.FromRatio(5, 6));

            var expected = DeterministicHash.Combine(
                DeterministicHash.Combine(
                    DeterministicHash.Combine(DeterministicHash.OffsetBasis, vector.X),
                    vector.Y),
                vector.Z);

            Assert.Equal(expected, DeterministicHash.Hash(vector));
        }

        [Fact]
        public void OffsetBasis_And_Prime_UseFnv1a64Values()
        {
            Assert.Equal(unchecked((long)0xCBF29CE484222325UL), DeterministicHash.OffsetBasis);
            Assert.Equal(unchecked((long)0x100000001B3UL), DeterministicHash.Prime);
        }
}
