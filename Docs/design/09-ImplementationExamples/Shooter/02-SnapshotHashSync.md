# Shooter Snapshot、Hash 与同步模型

> 文档类型：项目示例深潜
> 事实基线：2026-08-16
>
> 本文拆解 Shooter 示例的同步设计：packed snapshot 如何表达组件块，pure-state snapshot 如何支持 baseline/delta 与兴趣范围，state hash 如何验证一致性，客户端如何选择预测回滚、权威插值或混合同步控制器。

## 1. 同步层目标

Shooter 的同步层要支持多种场景：

- 小规模权威快照同步；
- 大规模实体预算裁剪；
- 重连与 late join；
- 客户端预测回滚；
- 权威插值；
- snapshot stale ignore；
- hash 验证。

因此它没有只使用单一 `WorldStateSnapshot`，而是同时提供 packed snapshot 和 pure-state snapshot。

## 2. packed snapshot

`ShooterPackedSnapshotExporter` 导出组件块式快照。

典型组件块包括：

| 组件块 | 内容 |
|--------|------|
| PlayerLifecycle | 玩家存在性、生命周期 |
| ProjectileLifecycle | 子弹存在性、生命周期 |
| EnemyLifecycle | 敌人存在性、生命周期 |
| PlayerTransform | 玩家位置/速度/瞄准 |
| ProjectileTransform | 子弹位置/速度 |
| EnemyTransform | 敌人位置/速度 |
| PlayerHealth | 玩家血量 |
| EnemyHealth | 敌人血量 |
| PlayerScore | 玩家分数 |
| ProjectileLifetime | 子弹寿命 |

```mermaid
flowchart TB
    A[ShooterBattleRuntimePort.ExportPackedSnapshot] --> B[ShooterPackedSnapshotExporter]
    B --> C[稳定排序实体]
    C --> D[导出 Lifecycle Chunks]
    C --> E[导出 Transform Chunks]
    C --> F[导出 Health/Score/Lifetime Chunks]
    D --> G[ShooterPackedSnapshotPayload]
    E --> G
    F --> G
    G --> H[Frame + StateHash + Flags]
```

## 3. packed snapshot 的适用场景

packed snapshot 适合：

- full snapshot；
- key frame；
- authority override；
- 重连恢复；
- smoke 验证；
- 需要完整导入 runtime 的场景。

客户端应用 packed snapshot 时，会调用 runtime importer，让本地 runtime 状态对齐服务端权威状态。

## 4. pure-state snapshot

`ShooterPureStateSnapshotExporter` 更偏向“大规模状态分发”。它可以根据设置导出：

- full baseline；
- delta；
- low-frequency frame；
- 按兴趣范围裁剪后的实体集合；
- 按预算限制后的实体集合。

```mermaid
flowchart TD
    A[ExportPureStateSnapshot] --> B[NormalizeSettings]
    B --> C{FullBaseline?}
    C -->|Yes| D[使用 MaxEntityCount]
    C -->|No| E[使用 ActiveSyncBudget]
    D --> F[BuildCandidates]
    E --> F
    F --> G[InterestScope 过滤]
    G --> H[Priority 排序]
    H --> I[预算裁剪]
    I --> J[量化 Position/Velocity]
    J --> K[ShooterPureStateSnapshotPayload]
```

## 5. baseline 与 delta

pure-state delta 不是孤立可用的，它依赖 baseline。

客户端应用 delta 前必须确认：

- 已有 baseline；
- baseline frame 匹配；
- baseline hash 匹配；
- 当前 delta 未过期。

如果缺少 baseline，`ShooterPureStateSnapshotSyncController` 会标记需要 full baseline resync。

## 6. 状态 Hash

`ShooterStateHasher` 以确定性顺序 hash：

- 当前帧；
- 玩家 ID；
- 玩家位置、瞄准、血量、分数、生存状态；
- 子弹状态；
- 其他参与同步的实体状态。

位置等浮点状态会先量化再参与 hash，减少浮点误差影响。

```mermaid
flowchart LR
    A[CurrentFrame] --> H[StateHash]
    B[Players Ordered] --> H
    C[Projectiles Ordered] --> H
    D[Quantized Position] --> H
    E[Hp/Score/Alive] --> H
```

## 7. 客户端同步控制器选择

`ShooterClientSyncControllerFactory` 根据 `NetworkSyncModel` 创建策略控制器。packed 与 pure-state 是载荷类型，不是工厂中的独立同步策略。

| 模式 | 当前控制器 | 适用场景 |
|------|------------|----------|
| PredictRollback | `ShooterClientPredictRollbackSyncController` | 本地预测 + 权威校正，操作响应优先 |
| AuthoritativeInterpolation | `ShooterClientAuthoritativeInterpolationSyncController` | 延迟播放权威状态，一致性优先 |
| BatchStateSync | `ShooterClientAuthoritativeInterpolationSyncController` | 当前复用权威插值控制器，载荷可采用批量状态同步 |
| MassBattleLodSync | `ShooterClientAuthoritativeInterpolationSyncController` | 当前复用权威插值控制器，实体范围由 pure-state 预算和兴趣裁剪决定 |
| HybridHeroPrediction | `ShooterClientHybridHeroPredictionSyncController` | 主控英雄预测 + 其他实体插值 |

BatchStateSync 与 MassBattleLodSync 已有独立同步模型和配置语义，但当前没有专属客户端控制器。后续若两者需要不同的调度、缓存或恢复协议，应通过工厂注册新的策略控制器，而不是把差异塞进 packed 载荷解析。

## 8. packed snapshot 应用流程

策略控制器通过 `ShooterClientSnapshotApplyCoordinator` 提交 Gateway 快照。`ShooterFrameworkSnapshotPipeline` 将协议 payload 路由为 packed 或 pure-state 类型；packed stage 的应用上下文负责版本兼容、过期帧判定、runtime 导入和表现投影。

```mermaid
sequenceDiagram
    participant Gateway as Gateway Push
    participant Coordinator as ShooterClientSnapshotApplyCoordinator
    participant Pipeline as ShooterFrameworkSnapshotPipeline
    participant Runtime as ShooterBattleRuntimePort
    participant Presentation as ShooterPresentationFacade

    Gateway->>Coordinator: ApplyGatewaySnapshot(snapshot)
    Coordinator->>Pipeline: ApplyGatewaySnapshot(snapshot)
    Pipeline->>Pipeline: 解码 payload 并路由 packed stage
    Pipeline->>Pipeline: 检查协议版本与 stale frame
    Pipeline->>Runtime: ImportPackedSnapshot(packed)
    Runtime-->>Pipeline: success/fail
    Pipeline->>Pipeline: 记录 LastAppliedFrame/Hash/Flags
    Pipeline->>Presentation: ApplyInterpolatedGatewaySnapshot
    Pipeline-->>Coordinator: ShooterSnapshotApplyResult
```

## 9. pure-state 应用流程

```mermaid
sequenceDiagram
    participant Gateway as Gateway Push
    participant Ctrl as ShooterPureStateSnapshotSyncController
    participant Apply as ApplySnapshot Delegate
    participant Health as SyncHealthEvents

    Gateway->>Ctrl: ApplyGatewaySnapshot(pureState)
    Ctrl->>Ctrl: 判断 stale
    Ctrl->>Ctrl: 判断 baseline/delta 是否可应用
    alt 缺少 baseline
        Ctrl->>Health: SnapshotBaselineMissing
        Ctrl-->>Gateway: NeedsFullBaselineResync
    else 可应用
        Ctrl->>Apply: applySnapshot(pureState)
        Ctrl->>Health: Clear/Record diagnostics
    end
```

## 10. 源码索引

| 模块 | 源码 |
|------|------|
| packed 导出 | `Unity/Packages/com.abilitykit.demo.shooter.runtime/Runtime/Application/Synchronization/ShooterPackedSnapshotExporter.cs` |
| packed 导入 | `Unity/Packages/com.abilitykit.demo.shooter.runtime/Runtime/Application/Synchronization/ShooterPackedSnapshotImporter.cs` |
| packed codec | `Unity/Packages/com.abilitykit.demo.shooter.runtime/Runtime/Application/Synchronization/ShooterPackedSnapshotBytesCodec.cs` |
| pure-state 导出 | `Unity/Packages/com.abilitykit.demo.shooter.runtime/Runtime/Application/Synchronization/ShooterPureStateSnapshotExporter.cs` |
| 状态 hash | `Unity/Packages/com.abilitykit.demo.shooter.runtime/Runtime/Application/Synchronization/ShooterStateHasher.cs` |
| 客户端 session | `Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Client/ShooterClientSession.cs` |
| 输入协调 | `Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Client/Session/ShooterClientInputCoordinator.cs` |
| 同步控制器工厂 | `Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Client/Synchronization/ShooterClientSyncControllerFactory.cs` |
| 快照应用协调 | `Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Client/Synchronization/ShooterClientSnapshotApplyCoordinator.cs` |
| packed/pure-state 路由与应用管线 | `Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Client/Synchronization/ShooterFrameworkSnapshotPipeline.cs` |
| pure-state baseline/delta 控制器 | `Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Client/Synchronization/ShooterPureStateSnapshotSyncController.cs` |

## 11. 三种事实、会话协商与证据边界

| 对象 | 回答的问题 | 不能替代 |
|------|------------|----------|
| packed snapshot | 权威组件块如何完整或增量导入 runtime | AOI 公平性、Profile 协商 |
| pure-state snapshot | 观察者可见实体如何按 baseline/delta、预算和生命周期发送 | 完整世界恢复、客户端预测状态 |
| state hash | 当前完整权威世界是否与预期状态一致 | 裁剪载荷摘要、跨平台确定性证明 |

正式客户端会话不是仅凭本地枚举选控制器。`ShooterClientSyncControllerFactory.CreateSession` 使用 `NetworkSyncSessionBuilder`，组合 profile catalog、本地能力与 schema 范围、Room 远端能力以及 `Ignore`、`NegotiateWhenAvailable`、`Require` 策略，产出不可变 `NetworkSyncSessionDescriptor` 后再创建控制器。兼容构造路径仍可缺少远端声明，但不能据此宣称正式链会静默接受任意 profile 或 schema。

Pure-state 的位置和速度以 1000 比例量化，packed codec 也会把 `float` 字段编码为紧凑表示；量化是 wire/storage 表达，不等于运行时计算已经采用定点数。`StateHash` 来自完整权威世界，即使 pure-state 载荷只包含预算内实体也不改变这一语义。

## 12. 同步载荷到表现投影的替换语义

网络 full/delta 不能只看 payload 名称，还要看映射后的 `ShooterSnapshotViewBatch.ShouldReplaceMissingEntities`。当前投影先处理 full replace，再处理显式删除、生命周期变化和组件：

| 输入 | View projection 行为 |
|------|----------------------|
| replace 型 Full batch | 删除 `EntityChanges` 中未出现的已有实体 |
| Delta batch | 保留未提及实体，只处理 `RemovedEntities` 或 `Alive=false` |
| 非 replace 批次出现缺失 Player 的 transform/health/score | 先恢复 Player entity，再应用组件 |
| 只有 Bullet/Enemy 组件且实体缺失 | 不据此创建实体，组件更新被 store 拒绝 |

因此 baseline/delta、生命周期列表和组件块必须一起设计。客户端恢复缺失 Player 是 Shooter 为本地主控连续性选择的项目策略，不是通用 snapshot dispatcher 的职责，也不应被推广成所有实体类型的自动创建规则。

Batch N 曾记录 Shooter Runtime `489/489` 与 AOI/LOD `8/8`，属于当时的历史 E3。后续 Batch W 全量复跑为 `481/490`，9 项是默认模型、acceptance 数量和 snapshot/session 旧预期漂移；聚焦 battle handle/controller factory 为 `22/22`。Batch X 的 projection/PlaySessionRunner 聚焦测试 `66/66` 通过，通用 Snapshot routing `7/7` 通过；它们不覆盖跨平台 hash 或真实网络。本批未产生真实网络 artifact、跨平台 hash 对照或 Unity PlayMode 结果。

*文档版本：v3.2 | 最后更新：2026-08-16*
