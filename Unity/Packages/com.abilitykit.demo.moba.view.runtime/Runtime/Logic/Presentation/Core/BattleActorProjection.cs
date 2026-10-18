using System;
using System.Collections.Generic;
using AbilityKit.Game.Battle.Agent;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleActorProjection : IBattleClientProjection
    {
        private readonly IBattleActorViewPort _port;
        private readonly Func<int> _excludedActorId;
        private readonly BattleActorViewProjectionWorkspace _workspace =
            new BattleActorViewProjectionWorkspace();

        public BattleActorProjection(IBattleActorViewPort port, Func<int> excludedActorId = null)
        {
            _port = port ?? throw new ArgumentNullException(nameof(port));
            _excludedActorId = excludedActorId ?? (() => 0);
        }

        public void Apply(in GatewayStateSyncSnapshot snapshot) =>
            BattleActorViewProjector.ApplyWithoutBatch(
                _port, in snapshot, _excludedActorId(), _workspace);

        public void Reset()
        {
            var ids = new List<int>(_port.GetActorIds());
            foreach (var id in ids) _port.Remove(id);
        }
    }
}
