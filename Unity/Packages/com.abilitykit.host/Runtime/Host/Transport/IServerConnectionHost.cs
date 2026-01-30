using AbilityKit.Ability.Host;

namespace AbilityKit.Ability.Host.Transport
{
    public interface IServerConnectionHost
    {
        void Connect(IServerConnection connection);
        void Disconnect(ServerClientId clientId);
    }
}
