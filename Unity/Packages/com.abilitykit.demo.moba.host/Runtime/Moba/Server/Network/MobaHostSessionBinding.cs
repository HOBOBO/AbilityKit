using System;
using AbilityKit.Network.Host;

namespace AbilityKit.Ability.Host.Extensions.Moba.Server.Network
{
    public readonly struct MobaHostSessionBinding
    {
        public MobaHostSessionBinding(string worldId, string playerId)
        {
            if (string.IsNullOrWhiteSpace(worldId)) throw new ArgumentException("World id is required.", nameof(worldId));
            if (string.IsNullOrWhiteSpace(playerId)) throw new ArgumentException("Player id is required.", nameof(playerId));
            WorldId = worldId;
            PlayerId = playerId;
        }

        public string WorldId { get; }
        public string PlayerId { get; }
    }

    public static class MobaHostSessionBindings
    {
        private const string BindingKey = "abilitykit.moba.host.binding";

        public static void Bind(IServerNetworkSession session, in MobaHostSessionBinding binding)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            session.Context.Set(BindingKey, binding);
            session.Context.MarkEstablished();
        }

        public static bool TryGet(IServerNetworkSession session, out MobaHostSessionBinding binding)
        {
            if (session == null)
            {
                binding = default;
                return false;
            }

            return session.Context.TryGet(BindingKey, out binding);
        }
    }
}
