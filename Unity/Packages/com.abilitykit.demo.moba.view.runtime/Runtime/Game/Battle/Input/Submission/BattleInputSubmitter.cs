using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Requests;

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

        public void Submit(in PlayerInputCommand cmd)
        {
            _ctx.InputRecordWriter?.Append(in cmd);
            _ctx.Session.SubmitInput(new SubmitInputRequest(_worldId, cmd));
            (_ctx.LocalInputQueue ??= new BattleLocalInputQueue())
                .Enqueue(new LocalPlayerInputEvent(_playerId, cmd.OpCode, cmd.Payload));
        }
    }
}
