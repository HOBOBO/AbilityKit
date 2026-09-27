using System;
using System.Collections.Generic;
using AbilityKit.Ability.StateSync;
using AbilityKit.Ability.StateSync.Prediction;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Share.Prediction
{
    /// <summary>State slots shared by MOBA StateSync prediction consumers.</summary>
    public static class MobaPredictionSlotNames
    {
        public const string Position = "position";
        public const string Velocity = "velocity";
        public const string Rotation = "rotation";
        public const string Health = "health";
        public const string MaxHealth = "maxHealth";
        public const string Mana = "mana";
        public const string MaxMana = "maxMana";
        public const string CooldownPrefix = "cooldown.";
        public const string AnimationState = "animationState";
        public const string MovementMode = "movement.mode";
        public const string MovementGrounded = "movement.grounded";
        public const string MovementContactId = "movement.contactId";
        public const string ActionId = "action.id";
        public const string ActionInstanceId = "action.instanceId";
        public const string ActionPhase = "action.phase";
        public const string ActionPhaseStartFrame = "action.phaseStartFrame";
        public const string ActionCursorFrame = "action.cursorFrame";
        public const string ActionInterruptEpoch = "action.interruptEpoch";
        public const string ActionInputLockMask = "action.inputLockMask";
        public const string SkillPredictionKey = "skill.predictionKey";
        public const string SkillId = "skill.id";
        public const string SkillPhase = "skill.phase";
        public const string SkillPhaseStartFrame = "skill.phaseStartFrame";
        public const string SkillCooldownEndFrame = "skill.cooldownEndFrame";
        public const string SkillReservedResource = "skill.reservedResource";
        public const string SkillInputLockMask = "skill.inputLockMask";
        public const string SkillMovementLocked = "skill.movementLocked";
    }

    public enum MobaPredictionMovementMode
    {
        Grounded = 0,
        Airborne = 1,
        Dash = 2,
        Knockback = 3,
        Teleport = 4,
    }

    public enum MobaPredictionActionPhase
    {
        None = 0,
        Moving = 1,
        Casting = 2,
        Recovery = 3,
        Knockback = 4,
        Stunned = 5,
        Dead = 6,
    }

    public static class MobaPredictionInputLockMasks
    {
        public const int Movement = 1 << 0;
        public const int Skill = 1 << 1;
    }

    public enum MobaActionAckDisposition
    {
        Accepted,
        Rejected,
        Stale,
        IdentityMismatch,
        Invalid,
        InterruptEpochAdvanced,
    }

    public readonly struct MobaActionAckResult
    {
        public MobaActionAckResult(
            MobaActionAckDisposition disposition,
            int predictionKey,
            int authoritativeFrame,
            MobaActionAckReason reason,
            int interruptEpoch)
        {
            Disposition = disposition;
            PredictionKey = predictionKey;
            AuthoritativeFrame = authoritativeFrame;
            Reason = reason;
            InterruptEpoch = interruptEpoch;
        }

        public MobaActionAckDisposition Disposition { get; }
        public int PredictionKey { get; }
        public int AuthoritativeFrame { get; }
        public MobaActionAckReason Reason { get; }
        public int InterruptEpoch { get; }
    }

    /// <summary>
    /// Orders reliable action acknowledgements independently from state snapshots and authority
    /// events. It never mutates StateSlots: rejected resources, cooldowns and phases still come
    /// from the authoritative snapshot for the same entity version.
    /// </summary>
    public sealed class MobaActionAckTracker
    {
        private readonly Dictionary<int, long> _latestSequenceByPredictionKey =
            new Dictionary<int, long>();
        private int _actorId;
        private int _entityVersion;

        public MobaActionAckTracker(int actorId, int entityVersion)
        {
            Reset(actorId, entityVersion);
        }

        public int ActorId => _actorId;
        public int EntityVersion => _entityVersion;
        public int LatestInterruptEpoch { get; private set; }
        public long AcceptedCount { get; private set; }
        public long RejectedCount { get; private set; }
        public long StaleCount { get; private set; }

        public MobaActionAckResult Apply(in MobaActionAckEntry ack)
        {
            if (ack.ActorId != _actorId || ack.EntityVersion != _entityVersion)
                return CreateResult(MobaActionAckDisposition.IdentityMismatch, in ack);

            if (ack.PredictionKey < 0
                || ack.AuthoritativeFrame < 0
                || ack.InterruptEpoch < 0
                || ack.AuthoritySequence < 0)
            {
                return CreateResult(MobaActionAckDisposition.Invalid, in ack);
            }

            if (_latestSequenceByPredictionKey.TryGetValue(ack.PredictionKey, out var latest)
                && ack.AuthoritySequence <= latest)
            {
                StaleCount++;
                return CreateResult(MobaActionAckDisposition.Stale, in ack);
            }

            _latestSequenceByPredictionKey[ack.PredictionKey] = ack.AuthoritySequence;
            if (ack.PredictionKey == 0)
            {
                LatestInterruptEpoch = ack.InterruptEpoch;
                return CreateResult(MobaActionAckDisposition.InterruptEpochAdvanced, in ack);
            }

            LatestInterruptEpoch = Math.Max(LatestInterruptEpoch, ack.InterruptEpoch);
            if (ack.Accepted)
            {
                AcceptedCount++;
                return CreateResult(MobaActionAckDisposition.Accepted, in ack);
            }

            RejectedCount++;
            return CreateResult(MobaActionAckDisposition.Rejected, in ack);
        }

        public void Reset(int actorId, int entityVersion)
        {
            if (actorId < 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (entityVersion < 0) throw new ArgumentOutOfRangeException(nameof(entityVersion));

            _actorId = actorId;
            _entityVersion = entityVersion;
            LatestInterruptEpoch = 0;
            AcceptedCount = 0;
            RejectedCount = 0;
            StaleCount = 0;
            _latestSequenceByPredictionKey.Clear();
        }

        private static MobaActionAckResult CreateResult(
            MobaActionAckDisposition disposition,
            in MobaActionAckEntry ack)
        {
            return new MobaActionAckResult(
                disposition,
                ack.PredictionKey,
                ack.AuthoritativeFrame,
                (MobaActionAckReason)ack.Reason,
                ack.InterruptEpoch);
        }
    }

    /// <summary>Immutable movement command retained by the StateSync input history.</summary>
    public sealed class MobaMovePredictionInput : IInputCommand
    {
        public MobaMovePredictionInput(
            float velocityX,
            float velocityZ,
            float rotation,
            int interruptEpoch = 0)
        {
            if (interruptEpoch < 0) throw new ArgumentOutOfRangeException(nameof(interruptEpoch));

            VelocityX = velocityX;
            VelocityZ = velocityZ;
            Rotation = rotation;
            InterruptEpoch = interruptEpoch;
        }

        public float VelocityX { get; }
        public float VelocityZ { get; }
        public float Rotation { get; }
        public int InterruptEpoch { get; }

        public static MobaMovePredictionInput FromPayload(byte[] payload, int interruptEpoch = 0)
        {
            if (!MobaMoveCodec.TryDeserialize(payload, out var velocityX, out var velocityZ, out _))
            {
                return new MobaMovePredictionInput(0f, 0f, 0f, interruptEpoch);
            }

            var rotation = velocityX == 0f && velocityZ == 0f
                ? 0f
                : (float)Math.Atan2(velocityX, velocityZ);
            return new MobaMovePredictionInput(velocityX, velocityZ, rotation, interruptEpoch);
        }
    }

    /// <summary>
    /// Shared local movement prediction policy. The coordinator owns snapshots,
    /// command history and replay; this handler owns only the MOBA slot mutation.
    /// </summary>
    public sealed class MobaMovementPredictionHandler : IPredictionHandler
    {
        private static readonly IReadOnlyList<string> Slots = new[]
        {
            MobaPredictionSlotNames.Position,
            MobaPredictionSlotNames.Velocity,
            MobaPredictionSlotNames.Rotation,
            MobaPredictionSlotNames.MovementMode,
            MobaPredictionSlotNames.MovementGrounded,
            MobaPredictionSlotNames.MovementContactId,
            MobaPredictionSlotNames.ActionId,
            MobaPredictionSlotNames.ActionInstanceId,
            MobaPredictionSlotNames.ActionPhase,
            MobaPredictionSlotNames.ActionPhaseStartFrame,
            MobaPredictionSlotNames.ActionCursorFrame,
            MobaPredictionSlotNames.ActionInterruptEpoch,
            MobaPredictionSlotNames.ActionInputLockMask,
        };

        private readonly float _frameTime;

        public MobaMovementPredictionHandler(float frameTime = 1f / 30f)
        {
            if (frameTime <= 0f) throw new ArgumentOutOfRangeException(nameof(frameTime));
            _frameTime = frameTime;
        }

        public string Name => "MobaMovement";
        public PredictionStrategy Strategy => PredictionStrategy.OptimisticWithRollback;
        public IReadOnlyList<string> RequiredSlots => Slots;

        public void Predict(IInputCommand input, StateSlots slots, Frame frame)
        {
            var move = input as MobaMovePredictionInput;
            if (move == null) return;
            if (move.InterruptEpoch != slots.GetInt(MobaPredictionSlotNames.ActionInterruptEpoch)) return;
            if ((slots.GetInt(MobaPredictionSlotNames.ActionInputLockMask)
                 & MobaPredictionInputLockMasks.Movement) != 0) return;

            var position = slots.GetPosition(MobaPredictionSlotNames.Position);
            slots.Set(
                MobaPredictionSlotNames.Position,
                new Vector3(
                    position.X + move.VelocityX * _frameTime,
                    position.Y,
                    position.Z + move.VelocityZ * _frameTime));
            slots.Set(
                MobaPredictionSlotNames.Velocity,
                new Vector3(move.VelocityX, 0f, move.VelocityZ));
            slots.Set(
                MobaPredictionSlotNames.Rotation,
                new Quaternion(0f, move.Rotation, 0f, 1f));
            slots.Set(
                MobaPredictionSlotNames.AnimationState,
                move.VelocityX != 0f || move.VelocityZ != 0f ? 1 : 0);
        }

        public PredictionResult Validate(StateSlots predicted, StateSlots server)
        {
            if (DiffersInt(predicted, server, MobaPredictionSlotNames.ActionInterruptEpoch))
                return PredictionResult.Critical("movement interrupt epoch mismatch");

            if (DiffersInt(predicted, server, MobaPredictionSlotNames.ActionId)
                || DiffersInt(predicted, server, MobaPredictionSlotNames.ActionInstanceId)
                || DiffersInt(predicted, server, MobaPredictionSlotNames.ActionPhase)
                || DiffersInt(predicted, server, MobaPredictionSlotNames.ActionInputLockMask))
            {
                return PredictionResult.Critical("movement action state mismatch");
            }

            if (DiffersInt(predicted, server, MobaPredictionSlotNames.MovementMode))
            {
                var predictedMode = (MobaPredictionMovementMode)predicted.GetInt(
                    MobaPredictionSlotNames.MovementMode);
                var serverMode = (MobaPredictionMovementMode)server.GetInt(
                    MobaPredictionSlotNames.MovementMode);
                return IsHardTransition(predictedMode, serverMode)
                    ? PredictionResult.Critical($"movement mode mismatch: {predictedMode}->{serverMode}")
                    : PredictionResult.Major($"movement mode mismatch: {predictedMode}->{serverMode}");
            }

            if (DiffersBool(predicted, server, MobaPredictionSlotNames.MovementGrounded)
                || DiffersInt(predicted, server, MobaPredictionSlotNames.MovementContactId))
            {
                return PredictionResult.Major("movement contact topology mismatch");
            }

            if (!server.Has(MobaPredictionSlotNames.Position)) return PredictionResult.Ok();

            var predictedPosition = predicted.GetPosition(MobaPredictionSlotNames.Position);
            var serverPosition = server.GetPosition(MobaPredictionSlotNames.Position);
            var dx = predictedPosition.X - serverPosition.X;
            var dz = predictedPosition.Z - serverPosition.Z;
            var distance = (float)Math.Sqrt(dx * dx + dz * dz);

            if (distance < 0.1f) return PredictionResult.Ok();
            if (distance < 1f) return PredictionResult.Minor($"dist={distance:F2}");
            if (distance < 5f) return PredictionResult.Major($"dist={distance:F2}");
            return PredictionResult.Critical($"dist={distance:F2}");
        }

        public void ApplyServerState(StateSlots server, StateSlots current)
        {
            CopyIfPresent(server, current, MobaPredictionSlotNames.Position, SlotKind.Position);
            CopyIfPresent(server, current, MobaPredictionSlotNames.Velocity, SlotKind.Position);
            CopyIfPresent(server, current, MobaPredictionSlotNames.Rotation, SlotKind.Rotation);
            CopyIfPresent(server, current, MobaPredictionSlotNames.AnimationState, SlotKind.Integer);
            CopyIfPresent(server, current, MobaPredictionSlotNames.MovementMode, SlotKind.Integer);
            CopyIfPresent(server, current, MobaPredictionSlotNames.MovementGrounded, SlotKind.Boolean);
            CopyIfPresent(server, current, MobaPredictionSlotNames.MovementContactId, SlotKind.Integer);
            CopyIfPresent(server, current, MobaPredictionSlotNames.ActionId, SlotKind.Integer);
            CopyIfPresent(server, current, MobaPredictionSlotNames.ActionInstanceId, SlotKind.Integer);
            CopyIfPresent(server, current, MobaPredictionSlotNames.ActionPhase, SlotKind.Integer);
            CopyIfPresent(server, current, MobaPredictionSlotNames.ActionPhaseStartFrame, SlotKind.Integer);
            CopyIfPresent(server, current, MobaPredictionSlotNames.ActionCursorFrame, SlotKind.Integer);
            CopyIfPresent(server, current, MobaPredictionSlotNames.ActionInterruptEpoch, SlotKind.Integer);
            CopyIfPresent(server, current, MobaPredictionSlotNames.ActionInputLockMask, SlotKind.Integer);
        }

        private static bool DiffersInt(StateSlots predicted, StateSlots server, string name)
        {
            return server.Has(name) && (!predicted.Has(name) || predicted.GetInt(name) != server.GetInt(name));
        }

        private static bool DiffersBool(StateSlots predicted, StateSlots server, string name)
        {
            return server.Has(name) && (!predicted.Has(name) || predicted.GetBool(name) != server.GetBool(name));
        }

        private static bool IsHardTransition(
            MobaPredictionMovementMode predicted,
            MobaPredictionMovementMode server)
        {
            return predicted == MobaPredictionMovementMode.Knockback
                || predicted == MobaPredictionMovementMode.Teleport
                || server == MobaPredictionMovementMode.Knockback
                || server == MobaPredictionMovementMode.Teleport;
        }

        private static void CopyIfPresent(StateSlots source, StateSlots destination, string name, SlotKind kind)
        {
            if (!source.Has(name)) return;

            switch (kind)
            {
                case SlotKind.Position:
                    destination.Set(name, source.GetPosition(name));
                    break;
                case SlotKind.Rotation:
                    destination.Set(name, source.GetQuaternion(name));
                    break;
                case SlotKind.Integer:
                    destination.Set(name, source.GetInt(name));
                    break;
                case SlotKind.Boolean:
                    destination.Set(name, source.GetBool(name));
                    break;
            }
        }

        private enum SlotKind
        {
            Position,
            Rotation,
            Integer,
            Boolean,
        }
    }

    /// <summary>
    /// Immutable local skill command. Runtime input code maps the production skill payload to
    /// this project-level prediction contract after its normal validation and decoding step.
    /// </summary>
    public class MobaSkillPredictionInput : IInputCommand
    {
        public MobaSkillPredictionInput(
            int predictionKey,
            int skillId,
            int targetId = 0,
            int predictedPhase = 1,
            int cooldownFrames = 0,
            float resourceCost = 0f,
            int inputLockMask = 0,
            bool movementLocked = false)
        {
            if (predictionKey < 0) throw new ArgumentOutOfRangeException(nameof(predictionKey));
            if (skillId < 0) throw new ArgumentOutOfRangeException(nameof(skillId));
            if (predictedPhase < 0) throw new ArgumentOutOfRangeException(nameof(predictedPhase));
            if (cooldownFrames < 0) throw new ArgumentOutOfRangeException(nameof(cooldownFrames));
            if (resourceCost < 0f) throw new ArgumentOutOfRangeException(nameof(resourceCost));

            PredictionKey = predictionKey;
            SkillId = skillId;
            TargetId = targetId;
            PredictedPhase = predictedPhase;
            CooldownFrames = cooldownFrames;
            ResourceCost = resourceCost;
            InputLockMask = inputLockMask;
            MovementLocked = movementLocked;
        }

        public int PredictionKey { get; }
        public int SkillId { get; }
        public int TargetId { get; }
        public int PredictedPhase { get; }
        public int CooldownFrames { get; }
        public float ResourceCost { get; }
        public int InputLockMask { get; }
        public bool MovementLocked { get; }
    }

    /// <summary>
    /// Predicts the reversible logic transaction behind a skill windup. Animation and VFX
    /// remain presentation projections keyed by SkillPredictionKey and never enter StateSlots.
    /// </summary>
    public class MobaSkillPredictionHandler : IPredictionHandler
    {
        private static readonly IReadOnlyList<string> Slots = new[]
        {
            MobaPredictionSlotNames.SkillPredictionKey,
            MobaPredictionSlotNames.SkillId,
            MobaPredictionSlotNames.SkillPhase,
            MobaPredictionSlotNames.SkillPhaseStartFrame,
            MobaPredictionSlotNames.SkillCooldownEndFrame,
            MobaPredictionSlotNames.SkillReservedResource,
            MobaPredictionSlotNames.SkillInputLockMask,
            MobaPredictionSlotNames.SkillMovementLocked,
            MobaPredictionSlotNames.Mana,
        };

        private readonly Func<int, int> _getCooldownFrames;

        public MobaSkillPredictionHandler(Func<int, int> getCooldownFrames = null)
        {
            _getCooldownFrames = getCooldownFrames ?? (_ => 0);
        }

        public string Name => "MobaSkillTransaction";
        public PredictionStrategy Strategy => PredictionStrategy.OptimisticWithRollback;
        public IReadOnlyList<string> RequiredSlots => Slots;

        public void Predict(IInputCommand input, StateSlots slots, Frame frame)
        {
            var skill = input as MobaSkillPredictionInput;
            if (skill == null) return;

            var cooldownFrames = skill.CooldownFrames > 0
                ? skill.CooldownFrames
                : Math.Max(0, _getCooldownFrames(skill.SkillId));
            var availableResource = slots.GetFloat(MobaPredictionSlotNames.Mana);

            slots.Set(MobaPredictionSlotNames.SkillPredictionKey, skill.PredictionKey);
            slots.Set(MobaPredictionSlotNames.SkillId, skill.SkillId);
            slots.Set(MobaPredictionSlotNames.SkillPhase, skill.PredictedPhase);
            slots.Set(MobaPredictionSlotNames.SkillPhaseStartFrame, frame.Value);
            slots.Set(MobaPredictionSlotNames.SkillCooldownEndFrame, frame.Value + cooldownFrames);
            slots.Set(MobaPredictionSlotNames.SkillReservedResource, skill.ResourceCost);
            slots.Set(MobaPredictionSlotNames.SkillInputLockMask, skill.InputLockMask);
            slots.Set(MobaPredictionSlotNames.SkillMovementLocked, skill.MovementLocked);
            slots.Set(MobaPredictionSlotNames.Mana, Math.Max(0f, availableResource - skill.ResourceCost));
        }

        public PredictionResult Validate(StateSlots predicted, StateSlots server)
        {
            if (!HasAnySkillState(server)) return PredictionResult.Ok();

            if (DiffersInt(predicted, server, MobaPredictionSlotNames.SkillPredictionKey)
                || DiffersInt(predicted, server, MobaPredictionSlotNames.SkillId))
            {
                return PredictionResult.Critical("skill prediction identity mismatch");
            }

            if (DiffersInt(predicted, server, MobaPredictionSlotNames.SkillPhase)
                || DiffersInt(predicted, server, MobaPredictionSlotNames.SkillPhaseStartFrame)
                || DiffersInt(predicted, server, MobaPredictionSlotNames.SkillCooldownEndFrame)
                || DiffersFloat(predicted, server, MobaPredictionSlotNames.SkillReservedResource)
                || DiffersInt(predicted, server, MobaPredictionSlotNames.SkillInputLockMask)
                || DiffersBool(predicted, server, MobaPredictionSlotNames.SkillMovementLocked)
                || DiffersFloat(predicted, server, MobaPredictionSlotNames.Mana))
            {
                return PredictionResult.Major("skill transaction state mismatch");
            }

            return PredictionResult.Ok();
        }

        public void ApplyServerState(StateSlots server, StateSlots current)
        {
            CopyIntIfPresent(server, current, MobaPredictionSlotNames.SkillPredictionKey);
            CopyIntIfPresent(server, current, MobaPredictionSlotNames.SkillId);
            CopyIntIfPresent(server, current, MobaPredictionSlotNames.SkillPhase);
            CopyIntIfPresent(server, current, MobaPredictionSlotNames.SkillPhaseStartFrame);
            CopyIntIfPresent(server, current, MobaPredictionSlotNames.SkillCooldownEndFrame);
            CopyFloatIfPresent(server, current, MobaPredictionSlotNames.SkillReservedResource);
            CopyIntIfPresent(server, current, MobaPredictionSlotNames.SkillInputLockMask);
            CopyBoolIfPresent(server, current, MobaPredictionSlotNames.SkillMovementLocked);
            CopyFloatIfPresent(server, current, MobaPredictionSlotNames.Mana);
        }

        private static bool HasAnySkillState(StateSlots slots)
        {
            return slots.Has(MobaPredictionSlotNames.SkillPredictionKey)
                || slots.Has(MobaPredictionSlotNames.SkillId)
                || slots.Has(MobaPredictionSlotNames.SkillPhase);
        }

        private static bool DiffersInt(StateSlots predicted, StateSlots server, string name)
        {
            return server.Has(name) && (!predicted.Has(name) || predicted.GetInt(name) != server.GetInt(name));
        }

        private static bool DiffersFloat(StateSlots predicted, StateSlots server, string name)
        {
            return server.Has(name)
                && (!predicted.Has(name) || Math.Abs(predicted.GetFloat(name) - server.GetFloat(name)) > 0.001f);
        }

        private static bool DiffersBool(StateSlots predicted, StateSlots server, string name)
        {
            return server.Has(name) && (!predicted.Has(name) || predicted.GetBool(name) != server.GetBool(name));
        }

        private static void CopyIntIfPresent(StateSlots source, StateSlots destination, string name)
        {
            if (source.Has(name)) destination.Set(name, source.GetInt(name));
        }

        private static void CopyFloatIfPresent(StateSlots source, StateSlots destination, string name)
        {
            if (source.Has(name)) destination.Set(name, source.GetFloat(name));
        }

        private static void CopyBoolIfPresent(StateSlots source, StateSlots destination, string name)
        {
            if (source.Has(name)) destination.Set(name, source.GetBool(name));
        }
    }
}
