using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AbilityKit.Orleans.Gateway.TcpGateway;

public sealed class TcpGatewaySessionRegistry : ITcpGatewaySessionRegistry
{
    private readonly ConcurrentDictionary<long, TcpClientSession> _connections = new();
    private readonly ConcurrentDictionary<string, long> _tokenToConnection = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, long> _accountToConnection = new(StringComparer.Ordinal);

    private readonly IOptions<TcpGatewayOptions> _options;
    private readonly ILogger<TcpGatewaySessionRegistry> _logger;

    public TcpGatewaySessionRegistry(IOptions<TcpGatewayOptions> options, ILogger<TcpGatewaySessionRegistry> logger)
    {
        _options = options;
        _logger = logger;
    }

    public void Register(long connectionId, TcpClientSession session)
    {
        _connections[connectionId] = session;
    }

    public void Unregister(long connectionId)
    {
        _connections.TryRemove(connectionId, out _);

        foreach (var kv in _tokenToConnection)
        {
            if (kv.Value == connectionId)
            {
                _tokenToConnection.TryRemove(kv.Key, out _);
            }
        }

        foreach (var kv in _accountToConnection)
        {
            if (kv.Value == connectionId)
            {
                _accountToConnection.TryRemove(kv.Key, out _);
            }
        }
    }

    public bool TryGetSession(long connectionId, out TcpClientSession session)
    {
        return _connections.TryGetValue(connectionId, out session);
    }

    public void BindAccount(string accountId, long connectionId)
    {
        if (string.IsNullOrWhiteSpace(accountId)) return;
        _accountToConnection[accountId] = connectionId;
    }

    public void UnbindAccount(string accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId)) return;
        _accountToConnection.TryRemove(accountId, out _);
    }

    public bool TryGetConnectionIdByAccount(string accountId, out long connectionId)
    {
        if (string.IsNullOrWhiteSpace(accountId))
        {
            connectionId = 0;
            return false;
        }
        return _accountToConnection.TryGetValue(accountId, out connectionId);
    }

    public void BindToken(string sessionToken, long connectionId)
    {
        if (string.IsNullOrWhiteSpace(sessionToken)) return;
        _tokenToConnection[sessionToken] = connectionId;
    }

    public void UnbindToken(string sessionToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken)) return;
        _tokenToConnection.TryRemove(sessionToken, out _);
    }

    public bool TryGetConnectionIdByToken(string sessionToken, out long connectionId)
    {
        return _tokenToConnection.TryGetValue(sessionToken, out connectionId);
    }

    public async Task<bool> TrySendKickAsync(string sessionToken, string reason, CancellationToken cancellationToken)
    {
        if (!TryGetConnectionIdByToken(sessionToken, out var connectionId))
        {
            return false;
        }

        if (!_connections.TryGetValue(connectionId, out var session))
        {
            _tokenToConnection.TryRemove(sessionToken, out _);
            return false;
        }

        try
        {
            var payload = TcpGatewayJson.Serialize(new { sessionToken, reason });
            await session.SendServerPushAsync(_options.Value.KickPushOpCode, payload, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send kick push. Token={Token} ConnectionId={ConnectionId}", sessionToken, connectionId);
            return false;
        }
    }
}
