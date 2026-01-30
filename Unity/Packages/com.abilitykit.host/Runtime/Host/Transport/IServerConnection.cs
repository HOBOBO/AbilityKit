using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host.Transport
{
    public interface IServerConnection
    {
        ServerClientId ClientId { get; }

        void Send(ServerMessage message);
    }
}
