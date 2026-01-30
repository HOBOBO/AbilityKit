using AbilityKit.Ability.Host.Transport;

namespace AbilityKit.Ability.Host
{
    public interface IHostServer : IWorldHost, IServerConnectionHost
    {
        void Connect(IHostClient client);
    }
}
