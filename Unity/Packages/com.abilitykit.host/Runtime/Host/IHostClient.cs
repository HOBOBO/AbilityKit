using AbilityKit.Ability.Host.Transport;

namespace AbilityKit.Ability.Host
{
    public interface IHostClient
    {
        ServerClientId ClientId { get; }

        void OnMessage(ServerMessage message);
    }
}
