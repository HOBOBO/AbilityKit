using AbilityKit.Ability.Host;
using AbilityKit.Core.Recording.FrameRecord;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Battle.Entity;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleContext
    {
        private readonly BattleHudInputState _hudInput = new BattleHudInputState();

        public IFrameRecordWriter InputRecordWriter;
        public BattleLocalInputQueue LocalInputQueue;

        internal bool TryReadHudMove(out float dx, out float dz)
        {
            return _hudInput.TryReadMove(out dx, out dz);
        }

        internal bool TryConsumeHudSkillClick(out int slot)
        {
            return _hudInput.TryConsumeSkillClick(out slot);
        }

        internal bool TryConsumeHudSkillAimSubmit(
            out int slot,
            out float aimPosX,
            out float aimPosY,
            out float aimPosZ,
            out float aimDirX,
            out float aimDirY,
            out float aimDirZ)
        {
            return _hudInput.TryConsumeSkillAimSubmit(
                out slot,
                out aimPosX,
                out aimPosY,
                out aimPosZ,
                out aimDirX,
                out aimDirY,
                out aimDirZ);
        }

        public void BeginHudMove()
        {
            _hudInput.BeginMove();
        }

        public void EndHudMove()
        {
            _hudInput.EndMove();
        }

        public void SetHudMove(float dx, float dz)
        {
            _hudInput.SetMove(dx, dz);
        }

        public void SubmitHudSkillClick(int slot)
        {
            _hudInput.SubmitSkillClick(slot);
        }

        public void SetHudSkillAim(int slot, float dx, float dz, bool aiming)
        {
            _hudInput.SetSkillAim(slot, dx, dz, aiming);
        }

        public void CancelHudSkillAim()
        {
            _hudInput.CancelSkillAim();
        }

        public void SubmitHudSkillAim(int slot, float aimDx, float aimDz)
        {
            var aimOffset = new Vector3(aimDx, 0f, aimDz);
            var aimDir = aimOffset.sqrMagnitude > 0.0001f ? aimOffset.normalized : Vector3.zero;
            var aimPos = aimOffset;
 
            if (TryResolveLocalActorWorldPos(out var casterPos))
            {
                aimPos = casterPos + aimOffset;
            }

            _hudInput.SubmitSkillAim(
                slot,
                aimDx,
                aimDz,
                aimPos.x,
                aimPos.y,
                aimPos.z,
                aimDir.x,
                aimDir.y,
                aimDir.z);
        }

        internal bool TryReadHudSkillAimPreview(out int slot, out float dx, out float dz, out int submissionVersion)
        {
            return _hudInput.TryReadSkillAimPreview(out slot, out dx, out dz, out submissionVersion);
        }

        internal bool TryResolveLocalActorId(out int actorId)
        {
            actorId = LocalActorId;
            if (actorId > 0)
            {
                return true;
            }

            if (!TryResolveMappedLocalActorId(out actorId))
            {
                return false;
            }

            LocalActorId = actorId;
            return true;
        }

        internal bool TryResolveLocalActorWorldPos(out Vector3 pos)
        {
            pos = default;
            if (EntityQuery == null) return false;

            if (TryResolveActorWorldPos(LocalActorId, out pos))
            {
                return true;
            }

            if (!TryResolveMappedLocalActorId(out var actorId) ||
                !TryResolveActorWorldPos(actorId, out pos))
            {
                return false;
            }

            LocalActorId = actorId;
            return true;
        }

        private bool TryResolveMappedLocalActorId(out int actorId)
        {
            actorId = 0;
            if (!TryGetRuntimeWorld(out var world) ||
                world.Services == null ||
                !world.Services.TryResolve<MobaPlayerActorMapService>(out var playerActors) ||
                playerActors == null)
            {
                return false;
            }

            var playerIdValue = ResolveLocalControlPlayerId();
            if (string.IsNullOrEmpty(playerIdValue))
            {
                playerIdValue = "p1";
            }

            return playerActors.TryGetActorId(new PlayerId(playerIdValue), out actorId) &&
                   actorId > 0;
        }

        private bool TryResolveActorWorldPos(int actorId, out Vector3 pos)
        {
            pos = default;
            if (actorId <= 0 ||
                !EntityQuery.TryGetTransform(new BattleNetId(actorId), out var transform) ||
                transform == null)
            {
                return false;
            }

            pos = transform.Position;
            return true;
        }

        private void ResetHudInput()
        {
            _hudInput.Reset();
        }
    }
}
