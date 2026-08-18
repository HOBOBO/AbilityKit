using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Demo.Moba.Input;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 处理 MOBA 移动输入命令。
    /// </summary>
    [MobaInputCommandHandler(AbilityKit.Protocol.Moba.MobaOpCodes.Input.Move)]
    public sealed class MobaMoveInputCommandHandler : IMobaInputCommandHandler
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

            if (!MobaMoveCodec.TryDeserialize(command.Payload, out float dx, out float dz, out var payloadError))
            {
                result = MobaInputCommandResult.Rejected(
                    command,
                    MobaInputCommandFailureCode.PayloadInvalid,
                    $"PayloadInvalid(Player={command.Player.Value},Actor={actorId},Error={payloadError})",
                    actorId);
                return false;
            }

            var intent = MobaActorIntent.MoveDirection(dx, dz);
            if (!intent.IsValid())
            {
                result = MobaInputCommandResult.Rejected(
                    command,
                    MobaInputCommandFailureCode.PayloadInvalid,
                    "PayloadInvalid(IntentNotFinite)",
                    actorId);
                return false;
            }

            if (!entity.hasMoveInput) entity.AddMoveInput(intent.MoveX, intent.MoveZ);
            else entity.ReplaceMoveInput(intent.MoveX, intent.MoveZ);

            result = MobaInputCommandResult.Accepted(command, actorId);
            return true;
        }
    }
}
