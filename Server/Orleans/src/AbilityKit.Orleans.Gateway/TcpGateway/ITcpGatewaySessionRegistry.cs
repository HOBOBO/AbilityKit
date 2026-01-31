namespace AbilityKit.Orleans.Gateway.TcpGateway;

public interface ITcpGatewaySessionRegistry
{
    void Register(long connectionId, TcpClientSession session);

    void Unregister(long connectionId);

    bool TryGetSession(long connectionId, out TcpClientSession session);

    void BindAccount(string accountId, long connectionId);

    void UnbindAccount(string accountId);

    bool TryGetConnectionIdByAccount(string accountId, out long connectionId);

    void BindToken(string sessionToken, long connectionId);

    void UnbindToken(string sessionToken);

    bool TryGetConnectionIdByToken(string sessionToken, out long connectionId);

    Task<bool> TrySendKickAsync(string sessionToken, string reason, CancellationToken cancellationToken);
}
