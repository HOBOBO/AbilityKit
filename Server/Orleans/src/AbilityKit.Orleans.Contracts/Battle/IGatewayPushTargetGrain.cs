using Orleans;

namespace AbilityKit.Orleans.Contracts.Battle;

/// <summary>
/// Gateway 推送目标 Grain 接口。
/// 每个账号使用独立的字符串 Grain Key，避免慢连接阻塞其他账号的推送。
/// </summary>
public interface IGatewayPushTargetGrain : IGrainWithStringKey
{
    /// <summary>
    /// 向指定账号推送消息
    /// </summary>
    Task<bool> PushToAccountAsync(string accountId, uint opCode, byte[] payload);

    /// <summary>
    /// 向指定 Token 推送消息
    /// </summary>
    Task<bool> PushToTokenAsync(string token, uint opCode, byte[] payload);
}
