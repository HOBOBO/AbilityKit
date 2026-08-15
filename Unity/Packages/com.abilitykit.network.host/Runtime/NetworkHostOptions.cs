using System;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Network.Host
{
    public sealed class NetworkHostOptions
    {
        public int MaxConnections { get; set; } = 128;
        public int MaxPendingRequestsPerSession { get; set; } = 256;
        public TimeSpan IdleTimeout { get; set; } = TimeSpan.Zero;
        public TimeSpan EstablishmentTimeout { get; set; } = TimeSpan.Zero;
        public IFrameCodec FrameCodec { get; set; }
        public IDispatcher CallbackDispatcher { get; set; }
        public IDispatcher IoDispatcher { get; set; }
        public IMonotonicClock Clock { get; set; }
        public IServerRequestHandler RequestHandler { get; set; }
        public IAsyncServerRequestHandler AsyncRequestHandler { get; set; }
        public IChannelAdmissionPolicy AdmissionPolicy { get; set; }
        public Action<NetworkPipelineBuilder> ConfigurePipeline { get; set; }
    }

    public sealed class NetworkPipelineBuilder
    {
        private readonly AbilityKit.Network.Runtime.NetworkPipeline _pipeline;

        internal NetworkPipelineBuilder(AbilityKit.Network.Runtime.NetworkPipeline pipeline)
        {
            _pipeline = pipeline;
        }

        public NetworkPipelineBuilder Use(INetworkMiddleware middleware)
        {
            _pipeline.Add(middleware);
            return this;
        }
    }
}
