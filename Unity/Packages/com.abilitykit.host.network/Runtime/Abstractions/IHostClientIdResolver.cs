using AbilityKit.Network.Host;

namespace AbilityKit.Ability.Host.Network
{
    public interface IHostClientIdResolver
    {
        ServerClientId Resolve(IServerNetworkSession session);
    }

    public sealed class ChannelHostClientIdResolver : IHostClientIdResolver
    {
        public static readonly ChannelHostClientIdResolver Instance = new ChannelHostClientIdResolver();

        private ChannelHostClientIdResolver()
        {
        }

        public ServerClientId Resolve(IServerNetworkSession session)
        {
            return new ServerClientId(session.Id);
        }
    }
}
