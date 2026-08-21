#nullable enable

namespace AbilityKit.Network.Runtime.Observability
{
    public interface INetworkTrafficObserver
    {
        void OnTraffic(NetworkTrafficEvent trafficEvent);
    }
}
