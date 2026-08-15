using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Game.Flow
{
    internal readonly struct BattleInputSubmitter
    {
        private readonly BattleInputRuntime _runtime;
        private readonly PlayerId _playerId;
        private readonly WorldId _worldId;

        public BattleInputSubmitter(BattleContext context, PlayerId playerId, WorldId worldId)
        {
            _runtime = context?.InputRuntime;
            _playerId = playerId;
            _worldId = worldId;
        }

        internal BattleInputSubmitter(BattleInputRuntime runtime, PlayerId playerId, WorldId worldId)
        {
            _runtime = runtime;
            _playerId = playerId;
            _worldId = worldId;
        }

        public bool Submit(in PlayerInputCommand command)
        {
            return _runtime != null && _runtime.Submit(in command, _playerId, _worldId);
        }
    }
}
