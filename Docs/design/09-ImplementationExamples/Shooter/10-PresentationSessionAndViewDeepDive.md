# Shooter Presentation Session 与 View Pipeline 深潜

> 文档类型：项目示例深潜
> 事实基线：2026-08-16
>
> 本文补充 Shooter 示例中尚未单独展开的表现会话、快照流、投影、绑定器、插值播放与权威对比诊断。它解释服务端或本地模拟产出的 snapshot 如何变成表现层可消费的 ViewModel，以及 reconnect、lag compensation、网络模拟等诊断如何进入验收链路。

## 1. 设计目标

Shooter View Runtime 的表现层不是直接读取战斗世界，而是通过稳定的 snapshot/view pipeline 解耦。

| 目标 | 说明 | 代表源码 |
|------|------|----------|
| 网络输入解码 | Gateway push、packed snapshot、pure-state snapshot 统一进入 facade | `ShooterPresentationFacade`、`ShooterGatewaySnapshotDecoder` |
| 状态转 ViewModel | runtime snapshot 被适配成 view batch 与 view model | `ShooterSnapshotViewAdapter`、`ShooterSnapshotViewModelMapper` |
| 流式播放 | 快照进入环形缓冲，可按 playback frame 采样和插值 | `ShooterSnapshotStream`、`ShooterSnapshotSamplingPolicy` |
| 投影存储 | batch 增量应用到 view entity store，支持 full snapshot 替换缺失实体 | `ShooterSnapshotViewProjection`、`ShooterViewEntityStore` |
| 渲染绑定 | 根据渲染后端绑定到 GameObject 或 DOTS sink | `ShooterSnapshotViewBinder`、`ShooterDotsSnapshotViewBinder` |
| 会话生命周期 | presentation facade、binder、client connection 由 session/context 管理 | `ShooterPresentationSession`、`ShooterPresentationSessionContext` |
| 验收诊断 | 权威对比、网络条件、lag compensation、fast reconnect 形成独立诊断层 | `ShooterAuthoritativeComparisonDriver`、`ShooterFastReconnectDriver` |

## 2. 表现管线全景

```mermaid
flowchart TB
    subgraph Input["Snapshot 输入"]
        GatewayPush["Gateway opCode + payload"]
        Packed["Packed Snapshot"]
        Pure["Pure-state Snapshot"]
        Local["Local Prediction Snapshot"]
    end

    subgraph Facade["Presentation Facade"]
        Decoder["ShooterGatewaySnapshotDecoder"]
        PureSync["ShooterPureStateSnapshotSyncController"]
        Adapter["ShooterSnapshotViewAdapter"]
        Reconcile["ShooterReconciliationDiagnosticsStream"]
    end

    subgraph Stream["View Stream"]
        Batch["ShooterSnapshotViewBatch"]
        SnapshotStream["ShooterSnapshotStream"]
        Sampling["ShooterSnapshotSamplingPolicy"]
    end

    subgraph Projection["View Projection"]
        Mapper["ShooterSnapshotViewModelMapper"]
        Store["ShooterViewEntityStore"]
        ProjectionApply["ShooterSnapshotViewProjection"]
        VM["ShooterSnapshotViewModel"]
    end

    subgraph Binding["渲染绑定"]
        Binder["ShooterSnapshotViewBinder"]
        DotsBinder["ShooterDotsSnapshotViewBinder"]
        Sink["IShooterSnapshotViewSink"]
        Backend["ShooterViewRenderBackendFactory"]
    end

    GatewayPush --> Decoder --> PureSync
    GatewayPush --> Decoder --> Adapter
    Packed --> Adapter
    Pure --> PureSync --> Adapter
    Local --> Adapter
    Adapter --> Batch --> SnapshotStream --> Sampling
    Adapter --> Mapper --> VM
    SnapshotStream --> Binder
    Binder --> Sink
    Backend --> Binder
    Backend --> DotsBinder
    Batch --> ProjectionApply --> Store
    Facade --> Reconcile
```

## 3. `ShooterPresentationFacade`：表现层统一门面

`ShooterPresentationFacade` 聚合四类对象：

| 成员 | 职责 |
|------|------|
| `ShooterGatewaySnapshotDecoder` | 判断 opCode 是否为 snapshot push，并解码 payload |
| `ShooterSnapshotViewAdapter` | 将 gateway/pure/local payload 转成 view batch 与 view model |
| `ShooterSnapshotStream` | 发布 batch 并维护插值采样缓冲 |
| `ShooterReconciliationDiagnosticsStream` | 发布客户端校验、回滚、对账诊断 |
| `ShooterPureStateSnapshotSyncController` | pure-state baseline/delta 应用、resync 状态与诊断 |

关键入口：

- `TryApplyGatewayPush(opCode, payload)`：用于网络收到数据后的统一入口；
- `ApplyGatewaySnapshot`：直接应用 gateway snapshot；
- `ApplyInterpolatedGatewaySnapshot`：带 controlled player id 的插值应用；
- `ApplyPureStateGatewaySnapshot` / `ApplyPureStateSnapshot`：纯状态路径；
- `ApplyLocalPredictionSnapshot`：本地预测或权威对比路径；
- `PublishReconciliation`：同步诊断输出；
- `Clear`：清空 adapter 状态并发布清理 batch。

```mermaid
sequenceDiagram
    participant Net as Client Network
    participant Facade as ShooterPresentationFacade
    participant Decoder as GatewaySnapshotDecoder
    participant Pure as PureStateSyncController
    participant Adapter as SnapshotViewAdapter
    participant Stream as ShooterSnapshotStream

    Net->>Facade: TryApplyGatewayPush(opCode, payload)
    Facade->>Decoder: IsSnapshotPush(opCode)
    Decoder-->>Facade: true/false
    Facade->>Decoder: Decode(payload)
    alt pure-state payload
        Facade->>Pure: ApplyGatewaySnapshot(snapshot)
        Pure->>Facade: ApplyPureStateSnapshot(payload)
        Facade->>Adapter: ApplyPureStateSnapshot(payload, controlledPlayerId)
    else packed/gateway payload
        Facade->>Adapter: ApplyGatewaySnapshot(snapshot)
    end
    Adapter-->>Facade: ShooterSnapshotViewBatch
    Facade->>Stream: Publish(batch)
```

## 4. `ShooterSnapshotStream`：环形缓冲与插值播放

`ShooterSnapshotStream` 同时承担事件流和播放缓冲：

| 能力 | 说明 |
|------|------|
| `Publish` | 存入环形缓冲并触发 `SnapshotApplied` |
| `TrySampleLatest` | 取最新 batch，用于 rebind all |
| `TrySample(playbackFrame)` | 在缓冲窗口里按 playback frame 采样 |
| `TryAdvancePlayback(deltaTime)` | 推进播放帧，并返回需要渲染的 batch |
| `Reset` | 清空缓冲、采样序列和播放状态 |

播放策略：

1. 初次播放时，`playbackFrame = latest.Frame - InterpolationDelayFrames`；
2. 后续每帧按 `deltaTime * PlaybackFramesPerSecond` 前进；
3. `TryFindSampleWindow` 找到 from/to 两个 batch；
4. `ShooterSnapshotSamplingPolicy.Sample` 决定返回插值 batch 还是离散 batch；
5. 非连续采样会用 sequence 去重，避免重复 apply 同一帧。

```mermaid
flowchart TD
    A["Publish batch"] --> B["Store in ring buffer"]
    B --> C["SnapshotApplied event"]
    D["TickInterpolation"] --> E{playback initialized?}
    E -->|no| F["latest frame - delay"]
    E -->|yes| G["advance by deltaTime * FPS"]
    F --> H["Find sample window"]
    G --> H
    H --> I["SamplingPolicy.Sample"]
    I --> J{continuous sample?}
    J -->|yes| K["return batch"]
    J -->|no| L{same sequence as last?}
    L -->|yes| M["skip"]
    L -->|no| K
```

## 5. 投影：从 ViewBatch 到 ViewEntityStore

`ShooterSnapshotViewProjection.Apply` 把 `ShooterSnapshotViewBatch` 应用到表现侧实体存储。应用顺序决定表现状态一致性：

1. 如果 batch 要求 full replace，则删除 full snapshot 中缺失的实体；
2. 应用显式 removed entities；
3. 应用 entity changes，区分新增、更新、死亡移除；
4. 非 replace 批次从 transform/health/score 恢复缺失的 Player entity；
5. 应用 transform、health、score、projectile lifetime 组件变化；
6. 生成 `ShooterViewProjectionApplyResult`，记录 frame、sequence、source、实体数和组件更新数。

```mermaid
flowchart TD
    A["Apply ShooterSnapshotViewBatch"] --> B{ShouldReplaceMissingEntities?}
    B -->|yes| C["Remove entities missing from full snapshot"]
    B -->|no| D["Apply removed entities"]
    C --> D
    D --> E["Apply entity changes"]
    E --> F["Recover missing player from components"]
    F --> G["Apply transform changes"]
    G --> H["Apply health changes"]
    H --> I["Apply score changes"]
    I --> J["Apply projectile lifetime changes"]
    J --> K["Build ProjectionApplyResult"]
```

这种投影模式让表现层可以独立维护自己的 entity store，不需要持有 runtime ECS/Svelto entity，也避免渲染对象直接依赖服务端权威结构。

## 6. 绑定器与渲染后端

`ShooterSnapshotViewBinder` 的职责很薄：监听 stream、决定即时 apply 还是插值 apply，然后把 batch 交给 sink。

| 方法 | 行为 |
|------|------|
| `Sync` | 直接 `sink.ApplySnapshot(batch)` |
| `TickInterpolation` | 插值开启时从 stream 推进播放并 Sync |
| `RebindAll` | 取最新 batch 重新绑定所有表现对象 |
| `Clear` | reset stream 并清空 sink |
| `OnSnapshotApplied` | 插值关闭时收到 batch 立即 Sync |

`ShooterPresentationSessionContext` 根据 `ViewRenderBackend` 创建 binder：

- GameObject 后端：常规 `ShooterSnapshotViewBinder`；
- DOTS 后端：`ShooterDotsSnapshotViewBinder`；
- `IShooterSnapshotViewSink` 可替换 GameObject binder 的具体 sink，但不会新增第三种 `ViewRenderBackend` 枚举。

这使同一套 network/sync/presentation facade 可以服务不同渲染实现。

## 7. Presentation Session 生命周期

`ShooterPresentationSession` 把表现上下文和客户端连接组合成一个可释放会话。

```mermaid
sequenceDiagram
    participant Host as Session Host
    participant Session as ShooterPresentationSession
    participant Client as IShooterPresentationClient
    participant Context as ShooterPresentationSessionContext
    participant Binder as IShooterViewBinder
    participant Facade as ShooterPresentationFacade

    Host->>Session: new(options, client)
    Session->>Context: CreateDefault()
    Session->>Client: subscribe DataReceived
    alt AutoStart
        Session->>Client: Connect()
    end
    Client-->>Session: DataReceived(opCode, payload)
    Session->>Facade: TryApplyGatewayPush(opCode, payload)
    Host->>Session: Tick(deltaTime)
    Session->>Client: Tick(deltaTime)
    Session->>Binder: TickInterpolation(deltaTime)
    Host->>Session: Dispose()
    Session->>Client: unsubscribe + Dispose
    Session->>Context: DisposeBinder()
```

`ShooterPresentationSessionContext` 还暴露内部 retain/release 语义，但当前只有 `ShooterPresentationSessionResolver` 调用它，生产链未发现 resolver 消费者。它不能被直接解释成已闭环的共享所有权方案：引用数为 0 后再次 `Release` 仍会再次清理 binder，归零后也允许再次 `Retain` 已释放的 binder，当前没有 underflow 或 disposed-state 防护。

## 8. Fast Reconnect 驱动

`ShooterFastReconnectDriver` 是框架无关 `FastReconnectSession` 在 Shooter 示例中的消费方。它把 Shooter 的恢复状态投影到框架阶段机：

- `Connected`；
- `Disconnected`；
- `Resuming`；
- `AwaitingFullSnapshot`；
- `Recovered`。

关键设计：

1. `Heartbeat(authoritativeFrame)` 只在 connected/recovered 阶段观察权威帧；
2. `Reconcile(target, authoritativeFrame, gapHint)` 每次最多推进 8 步，避免非法迁移死循环；
3. gap 小于恢复窗口时走短追帧，大于窗口时强制进入 full snapshot；
4. 每个步骤收集 `SyncHealthEvent`，上层可以把恢复过程纳入统一健康遥测；
5. 非法状态迁移被 catch 并返回 false，保持恢复层增量、安全。

```mermaid
stateDiagram-v2
    [*] --> Connected
    Connected --> Disconnected: Disconnect
    Disconnected --> Resuming: Reconnect small gap
    Disconnected --> AwaitingFullSnapshot: Reconnect large gap
    Resuming --> Recovered: CompleteRecovery
    AwaitingFullSnapshot --> Recovered: CompleteRecovery
    Recovered --> Connected: Observe server frame
    Recovered --> Disconnected: new gap
```

## 9. 权威对比与网络条件诊断

`ShooterAuthoritativeComparisonDriver` 是验收场景中“客户端控制器 vs 权威世界”的桥接层。

| 成员 | 职责 |
|------|------|
| `IShooterClientSyncController` | 客户端同步控制器，被 carrier network link 投喂权威快照 |
| `ShooterBattleRuntimePort` | 权威世界，每次 Advance 推进 Tick |
| `ShooterCarrierNetworkLink` | 按 `NetworkConditionProfile` 模拟延迟、丢包、投递 |
| `ShooterLagCompensationService` | 记录权威帧历史并评估射击回溯 |
| pending inputs queue | 输入按网络时间排队，达到投递时间才提交到权威世界 |

Advance 流程：

```mermaid
sequenceDiagram
    participant Test as Acceptance Session
    participant Driver as ShooterAuthoritativeComparisonDriver
    participant World as ShooterBattleRuntimePort
    participant Lag as ShooterLagCompensationService
    participant LinkSvc as ShooterCarrierNetworkLink
    participant Client as ClientSyncController

    Test->>Driver: EnqueueInput(commandFrame, command)
    Test->>Driver: Advance(stepCount, deltaSeconds)
    loop each step
        Driver->>Driver: DeliverDueInputs()
        Driver->>World: SubmitInput + Tick
        Driver->>Lag: RecordFrame(world)
        Driver->>World: ExportPackedSnapshot(authorityOverride)
        Driver->>LinkSvc: PublishSnapshot(snapshot, elapsed)
        Driver->>LinkSvc: Advance(clockMs)
        LinkSvc-->>Client: Apply authoritative snapshot when due
    end
```

这条链路把网络条件、权威世界推进、快照发布、客户端同步控制器和 lag compensation 放在同一个可验收循环里。

## 10. 与已有 Shooter 文档的边界

| 已有文档 | 本文补充点 |
|----------|------------|
| `04-ClientSyncStrategies.md` | 该文说明 sync controller 策略；本文说明 sync controller 产物如何进入表现流 |
| `08-NetworkModulesDeepDive.md` | 该文说明网络模块边界；本文细化 presentation facade、stream、binder 与 reconnect 状态机 |
| `09-SveltoPerformanceModeDeepDive.md` | 该文说明 runtime 性能模拟；本文说明 runtime snapshot 如何脱离 ECS/Svelto 投影到 view store |
| `07-SmokeValidationCases.md` | 该文说明验收用例；本文解释 authoritative comparison driver 如何构造验收闭环 |

## 11. 仍值得继续拆分的点

| 候选专题 | 拆分理由 |
|----------|----------|
| Authoritative Interpolation Controller | 插值同步控制器内部 buffer、time anchor、stale ignore 与 controlled player 过滤可独立成文 |
| Hybrid Hero Prediction | 英雄预测、远端权威插值、回滚校验三者混合策略值得单独画时序图 |
| Reconciliation Diagnostics | `ShooterReconciliationDiagnosticsStream`、hash mismatch、snapshot apply result 可形成诊断专题 |
| DOTS View Binder | DOTS 后端和 GameObject 后端的表现绑定差异可以单独说明 |

## 12. 源码锚点

| 主题 | 源码 |
|------|------|
| presentation facade | `Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Presentation/ShooterPresentationFacade.cs` |
| presentation session | `Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Presentation/Session/ShooterPresentationSession.cs` |
| session context | `Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Presentation/ShooterPresentationSessionContext.cs` |
| snapshot stream | `Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Presentation/Snapshot/ShooterSnapshotStream.cs` |
| view projection | `Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Presentation/View/ShooterSnapshotViewProjection.cs` |
| view binder | `Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Presentation/View/ShooterSnapshotViewBinder.cs` |
| fast reconnect driver | `Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Client/Synchronization/ShooterFastReconnectDriver.cs` |
| authoritative comparison | `Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Client/Synchronization/ShooterAuthoritativeComparisonDriver.cs` |

## 13. 缓冲所有权、静态宿主与证据边界

`ShooterSnapshotStream` 是固定容量环形缓冲，默认容量 64、采样率 30 FPS、插值延迟 2 帧。覆盖旧 batch 时必须释放其 pooled resources；`Reset` 会释放全部批次并清空 transform tracks 与 transient sampling buffer。普通采样返回独立结果，transient 采样以低分配为目标但结果只在下一次相关采样/重置前有效，调用方不得长期持有其内部缓冲。

`ShooterPresentationSession` 拥有 client 事件订阅、client 和 binder：Dispose 解除订阅并依次释放这些资源。`ShooterPresentationSessionHost` 则是进程内静态单例 owner，`Start` 会先 `Stop` 旧会话，`Stop` 在 `finally` 中清除 current 并发布状态事件；该类没有线程安全保证，创建、Tick、切换和销毁应由同一宿主线程串行调用。

表现时间线仍使用 `float` playback frame 和 delta。池化、环形缓冲与 transient API 只降低分配，不等于零分配或跨平台逐位一致。GameObject/DOTS binder、Shooter facade 与静态 SessionHost 都是项目表现策略；公共框架应保留 snapshot routing、生命周期和 adapter 契约，而不是固化 Shooter 的 ViewModel 与应用会话。

### 13.1 当前存在三条不同质量的宿主路线

| 路线 | 已有清理 | 当前缺口 |
|------|----------|----------|
| `ShooterPresentationSessionHost` -> `ShooterPresentationSession` | Start 先 Stop；Session Dispose 解绑并释放 client、binder | 静态宿主无锁；新构造失败不会恢复旧 session |
| `ShooterPlayModeSessionHost` -> `ShooterPlaySessionRunner` | subsystem reset/uninstall 拆 runner、PlayerLoop、network hook、registry；runner Stop Dispose acceptance session 并 Clear view | 真实 Unity reload/长时间运行仍需 E4 验证 |
| `ShooterRemoteStateSyncPlayModeHost` -> `ShooterClientNetworkLauncher` | runtime state Dispose launcher 与 runtime world；launcher 释放 checkpoint binding、data plane、transport、connection | `ShooterClientSession` 不实现 `IDisposable`，presentation context、sync controller、reliable consumer/recovery coordinator 未由这条 teardown 显式释放 |

GameObject/DOTS binder 的 Dispose 都会解绑 `SnapshotApplied` 并 Clear；但多个 binder 若共享同一 facade，任一 binder 的 Clear 都会 reset 共享 `Snapshots`，所以“共享 facade、多 binder 独立销毁”不是当前保证。Binder Dispose 本身幂等，但 Dispose 后手工调用 Sync/Tick/RebindAll 也没有统一抛 `ObjectDisposedException`，宿主必须停止后续调用。

Batch N 的 `489/489` 是 2026-08-16 的历史 Shooter Runtime E3。Batch W 后续全量为 `481/490`，9 项属于默认模型、acceptance 数量和 snapshot/session 旧预期漂移；相关聚焦 battle handle/controller factory `22/22` 通过。Batch X 的 projection/PlaySessionRunner 聚焦测试 `66/66` 通过，但不直接覆盖静态 PresentationSessionHost、remote teardown 或 Unity binder 渲染。没有运行 Unity PlayMode、截图/渲染验收或长时间插值压力测试，不能据此新增 E4/E5 表现成熟度结论。

*文档版本：v3.2 | 最后更新：2026-08-16*
