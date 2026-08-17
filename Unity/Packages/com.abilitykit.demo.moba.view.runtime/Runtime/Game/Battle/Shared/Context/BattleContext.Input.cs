using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Recording.FrameRecord;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Battle.Entity;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleContext :
        IBattleLocalActorResolutionPort,
        IBattleHudInputReadPort,
        IBattleInputSubmissionPort,
        IBattleHudAimPreviewReadPort
    {
        private readonly ReferenceBindingOwner<BattleInputRuntime> _inputRuntimeBinding =
            new ReferenceBindingOwner<BattleInputRuntime>();
        private readonly ReferenceBindingOwner<IFrameRecordWriter> _inputRecordWriterBinding =
            new ReferenceBindingOwner<IFrameRecordWriter>();

        public IFrameRecordWriter InputRecordWriter => _inputRecordWriterBinding.Value;
        public BattleLocalInputQueue LocalInputQueue => EnsureInputRuntime().LocalInputQueue;

        internal BattleInputRuntime InputRuntime => EnsureInputRuntime();

        internal void BindInputRuntime(BattleInputRuntime runtime)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (ReferenceEquals(_inputRuntimeBinding.Value, runtime)) return;

            ReleaseInputRuntimeBinding();
            _inputRuntimeBinding.Bind(runtime);
            runtime.Bind(this);
        }

        internal void UnbindInputRuntime(BattleInputRuntime runtime)
        {
            if (!_inputRuntimeBinding.TryClear(runtime, out _, out _)) return;
            runtime.Unbind(this);
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

        bool IBattleHudInputReadPort.TryReadMove(out float dx, out float dz) =>
            TryReadHudMove(out dx, out dz);

        bool IBattleHudInputReadPort.TryConsumeSkillClick(out int slot) =>
            TryConsumeHudSkillClick(out slot);

        bool IBattleHudInputReadPort.TryConsumeSkillAimSubmit(
            out BattleSkillAimSubmitInput input)
        {
            if (TryConsumeHudSkillAimSubmit(
                    out var slot,
                    out var aimPosX,
                    out var aimPosY,
                    out var aimPosZ,
                    out var aimDirX,
                    out var aimDirY,
                    out var aimDirZ))
            {
                input = new BattleSkillAimSubmitInput(
                    slot,
                    aimPosX,
                    aimPosY,
                    aimPosZ,
                    aimDirX,
                    aimDirY,
                    aimDirZ);
                return true;
            }

            input = default;
            return false;
        }

        bool IBattleInputSubmissionPort.Submit(
            in PlayerInputCommand command,
            PlayerId playerId,
            WorldId worldId) =>
            EnsureInputRuntime().Submit(in command, playerId, worldId);

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

        bool IBattleHudAimPreviewReadPort.TryReadAimPreview(
            out int slot,
            out float dx,
            out float dz,
            out int submissionVersion) =>
            TryReadHudSkillAimPreview(out slot, out dx, out dz, out submissionVersion);

        bool IBattleHudAimPreviewReadPort.TryResolveLocalActorWorldPosition(
            out float x,
            out float y,
            out float z)
        {
            if (TryResolveLocalActorWorldPos(out var position))
            {
                x = position.x;
                y = position.y;
                z = position.z;
                return true;
            }

            x = 0f;
            y = 0f;
            z = 0f;
            return false;
        }

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

        internal long BindInputRecordWriter(IFrameRecordWriter writer)
        {
            return _inputRecordWriterBinding.Bind(
                writer ?? throw new ArgumentNullException(nameof(writer)));
        }

        internal bool ClearInputRecordWriter(long bindingGeneration, IFrameRecordWriter writer)
        {
            return _inputRecordWriterBinding.TryClear(
                bindingGeneration,
                writer,
                out _,
                out _);
        }

        internal void ClearInputRecordWriter(IFrameRecordWriter writer)
        {
            _inputRecordWriterBinding.TryClear(writer, out _, out _);
        }

        private BattleInputRuntime EnsureInputRuntime()
        {
            var runtime = _inputRuntimeBinding.Value;
            if (runtime != null) return runtime;

            runtime = new BattleInputRuntime();
            _inputRuntimeBinding.Bind(runtime, ownsValue: true);
            runtime.Bind(this);
            return runtime;
        }

        private void ResetInputRuntime()
        {
            ReleaseInputRuntimeBinding();
            _inputRecordWriterBinding.Reset(out _, out _);
        }

        private void ReleaseInputRuntimeBinding()
        {
            if (!_inputRuntimeBinding.Reset(out var runtime, out var ownsRuntime)) return;

            if (ownsRuntime) runtime.Dispose();
            else runtime.Unbind(this);
        }
    }
}
