using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Game.Flow
{
    internal readonly struct BattleInputSubmitter
    {
        private readonly IBattleInputSubmissionPort _input;
        private readonly PlayerId _playerId;
        private readonly WorldId _worldId;

        public BattleInputSubmitter(
            IBattleInputSubmissionPort input,
            PlayerId playerId,
            WorldId worldId)
        {
            _input = input;
            _playerId = playerId;
            _worldId = worldId;
        }

        public bool Submit(in PlayerInputCommand command)
        {
            return _input != null && _input.Submit(in command, _playerId, _worldId);
        }
    }
}
