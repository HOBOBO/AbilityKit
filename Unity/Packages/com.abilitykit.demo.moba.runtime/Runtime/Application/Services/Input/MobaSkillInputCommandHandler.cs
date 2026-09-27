using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 处理 MOBA 技能输入命令。
    /// </summary>
    [MobaInputCommandHandler(AbilityKit.Protocol.Moba.MobaOpCodes.Input.SkillInput)]
    public sealed class MobaSkillInputCommandHandler : IMobaInputCommandHandler
    {
        public bool Handle(MobaInputCommandContext context, FrameIndex frame, PlayerInputCommand command, out MobaInputCommandResult result)
        {
            if (context == null)
            {
                result = MobaInputCommandResult.Rejected(command, MobaInputCommandFailureCode.ContextMissing);
                return false;
            }

            if (context.Phase == null || !context.Phase.InGame)
            {
                result = MobaInputCommandResult.Rejected(command, MobaInputCommandFailureCode.NotInGame);
                return false;
            }

            if (context.PlayerActorMap == null || !context.PlayerActorMap.TryGetActorId(command.Player, out int actorId))
            {
                result = MobaInputCommandResult.Rejected(command, MobaInputCommandFailureCode.ActorMapMissing);
                return false;
            }

            if (!context.TryGetEntity(actorId, out ActorEntity entity) || entity == null)
            {
                result = MobaInputCommandResult.Rejected(command, MobaInputCommandFailureCode.ActorEntityMissing, actorId);
                return false;
            }

            if (!entity.hasTransform)
            {
                result = MobaInputCommandResult.Rejected(command, MobaInputCommandFailureCode.TransformMissing, actorId);
                return false;
            }

            if (command.Payload == null || command.Payload.Length == 0)
            {
                result = MobaInputCommandResult.Rejected(command, MobaInputCommandFailureCode.PayloadMissing, actorId);
                return false;
            }

            if (!SkillInputCodec.TryDeserialize(command.Payload, out SkillInputEvent evt, out var payloadError))
            {
                result = MobaInputCommandResult.Rejected(
                    command,
                    MobaInputCommandFailureCode.PayloadInvalid,
                    $"PayloadInvalid(Player={command.Player.Value},Actor={actorId},Error={payloadError})",
                    actorId);
                return false;
            }

            var skillId = ResolveSkillId(entity, evt.Slot);
            MobaActionAckSnapshotService actionAcks = null;
            MobaActorRegistry actors = null;
            var replayAcceptedDecision = false;
            context.Services?.TryResolve(out actionAcks);
            context.Services?.TryResolve(out actors);

            if (evt.PredictionKey > 0)
            {
                if (actionAcks == null || actors == null)
                {
                    result = MobaInputCommandResult.Rejected(
                        command,
                        MobaInputCommandFailureCode.HandlerRejected,
                        "Predicted skill input requires action authority services.",
                        actorId);
                    return false;
                }

                if (context.IsReplay)
                {
                    var replayDisposition = actionAcks.ResolveReplay(
                        actorId,
                        evt.EntityVersion,
                        evt.PredictionKey,
                        out var replayDecision);
                    if (replayDisposition != MobaActionReplayDisposition.ExecuteAccepted)
                    {
                        var accepted = replayDisposition == MobaActionReplayDisposition.SkipDuplicate &&
                                       replayDecision.Accepted;
                        result = accepted
                            ? MobaInputCommandResult.Accepted(command, "Duplicate accepted action skipped during replay.", actorId)
                            : MobaInputCommandResult.Rejected(
                                command,
                                MobaInputCommandFailureCode.SkillRejected,
                                replayDisposition == MobaActionReplayDisposition.DecisionMissing
                                    ? "Replay action has no authoritative decision."
                                    : "Rejected action skipped during replay.",
                                actorId);
                        return accepted;
                    }

                    replayAcceptedDecision = true;
                }
                else if (actionAcks.TryGetDecision(actorId, evt.EntityVersion, evt.PredictionKey, out var prior))
                {
                    actionAcks.Repeat(in prior);
                    result = prior.Accepted
                        ? MobaInputCommandResult.Accepted(command, "Duplicate prediction key already accepted.", actorId)
                        : MobaInputCommandResult.Rejected(
                            command,
                            MobaInputCommandFailureCode.SkillRejected,
                            "Duplicate prediction key already rejected.",
                            actorId);
                    return prior.Accepted;
                }

                var currentEntityVersion = actors.GetEntityVersion(actorId);
                if (evt.EntityVersion <= 0 || evt.EntityVersion != currentEntityVersion)
                {
                    ThrowIfAcceptedReplayFailed(
                        replayAcceptedDecision,
                        actorId,
                        evt,
                        $"entity version mismatch. expected={currentEntityVersion}, actual={evt.EntityVersion}");
                    if (!context.IsReplay) actionAcks.Report(
                        actorId,
                        currentEntityVersion,
                        evt.PredictionKey,
                        skillId,
                        false,
                        frame.Value,
                        MobaActionAckReason.EntityVersionChanged);
                    result = MobaInputCommandResult.Rejected(
                        command,
                        MobaInputCommandFailureCode.PredictionIdentityRejected,
                        $"Entity version mismatch. expected={currentEntityVersion}, actual={evt.EntityVersion}",
                        actorId);
                    return false;
                }

                if (evt.TargetFrame != frame.Value)
                {
                    ThrowIfAcceptedReplayFailed(
                        replayAcceptedDecision,
                        actorId,
                        evt,
                        $"target frame mismatch. expected={frame.Value}, actual={evt.TargetFrame}");
                    if (!context.IsReplay) actionAcks.Report(
                        actorId,
                        currentEntityVersion,
                        evt.PredictionKey,
                        skillId,
                        false,
                        frame.Value,
                        MobaActionAckReason.InvalidInput);
                    result = MobaInputCommandResult.Rejected(
                        command,
                        MobaInputCommandFailureCode.FramePolicyRejected,
                        $"Predicted target frame mismatch. expected={frame.Value}, actual={evt.TargetFrame}",
                        actorId);
                    return false;
                }

                var currentInterruptEpoch = actionAcks.GetInterruptEpoch(actorId, currentEntityVersion);
                if (!context.IsReplay && evt.InterruptEpoch != currentInterruptEpoch)
                {
                    actionAcks.Report(
                        actorId,
                        currentEntityVersion,
                        evt.PredictionKey,
                        skillId,
                        false,
                        frame.Value,
                        MobaActionAckReason.Interrupted);
                    result = MobaInputCommandResult.Rejected(
                        command,
                        MobaInputCommandFailureCode.InterruptEpochRejected,
                        $"Interrupt epoch mismatch. expected={currentInterruptEpoch}, actual={evt.InterruptEpoch}",
                        actorId);
                    return false;
                }

                if (!context.IsReplay && !actionAcks.TryAcceptInputSequence(actorId, currentEntityVersion, evt.InputSequence))
                {
                    actionAcks.Report(
                        actorId,
                        currentEntityVersion,
                        evt.PredictionKey,
                        skillId,
                        false,
                        frame.Value,
                        MobaActionAckReason.InvalidInput);
                    result = MobaInputCommandResult.Rejected(
                        command,
                        MobaInputCommandFailureCode.InputSequenceRejected,
                        $"Skill input sequence is stale or invalid. sequence={evt.InputSequence}",
                        actorId);
                    return false;
                }
            }

            if (context.Skills == null)
            {
                ThrowIfAcceptedReplayFailed(
                    replayAcceptedDecision,
                    actorId,
                    evt,
                    "skill executor is missing");
                result = MobaInputCommandResult.Rejected(command, MobaInputCommandFailureCode.SkillExecutorMissing, actorId);
                return false;
            }

            var skillResult = context.Skills.TryHandleInputResult(actorId, in evt, context.DiagnosticCommandId);
            if (!skillResult.Success)
            {
                ThrowIfAcceptedReplayFailed(
                    replayAcceptedDecision,
                    actorId,
                    evt,
                    "skill execution was rejected: " + (skillResult.Code ?? "unknown"));
                if (!context.IsReplay) actionAcks?.Report(
                    actorId,
                    evt.EntityVersion,
                    evt.PredictionKey,
                    skillId,
                    false,
                    frame.Value,
                    ResolveAckReason(in skillResult));
                var failedRuntimeHandle = skillResult.RuntimeHandle;
                result = MobaInputCommandResult.Rejected(
                    command,
                    MobaInputCommandFailureCode.SkillRejected,
                    CreateSkillRejectedMessage(in skillResult, evt.Slot, evt.TargetActorId),
                    actorId)
                    .WithSkillDiagnostic(evt.Slot, (int)evt.Phase, evt.TargetActorId,
                        in failedRuntimeHandle);
                return false;
            }

            if (replayAcceptedDecision)
                actionAcks.CompleteReplay(actorId, evt.EntityVersion, evt.PredictionKey);

            var runtimeHandle = skillResult.RuntimeHandle;
            if (!context.IsReplay) actionAcks?.Report(
                actorId,
                evt.EntityVersion,
                evt.PredictionKey,
                skillId,
                true,
                frame.Value,
                MobaActionAckReason.None);
            result = MobaInputCommandResult.Accepted(command, actorId)
                .WithSkillDiagnostic(evt.Slot, (int)evt.Phase, evt.TargetActorId,
                    in runtimeHandle);
            return true;
        }

        private static void ThrowIfAcceptedReplayFailed(
            bool replayAcceptedDecision,
            int actorId,
            in SkillInputEvent evt,
            string reason)
        {
            if (!replayAcceptedDecision) return;

            throw new System.InvalidOperationException(
                $"Accepted action could not be replayed. actor={actorId}, entityVersion={evt.EntityVersion}, " +
                $"predictionKey={evt.PredictionKey}, reason={reason}.");
        }

        private static string CreateSkillRejectedMessage(in MobaSkillInputHandleResult skillResult, int slot, int targetActorId)
        {
            var code = string.IsNullOrEmpty(skillResult.Code) ? "skill.input.rejected" : skillResult.Code;
            return "SkillRejected(Code=" + code + ",Slot=" + slot + ",Target=" + targetActorId + ")";
        }

        private static int ResolveSkillId(ActorEntity entity, int slot)
        {
            if (entity == null || !entity.hasSkillLoadout || slot <= 0) return 0;
            var skills = entity.skillLoadout.ActiveSkills;
            var index = slot - 1;
            return skills != null && index < skills.Length && skills[index] != null
                ? skills[index].SkillId
                : 0;
        }

        private static MobaActionAckReason ResolveAckReason(in MobaSkillInputHandleResult skillResult)
        {
            var code = skillResult.Code ?? string.Empty;
            if (code.IndexOf("cooldown", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return MobaActionAckReason.Cooldown;
            if (code.IndexOf("resource", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                code.IndexOf("charge", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return MobaActionAckReason.ResourceInsufficient;
            if (code.IndexOf("control", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return MobaActionAckReason.Controlled;
            if (code.IndexOf("target", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return MobaActionAckReason.TargetInvalid;
            if (code.IndexOf("interrupt", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return MobaActionAckReason.Interrupted;
            return MobaActionAckReason.ServerPolicy;
        }
    }
}
