using Orleans;

namespace AbilityKit.Orleans.Contracts.FrameSync;

/// <summary>
/// Orleans 服务器侧帧同步通道。
/// 它与状态同步并存，让战斗房间可以选择帧同步或状态同步流程，同时在本地 grain 调用与网关网络请求之间共享协议模型。
/// </summary>
public interface IBattleFrameSyncGrain : IGrainWithStringKey
{
    Task InitializeAsync(FrameSyncStartOptions options);

    Task SubscribeAsync(IFrameSyncObserver observer);

    Task UnsubscribeAsync(IFrameSyncObserver observer);

    Task SubmitInputAsync(ulong worldId, int frame, FrameInputItem input);

    Task<FrameInputSubmitResult> SubmitInputWithResultAsync(ulong worldId, int frame, FrameInputItem input);

    /// <summary>
    /// 请求服务端已缓存的帧输入历史，用于客户端重连后追帧。
    /// 返回 null 表示历史不完整，客户端应回退到全量快照路径。
    /// </summary>
    Task<FrameSyncCatchUpPayload?> RequestCatchUpAsync(FrameSyncCatchUpRequest request);

    /// <summary>
    /// 导出当前战斗的完整帧输入录制，用于离线回放和 desync 复现。
    /// 仅在 <see cref="FrameSyncStartOptions.EnableRecording"/> 开启时可用。
    /// 录制未启用或战斗未开始时返回 null。
    /// </summary>
    Task<FrameSyncRecording?> DumpRecordingAsync();

    /// <summary>
    /// 获取帧同步运行时健康指标，用于运维监控和联调诊断。
    /// </summary>
    Task<FrameSyncMetrics> GetMetricsAsync();

    /// <summary>
    /// 运行时调整 Tick 频率（Hz）。受 <see cref="FrameSyncStartOptions.MinTickRate"/>
    /// 和 <see cref="FrameSyncStartOptions.MaxTickRate"/> 约束。
    /// 返回调整后的实际频率。
    /// </summary>
    Task<int> AdjustTickRateAsync(int targetTickRate);

    /// <summary>
    /// Stops frame progression and releases all runtime state for the battle.
    /// </summary>
    Task DestroyAsync();
}
