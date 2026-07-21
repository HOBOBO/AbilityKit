using AbilityKit.Orleans.Gateway.Abstractions;
using Microsoft.Extensions.Options;

namespace AbilityKit.Orleans.Gateway.Core;

/// <summary>
/// Gateway 请求路由器
/// </summary>
public sealed class GatewayRequestRouter : IGatewayRequestRouter
{
    private readonly IGatewayHandlerRegistry _registry;
    private readonly ILogger<GatewayRequestRouter> _logger;
    private readonly int _requestTimeoutMs;

    public GatewayRequestRouter(
        IGatewayHandlerRegistry registry,
        IOptions<GatewayOptions> options,
        ILogger<GatewayRequestRouter> logger)
    {
        _registry = registry;
        _logger = logger;
        _requestTimeoutMs = options.Value.RequestTimeoutMs;
    }

    public async Task<GatewayResponse> RouteAsync(
        GatewaySessionContext context,
        uint opCode,
        uint seq,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        var handler = _registry.GetHandler(opCode);
        if (handler == null)
        {
            return GatewayResponse.Error(seq, GatewayStatusCode.UnhandledOpCode);
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (_requestTimeoutMs > 0) cts.CancelAfter(_requestTimeoutMs);

        var request = new GatewayRequest(seq, payload);

        try
        {
            return await handler.HandleAsync(request, context, cts.Token);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                exception,
                "Gateway request timed out. OpCode={OpCode}, Seq={Seq}, ConnectionId={ConnectionId}, PayloadLength={PayloadLength}",
                opCode,
                seq,
                context.ConnectionId,
                payload.Length);
            return GatewayResponse.Error(seq, GatewayStatusCode.Timeout);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Gateway request failed. OpCode={OpCode}, Seq={Seq}, ConnectionId={ConnectionId}, PayloadLength={PayloadLength}",
                opCode,
                seq,
                context.ConnectionId,
                payload.Length);
            return GatewayResponse.Error(seq, GatewayStatusCode.Exception);
        }
    }
}
