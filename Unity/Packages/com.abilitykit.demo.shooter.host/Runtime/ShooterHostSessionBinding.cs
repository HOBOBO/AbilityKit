using System;
using AbilityKit.Network.Host;

namespace AbilityKit.Demo.Shooter.Host
{
    public readonly struct ShooterHostSessionBinding
    {
        public ShooterHostSessionBinding(string worldId, int playerId)
        {
            if (string.IsNullOrWhiteSpace(worldId)) throw new ArgumentException("World id is required.", nameof(worldId));
            if (playerId <= 0) throw new ArgumentOutOfRangeException(nameof(playerId));
            WorldId = worldId;
            PlayerId = playerId;
        }

        public string WorldId { get; }
        public int PlayerId { get; }
    }

    public static class ShooterHostSessionBindings
    {
        private const string BindingKey = "abilitykit.shooter.host.binding";

        public static void Bind(IServerNetworkSession session, in ShooterHostSessionBinding binding)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            session.Context.Set(BindingKey, binding);
            session.Context.MarkEstablished();
        }

        public static bool TryGet(IServerNetworkSession session, out ShooterHostSessionBinding binding)
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
