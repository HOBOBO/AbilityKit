using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AbilityKit.Orleans.Gateway.TcpGateway;

public sealed class TcpGatewayRequestRouter
{
    private readonly IOptions<TcpGatewayOptions> _options;
    private readonly ILogger<TcpGatewayRequestRouter> _logger;
    private readonly IReadOnlyList<ITcpGatewayRequestHandler> _handlers;

    public TcpGatewayRequestRouter(
        IEnumerable<ITcpGatewayRequestHandler> handlers,
        IOptions<TcpGatewayOptions> options,
        ILogger<TcpGatewayRequestRouter> logger)
    {
        _handlers = handlers.ToArray();
        _options = options;
        _logger = logger;
    }

    public async Task<TcpGatewayResponseEnvelope> RouteAsync(TcpClientSessionContext context, NetworkPacketHeader requestHeader, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        var opts = _options.Value;

        var handler = _handlers.FirstOrDefault(h => h.CanHandle(requestHeader.OpCode));
        if (handler == null)
        {
            _logger.LogWarning("Unhandled opcode: {OpCode}", requestHeader.OpCode);
            return new TcpGatewayResponseEnvelope(TcpGatewayStatusCode.UnhandledOpCode, ReadOnlyMemory<byte>.Empty);
        }

        var timeoutMs = opts.RequestTimeoutMs;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeoutMs > 0) cts.CancelAfter(timeoutMs);

        try
        {
            return await handler.HandleAsync(context, requestHeader, payload, cts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Request timeout. OpCode={OpCode} Seq={Seq}", requestHeader.OpCode, requestHeader.Seq);
            return new TcpGatewayResponseEnvelope(TcpGatewayStatusCode.Timeout, ReadOnlyMemory<byte>.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Request handler exception. OpCode={OpCode} Seq={Seq}", requestHeader.OpCode, requestHeader.Seq);
            return new TcpGatewayResponseEnvelope(TcpGatewayStatusCode.Exception, Encoding.UTF8.GetBytes(ex.ToString()));
        }
    }
}
