# FrameSync 服务端能力（v0.1.0 新增）

源文件：`Server/Orleans/src/AbilityKit.Orleans.Grains/FrameSync/BattleFrameSyncGrain.cs`  
合约：`Server/Orleans/src/AbilityKit.Orleans.Contracts/FrameSync/` (IBattleFrameSyncGrain, FrameSyncModels)  
Gateway：`Server/Orleans/src/AbilityKit.Orleans.Gateway/Gateway/Handlers/` (CatchUpRequestHandler, GetFrameSyncMetricsHandler, SpectatorSubscribeHandler)

## BattleFrameSyncGrain 完整接口

```csharp
public interface IBattleFrameSyncGrain : IGrainWithStringKey
{
    Task InitializeAsync(FrameSyncStartOptions options);
    Task SubscribeAsync(IFrameSyncObserver observer);       // 玩家/观战者订阅
    Task UnsubscribeAsync(IFrameSyncObserver observer);
    Task<FrameInputSubmitResult> SubmitInputWithResultAsync(...);
    
    // v0.1.0 新增
    Task<FrameSyncCatchUpPayload?> RequestCatchUpAsync(FrameSyncCatchUpRequest request);
    Task<FrameSyncRecording?> DumpRecordingAsync();
    Task<FrameSyncMetrics> GetMetricsAsync();
    Task<int> AdjustTickRateAsync(int targetTickRate);
}
```

## 能力矩阵

| 能力 | 描述 | 状态 |
|------|------|------|
| 帧输入中继 | 收集客户端输入，按帧广播 FramePushedEvent | ✅ 原有 |
| **CatchUp 追帧** | `_inputHistory` SortedDictionary 环形缓冲 (MaxHistoryFrames=600)，`RequestCatchUpAsync` 查询 | ✅ v0.1.0 新增 |
| **帧录制** | `EnableRecording=true` 时 `_fullRecording` 全量保存不修剪，`DumpRecordingAsync` 导出 | ✅ v0.1.0 新增 |
| **健康监控** | `GetMetricsAsync` 返回结构化 `FrameSyncMetrics`（12 字段） | ✅ v0.1.0 新增 |
| **动态 TickRate** | `AdjustTickRateAsync [10,60]Hz` 范围 clamp | ✅ v0.1.0 新增 |
| **BattleWorldWithFrameSync** | 混合模式：`BattleFrameSyncGrain` 每帧 `await battleHost.TickFrameAsync()` 驱动权威世界 | ✅ v0.1.0 新增 |
| **观战支持** | `SpectatorSubscribeHandler` 注册仅接收广播的 observer | ✅ v0.1.0 新增 |

## FrameSyncStartOptions（v0.1.0 扩展）

| 字段 | Id | 默认值 | 说明 |
|------|-----|--------|------|
| RoomId | 0 | — | 房间 ID |
| WorldId | 1 | — | 世界 ID |
| TickRate | 2 | 30 | 基础 tick 频率 |
| BattleId | 3 | null | 战斗 ID（关联 BattleLogicHostGrain） |
| SyncTemplateId | 4 | null | 同步模板 ID |
| RuntimeMode | 5 | 0 | 0=BattleWorld, 1=FrameRelayOnly, 2=BattleWorldWithFrameSync |
| EnableRecording | 6 | false | 是否启用全量帧录制 |
| MinTickRate | 7 | 10 | 动态调整下限 |
| MaxTickRate | 8 | 60 | 动态调整上限 |

## 关键 Gateway Handler

| Handler | OpCode | 方向 | 说明 |
|---------|--------|------|------|
| SubmitFrameInputHandler | 2001 | C→S | 客户端提交帧输入 |
| CatchUpRequestHandler | 2002 | C→S | 请求追帧输入历史 |
| GetFrameSyncMetricsHandler | 2003 | C→S | 查询运行时指标 |
| SpectatorSubscribeHandler | 2004 | C→S | 观战者订阅 |
| FramePushed (push) | 9001 | S→C | 帧输入广播 |
| CatchUpPayloadPush (push) | 9002 | S→C | 追帧数据推送 |
| MetricsResponse (push) | 9003 | S→C | 指标响应 |

## BattleWorldWithFrameSync 模式

MOBA 默认模板从 `FrameRelayOnly` 改为 `BattleWorldWithFrameSync`：
- `RoomFrameSyncRoute` 同时创建 `BattleFrameSyncGrain`（帧时钟）和 `BattleLogicHostGrain`（世界模拟）
- `BattleFrameSyncGrain.OnTickAsync` 每帧调用 `battleHost.TickFrameAsync(worldId, frame, delta, inputs)`
- `BattleLogicHostGrain` 在 `_externalTickMode` 下跳过自身的 `StartBattleTimer()`
- 帧计数器由 `BattleFrameSyncGrain` 统一驱动，避免双定时器漂移
