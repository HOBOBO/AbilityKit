using AbilityKit.Ability.StateSync;
using AbilityKit.Ability.StateSync.Prediction;
using AbilityKit.Combat.Collision;
using AbilityKit.Combat.MotionSystem.Collision;
using AbilityKit.Combat.MotionSystem.Constraints;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Console.Battle.Context;
using AbilityKit.Demo.Moba.Console.Battle.Sync;
using AbilityKit.Demo.Moba.Services.Motion;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Demo.Moba.Share.Prediction;
using AbilityKit.Protocol.Moba.StateSync;
using Xunit;
using Xunit.Abstractions;
using ShareCuePredictionState = AbilityKit.Demo.Moba.Share.PresentationCuePredictionState;
using StateSyncVector3 = AbilityKit.Ability.StateSync.Vector3;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed class StateSyncPredictionEvidenceSmokeTests
{
    private readonly ITestOutputHelper _output;

    public StateSyncPredictionEvidenceSmokeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Hybrid_prediction_reconciles_real_wall_collision_and_replays_unconfirmed_inputs()
    {
        var config = BattleStartConfig.CreateDefault();
        config.SyncMode = SyncMode.Hybrid;
        config.EnableClientPrediction = true;
        var context = new ConsoleBattleContext { Plan = config.BuildPlan() };

        using var adapter = new HybridSyncAdapter();
        adapter.Initialize(context, config);
        var coordinator = adapter.GetCoordinator();
        var slots = coordinator.GetCurrentSlots();
        slots.Set(MobaPredictionSlotNames.Position, new StateSyncVector3(0f, 0f, 0f));
        slots.Set(MobaPredictionSlotNames.Velocity, new StateSyncVector3(0f, 0f, 0f));
        slots.Set(MobaPredictionSlotNames.Health, 100f);
        slots.Set(MobaPredictionSlotNames.MaxHealth, 100f);

        var input = new PlayerInput
        {
            OpCode = AbilityKit.Protocol.Moba.MobaOpCodes.Input.Move,
            Payload = MobaMoveCodec.Serialize(5f, 0f),
        };
        for (var i = 0; i < 4; i++) adapter.SubmitInput(input);

        var predictedBefore = slots.GetPosition(MobaPredictionSlotNames.Position);

        var collisionWorld = new GridCollisionWorld(cellSize: 1f, initialCapacity: 8);
        AddWall(collisionWorld, new Vec3(0.24f, 0f, 0f), new Vec3(0.04f, 1f, 2f));
        var motionWorld = new MobaMotionCollisionWorldAdapter(collisionWorld, null);
        var solver = new ConfigurableMotionSolver(
            motionWorld,
            (_, in _, in _, _) => MotionConstraints.Disabled);
        var constraints = new MotionCollisionConstraints(
            enable: true,
            allowPassThrough: false,
            endOverlapPolicy: MotionEndOverlapPolicy.ClampToLastValid,
            radius: 0.05f,
            skin: 0.01f,
            obstacleMask: MobaCollisionLayers.WorldMask,
            ignoreMask: 0);

        var authorityPosition = Vec3.Zero;
        var collisionCount = 0;
        for (var frame = 1; frame <= 2; frame++)
        {
            var result = solver.Resolve(
                adapter.LocalActorId,
                authorityPosition,
                new Vec3(5f / 30f, 0f, 0f),
                constraints);
            authorityPosition += result.AppliedDelta;
            if (result.Hit.Hit) collisionCount++;
        }
        Assert.True(collisionCount > 0, "The authority motion path must collide with the wall.");

        adapter.OnServerFrameConfirmed(2, new[]
        {
            new ActorStateSnapshot
            {
                ActorId = adapter.LocalActorId,
                X = authorityPosition.X,
                Y = authorityPosition.Y,
                Z = authorityPosition.Z,
                VelocityX = 0f,
                VelocityZ = 0f,
                Hp = 100f,
                HpMax = 100f,
            },
        });

        var reconciled = slots.GetPosition(MobaPredictionSlotNames.Position);
        Assert.Equal(1L, adapter.TotalServerStateApplications);
        Assert.Equal(1L, adapter.TotalRollbackCorrections);
        Assert.NotEqual(ConflictLevel.None, adapter.LastConflictLevel);
        Assert.Equal(2, coordinator.ConfirmedFrame.Value);
        Assert.Equal(4, coordinator.CurrentFrame.Value);
        Assert.True(reconciled.X < predictedBefore.X);
        Assert.True(reconciled.X > authorityPosition.X);

        _output.WriteLine(
            "movement evidence: predictedX={0:F4}; authorityX={1:F4}; reconciledX={2:F4}; " +
            "collisions={3}; replayedFrames={4}; conflict={5}",
            predictedBefore.X,
            authorityPosition.X,
            reconciled.X,
            collisionCount,
            coordinator.CurrentFrame.Value - coordinator.ConfirmedFrame.Value,
            adapter.LastConflictLevel);
    }

    [Fact]
    public void Presentation_cue_mapping_preserves_prediction_identity_and_authority_frames()
    {
        var entry = new MobaPresentationCueSnapshotEntry
        {
            Stage = (int)AbilityKit.Protocol.Moba.StateSync.PresentationCueStage.Started,
            RequestKey = "cast-q-87",
            PredictionKey = 87,
            PredictionState = (int)MobaPresentationCuePredictionState.Corrected,
            PredictedFrame = 120,
            ConfirmedFrame = 121,
        };

        var mapped = PresentationCueSnapshotMapper.Map(in entry);

        Assert.Equal(87, mapped.PredictionKey);
        Assert.Equal(ShareCuePredictionState.Corrected, mapped.PredictionState);
        Assert.Equal(120, mapped.PredictedFrame);
        Assert.Equal(121, mapped.ConfirmedFrame);
    }

    [Fact]
    public void Skill_prediction_reserves_resource_and_writes_the_complete_reversible_transaction()
    {
        using var coordinator = CreateSkillCoordinator(initialMana: 100f);

        coordinator.ProcessInput(new MobaSkillPredictionInput(
            predictionKey: 87,
            skillId: 6001,
            targetId: 22,
            predictedPhase: 1,
            cooldownFrames: 150,
            resourceCost: 20f,
            inputLockMask: 3,
            movementLocked: true));

        var slots = coordinator.GetCurrentSlots();
        Assert.Equal(87, slots.GetInt(MobaPredictionSlotNames.SkillPredictionKey));
        Assert.Equal(6001, slots.GetInt(MobaPredictionSlotNames.SkillId));
        Assert.Equal(1, slots.GetInt(MobaPredictionSlotNames.SkillPhase));
        Assert.Equal(1, slots.GetInt(MobaPredictionSlotNames.SkillPhaseStartFrame));
        Assert.Equal(151, slots.GetInt(MobaPredictionSlotNames.SkillCooldownEndFrame));
        Assert.Equal(20f, slots.GetFloat(MobaPredictionSlotNames.SkillReservedResource));
        Assert.Equal(80f, slots.GetFloat(MobaPredictionSlotNames.Mana));
        Assert.Equal(3, slots.GetInt(MobaPredictionSlotNames.SkillInputLockMask));
        Assert.True(slots.GetBool(MobaPredictionSlotNames.SkillMovementLocked));
    }

    [Fact]
    public void Skill_confirmation_calibrates_phase_and_late_rejection_cannot_replace_it()
    {
        using var coordinator = CreateSkillCoordinator(initialMana: 100f);
        coordinator.ProcessInput(new MobaSkillPredictionInput(
            predictionKey: 87,
            skillId: 6001,
            predictedPhase: 1,
            cooldownFrames: 150,
            resourceCost: 20f,
            inputLockMask: 3,
            movementLocked: true));
        coordinator.ProcessInputs(Array.Empty<IInputCommand>());

        coordinator.ApplyServerSnapshot(2, 7, CreateSkillAuthorityState(
            predictionKey: 87,
            skillId: 6001,
            phase: 2,
            phaseStartFrame: 2,
            cooldownEndFrame: 152,
            reservedResource: 0f,
            mana: 80f,
            inputLockMask: 1,
            movementLocked: false));

        var slots = coordinator.GetCurrentSlots();
        Assert.Equal(2, slots.GetInt(MobaPredictionSlotNames.SkillPhase));
        Assert.Equal(2, slots.GetInt(MobaPredictionSlotNames.SkillPhaseStartFrame));
        Assert.Equal(152, slots.GetInt(MobaPredictionSlotNames.SkillCooldownEndFrame));
        Assert.Equal(0f, slots.GetFloat(MobaPredictionSlotNames.SkillReservedResource));
        Assert.Equal(80f, slots.GetFloat(MobaPredictionSlotNames.Mana));
        Assert.Equal(1, slots.GetInt(MobaPredictionSlotNames.SkillInputLockMask));
        Assert.False(slots.GetBool(MobaPredictionSlotNames.SkillMovementLocked));

        coordinator.ApplyServerSnapshot(1, 7, CreateSkillAuthorityState(
            predictionKey: 87,
            skillId: 6001,
            phase: 0,
            phaseStartFrame: 0,
            cooldownEndFrame: 0,
            reservedResource: 0f,
            mana: 100f,
            inputLockMask: 0,
            movementLocked: false));

        Assert.Equal(2, coordinator.ConfirmedFrame.Value);
        Assert.Equal(2, slots.GetInt(MobaPredictionSlotNames.SkillPhase));
        Assert.Equal(80f, slots.GetFloat(MobaPredictionSlotNames.Mana));
    }

    [Fact]
    public void Skill_rejection_restores_resource_and_releases_cooldown_and_input_locks()
    {
        using var coordinator = CreateSkillCoordinator(initialMana: 100f);
        coordinator.ProcessInput(new MobaSkillPredictionInput(
            predictionKey: 88,
            skillId: 6002,
            predictedPhase: 1,
            cooldownFrames: 240,
            resourceCost: 35f,
            inputLockMask: 7,
            movementLocked: true));
        coordinator.ProcessInputs(Array.Empty<IInputCommand>());

        coordinator.ApplyServerSnapshot(2, 7, CreateSkillAuthorityState(
            predictionKey: 88,
            skillId: 6002,
            phase: 0,
            phaseStartFrame: 0,
            cooldownEndFrame: 0,
            reservedResource: 0f,
            mana: 100f,
            inputLockMask: 0,
            movementLocked: false));

        var slots = coordinator.GetCurrentSlots();
        Assert.Equal(0, slots.GetInt(MobaPredictionSlotNames.SkillPhase));
        Assert.Equal(0, slots.GetInt(MobaPredictionSlotNames.SkillCooldownEndFrame));
        Assert.Equal(0f, slots.GetFloat(MobaPredictionSlotNames.SkillReservedResource));
        Assert.Equal(100f, slots.GetFloat(MobaPredictionSlotNames.Mana));
        Assert.Equal(0, slots.GetInt(MobaPredictionSlotNames.SkillInputLockMask));
        Assert.False(slots.GetBool(MobaPredictionSlotNames.SkillMovementLocked));
    }

    [Fact]
    public void Movement_validation_uses_contact_topology_and_hard_transition_semantics()
    {
        var handler = new MobaMovementPredictionHandler();
        var predicted = CreateMovementState(
            x: 1f,
            mode: MobaPredictionMovementMode.Grounded,
            grounded: true,
            contactId: 10,
            interruptEpoch: 3,
            actionPhase: MobaPredictionActionPhase.Moving);
        var differentContact = CreateMovementState(
            x: 1.01f,
            mode: MobaPredictionMovementMode.Grounded,
            grounded: true,
            contactId: 11,
            interruptEpoch: 3,
            actionPhase: MobaPredictionActionPhase.Moving);
        var knockback = CreateMovementState(
            x: 1.01f,
            mode: MobaPredictionMovementMode.Knockback,
            grounded: false,
            contactId: -1,
            interruptEpoch: 4,
            actionPhase: MobaPredictionActionPhase.Knockback,
            inputLockMask: MobaPredictionInputLockMasks.Movement);

        Assert.Equal(ConflictLevel.Major, handler.Validate(predicted, differentContact).Level);
        Assert.Equal(ConflictLevel.Critical, handler.Validate(predicted, knockback).Level);
    }

    [Fact]
    public void Hard_interrupt_discards_pre_interrupt_move_replay_and_preserves_authority_motion()
    {
        using var coordinator = new PredictionCoordinator(7);
        coordinator.Register(new MobaMovementPredictionHandler(frameTime: 1f));
        coordinator.GetCurrentSlots().OverwriteFrom(CreateMovementState(
            x: 0f,
            mode: MobaPredictionMovementMode.Grounded,
            grounded: true,
            contactId: 10,
            interruptEpoch: 0,
            actionPhase: MobaPredictionActionPhase.Moving));

        var lastConflict = ConflictLevel.None;
        coordinator.OnRollbackExecuted += (_, level) => lastConflict = level;
        coordinator.ProcessInput(new MobaMovePredictionInput(1f, 0f, 0f, interruptEpoch: 0));
        coordinator.ProcessInput(new MobaMovePredictionInput(1f, 0f, 0f, interruptEpoch: 0));
        Assert.Equal(2f, coordinator.GetCurrentSlots().GetPosition(MobaPredictionSlotNames.Position).X);

        var authority = CreateMovementState(
            x: 0.25f,
            mode: MobaPredictionMovementMode.Knockback,
            grounded: false,
            contactId: -1,
            interruptEpoch: 1,
            actionPhase: MobaPredictionActionPhase.Knockback,
            inputLockMask: MobaPredictionInputLockMasks.Movement,
            velocityX: -4f);
        coordinator.ApplyServerSnapshot(serverFrame: 1, objectId: 7, serverSlots: authority);

        var reconciled = coordinator.GetCurrentSlots();
        Assert.Equal(ConflictLevel.Critical, lastConflict);
        Assert.Equal(2, coordinator.CurrentFrame.Value);
        Assert.Equal(1, coordinator.ConfirmedFrame.Value);
        Assert.Equal(0.25f, reconciled.GetPosition(MobaPredictionSlotNames.Position).X);
        Assert.Equal(-4f, reconciled.GetPosition(MobaPredictionSlotNames.Velocity).X);
        Assert.Equal((int)MobaPredictionMovementMode.Knockback,
            reconciled.GetInt(MobaPredictionSlotNames.MovementMode));
        Assert.Equal((int)MobaPredictionActionPhase.Knockback,
            reconciled.GetInt(MobaPredictionSlotNames.ActionPhase));
        Assert.Equal(1, reconciled.GetInt(MobaPredictionSlotNames.ActionInterruptEpoch));

        coordinator.ProcessInput(new MobaMovePredictionInput(3f, 0f, 0f, interruptEpoch: 1));
        Assert.Equal(0.25f, reconciled.GetPosition(MobaPredictionSlotNames.Position).X);
        Assert.Equal(-4f, reconciled.GetPosition(MobaPredictionSlotNames.Velocity).X);
    }

    [Fact]
    public void Action_ack_round_trip_filters_duplicates_and_entity_generation_mismatches()
    {
        var source = new MobaActionAckEntry
        {
            ActorId = 7,
            EntityVersion = 2,
            PredictionKey = 87,
            SkillId = 6001,
            Accepted = true,
            AuthoritativeFrame = 120,
            Reason = (int)MobaActionAckReason.None,
            InterruptEpoch = 3,
            AuthoritySequence = 9004,
        };
        var decoded = Assert.Single(MobaActionAckCodec.Deserialize(
            MobaActionAckCodec.Serialize(new[] { source })));
        var tracker = new MobaActionAckTracker(actorId: 7, entityVersion: 2);

        var accepted = tracker.Apply(in decoded);
        var duplicate = tracker.Apply(in decoded);
        decoded.EntityVersion = 3;
        decoded.AuthoritySequence = 9005;
        var wrongGeneration = tracker.Apply(in decoded);

        Assert.Equal(MobaActionAckDisposition.Accepted, accepted.Disposition);
        Assert.Equal(120, accepted.AuthoritativeFrame);
        Assert.Equal(3, tracker.LatestInterruptEpoch);
        Assert.Equal(MobaActionAckDisposition.Stale, duplicate.Disposition);
        Assert.Equal(MobaActionAckDisposition.IdentityMismatch, wrongGeneration.Disposition);
        Assert.Equal(1, tracker.AcceptedCount);
        Assert.Equal(0, tracker.RejectedCount);
        Assert.Equal(1, tracker.StaleCount);
    }

    [Fact]
    public void Action_ack_authority_journal_is_idempotent_and_rejects_stale_input_sequences()
    {
        var service = new MobaActionAckSnapshotService();

        Assert.True(service.TryAcceptInputSequence(7, 1, 10));
        Assert.False(service.TryAcceptInputSequence(7, 1, 9));
        var accepted = service.Report(
            actorId: 7,
            entityVersion: 1,
            predictionKey: 87,
            skillId: 6001,
            accepted: true,
            authoritativeFrame: 120,
            reason: MobaActionAckReason.None);
        var duplicate = service.Report(
            actorId: 7,
            entityVersion: 1,
            predictionKey: 87,
            skillId: 6001,
            accepted: false,
            authoritativeFrame: 121,
            reason: MobaActionAckReason.ServerPolicy);

        Assert.Equal(accepted.AuthoritySequence, duplicate.AuthoritySequence);
        Assert.True(duplicate.Accepted);
        Assert.Equal(1, service.AdvanceInterruptEpoch(7, 1));
        var interrupted = service.Report(
            actorId: 7,
            entityVersion: 1,
            predictionKey: 88,
            skillId: 6002,
            accepted: false,
            authoritativeFrame: 122,
            reason: MobaActionAckReason.Interrupted);
        Assert.True(interrupted.AuthoritySequence > accepted.AuthoritySequence);
        Assert.Equal(1, interrupted.InterruptEpoch);

        Assert.True(service.TryGetSnapshot(
            new AbilityKit.Ability.FrameSync.FrameIndex(122),
            out var snapshot));
        Assert.Equal(AbilityKit.Protocol.Moba.MobaOpCodes.Snapshot.ActionAck, snapshot.OpCode);
        var emitted = MobaActionAckCodec.Deserialize(snapshot.Payload);
        Assert.Equal(4, emitted.Length);
        Assert.Equal(accepted.AuthoritySequence, emitted[0].AuthoritySequence);
        Assert.Equal(accepted.AuthoritySequence, emitted[1].AuthoritySequence);
        Assert.Equal(0, emitted[2].PredictionKey);
        Assert.Equal((int)MobaActionAckReason.Interrupted, emitted[2].Reason);
        Assert.Equal(1, emitted[2].InterruptEpoch);
        Assert.True(emitted[2].AuthoritySequence > accepted.AuthoritySequence);
        Assert.Equal(interrupted.AuthoritySequence, emitted[3].AuthoritySequence);
        Assert.True(emitted[3].AuthoritySequence > emitted[2].AuthoritySequence);
    }

    [Fact]
    public void Action_ack_replay_executes_each_accepted_decision_once_without_republishing_ack()
    {
        var service = new MobaActionAckSnapshotService();
        service.Report(
            actorId: 7,
            entityVersion: 2,
            predictionKey: 87,
            skillId: 6001,
            accepted: true,
            authoritativeFrame: 120,
            reason: MobaActionAckReason.None);
        service.Report(
            actorId: 7,
            entityVersion: 2,
            predictionKey: 88,
            skillId: 6002,
            accepted: false,
            authoritativeFrame: 121,
            reason: MobaActionAckReason.ServerPolicy);
        service.Report(
            actorId: 7,
            entityVersion: 2,
            predictionKey: 86,
            skillId: 6000,
            accepted: true,
            authoritativeFrame: 119,
            reason: MobaActionAckReason.None);

        Assert.True(service.TryGetSnapshot(
            new AbilityKit.Ability.FrameSync.FrameIndex(121),
            out _));

        service.BeginReplay(
            new AbilityKit.Ability.FrameSync.FrameIndex(119),
            new AbilityKit.Ability.FrameSync.FrameIndex(121));
        try
        {
            Assert.Equal(
                MobaActionReplayDisposition.SkipDuplicate,
                service.ResolveReplay(7, 2, 86, out var includedInBaseline));
            Assert.True(includedInBaseline.Accepted);
            Assert.Equal(
                MobaActionReplayDisposition.ExecuteAccepted,
                service.ResolveReplay(7, 2, 87, out var accepted));
            Assert.True(accepted.Accepted);
            service.CompleteReplay(7, 2, 87);
            Assert.Equal(
                MobaActionReplayDisposition.SkipDuplicate,
                service.ResolveReplay(7, 2, 87, out var duplicate));
            Assert.Equal(accepted.AuthoritySequence, duplicate.AuthoritySequence);
            Assert.Throws<InvalidOperationException>(() => service.CompleteReplay(7, 2, 87));
            Assert.Equal(
                MobaActionReplayDisposition.SkipRejected,
                service.ResolveReplay(7, 2, 88, out var rejected));
            Assert.False(rejected.Accepted);
            Assert.Equal(
                MobaActionReplayDisposition.DecisionMissing,
                service.ResolveReplay(7, 2, 99, out _));

            Assert.False(service.TryGetSnapshot(
                new AbilityKit.Ability.FrameSync.FrameIndex(122),
                out _));
        }
        finally
        {
            service.EndReplay();
        }
    }

    [Fact]
    public void Action_ack_replay_fails_when_an_accepted_decision_is_not_executed()
    {
        var service = new MobaActionAckSnapshotService();
        service.Report(
            actorId: 7,
            entityVersion: 2,
            predictionKey: 87,
            skillId: 6001,
            accepted: true,
            authoritativeFrame: 120,
            reason: MobaActionAckReason.None);

        service.BeginReplay(
            new AbilityKit.Ability.FrameSync.FrameIndex(119),
            new AbilityKit.Ability.FrameSync.FrameIndex(121));

        var error = Assert.Throws<InvalidOperationException>(() => service.EndReplay());
        Assert.Contains("predictionKey=87", error.Message, StringComparison.Ordinal);

        service.BeginReplay(
            new AbilityKit.Ability.FrameSync.FrameIndex(120),
            new AbilityKit.Ability.FrameSync.FrameIndex(121));
        service.EndReplay();
    }

    [Fact]
    public void Actor_entity_version_changes_only_when_actor_id_is_bound_to_a_new_entity()
    {
        var registry = new MobaActorRegistry();
        var first = new ActorContext().CreateEntity();
        var replacement = new ActorContext().CreateEntity();

        registry.Register(7, first);
        Assert.Equal(1, registry.GetEntityVersion(7));
        registry.Unregister(7);
        registry.Register(7, first);
        Assert.Equal(1, registry.GetEntityVersion(7));

        registry.Unregister(7);
        registry.Register(7, replacement);
        Assert.Equal(2, registry.GetEntityVersion(7));
        Assert.True(registry.MatchesEntityVersion(7, 2));
        Assert.False(registry.MatchesEntityVersion(7, 1));
    }

    private static PredictionCoordinator CreateSkillCoordinator(float initialMana)
    {
        var coordinator = new PredictionCoordinator(7);
        coordinator.Register(new MobaSkillPredictionHandler());
        coordinator.GetCurrentSlots().Set(MobaPredictionSlotNames.Mana, initialMana);
        return coordinator;
    }

    private static StateSlots CreateSkillAuthorityState(
        int predictionKey,
        int skillId,
        int phase,
        int phaseStartFrame,
        int cooldownEndFrame,
        float reservedResource,
        float mana,
        int inputLockMask,
        bool movementLocked)
    {
        var slots = new StateSlots();
        slots.Set(MobaPredictionSlotNames.SkillPredictionKey, predictionKey);
        slots.Set(MobaPredictionSlotNames.SkillId, skillId);
        slots.Set(MobaPredictionSlotNames.SkillPhase, phase);
        slots.Set(MobaPredictionSlotNames.SkillPhaseStartFrame, phaseStartFrame);
        slots.Set(MobaPredictionSlotNames.SkillCooldownEndFrame, cooldownEndFrame);
        slots.Set(MobaPredictionSlotNames.SkillReservedResource, reservedResource);
        slots.Set(MobaPredictionSlotNames.Mana, mana);
        slots.Set(MobaPredictionSlotNames.SkillInputLockMask, inputLockMask);
        slots.Set(MobaPredictionSlotNames.SkillMovementLocked, movementLocked);
        return slots;
    }

    private static StateSlots CreateMovementState(
        float x,
        MobaPredictionMovementMode mode,
        bool grounded,
        int contactId,
        int interruptEpoch,
        MobaPredictionActionPhase actionPhase,
        int inputLockMask = 0,
        float velocityX = 0f)
    {
        var slots = new StateSlots();
        slots.Set(MobaPredictionSlotNames.Position, new StateSyncVector3(x, 0f, 0f));
        slots.Set(MobaPredictionSlotNames.Velocity, new StateSyncVector3(velocityX, 0f, 0f));
        slots.Set(MobaPredictionSlotNames.Rotation, AbilityKit.Ability.StateSync.Quaternion.Identity);
        slots.Set(MobaPredictionSlotNames.AnimationState, 0);
        slots.Set(MobaPredictionSlotNames.MovementMode, (int)mode);
        slots.Set(MobaPredictionSlotNames.MovementGrounded, grounded);
        slots.Set(MobaPredictionSlotNames.MovementContactId, contactId);
        slots.Set(MobaPredictionSlotNames.ActionId, (int)actionPhase);
        slots.Set(MobaPredictionSlotNames.ActionInstanceId, interruptEpoch);
        slots.Set(MobaPredictionSlotNames.ActionPhase, (int)actionPhase);
        slots.Set(MobaPredictionSlotNames.ActionPhaseStartFrame, 0);
        slots.Set(MobaPredictionSlotNames.ActionCursorFrame, 0);
        slots.Set(MobaPredictionSlotNames.ActionInterruptEpoch, interruptEpoch);
        slots.Set(MobaPredictionSlotNames.ActionInputLockMask, inputLockMask);
        return slots;
    }

    private static ColliderId AddWall(ICollisionWorld world, in Vec3 center, in Vec3 halfExtents)
    {
        var transform = new Transform3(center, Quat.Identity, Vec3.One);
        var shape = ColliderShape.CreateAabb(-halfExtents, halfExtents);
        return world.Add(in transform, in shape, MobaCollisionLayers.WorldId);
    }
}
