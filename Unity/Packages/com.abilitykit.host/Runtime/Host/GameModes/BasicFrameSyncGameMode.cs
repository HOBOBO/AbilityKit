using System;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host.GameModes
{
    public sealed class BasicFrameSyncGameMode : IGameMode
    {
        public IGameModeSession CreateSession(IWorldHost host, WorldCreateOptions options)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (options == null) throw new ArgumentNullException(nameof(options));

            if (host is not IFrameSyncSessionHost sessionHost)
            {
                throw new InvalidOperationException($"{nameof(BasicFrameSyncGameMode)} requires host to implement {nameof(IFrameSyncSessionHost)}.");
            }

            if (host is not IServerConnectionHost connectionHost)
            {
                throw new InvalidOperationException($"{nameof(BasicFrameSyncGameMode)} requires host to implement {nameof(IServerConnectionHost)}.");
            }

            var world = host.CreateWorld(options);

            if (!sessionHost.TryGetFrameSyncWorldSession(world.Id, out var worldSession) || worldSession == null)
            {
                throw new InvalidOperationException($"World {world.Id.Value} is not a frame-sync world.");
            }

            return new BasicFrameSyncSession(worldSession, connectionHost);
        }
    }
}
