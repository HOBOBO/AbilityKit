using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.CreateWorld;
using AbilityKit.Ability.Host.Extensions.Moba.Runtime;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Protocol.Moba;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaLogicWorldDriveGateTests
    {
        [Test]
        public void DriveState_TracksChangesAndDisposeRestoresSafeDefaults()
        {
            var state = new MobaLogicWorldDriveStateService();
            Assert.That(state.IsPaused, Is.False);
            Assert.That(state.IsReplayReady, Is.True);
            Assert.That(state.OwnsSimulation, Is.True);
            Assert.That(state.Revision, Is.Zero);

            state.Configure(
                MobaBattleLaunchSyncMode.Replay,
                MobaBattleLaunchAuthorityMode.ClientPrediction,
                ownsSimulation: true,
                replayMode: true,
                replayReady: false,
                reason: "test profile");
            Assert.That(state.IsReplayMode, Is.True);
            Assert.That(state.IsReplayReady, Is.False);
            Assert.That(state.Revision, Is.EqualTo(1));
            Assert.That(state.LastChangeReason, Is.EqualTo("test profile"));

            state.SetPaused(true, "test pause");
            state.SetPaused(true, "ignored duplicate");
            state.SetReplayReady(true, "validated replay");
            Assert.That(state.IsPaused, Is.True);
            Assert.That(state.IsReplayReady, Is.True);
            Assert.That(state.Revision, Is.EqualTo(3));
            Assert.That(state.LastChangeReason, Is.EqualTo("validated replay"));

            state.Dispose();
            Assert.That(state.IsPaused, Is.False);
            Assert.That(state.IsReplayMode, Is.False);
            Assert.That(state.IsReplayReady, Is.True);
            Assert.That(state.OwnsSimulation, Is.True);
            Assert.That(state.SyncMode, Is.EqualTo(MobaBattleLaunchSyncMode.FrameSync));
            Assert.That(state.AuthorityMode, Is.EqualTo(MobaBattleLaunchAuthorityMode.LocalAuthority));
            Assert.That(state.Revision, Is.Zero);
            Assert.That(state.LastChangeReason, Is.EqualTo("disposed"));
        }

        [TestCase(float.NaN, MobaLogicWorldDriveBlockReason.InvalidDeltaTime)]
        [TestCase(-0.01f, MobaLogicWorldDriveBlockReason.InvalidDeltaTime)]
        public void Gate_InvalidDeltaTimeHasHighestPriority(
            float deltaTime,
            MobaLogicWorldDriveBlockReason expected)
        {
            var gate = new MobaLogicWorldDriveGate();
            AssertDecision(gate.Evaluate(deltaTime), expected);
        }

        [Test]
        public void Gate_ReportsMissingAndInactivePrerequisitesInOrder()
        {
            var gate = new MobaLogicWorldDriveGate();
            AssertDecision(gate.Evaluate(0.02f), MobaLogicWorldDriveBlockReason.MissingPhaseService);

            var phase = new MobaLogicWorldRunGateService();
            Inject(gate, "_phase", phase);
            AssertDecision(gate.Evaluate(0.02f), MobaLogicWorldDriveBlockReason.NotInGame);

            phase.SetInGame("test");
            AssertDecision(gate.Evaluate(0.02f), MobaLogicWorldDriveBlockReason.MissingDriveState);
        }

        [Test]
        public void Gate_PrioritizesPauseSettlementReplayAndOwnership()
        {
            var phase = new MobaLogicWorldRunGateService();
            phase.SetInGame("test");
            var state = new MobaLogicWorldDriveStateService();
            state.Configure(
                MobaBattleLaunchSyncMode.Replay,
                MobaBattleLaunchAuthorityMode.ServerAuthority,
                ownsSimulation: false,
                replayMode: true,
                replayReady: false);
            state.SetPaused(true);
            var gameplay = new MobaGameplayService();
            SetPrivateField(gameplay, "_phase", MobaGameplayPhase.Ended);
            var gate = CreateGate(phase, state, gameplay, new ReadyRuntimePort());

            AssertDecision(gate.Evaluate(0.02f), MobaLogicWorldDriveBlockReason.Paused);
            state.SetPaused(false);
            AssertDecision(gate.Evaluate(0.02f), MobaLogicWorldDriveBlockReason.SettlementReached);

            SetPrivateField(gameplay, "_phase", MobaGameplayPhase.Running);
            AssertDecision(gate.Evaluate(0.02f), MobaLogicWorldDriveBlockReason.ReplayNotReady);
            state.SetReplayReady(true);
            AssertDecision(gate.Evaluate(0.02f), MobaLogicWorldDriveBlockReason.AuthorityDoesNotOwnSimulation);
        }

        [Test]
        public void Gate_WhenProfileOwnsReadySimulation_AllowsDrive()
        {
            var phase = new MobaLogicWorldRunGateService();
            phase.SetInGame("test");
            var state = new MobaLogicWorldDriveStateService();
            state.Configure(
                MobaBattleLaunchSyncMode.Hybrid,
                MobaBattleLaunchAuthorityMode.ClientPrediction,
                ownsSimulation: true,
                replayMode: false,
                replayReady: true);
            var gameplay = new MobaGameplayService();
            SetPrivateField(gameplay, "_phase", MobaGameplayPhase.Running);
            var gate = CreateGate(phase, state, gameplay, new ReadyRuntimePort());

            var decision = gate.Evaluate(0.02f);
            Assert.That(decision.CanDrive, Is.True, decision.ToString());
            Assert.That(decision.BlockReason, Is.EqualTo(MobaLogicWorldDriveBlockReason.None));
            Assert.That(gate.CanDriveLogicWorld(0.02f), Is.True);
        }

        private static MobaLogicWorldDriveGate CreateGate(
            MobaLogicWorldRunGateService phase,
            MobaLogicWorldDriveStateService state,
            MobaGameplayService gameplay,
            IMobaBattleRuntimePort runtime)
        {
            var gate = new MobaLogicWorldDriveGate();
            Inject(gate, "_phase", phase);
            Inject(gate, "_driveState", state);
            Inject(gate, "_gameplay", gameplay);
            Inject(gate, "_runtime", runtime);
            return gate;
        }

        private static void Inject<T>(MobaLogicWorldDriveGate gate, string fieldName, T value)
        {
            SetPrivateField(gate, fieldName, value);
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static void AssertDecision(
            MobaLogicWorldDriveDecision decision,
            MobaLogicWorldDriveBlockReason expected)
        {
            Assert.That(decision.CanDrive, Is.False, decision.ToString());
            Assert.That(decision.BlockReason, Is.EqualTo(expected));
            Assert.That(decision.Message, Is.Not.Empty);
        }

        private sealed class ReadyRuntimePort : IMobaBattleRuntimePort
        {
            public MobaBattleRuntimeStatus Status => new MobaBattleRuntimeStatus(
                MobaBattleRuntimeCapability.Input | MobaBattleRuntimeCapability.SnapshotOutput,
                null);

            public MobaGameStartResult TryStartGame(in MobaGameStartSpec spec)
            {
                return default;
            }

            public MobaInputSubmitResult Submit(
                FrameIndex frame,
                IReadOnlyList<PlayerInputCommand> inputs)
            {
                return default;
            }

            public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
            {
                snapshot = default;
                return false;
            }

            public int CollectSnapshots(
                FrameIndex frame,
                IList<WorldStateSnapshot> snapshots,
                int maxSnapshots = 32)
            {
                return 0;
            }

            public MobaDiagnosticEntityState[] GetDiagnosticEntityStates()
            {
                return Array.Empty<MobaDiagnosticEntityState>();
            }

            public int FillDiagnosticEntityStates(IList<MobaDiagnosticEntityState> buffer)
            {
                return 0;
            }

            public LogicWorldEntityState[] GetAllEntityStates()
            {
                return Array.Empty<LogicWorldEntityState>();
            }

            public int FillAllEntityStates(IList<LogicWorldEntityState> buffer)
            {
                return 0;
            }

            public void Dispose()
            {
            }
        }
    }
}
