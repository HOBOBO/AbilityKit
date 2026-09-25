using System;
using AbilityKit.Demo.Tiny.FrameSync;
using NUnit.Framework;

namespace AbilityKit.Demo.Tiny.Tests
{
    public sealed class TinyFrameSyncTests
    {
        [Test]
        public void LateInputReplaysAndMatchesAuthority()
        {
            var predicted = NewBattle();
            var session = new TinyFrameSyncSession(predicted);
            session.Predict(Array.Empty<TinyFrameInput>());
            session.Predict(Array.Empty<TinyFrameInput>());

            var authority = NewBattle();
            authority.Submit(1, new TinyInput(1, 0, true));
            authority.Tick();
            var firstHash = authority.ComputeHash();
            authority.Tick();

            Assert.That(session.ApplyAuthoritative(1,
                new[] { new TinyFrameInput(1, new TinyInput(1, 0, true)) }, firstHash),
                Is.EqualTo(TinyReconcileResult.Replayed));
            Assert.That(session.StateHash, Is.EqualTo(authority.ComputeHash()));
        }

        [Test]
        public void HashMismatchRequiresFullState()
        {
            var session = new TinyFrameSyncSession(NewBattle());
            session.Predict(Array.Empty<TinyFrameInput>());
            Assert.That(session.ApplyAuthoritative(1, Array.Empty<TinyFrameInput>(),
                session.StateHash + 1), Is.EqualTo(TinyReconcileResult.NeedsFullSnapshot));
            Assert.That(session.RequiresFullSnapshot, Is.True);
        }

        [Test]
        public void FullStateCodecPreservesRollbackFields()
        {
            var battle = NewBattle();
            battle.Submit(1, new TinyInput(1, 0, true));
            battle.Tick();
            var state = battle.CaptureState();
            var restored = TinyBattleStateCodec.Decode(TinyBattleStateCodec.Encode(state));
            Assert.That(restored.Frame, Is.EqualTo(state.Frame));
            Assert.That(restored.Actors, Is.EqualTo(state.Actors));
        }

        private static TinyBattle NewBattle()
        {
            var battle = new TinyBattle();
            battle.AddPlayer(1, -1, 0);
            battle.AddPlayer(2, 1, 0);
            return battle;
        }
    }
}
