using System;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Flow;
using NUnit.Framework;

namespace AbilityKit.Game.Tests
{
    public sealed class BattleCharacterHfsmComponentTests
    {
        [Test]
        public void PresentationActionProjectsWithoutLogicWorld()
        {
            var view = new BattleCharacterHfsmComponent();
            var action = new CharacterActionData(41, 1, 105, "life/alive/action/idle",
                ((long)41 << 32) | 100u, 100, 5, 77, 7001);
            view.ApplyPresentation(in action);

            Assert.AreEqual(105, view.SnapshotFrame);
            Assert.AreEqual(77, view.Action.CastInstanceId);
            Assert.IsNotNull(view.Evaluate(106, 30));
            Assert.IsNull(view.Evaluate(104, 30));
        }

        [Test]
        public void CharacterActionProjectionRejectsStaleVersionAndFrame()
        {
            var context = new BattleContext();
            context.ObserveActorSpawnIdentity(41, 2);
            var current = new CharacterActionData(41, 2, 105, "life/alive/action/casting",
                ((long)41 << 32) | 100u, 100, 5, 77, 7001);
            var stale = new CharacterActionData(41, 2, 104, "life/alive/action/idle",
                ((long)41 << 32) | 100u, 100, 4, 0, 0);
            context.ApplyCharacterActions(new[] { current, stale });
            Assert.IsTrue(((IBattleCharacterActionReadPort)context).TryGetCharacterAction(41, out var projected));
            Assert.AreEqual("life/alive/action/casting", projected.Path);

            context.ObserveActorSpawnIdentity(41, 3);
            Assert.IsFalse(((IBattleCharacterActionReadPort)context).TryGetCharacterAction(41, out _));
            context.ForgetActorIdentity(41);
        }

        [Test]
        public void CharacterActionProjectionWaitsForMatchingSpawnAfterReconnect()
        {
            var context = new BattleContext();
            var action = new CharacterActionData(41, 3, 105, "life/alive/action/casting",
                ((long)41 << 32) | 100u, 100, 5, 77, 7001);
            context.ApplyCharacterActions(new[] { action });

            Assert.IsFalse(((IBattleCharacterActionReadPort)context).TryGetCharacterAction(41, out _));
            context.ObserveActorSpawnIdentity(41, 2);
            Assert.IsFalse(((IBattleCharacterActionReadPort)context).TryGetCharacterAction(41, out _));
            context.ObserveActorSpawnIdentity(41, 3);
            Assert.IsTrue(((IBattleCharacterActionReadPort)context).TryGetCharacterAction(41, out var projected));
            Assert.AreEqual(105, projected.SnapshotFrame);
        }

        [Test]
        public void SnapshotRestoresCastingFrameWithoutTransitionReplay()
        {
            var definition = MobaCharacterHfsmProfile.CreateDefinition();
            using (var logic = new MobaCharacterHfsmRuntime(41, definition, 0, Fixed64.Zero))
            {
                logic.Tick(1, Fixed64.FromRaw(1000), true, false, false, 100, 7001);
                logic.Tick(2, Fixed64.FromRaw(2000), true, false, false, 100, 7001);
                var view = new BattleCharacterHfsmComponent(definition);
                view.ApplySnapshot(logic.CaptureSnapshot());
                Assert.AreEqual("life/alive/action/casting", view.Action.Path);
                Assert.AreEqual(1, view.Action.LocalFrame);
                Assert.AreEqual(logic.DefinitionHash, view.DefinitionHash);

                logic.Tick(3, Fixed64.FromRaw(3000), true, false, false, 100, 7001);
                view.ApplySnapshot(logic.CaptureSnapshot());
                Assert.AreEqual(2, view.Action.LocalFrame);
            }
        }

        [Test]
        public void InvalidActionDoesNotPartiallyRestoreView()
        {
            var definition = MobaCharacterHfsmProfile.CreateDefinition();
            using (var logic = new MobaCharacterHfsmRuntime(41, definition, 0, Fixed64.Zero))
            {
                var view = new BattleCharacterHfsmComponent(definition);
                view.ApplySnapshot(logic.CaptureSnapshot());
                var original = view.Action;
                logic.Tick(1, Fixed64.FromRaw(1000), true, false, true, 0, 0);
                var invalid = logic.CaptureSnapshot();
                invalid.Action = new MobaCharacterActionState(invalid.Action.Path,
                    invalid.Action.InstanceId, invalid.Action.StartFrame, 99, 0, 0);
                Assert.Throws<InvalidOperationException>(() => view.ApplySnapshot(invalid));
                Assert.AreEqual(0, view.SnapshotFrame);
                Assert.AreEqual(original.InstanceId, view.Action.InstanceId);
            }
        }
    }
}
