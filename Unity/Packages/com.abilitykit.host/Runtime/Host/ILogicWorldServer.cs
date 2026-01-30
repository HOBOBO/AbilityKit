using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;

namespace AbilityKit.Ability.Host
{
    public interface ILogicWorldServer : IHostServer, IPlayerSessionHost
    {
        void Connect(ILogicServerClient client);
    }
}
