using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Game.Flow
{
    internal readonly struct BattleInputSubmitter
    {
        private readonly BattleContext _ctx;
        private readonly PlayerId _playerId;
        private readonly WorldId _worldId;

        public BattleInputSubmitter(BattleContext ctx, PlayerId playerId, WorldId worldId)
        {
            _ctx = ctx;
            _playerId = playerId;
            _worldId = worldId;
        }

        public bool Submit(in PlayerInputCommand cmd)
        {
            if (_ctx == null || !_ctx.CanSubmitGameplayInput || _ctx.Session == null)
            {
                return false;
            }

            _ctx.InputRecordWriter?.Append(in cmd);
            _ctx.Session.SubmitInput(new SubmitInputRequest(_worldId, cmd));
            (_ctx.LocalInputQueue ??= new BattleLocalInputQueue())
                .Enqueue(new LocalPlayerInputEvent(
                    cmd.Frame,
                    _playerId,
                    cmd.OpCode,
                    cmd.Payload,
                    canRetargetIfStale: cmd.OpCode == MobaOpCodes.Input.Move));
            return true;
        }
    }
}
