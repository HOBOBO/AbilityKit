using System.Diagnostics;
using AbilityKit.Protocol.Moba.GatewayTimeSync;
using Microsoft.Extensions.Options;
using Orleans;

namespace AbilityKit.Orleans.Gateway.TcpGateway;

public sealed class TimeSyncRequestHandler : ITcpGatewayRequestHandler
{
    private readonly IOptions<TcpGatewayOptions> _options;

    public TimeSyncRequestHandler(IOptions<TcpGatewayOptions> options)
    {
        _options = options;
    }

    public bool CanHandle(uint opCode) => opCode == _options.Value.TimeSyncOpCode;

    public Task<TcpGatewayResponseEnvelope> HandleAsync(TcpClientSessionContext context, NetworkPacketHeader header, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        var req = WireTimeSyncBinary.DeserializeTimeSyncReq(payload.Span);

        var serverNowTicks = Stopwatch.GetTimestamp();
        var serverFreq = Stopwatch.Frequency;

        var res = new WireTimeSyncRes(req.ClientSendTicks, serverNowTicks, serverFreq);
        var respPayload = WireTimeSyncBinary.Serialize(in res);
        return Task.FromResult(new TcpGatewayResponseEnvelope(TcpGatewayStatusCode.Ok, respPayload));
    }
}
