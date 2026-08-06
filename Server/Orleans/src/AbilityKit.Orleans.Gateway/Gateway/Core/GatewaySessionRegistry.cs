using System.Collections.Concurrent;
using AbilityKit.Orleans.Gateway.Abstractions;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Orleans.Gateway.Core;

/// <summary>
/// Gateway 会话注册表
/// </summary>
public sealed class GatewaySessionRegistry : IGatewaySessionRegistry
{
    private readonly ConcurrentDictionary<long, IGatewayTransportSession> _sessions = new();
    private readonly ConcurrentDictionary<string, long> _tokenToConnectionId = new();
    private readonly ConcurrentDictionary<string, long> _accountToConnectionId = new();

    public void Register(long connectionId, IGatewayTransportSession session)
    {
        _sessions[connectionId] = session;
    }

    public void Unregister(long connectionId)
    {
        _sessions.TryRemove(connectionId, out _);
        RemoveBindingsForConnection(_tokenToConnectionId, connectionId);
        RemoveBindingsForConnection(_accountToConnectionId, connectionId);
    }

    public bool TryGetSession(long connectionId, out IGatewayTransportSession? session)
    {
        return _sessions.TryGetValue(connectionId, out session);
    }

    public bool TryGetConnectionIdByToken(string token, out long connectionId)
    {
        return _tokenToConnectionId.TryGetValue(token, out connectionId);
    }

    public bool TryGetConnectionIdByAccount(string accountId, out long connectionId)
    {
        return _accountToConnectionId.TryGetValue(accountId, out connectionId);
    }

    public void BindToken(string token, long connectionId)
    {
        _tokenToConnectionId[token] = connectionId;
    }

    public void UnbindToken(string token)
    {
        _tokenToConnectionId.TryRemove(token, out _);
    }

    public void BindAccount(string accountId, long connectionId)
    {
        _accountToConnectionId[accountId] = connectionId;
    }

    public void UnbindAccount(string accountId)
    {
        _accountToConnectionId.TryRemove(accountId, out _);
    }

    public async Task<bool> TrySendKickAsync(string token, string reason, CancellationToken cancellationToken)
    {
        if (!_tokenToConnectionId.TryGetValue(token, out var connectionId))
            return false;

        if (!_sessions.TryGetValue(connectionId, out var session))
            return false;

        try
        {
            await session.SendServerPushAsync(
                RoomGatewayOpCodes.SessionKicked,
                System.Text.Encoding.UTF8.GetBytes(reason),
                cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TrySendPushToAccountAsync(string accountId, uint opCode, byte[] payload, CancellationToken cancellationToken = default)
    {
        if (!_accountToConnectionId.TryGetValue(accountId, out var connectionId))
            return false;

        if (!_sessions.TryGetValue(connectionId, out var session))
            return false;

        try
        {
            await session.SendServerPushAsync(opCode, payload, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TrySendPushToTokenAsync(string token, uint opCode, byte[] payload, CancellationToken cancellationToken = default)
    {
        if (!_tokenToConnectionId.TryGetValue(token, out var connectionId))
            return false;

        if (!_sessions.TryGetValue(connectionId, out var session))
            return false;

        try
        {
            await session.SendServerPushAsync(opCode, payload, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void RemoveBindingsForConnection(
        ConcurrentDictionary<string, long> bindings,
        long connectionId)
    {
        foreach (var binding in bindings)
        {
            if (binding.Value == connectionId)
            {
                bindings.TryRemove(binding);
            }
        }
    }
}
