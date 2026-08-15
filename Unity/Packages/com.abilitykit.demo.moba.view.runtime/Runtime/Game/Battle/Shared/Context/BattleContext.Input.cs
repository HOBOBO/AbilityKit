using System;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Recording.FrameRecord;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Battle.Entity;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleContext : IBattleLocalActorResolutionPort
    {
        private BattleInputRuntime _inputRuntime;
        private bool _ownsInputRuntime;
        private IFrameRecordWriter _inputRecordWriter;

        public IFrameRecordWriter InputRecordWriter => _inputRecordWriter;
        public BattleLocalInputQueue LocalInputQueue => EnsureInputRuntime().LocalInputQueue;

        internal BattleInputRuntime InputRuntime => EnsureInputRuntime();

        internal void BindInputRuntime(BattleInputRuntime runtime)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (ReferenceEquals(_inputRuntime, runtime)) return;

            ReleaseInputRuntimeBinding();
            _inputRuntime = runtime;
            _ownsInputRuntime = false;
            runtime.Bind(this);
        }

        internal void UnbindInputRuntime(BattleInputRuntime runtime)
        {
            if (!ReferenceEquals(_inputRuntime, runtime)) return;

            runtime.Unbind(this);
            _inputRuntime = null;
            _ownsInputRuntime = false;
        }

        internal bool TryReadHudMove(out float dx, out float dz) =>
            EnsureInputRuntime().TryReadMove(out dx, out dz);

        internal bool TryConsumeHudSkillClick(out int slot) =>
            EnsureInputRuntime().TryConsumeSkillClick(out slot);

        internal bool TryConsumeHudSkillAimSubmit(
            out int slot,
            out float aimPosX,
            out float aimPosY,
            out float aimPosZ,
            out float aimDirX,
            out float aimDirY,
            out float aimDirZ) =>
            EnsureInputRuntime().TryConsumeSkillAimSubmit(
                out slot,
                out aimPosX,
                out aimPosY,
                out aimPosZ,
                out aimDirX,
                out aimDirY,
                out aimDirZ);

        public void BeginHudMove() => EnsureInputRuntime().BeginMove();
        public void EndHudMove() => EnsureInputRuntime().EndMove();
        public void SetHudMove(float dx, float dz) => EnsureInputRuntime().SetMove(dx, dz);
        public void SubmitHudSkillClick(int slot) => EnsureInputRuntime().SubmitSkillClick(slot);
        public void SetHudSkillAim(int slot, float dx, float dz, bool aiming) =>
            EnsureInputRuntime().SetSkillAim(slot, dx, dz, aiming);
        public void CancelHudSkillAim() => EnsureInputRuntime().CancelSkillAim();
        public void SubmitHudSkillAim(int slot, float aimDx, float aimDz) =>
            EnsureInputRuntime().SubmitSkillAim(slot, aimDx, aimDz);

        internal bool TryReadHudSkillAimPreview(
            out int slot,
            out float dx,
            out float dz,
            out int submissionVersion) =>
            EnsureInputRuntime().TryReadSkillAimPreview(out slot, out dx, out dz, out submissionVersion);

        internal bool TryResolveLocalActorId(out int actorId) =>
            EnsureInputRuntime().TryResolveLocalActorId(out actorId);

        internal bool TryResolveLocalActorWorldPos(out Vector3 position) =>
            EnsureInputRuntime().TryResolveLocalActorWorldPosition(out position);

        int IBattleLocalActorResolutionPort.CachedActorId
        {
            get => LocalActorId;
            set => LocalActorId = value;
        }

        bool IBattleLocalActorResolutionPort.TryResolveMappedActorId(out int actorId)
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
            if (string.IsNullOrEmpty(playerIdValue)) playerIdValue = "p1";
            return playerActors.TryGetActorId(new PlayerId(playerIdValue), out actorId) && actorId > 0;
        }

        bool IBattleLocalActorResolutionPort.TryResolveActorWorldPosition(int actorId, out Vector3 position)
        {
            position = default;
            if (actorId <= 0 ||
                EntityQuery == null ||
                !EntityQuery.TryGetTransform(new BattleNetId(actorId), out var transform) ||
                transform == null)
            {
                return false;
            }

            position = transform.Position;
            return true;
        }

        internal void BindInputRecordWriter(IFrameRecordWriter writer)
        {
            _inputRecordWriter = writer;
        }

        internal void ClearInputRecordWriter(IFrameRecordWriter writer)
        {
            if (ReferenceEquals(_inputRecordWriter, writer))
            {
                _inputRecordWriter = null;
            }
        }

        private BattleInputRuntime EnsureInputRuntime()
        {
            if (_inputRuntime != null) return _inputRuntime;

            _inputRuntime = new BattleInputRuntime();
            _ownsInputRuntime = true;
            _inputRuntime.Bind(this);
            return _inputRuntime;
        }

        private void ResetInputRuntime()
        {
            ReleaseInputRuntimeBinding();
            _inputRecordWriter = null;
        }

        private void ReleaseInputRuntimeBinding()
        {
            var runtime = _inputRuntime;
            var ownsRuntime = _ownsInputRuntime;
            _inputRuntime = null;
            _ownsInputRuntime = false;
            if (runtime == null) return;

            if (ownsRuntime) runtime.Dispose();
            else runtime.Unbind(this);
        }
    }
}
