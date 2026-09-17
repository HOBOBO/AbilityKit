using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Protocol.Moba;

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

            if (context.Skills == null)
            {
                result = MobaInputCommandResult.Rejected(command, MobaInputCommandFailureCode.SkillExecutorMissing, actorId);
                return false;
            }

            var skillResult = context.Skills.TryHandleInputResult(actorId, in evt, context.DiagnosticCommandId);
            if (!skillResult.Success)
            {
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

            var runtimeHandle = skillResult.RuntimeHandle;
            result = MobaInputCommandResult.Accepted(command, actorId)
                .WithSkillDiagnostic(evt.Slot, (int)evt.Phase, evt.TargetActorId,
                    in runtimeHandle);
            return true;
        }

        private static string CreateSkillRejectedMessage(in MobaSkillInputHandleResult skillResult, int slot, int targetActorId)
        {
            var code = string.IsNullOrEmpty(skillResult.Code) ? "skill.input.rejected" : skillResult.Code;
            return "SkillRejected(Code=" + code + ",Slot=" + slot + ",Target=" + targetActorId + ")";
        }
    }
}
