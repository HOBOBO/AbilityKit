using AbilityKit.Demo.Moba.Services.StateMachine;
using NUnit.Framework;

namespace AbilityKit.Game.Tests
{
    public sealed class MobaCharacterModeRuntimeTests
    {
        [Test]
        public void ModeEnterExitUsesDeterministicInstanceAndLocalFrame()
        {
            var runtime = new MobaCharacterModeRuntime(41, 0);
            var normal = runtime.Current;

            Assert.AreEqual(MobaCharacterModeState.NormalModeId, normal.ModeId);
            Assert.AreEqual(((long)41 << 32) | 1u, normal.InstanceId);
            Assert.IsTrue(runtime.RequestEnter("curtain_call", 10, 7001, 1));

            runtime.Advance(1);
            var active = runtime.Current;
            Assert.AreEqual("curtain_call", active.ModeId);
            Assert.AreEqual(7001L, active.OwnerInstanceId);
            Assert.AreEqual(1, active.StartFrame);
            Assert.AreEqual(0, active.LocalFrame);

            runtime.Advance(4);
            Assert.AreEqual(3, runtime.Current.LocalFrame);
            Assert.IsTrue(runtime.RequestExit("curtain_call", 7001, 5));
            runtime.Advance(5);

            Assert.AreEqual(MobaCharacterModeState.NormalModeId, runtime.Current.ModeId);
            Assert.AreEqual(0L, runtime.Current.OwnerInstanceId);
            Assert.AreEqual(5, runtime.Current.StartFrame);
            Assert.AreEqual(0, runtime.Current.LocalFrame);
            Assert.AreNotEqual(active.InstanceId, runtime.Current.InstanceId);
        }

        [Test]
        public void LowerPriorityModeDoesNotReplaceActiveMode()
        {
            var runtime = new MobaCharacterModeRuntime(41, 0);
            Assert.IsTrue(runtime.RequestEnter("mode_a", 20, 1, 1));
            runtime.Advance(1);
            Assert.IsTrue(runtime.RequestEnter("mode_b", 10, 2, 2));
            runtime.Advance(2);

            Assert.AreEqual("mode_a", runtime.Current.ModeId);
            Assert.AreEqual(20, runtime.Current.Priority);
        }

        [Test]
        public void ModePriorityContractUsesStableSparseBands()
        {
            Assert.IsTrue(MobaCharacterModePriority.CanReplace(
                MobaCharacterModePriority.SpecialMode, MobaCharacterModePriority.Action));
            Assert.IsFalse(MobaCharacterModePriority.CanReplace(
                MobaCharacterModePriority.Action, MobaCharacterModePriority.SpecialMode));
            Assert.Less(MobaCharacterModePriority.SpecialMode, MobaCharacterModePriority.HardControl);
            Assert.Less(MobaCharacterModePriority.HardControl, MobaCharacterModePriority.Dead);
        }

        [Test]
        public void SnapshotRestoresModeCursorAndNextInstanceSequence()
        {
            var runtime = new MobaCharacterModeRuntime(41, 0);
            runtime.RequestEnter("mode_a", 20, 1, 1);
            runtime.Advance(3);
            var snapshot = runtime.CaptureSnapshot();

            runtime.RequestExit("mode_a", 1, 4);
            runtime.Advance(4);
            runtime.RestoreSnapshot(snapshot);

            Assert.AreEqual("mode_a", runtime.Current.ModeId);
            Assert.AreEqual(snapshot.Current.InstanceId, runtime.Current.InstanceId);
            Assert.AreEqual(2, runtime.Current.LocalFrame);
            Assert.AreEqual(snapshot.NextInstanceSequence, runtime.CaptureSnapshot().NextInstanceSequence);
        }

        [Test]
        public void CharacterHfsmPublishesModeWithActionSnapshot()
        {
            var definition = MobaCharacterHfsmTestFixture.CreateDefinition();
            using (var runtime = new MobaCharacterHfsmRuntime(41, definition, 0,
                       AbilityKit.Deterministic.Fixed64.Zero, registerForInspection: false))
            {
                Assert.IsTrue(runtime.RequestModeEnter("curtain_call", 10, 7001, 1));
                runtime.Tick(1, AbilityKit.Deterministic.Fixed64.One, true, false, false, 7001, 30010401);

                Assert.AreEqual("curtain_call", runtime.Mode.ModeId);
                Assert.AreEqual(runtime.Mode.ModeId, runtime.Action.ModeId);
                Assert.AreEqual(runtime.Mode.InstanceId, runtime.Action.ModeInstanceId);

                var snapshot = runtime.CaptureSnapshot();
                using (var restored = new MobaCharacterHfsmRuntime(41, definition, 0,
                           AbilityKit.Deterministic.Fixed64.Zero, registerForInspection: false))
                {
                    restored.RestoreSnapshot(snapshot);
                    Assert.AreEqual(runtime.Mode.ModeId, restored.Mode.ModeId);
                    Assert.AreEqual(runtime.Mode.InstanceId, restored.Mode.InstanceId);
                    Assert.AreEqual(runtime.Action.ModeLocalFrame, restored.Action.ModeLocalFrame);
                }
            }
        }

        [Test]
        public void SameFrameRequestIsAppliedByTheCurrentTick()
        {
            var runtime = new MobaCharacterModeRuntime(41, 4);
            Assert.IsTrue(runtime.RequestEnter("curtain_call", 10, 7001, 4));

            runtime.Advance(4);

            Assert.AreEqual("curtain_call", runtime.Current.ModeId);
            Assert.AreEqual(4, runtime.Current.StartFrame);
            Assert.AreEqual(0, runtime.Current.LocalFrame);
        }

        [Test]
        public void SnapshotRestoresPendingModeRequest()
        {
            var runtime = new MobaCharacterModeRuntime(41, 4);
            runtime.RequestEnter("curtain_call", 10, 7001, 6);
            var snapshot = runtime.CaptureSnapshot();

            runtime.Advance(6);
            runtime.RestoreSnapshot(snapshot);
            runtime.Advance(6);

            Assert.AreEqual("curtain_call", runtime.Current.ModeId);
            Assert.AreEqual(6, runtime.Current.StartFrame);
            Assert.AreEqual(0, runtime.Current.LocalFrame);
        }

        [Test]
        public void DeathResetWinsOverModeEntryRequestedOnTheSameFrame()
        {
            var runtime = new MobaCharacterModeRuntime(41, 4);
            runtime.RequestEnter("curtain_call", 1000, 7001, 6);
            runtime.RequestResetToNormal(6, MobaCharacterModeTransitionReason.Death);

            runtime.Advance(6);

            Assert.AreEqual(MobaCharacterModeState.NormalModeId, runtime.Current.ModeId);
            Assert.AreEqual(MobaCharacterModeTransitionReason.Death,
                runtime.Current.TransitionReason);
        }

        [Test]
        public void SameModeFromAnotherOwnerReplacesOnlyWhenPriorityAllows()
        {
            var runtime = new MobaCharacterModeRuntime(41, 0);
            runtime.RequestEnter("curtain_call", 10, 1001, 1);
            runtime.Advance(1);
            runtime.RequestEnter("curtain_call", 20, 1002, 2);
            runtime.Advance(2);

            Assert.AreEqual("curtain_call", runtime.Current.ModeId);
            Assert.AreEqual(1002L, runtime.Current.OwnerInstanceId);
            Assert.AreEqual(20, runtime.Current.Priority);
        }

        [Test]
        public void ControlledCharacterResetsModeAtNextHfsmTick()
        {
            var definition = MobaCharacterHfsmTestFixture.CreateDefinition();
            using (var runtime = new MobaCharacterHfsmRuntime(41, definition, 0,
                       AbilityKit.Deterministic.Fixed64.Zero, registerForInspection: false))
            {
                runtime.RequestModeEnter("curtain_call", 10, 7001, 1);
                runtime.Tick(1, AbilityKit.Deterministic.Fixed64.One, true, false, false, 7001, 30010401);
                Assert.AreEqual("curtain_call", runtime.Mode.ModeId);

                runtime.Tick(2, AbilityKit.Deterministic.Fixed64.FromRaw(2), true, true, false, 7001, 30010401);
                Assert.AreEqual(MobaCharacterModeState.NormalModeId, runtime.Mode.ModeId);
                Assert.AreEqual(MobaCharacterModeTransitionReason.Controlled,
                    runtime.Mode.TransitionReason);
            }
        }
    }
}
