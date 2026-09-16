using System;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Game.Battle.Component;
using NUnit.Framework;

namespace AbilityKit.Game.Tests
{
    public sealed class BattleCharacterHfsmComponentTests
    {
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
