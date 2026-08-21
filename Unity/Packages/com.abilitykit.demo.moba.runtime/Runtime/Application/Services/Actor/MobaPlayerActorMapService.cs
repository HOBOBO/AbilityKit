using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Demo.Moba.Services
{
    public interface IMobaPlayerActorBindingTransaction : IService
    {
        void Bind(PlayerId playerId, int actorId);
        bool Unbind(PlayerId playerId, int expectedActorId);
    }

    [WorldService(typeof(IMobaPlayerActorBindingTransaction))]
    [WorldService(typeof(MobaPlayerActorMapService))]
    public sealed class MobaPlayerActorMapService : IService, IMobaPlayerActorBindingTransaction
    {
        private readonly Dictionary<string, int> _map = new Dictionary<string, int>();

        public void Bind(PlayerId playerId, int actorId)
        {
            if (string.IsNullOrEmpty(playerId.Value)) return;
            if (actorId <= 0) return;
            _map[playerId.Value] = actorId;
        }

        public bool Unbind(PlayerId playerId, int expectedActorId)
        {
            if (string.IsNullOrEmpty(playerId.Value) || expectedActorId <= 0) return false;
            if (!_map.TryGetValue(playerId.Value, out var actorId) || actorId != expectedActorId) return false;
            return _map.Remove(playerId.Value);
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

        public void Dispose()
        {
            _map.Clear();
        }
    }
}
