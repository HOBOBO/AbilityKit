using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Network.Battle.Projection;

namespace AbilityKit.Game.Flow
{
    /// <summary>Reads projection data from a world without exposing the world to a view.</summary>
    internal sealed class BattleWorldProjectionViewSource :
        IBattleProjectionViewSource, IBattleProjectionViewEpochSource
    {
        private readonly Func<IWorld> _world;
        private readonly Func<int> _frame;
        private readonly WorldId _worldId;
        private IWorld _lastWorld;
        private IActorProjectionProducer _producer;
        private long _epoch;

        internal BattleWorldProjectionViewSource(
            WorldId worldId, Func<IWorld> world, Func<int> frame)
        {
            _worldId = worldId;
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _frame = frame ?? throw new ArgumentNullException(nameof(frame));
        }

        public WorldId WorldId => _worldId;
        public long ProjectionEpoch => _epoch;

        internal void Invalidate() => _epoch++;

        public bool TryGetProjection(out IActorProjectionProducer producer, out int frame)
        {
            var world = _world();
            if (!ReferenceEquals(world, _lastWorld))
            {
                _lastWorld = world;
                _producer = null;
                _epoch++;
            }

            frame = world != null ? _frame() : 0;
            if (world != null && _producer == null)
                world.Services?.TryResolve(out _producer);
            producer = _producer;
            return producer != null;
        }
    }
}
