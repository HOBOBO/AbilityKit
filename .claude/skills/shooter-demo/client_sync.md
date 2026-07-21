# 客户端同步策略

位置：`view.runtime/Runtime/Client/Synchronization/`

## 三种策略（ShooterClientSyncControllerFactory）

| 策略 | Harness Carrier | 核心 Adapter | 用途 |
|------|----------------|-------------|------|
| **PredictRollback** | `ShooterDemoHarnessCarrier` | `ShooterClientPredictionRuntimeAdapter` + `ShooterPackedSnapshotRollbackProvider` | 客户端预测 + packed snapshot 回滚 |
| **AuthoritativeInterpolation** | `ShooterInterpolationDemoHarnessCarrier` | `ShooterAuthoritativeComparisonDriver` | 纯权威插值（无预测） |
| **HybridHeroPrediction** | `ShooterHybridDemoHarnessCarrier` | （内部） | 本地英雄预测 + 远程权威插值 |

**关键**：不复用 moba 的 `ClientPredictionDriverModule`（底层 ECS 不同）。

## PredictRollback 关键类

- `ShooterClientPredictionRuntimeAdapter`：把客户端预测接到 packed snapshot
- `ShooterPackedSnapshotRollbackProvider`：实现 `IRollbackStateProvider`，提供 packed snapshot 的 Export/Import
- `ShooterStateRecoveryExample`：状态恢复示例

## Drift Recovery 状态机

`ShooterClientRecoveryCoordinator.cs` + `ShooterClientDriftRecoveryPolicy.cs`：

```
Normal ─drift 检测→ CatchUp ─请求全量→ AwaitingFullSnapshot
                                              │
                                              ▼
                                       ApplyingFullSnapshot ─应用完→ Recovered ─→ Normal
```

## Time Anchor 与 Reconnect

- `ShooterTimeAnchorCoordinator` — 客户端时间锚（投影服务器时间）
- `ShooterFastReconnectDriver` + `ShooterReconnectLaunchOptionsBuilder` — 快速重连

## Lag Compensation

`runtime/Runtime/Application/Synchronization/ShooterLagCompensationService.cs` + `view.runtime/Runtime/Hosting/ShooterRemoteLatencyCompensationDiagnostics.cs`：

服务端在命中检测时回滚到玩家发送命令时的世界状态，避免高延迟玩家因"看到的是旧画面"被错误判 miss。

## 框架契约适配

`ShooterClientSyncStrategyMapping` 把示例特定诊断映射到 `com.abilitykit.network.runtime` 框架契约：

- `IClientSyncStrategy<TInput, TSample>`
- `SyncTickResult`
- `SyncReconciliationReport`
- `SyncRecoveryState`

## 网络条件模拟

`view.runtime/Runtime/Network/ShooterNetworkConditionProvider` + `NetworkConditionProfile`：模拟丢包/延迟/乱序。

## Acceptance Lab

`view.runtime/Runtime/Client/Synchronization/ShooterAcceptanceLab` + `ShooterAcceptanceSpecs`：客户端同步策略的验收工具。

## CarrierNetworkLink

`ShooterCarrierNetworkLink`：Harness Carrier 与网络的链接胶水。

## 相关 Editor 面板

无专门 shooter 同步面板（moba 才有 6 个 BattleDebug 帧同步面板）；shooter 用 `ShooterDemoDiagnostics` + `ShooterCrossLayerDiagnostics` + `ShooterReconciliationDiagnosticsStream`。
