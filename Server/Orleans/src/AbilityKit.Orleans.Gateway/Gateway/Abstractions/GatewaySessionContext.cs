namespace AbilityKit.Orleans.Gateway;

/// <summary>
/// Gateway 会话上下文
/// </summary>
public sealed class GatewaySessionContext
{
    public long ConnectionId { get; }
    public string SessionId => ConnectionId.ToString();
    /// <summary>
    /// 本次连接的服务端来源标识。连接编号复用时也不复用此标识。
    /// </summary>
    internal Guid ConnectionGeneration { get; } = Guid.NewGuid();
    internal volatile ScopedStateSyncInputIdentity? ScopedStateSyncInput;
    public string? AccountId { get; set; }
    public string? RoomId { get; set; }
    public string? SessionToken { get; set; }

    public GatewaySessionContext(long connectionId)
    {
        ConnectionId = connectionId;
    }
}

/// <summary>
/// 已完成订阅的输入身份。随连接销毁，不使用客户端代际为准入计数开新槽。
/// </summary>
internal sealed record ScopedStateSyncInputIdentity(string AccountId, string BattleId, ulong WorldId,
    uint PlayerId, Guid InputGeneration)
{
    internal bool Matches(string accountId, string battleId, ulong worldId, uint playerId, Guid inputGeneration) =>
        AccountId == accountId && BattleId == battleId && WorldId == worldId && PlayerId == playerId &&
        InputGeneration == inputGeneration;
}
