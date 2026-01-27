namespace AbilityKit.Orleans.Gateway.TcpGateway;

public interface ITcpGatewaySessionRegistry
{
    void Register(long connectionId, TcpClientSession session);

    void Unregister(long connectionId);

    void BindToken(string sessionToken, long connectionId);

    void UnbindToken(string sessionToken);

    bool TryGetConnectionIdByToken(string sessionToken, out long connectionId);

    Task<bool> TrySendKickAsync(string sessionToken, string reason, CancellationToken cancellationToken);
}
