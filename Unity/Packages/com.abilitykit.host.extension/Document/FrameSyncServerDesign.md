# FrameSync 服务端设计文档（v0.1.0）

**最后更新**: 2026-08-01  
**覆盖范围**: `BattleFrameSyncGrain` (Orleans) + 帧同步合约 + Gateway 处理器 + 协议层

## 1. 架构概览

```
                    ┌─ Orleans Silo ──────────────────────────┐
                    │                                          │
  Client A ──TCP──→ │  Gateway ──→ SubmitFrameInputHandler     │
  Client B ──TCP──→ │      │         (opcode 2001)             │
  Spectator ─TCP──→ │      │                                   │
                    │      ├──→ CatchUpRequestHandler          │
                    │      │      (opcode 2002)                │
                    │      ├──→ GetFrameSyncMetricsHandler     │
                    │      │      (opcode 2003)                │
                    │      ├──→ SpectatorSubscribeHandler      │
                    │      │      (opcode 2004)                │
                    │      │                                   │
                    │      └──→ GatewayFrameSyncSubscriptionMgr│
                    │              │                            │
                    │              ↓                            │
                    │       BattleFrameSyncGrain                │
                    │       ┌─ _inputsByFrame (当前帧输入)      │
                    │       ├─ _inputHistory  (CatchUp 环形缓冲)│
                    │       ├─ _fullRecording (录制模式)        │
                    │       ├─ _observers     (IFrameSyncObserver)│
                    │       └─ OnTickAsync → 广播 FramePushed   │
                    │              │                            │
                    │              ↓ (BattleWorldWithFrameSync) │
                    │       BattleLogicHostGrain.TickFrameAsync │
                    │       ┌─ MobaBattleRuntimeSession.Tick    │
                    │       │   ├─ 生成 Bot 输入                │
                    │       │   ├─ _runtimePort.Submit          │
                    │       │   └─ _battleWorld.Tick            │
                    │       └─ PushSnapshot + FlushDeliveries   │
                    └──────────────────────────────────────────┘
```

## 2. BattleFrameSyncGrain 能力

### 2.1 帧同步中继

每 `tickInterval` (默认 1/30s) 驱动。收集各客户端提交的输入，按时隙广播 `FramePushedEvent` 给所有 `IFrameSyncObserver`。

关键约束：
- `MaxCatchUpFramesPerTimer = 5` — 单次定时器回调最多补发帧数
- `MaxFutureLeadFrames = 120` — 拒绝超前服务端 120 帧以上的输入
- 输入存储 `Dictionary<int, List<FrameInputItem>>`，消费即删除

### 2.2 CatchUp 追帧（v0.1.0 新增）

- `_inputHistory` — `SortedDictionary<int, List<FrameInputItem>>` 环形缓冲
- `MaxHistoryFrames = 600` — 历史容量，超出自动修剪
- `RequestCatchUpAsync(request)` — 按帧范围提取输入，任一帧缺失返回 null
- Gateway 通过 `CatchUpRequestHandler` 暴露

用途：客户端断线重连 / 观战者加入房间时追帧。

### 2.3 帧录制（v0.1.0 新增）

`EnableRecording = true` 时，`_fullRecording` (List) 全量保存每帧输入不修剪。上限 `MaxRecordingFrames = 10800` (3h@60fps)，达上限静默停止。通过 `DumpRecordingAsync()` 导出完整 `FrameSyncRecording` (启动参数 + 全帧输入 + 时间戳)。

### 2.4 健康监控（v0.1.0 新增）

`GetMetricsAsync()` 返回 `FrameSyncMetrics`：
- CurrentFrame, TickRate, ObserverCount
- AvgTickDeltaMs, LastTickDeltaMs, EffectiveHz
- TotalInputsReceived, CatchUpHistoryFrames, RecordingFrameCount
- UptimeSeconds

### 2.5 动态 TickRate（v0.1.0 新增）

`AdjustTickRateAsync(target)` — clamp 到 `[MinTickRate, MaxTickRate]` 范围，重算 `_tickInterval` 和 `_nextTickDueUtc`。调用方需自行广播新频率给客户端。

### 2.6 BattleWorldWithFrameSync 混合模式（v0.1.0 新增）

`ServerBattleRuntimeMode.BattleWorldWithFrameSync` 模式下：
- `RoomFrameSyncRoute` 同时创建 `BattleFrameSyncGrain` + `BattleLogicHostGrain`
- `BattleFrameSyncGrain.OnTickAsync` 每帧调用 `battleHost.TickFrameAsync()`
- `BattleLogicHostGrain` 在 `_externalTickMode` 下跳过自身定时器
- 帧时钟统一由 `BattleFrameSyncGrain` 驱动，避免双定时器漂移

### 2.7 观战支持（v0.1.0 新增）

`SpectatorSubscribeHandler` 注册仅接收广播的 `IFrameSyncObserver`。观战者不参与房间成员身份检查，无法通过 `SubmitFrameInputHandler` 提交输入。

## 3. 合约定义

### FrameSyncStartOptions (Orleans [GenerateSerializer])

| Id | 字段 | 类型 | 默认 | 说明 |
|----|------|------|------|------|
| 0 | RoomId | ulong | — | |
| 1 | WorldId | ulong | — | |
| 2 | TickRate | int | 30 | |
| 3 | BattleId | string? | null | 战斗 ID (关联 BLH) |
| 4 | SyncTemplateId | string? | null | |
| 5 | RuntimeMode | int | 0 | 0=BattleWorld, 1=FrameRelayOnly, 2=BattleWorldWithFrameSync |
| 6 | EnableRecording | bool | false | |
| 7 | MinTickRate | int | 10 | |
| 8 | MaxTickRate | int | 60 | |

### 新增类型（v0.1.0）

- `FrameSyncCatchUpRequest` / `FrameSyncCatchUpPayload` — CatchUp 请求/响应
- `FrameSyncRecording` — 帧录制数据
- `FrameSyncMetrics` — 运行时健康指标 (12 字段)
- `BattleTickFrameResult` — 外部 Tick 结果 (Frame, WorldTicked, StateHash)

## 4. 协议 (protocol.moba/GatewayFrameSync)

| OpCode | 名称 | 方向 | Wire 类型 |
|--------|------|------|-----------|
| 2001 | SubmitFrameInput | C→S | WireSubmitFrameInputReq / Res |
| 2002 | CatchUpRequest | C→S | WireCatchUpRequest |
| 2003 | GetMetricsRequest | C→S | - (无 payload，从 session 推断) |
| 2004 | SpectatorSubscribe | C→S | payload: roomId (ulong) |
| 9001 | FramePushed | S→C | WireFramePushedPush |
| 9002 | CatchUpPayloadPush | S→C | WireCatchUpPayloadPush |
| 9003 | MetricsResponse | S→C | WireFrameSyncMetrics |
| - | SpectatorSubscribeRes | S→C | WireSpectatorSubscribeRes (作为请求响应返回) |
