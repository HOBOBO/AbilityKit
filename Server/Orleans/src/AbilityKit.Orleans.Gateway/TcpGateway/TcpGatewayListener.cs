using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AbilityKit.Orleans.Gateway.TcpGateway;

public sealed class TcpGatewayListener : BackgroundService
{
    private readonly IOptions<TcpGatewayOptions> _options;
    private readonly ILogger<TcpGatewayListener> _logger;
    private readonly TcpGatewayRequestRouter _router;
    private readonly ITcpGatewaySessionRegistry _registry;
    private TcpListener? _listener;
    private readonly ConcurrentDictionary<long, TcpClientSession> _sessions = new();
    private long _nextSessionId;

    public TcpGatewayListener(IOptions<TcpGatewayOptions> options, TcpGatewayRequestRouter router, ITcpGatewaySessionRegistry registry, ILogger<TcpGatewayListener> logger)
    {
        _options = options;
        _router = router;
        _registry = registry;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = _options.Value;
        if (!opts.Enabled)
        {
            _logger.LogInformation("TcpGateway is disabled.");
            return;
        }

        var ip = IPAddress.TryParse(opts.Host, out var parsed) ? parsed : IPAddress.Any;
        _listener = new TcpListener(ip, opts.Port);
        _listener.Start();

        _logger.LogInformation("TcpGateway listening on {Host}:{Port}", opts.Host, opts.Port);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(stoppingToken);
                client.NoDelay = true;

                var sessionId = Interlocked.Increment(ref _nextSessionId);

                _ = Task.Run(async () =>
                {
                    var sessionLogger = _logger;
                    var session = new TcpClientSession(sessionId, client, opts, _router, sessionLogger);

                    _sessions[sessionId] = session;
                    _registry.Register(sessionId, session);
                    _logger.LogInformation("Tcp session {SessionId} accepted. ActiveSessions={Count}", sessionId, _sessions.Count);

                    await session.RunAsync(stoppingToken);

                    _sessions.TryRemove(sessionId, out _);
                    _registry.Unregister(sessionId);
                    _logger.LogInformation("Tcp session {SessionId} ended. ActiveSessions={Count}", sessionId, _sessions.Count);
                }, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _listener.Stop();
        }
    }
}
