using AbilityKit.Ability.Host.Extensions.Moba.Server.Network;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Host.Network;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;
using AbilityKit.Network.Protocol;
using AbilityKit.Protocol.Moba.Generated.GatewayFrameSync;
using Xunit;

namespace AbilityKit.Demo.Moba.Host.Tests;

public sealed class MobaHostNetworkRequestHandlerTests
{
    [Fact]
    public async Task SubmitFrameInput_RejectsSessionWithoutAuthoritativeBinding()
    {
        using var network = new InProcessHostNetwork(
            UnsupportedHostMessageCodec.Instance,
            new MobaHostNetworkRequestHandler());
        var runtime = new HostRuntime(new WorldManager(new EmptyWorldFactory()));
        network.Connections.Attach(runtime);
        network.Start();

        using var client = network.CreateClientConnection();
        var responseCompletion = new TaskCompletionSource<WireSubmitFrameInputRes>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        client.PacketReceived += (opCode, sequence, payload) =>
        {
            if (opCode == OpCodes.SubmitFrameInput && sequence == 7)
            {
                responseCompletion.TrySetResult(WireCustomBinary.DeserializeSubmitFrameInputRes(payload));
            }
        };
        client.Open("inprocess", 1);

        var request = new WireSubmitFrameInputReq(123, 456, 999, 10, 1, new byte[] { 1 });
        client.Send(
            OpCodes.SubmitFrameInput,
            WireCustomBinary.Serialize(in request),
            (ushort)NetworkPacketFlags.Request,
            7);

        var response = await responseCompletion.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(response.Accepted);
        Assert.Equal((int)MobaHostInputReasonCode.SessionNotBound, response.ReasonCode);
    }

    private sealed class EmptyWorldFactory : IWorldFactory
    {
        public IWorld Create(WorldCreateOptions options) => throw new NotSupportedException();
    }
}
