using System.Linq;
using NUnit.Framework;

namespace AbilityKit.Demo.Tiny.Logic.Tests
{
    public sealed class TinyLogicTests
    {
        [Test]
        public void SnapshotResumeMatchesUninterruptedBattle()
        {
            var uninterrupted = CreateBattle();
            uninterrupted.Submit(1, new TinyInput(1, 0, true));
            uninterrupted.Tick();
            var checkpoint = TinyBattleStateCodec.Encode(uninterrupted.CaptureState());
            var resumed = new TinyBattle();
            resumed.RestoreState(TinyBattleStateCodec.Decode(checkpoint));

            for (var frame = 0; frame < 40; frame++)
            {
                var input = new TinyInput((sbyte)(frame % 3 - 1), 0, frame % 7 == 0);
                uninterrupted.Submit(2, input);
                resumed.Submit(2, input);
                uninterrupted.Tick();
                resumed.Tick();
                Assert.That(resumed.ComputeHash(), Is.EqualTo(uninterrupted.ComputeHash()),
                    "Diverged after restoring frame " + frame);
            }
            Assert.That(resumed.CaptureState().Actors.Select(actor => actor.PlayerId),
                Is.EqualTo(new uint[] { 1, 2 }));
        }

        [Test]
        public void CorruptSnapshotCannotBecomeRecoveryBaseline()
        {
            var payload = TinyBattleStateCodec.Encode(CreateBattle().CaptureState());
            payload[5] = 0;
            Assert.That(() => TinyBattleStateCodec.Decode(payload), Throws.ArgumentException);
        }

        private static TinyBattle CreateBattle()
        {
            var battle = new TinyBattle();
            battle.AddPlayer(1, -1, 0);
            battle.AddPlayer(2, 1, 0);
            return battle;
        }
    }
}
