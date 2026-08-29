using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Protocol.Moba;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal interface IBattleLocalActorResolutionPort
    {
        int CachedActorId { get; set; }
        bool TryResolveMappedActorId(out int actorId);
        bool TryResolveActorWorldPosition(int actorId, out Vector3 position);
    }

    internal sealed class BattleLocalActorResolver
    {
        private readonly IBattleLocalActorResolutionPort _port;

        internal BattleLocalActorResolver(IBattleLocalActorResolutionPort port)
        {
            _port = port ?? throw new ArgumentNullException(nameof(port));
        }

        internal bool TryResolveActorId(out int actorId)
        {
            if (_port.TryResolveMappedActorId(out actorId) && actorId > 0)
            {
                // The player-to-actor map is authoritative. A snapshot/HUD callback may have
                // populated the cache before actor ownership was available, so always repair it.
                _port.CachedActorId = actorId;
                return true;
            }

            actorId = _port.CachedActorId;
            return actorId > 0;
        }

        internal bool TryResolveWorldPosition(out Vector3 position)
        {
            if (_port.TryResolveMappedActorId(out var mappedActorId) && mappedActorId > 0)
            {
                _port.CachedActorId = mappedActorId;
                return _port.TryResolveActorWorldPosition(mappedActorId, out position);
            }

            if (_port.TryResolveActorWorldPosition(_port.CachedActorId, out position)) return true;
            position = default;
            return false;
        }
    }

    internal readonly struct BattleAimProjection
    {
        internal BattleAimProjection(Vector3 position, Vector3 direction)
        {
            Position = position;
            Direction = direction;
        }

        internal Vector3 Position { get; }
        internal Vector3 Direction { get; }
    }

    internal sealed class BattleAimProjectionService
    {
        private readonly BattleLocalActorResolver _actorResolver;

        internal BattleAimProjectionService(BattleLocalActorResolver actorResolver)
        {
            _actorResolver = actorResolver ?? throw new ArgumentNullException(nameof(actorResolver));
        }

        internal BattleAimProjection Project(float aimDx, float aimDz)
        {
            var offset = new Vector3(aimDx, 0f, aimDz);
            var direction = offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector3.zero;
            var position = _actorResolver.TryResolveWorldPosition(out var casterPosition)
                ? casterPosition + offset
                : offset;
            return new BattleAimProjection(position, direction);
        }
    }

    internal sealed class BattleInputRuntime : IDisposable, IBattleInputSubmissionPort
    {
        private readonly BattleHudInputState _hudInput = new BattleHudInputState();
        private BattleContext _context;
        private BattleLocalActorResolver _actorResolver;
        private BattleAimProjectionService _aimProjection;

        internal BattleContext Context => _context;
        internal BattleLocalInputQueue LocalInputQueue { get; } = new BattleLocalInputQueue();

        internal void Bind(BattleContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (ReferenceEquals(_context, context)) return;

            Unbind(_context);
            _context = context;
            _actorResolver = new BattleLocalActorResolver(context);
            _aimProjection = new BattleAimProjectionService(_actorResolver);
        }

        internal void Unbind(BattleContext context)
        {
            if (context != null && !ReferenceEquals(_context, context)) return;

            _context = null;
            _actorResolver = null;
            _aimProjection = null;
            ResetTransientState();
        }

        internal bool TryReadMove(out float dx, out float dz) =>
            _hudInput.TryReadMove(out dx, out dz);

        internal bool TryConsumeSkillClick(out int slot) =>
            _hudInput.TryConsumeSkillClick(out slot);

        internal bool TryConsumeSkillAimSubmit(
            out int slot,
            out float aimPosX,
            out float aimPosY,
            out float aimPosZ,
            out float aimDirX,
            out float aimDirY,
            out float aimDirZ) =>
            _hudInput.TryConsumeSkillAimSubmit(
                out slot,
                out aimPosX,
                out aimPosY,
                out aimPosZ,
                out aimDirX,
                out aimDirY,
                out aimDirZ);

        internal bool TryReadSkillAimPreview(
            out int slot,
            out float dx,
            out float dz,
            out int submissionVersion) =>
            _hudInput.TryReadSkillAimPreview(out slot, out dx, out dz, out submissionVersion);

        internal void BeginMove() => _hudInput.BeginMove();
        internal void EndMove() => _hudInput.EndMove();
        internal void SetMove(float dx, float dz) => _hudInput.SetMove(dx, dz);
        internal void SubmitSkillClick(int slot) => _hudInput.SubmitSkillClick(slot);
        internal void SetSkillAim(int slot, float dx, float dz, bool aiming) =>
            _hudInput.SetSkillAim(slot, dx, dz, aiming);
        internal void CancelSkillAim() => _hudInput.CancelSkillAim();

        internal void SubmitSkillAim(int slot, float aimDx, float aimDz)
        {
            var offset = new Vector3(aimDx, 0f, aimDz);
            var projection = _aimProjection != null
                ? _aimProjection.Project(aimDx, aimDz)
                : new BattleAimProjection(
                    offset,
                    offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector3.zero);
            _hudInput.SubmitSkillAim(
                slot,
                aimDx,
                aimDz,
                projection.Position.x,
                projection.Position.y,
                projection.Position.z,
                projection.Direction.x,
                projection.Direction.y,
                projection.Direction.z);
        }

        internal bool TryResolveLocalActorId(out int actorId)
        {
            actorId = 0;
            return _actorResolver != null && _actorResolver.TryResolveActorId(out actorId);
        }

        internal bool TryResolveLocalActorWorldPosition(out Vector3 position)
        {
            position = default;
            return _actorResolver != null && _actorResolver.TryResolveWorldPosition(out position);
        }

        bool IBattleInputSubmissionPort.Submit(
            in PlayerInputCommand command,
            PlayerId playerId,
            WorldId worldId) =>
            Submit(in command, playerId, worldId);

        internal bool Submit(in PlayerInputCommand command, PlayerId playerId, WorldId worldId)
        {
            var context = _context;
            if (context == null || !context.CanSubmitGameplayInput || context.Session == null)
            {
                return false;
            }

            context.InputRecordWriter?.Append(in command);
            context.Session.SubmitInput(new SubmitInputRequest(worldId, command));
            LocalInputQueue.Enqueue(new LocalPlayerInputEvent(
                command.Frame,
                playerId,
                command.OpCode,
                command.Payload,
                canRetargetIfStale: command.OpCode == MobaOpCodes.Input.Move));
            return true;
        }

        internal void ResetTransientState()
        {
            _hudInput.Reset();
            LocalInputQueue.Clear();
        }

        public void Dispose()
        {
            Unbind(_context);
            LocalInputQueue.Dispose();
        }
    }
}
