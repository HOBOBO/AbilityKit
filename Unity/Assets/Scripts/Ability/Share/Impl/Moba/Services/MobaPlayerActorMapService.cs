using System;
using System.Collections.Generic;
using AbilityKit.Ability.Server;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaPlayerActorMapService
    {
        private readonly Dictionary<string, int> _map = new Dictionary<string, int>();

        public void Bind(PlayerId playerId, int actorId)
        {
            if (string.IsNullOrEmpty(playerId.Value)) return;
            if (actorId <= 0) return;
            _map[playerId.Value] = actorId;
        }

        public bool TryGetActorId(PlayerId playerId, out int actorId)
        {
            if (string.IsNullOrEmpty(playerId.Value))
            {
                actorId = 0;
                return false;
            }

            return _map.TryGetValue(playerId.Value, out actorId) && actorId > 0;
        }

        public void Clear()
        {
            _map.Clear();
        }
    }
}
