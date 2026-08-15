using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleInputFeature : IGamePhaseFeature
    {
        private readonly BattleMoveInputState _moveInputState;
        private readonly BattleMoveInputState _secondaryMoveInputState;
        private BattleContext _ctx;
        private float _inputDiagCooldown;

        public int TickCount { get; private set; }
        public int MoveReadCount { get; private set; }
        public int MoveSubmitAttemptCount { get; private set; }
        public int MoveSubmitSuccessCount { get; private set; }
        public int SkillSubmitAttemptCount { get; private set; }
        public int SkillSubmitSuccessCount { get; private set; }
        public bool HasContext => _ctx != null;
        public bool CanSubmitGameplayInput => _ctx?.CanSubmitGameplayInput == true;

        public BattleInputFeature()
        {
            _moveInputState = new BattleMoveInputState();
            _secondaryMoveInputState = new BattleMoveInputState();
        }

        public void OnAttach(in GamePhaseContext ctx)
        {
            ctx.Features.TryGet(out _ctx);
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            _ctx = null;
            _moveInputState.Reset();
            _secondaryMoveInputState.Reset();
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            TickCount++;
            if (_ctx == null || _ctx.Session == null) return;
            if (_ctx.Plan.RunModeOptions.EnableInputReplay) return;
            if (!_ctx.CanSubmitGameplayInput) return;

            var plan = _ctx.Plan;
            var playerId = BattleInputSessionIdentity.ResolvePlayerId(_ctx);
            var worldId = BattleInputSessionIdentity.ResolveWorldId(in plan);
            var inputObservedFrame = _ctx.LastFrame;
            var prediction = _ctx.PredictionStats;
            if (prediction != null &&
                prediction.TryGetFrames(worldId, out var confirmedFrame, out var predictedFrame))
            {
                inputObservedFrame = SessionSimRuntimeTuning.ResolveInputObservedFrame(
                    inputObservedFrame,
                    confirmedFrame.Value,
                    predictedFrame.Value);
            }
            var nextFrame = SessionSimRuntimeTuning.ResolveInputSubmitFrame(inputObservedFrame, in plan);
            var inputRuntime = _ctx.InputRuntime;
            var submitter = new BattleInputSubmitter(inputRuntime, playerId, worldId);

            if (!BattleHudInputSource.TryReadMove(_ctx, out var dx, out var dz))
            {
                BattleKeyboardInputSource.ReadMove(out dx, out dz);
            }
            else
            {
                MoveReadCount++;
            }

            if (_moveInputState.TryGetMoveToSubmit(nextFrame, dx, dz, out var submitDx, out var submitDz))
            {
                var moveCmd = BattleInputCommandFactory.CreateMove(nextFrame, playerId, submitDx, submitDz);
                MoveSubmitAttemptCount++;
                if (submitter.Submit(in moveCmd)) MoveSubmitSuccessCount++;
            }
            else
            {
                TickInputDiagnostics(deltaTime);
            }

            SubmitLocalTrainingOpponentMove(nextFrame, playerId, worldId);

            if (BattleKeyboardInputSource.TryReadSkillSlotDown(out var keyboardSlot))
            {
                var skillCmd = BattleInputCommandFactory.CreateSkillSlot(nextFrame, playerId, keyboardSlot);
                SubmitSkill(submitter, in skillCmd);
            }

            if (BattleHudInputSource.TryConsumeSkillClick(_ctx, out var hudSlot))
            {
                var skillCmd = BattleInputCommandFactory.CreateSkillSlot(nextFrame, playerId, hudSlot);
                SubmitSkill(submitter, in skillCmd);
            }

            if (BattleHudInputSource.TryConsumeSkillAimSubmit(_ctx, out var aimInput))
            {
                var aimCmd = BattleInputCommandFactory.CreateSkillAimRelease(
                    nextFrame,
                    playerId,
                    aimInput.Slot,
                    aimInput.AimPosX,
                    aimInput.AimPosY,
                    aimInput.AimPosZ,
                    aimInput.AimDirX,
                    aimInput.AimDirY,
                    aimInput.AimDirZ);
                SubmitSkill(submitter, in aimCmd);
            }

            inputRuntime.LocalInputQueue.Flush();
        }

        private void SubmitSkill(BattleInputSubmitter submitter, in PlayerInputCommand command)
        {
            SkillSubmitAttemptCount++;
            if (submitter.Submit(in command)) SkillSubmitSuccessCount++;
        }

        private void SubmitLocalTrainingOpponentMove(int nextFrame, PlayerId primaryPlayerId, WorldId worldId)
        {
            if (!BattleInputSessionIdentity.TryResolveLocalTrainingOpponent(
                    _ctx,
                    primaryPlayerId,
                    out var opponentPlayerId))
            {
                return;
            }

            BattleKeyboardInputSource.ReadSecondaryMove(out var dx, out var dz);
            if (!_secondaryMoveInputState.TryGetMoveToSubmit(nextFrame, dx, dz, out var submitDx, out var submitDz))
            {
                return;
            }

            var submitter = new BattleInputSubmitter(_ctx.InputRuntime, opponentPlayerId, worldId);
            var moveCmd = BattleInputCommandFactory.CreateMove(nextFrame, opponentPlayerId, submitDx, submitDz);
            submitter.Submit(in moveCmd);
        }

        private void TickInputDiagnostics(float deltaTime)
        {
            _inputDiagCooldown -= deltaTime;
            if (_inputDiagCooldown <= 0f)
            {
                _inputDiagCooldown = 1f;
            }
        }
    }
}
