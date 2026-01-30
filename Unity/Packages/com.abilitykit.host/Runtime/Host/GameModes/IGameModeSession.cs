using System;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host.GameModes
{
    public interface IGameModeSession : IDisposable
    {
        WorldId WorldId { get; }

        bool AddClient(IServerConnection connection, PlayerId playerId);
        bool RemoveClient(IServerConnection connection, PlayerId playerId);

        bool SubmitInput(IServerConnection connection, PlayerInputCommand input);

        void Tick(float deltaTime);
    }
}
