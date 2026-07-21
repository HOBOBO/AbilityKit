# 表现层（Presentation）

位置：`view.runtime/Runtime/Presentation/`

## PresentationSession（核心）

`ShooterPresentationSession` + `ShooterPresentationSessionHost` + `ShooterPresentationSessionContext` + `ShooterPresentationSessionResolver`：

- `IShooterPresentationTransport` / `IShooterPresentationClient`：表现层传输契约
- `ShooterSnapshotViewModel` / `ShooterSnapshotViewModelMapper`：把 snapshot 映射为 ViewModel
- `ShooterSnapshotViewAdapter`：适配

## Snapshot 流

`Presentation/Snapshot/`：

- `ShooterSnapshotStream`：快照流（含插值缓冲）
- `ShooterSnapshotSamplingPolicy`：采样策略
- `ShooterReconciliationDiagnosticsStream`：reconciliation 诊断流

## View 投影

`Presentation/View/`：

- `ShooterSnapshotViewProjection`：snapshot → 视图投影
- `ShooterSnapshotViewBinder`：传统 GameObject 绑定
- `ShooterDotsSnapshotViewBinder`：Unity DOTS 渲染绑定
- `ShooterSnapshotViewBatch`：批量更新
- `ShooterViewEntityStore`：视图实体存储
- `IShooterSnapshotViewSink` / `ShooterNullSnapshotViewSink` / `ShooterProjectedSnapshotViewSink`
- `ShooterViewRenderBackendFactory`：渲染后端工厂

## ViewEvents

`Presentation/ViewEvents/`：

- `IShooterViewEventSink`：事件接收
- `ShooterViewEventSink`：默认实现
- `DebugShooterViewEventSink`：调试实现（含详细日志）

事件类型（`ShooterEventType`）：`Hit` / `Fire` / `MatchVictory` / `MatchDefeat` / `MatchEnded`

## EntityViewModel

`Presentation/EntityViewModel/`：

- `ShooterEntityBase`：基类
- `ShooterPlayerEntity`：玩家视图实体
- `ShooterBulletEntity`：子弹视图实体
- `ShooterEntityFactory`：工厂
- `ShooterEntityQuery` / `IShooterEntityQuery`：查询
- `ShooterEntityLookup`：lookup
- `ShooterTransformComponent`：transform
- `ShooterEntityFeature`：feature 抽象

## Hosting 诊断

`view.runtime/Runtime/Hosting/`：

- `ShooterHostPorts`：host 端口集合
- `ShooterHostDiagnostics`：host 诊断
- `ShooterCrossLayerDiagnostics`：跨层诊断
- `ShooterRemoteLatencyCompensationDiagnostics`：远程延迟补偿诊断

## 与 Presentation 的协作

```
Client/Synchronization/ShooterClientSnapshotApplyCoordinator
    ↓ 应用 snapshot
Presentation/Snapshot/ShooterSnapshotStream
    ↓ 采样
Presentation/View/ShooterSnapshotViewProjection
    ↓ 投影
Presentation/View/ShooterSnapshotViewBinder（或 DotsBinder）
    ↓ 绑定到 GameObject / DOTS Entity
Presentation/EntityViewModel/{PlayerEntity, BulletEntity}
    ↓ 表现层副作用
Presentation/ViewEvents/ShooterViewEventSink
    ↓ ViewEvent（Hit/Fire/MatchVictory/...）
```
