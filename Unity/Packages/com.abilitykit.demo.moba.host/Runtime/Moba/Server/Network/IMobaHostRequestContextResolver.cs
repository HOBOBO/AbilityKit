using AbilityKit.Network.Host;
using AbilityKit.Protocol.Moba.Generated.GatewayFrameSync;

namespace AbilityKit.Ability.Host.Extensions.Moba.Server.Network
{
    public interface IMobaHostRequestContextResolver
    {
        bool TryResolve(
            IServerNetworkSession session,
            in WireSubmitFrameInputReq request,
            out MobaHostSessionBinding binding);
    }

    public sealed class BoundMobaHostRequestContextResolver : IMobaHostRequestContextResolver
    {
        public static readonly BoundMobaHostRequestContextResolver Instance =
            new BoundMobaHostRequestContextResolver();

        private BoundMobaHostRequestContextResolver()
        {
        }

        public bool TryResolve(
            IServerNetworkSession session,
            in WireSubmitFrameInputReq request,
            out MobaHostSessionBinding binding)
        {
            return MobaHostSessionBindings.TryGet(session, out binding);
        }
    }
}
