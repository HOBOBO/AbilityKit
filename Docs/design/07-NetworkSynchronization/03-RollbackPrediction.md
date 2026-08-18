# 7.3 回滚预测

> **文档类型：Canonical 设计**
>
> **事实基线：2026-08-16**
>
> **规范范围：** Provider 注册、快照捕获/恢复、输入历史、预测重演与 Host 接入；表现重整由项目层负责。

> 基于真实源码说明 AbilityKit 的回滚预测能力：`IRollbackStateProvider` 多 Provider 快照、`RollbackCoordinator` 捕获/恢复、环形历史缓存、输入历史、状态哈希冲突检测、客户端预测重演、服务端回滚重演，以及 MOBA/Shooter Demo 的接入方式。

---

## 目录

1. [能力定位](#1-能力定位)
2. [源码入口](#2-源码入口)
3. [总体结构](#3-总体结构)
4. [回滚快照模型](#4-回滚快照模型)
5. [Provider 注册与确定性顺序](#5-provider-注册与确定性顺序)
6. [捕获、缓存与恢复流程](#6-捕获缓存与恢复流程)
7. [客户端预测与重演](#7-客户端预测与重演)
8. [Host Extension 集成](#8-host-extension-集成)
9. [Demo 接入：MOBA 与 Shooter](#9-demo-接入moba-与-shooter)
10. [设计约束与查漏补缺](#10-设计约束与查漏补缺)

---

## 1. 能力定位

AbilityKit 的回滚预测不是单一算法，而是一组围绕“保存过去状态、发现分歧、恢复到旧帧、重新模拟到当前帧”的基础设施。

| 能力 | 作用 | 代表类型 |
|------|------|----------|
| 状态导出/导入 | 由业务模块定义如何把局部状态序列化为字节并恢复 | `IRollbackStateProvider` |
| Provider 注册 | 管理多个状态域，保证导出/导入顺序稳定 | `RollbackRegistry` |
| 世界回滚快照 | 将一个帧上的多个 Provider payload 聚合为快照 | `WorldRollbackSnapshot`、`WorldRollbackSnapshotEntry` |
| 快照缓存 | 用固定容量环形数组保存最近 N 帧快照 | `RollbackSnapshotRingBuffer` |
| 回滚协调 | 捕获、存储、恢复、清理和操作结果上报 | `RollbackCoordinator`、`RollbackOperationResult` |
| 输入历史 | 保存预测帧或权威帧输入，用于恢复后重演 | `InputHistoryRingBuffer` |
| 哈希校验 | 比较预测状态与权威状态，发现分歧 | `WorldStateHashRingBuffer`、`ClientPredictionReconciler` |
| Host 集成 | 在 Host Tick 生命周期中消费权威输入、预测本地输入、回滚重演 | `ClientPredictionDriverModule`、`ServerRollbackModule` |

回滚预测与状态同步、帧同步的关系：

- 帧同步提供“按帧提交输入并推进世界”的确定性基础。
- 状态同步提供权威快照或状态哈希，用于判断客户端是否偏离服务器。
- 回滚预测在偏离时恢复到某个历史帧，然后用保存的输入重新推进。
- 业务状态是否能回滚，取决于业务是否注册了足够完整的 `IRollbackStateProvider`。

---

## 2. 源码入口

| 源码 | 说明 |
|------|------|
| [IRollbackStateProvider.cs](../../../Unity/Packages/com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/IRollbackStateProvider.cs) | 回滚状态 Provider 接口，定义 `Key`、`Export`、`Import` |
| [RollbackRegistry.cs](../../../Unity/Packages/com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/RollbackRegistry.cs) | Provider 注册表，按 `Key` 排序并在 Coordinator 构造时封存 |
| [WorldRollbackSnapshot.cs](../../../Unity/Packages/com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/WorldRollbackSnapshot.cs) | 回滚快照与条目结构，使用 `BinaryObjectCodec` 编解码 |
| [RollbackSnapshotRingBuffer.cs](../../../Unity/Packages/com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/RollbackSnapshotRingBuffer.cs) | 固定容量回滚快照环形缓存 |
| [RollbackCoordinator.cs](../../../Unity/Packages/com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/RollbackCoordinator.cs) | 协调器直接使用 `ArrayPool<WorldRollbackSnapshotEntry>` 和 `ArrayPool<IRollbackStateProvider>`，降低捕获与恢复临时数组分配 |
| [RollbackCoordinator.cs](../../../Unity/Packages/com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/RollbackCoordinator.cs) | 回滚核心协调器，负责捕获、存储、恢复、清理 |
| [RollbackOperationResult.cs](../../../Unity/Packages/com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/RollbackOperationResult.cs) | 回滚操作状态、错误类型和统计信息 |
| [InputHistoryRingBuffer.cs](../../../Unity/Packages/com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/InputHistoryRingBuffer.cs) | 按帧保存输入数组的环形缓存 |
| [WorldStateHashRingBuffer.cs](../../../Unity/Packages/com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/WorldStateHashRingBuffer.cs) | 按帧保存状态哈希的环形缓存 |
| [ClientPredictionReconciler.cs](../../../Unity/Packages/com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/ClientPredictionReconciler.cs) | 比较预测哈希与权威哈希，触发回滚请求 |
| [ClientPredictionRunner.cs](../../../Unity/Packages/com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/ClientPredictionRunner.cs) | 轻量客户端预测 Runner：提交输入、Tick、捕获、哈希记录、恢复重演 |
| [ClientPredictionDriverModule.cs](../../../Unity/Packages/com.abilitykit.host.extension/Runtime/FrameSync/ClientPredictionDriverModule.cs) | Host Extension 中完整的远端输入驱动、预测窗口、回滚和重演模块 |
| [ServerRollbackModule.cs](../../../Unity/Packages/com.abilitykit.host.extension/Runtime/Rollback/ServerRollbackModule.cs) | 服务端回滚重演模块，监听帧同步事件并保存输入/快照 |
| [ShooterPackedSnapshotRollbackProvider.cs](../../../Unity/Packages/com.abilitykit.demo.shooter.runtime/Runtime/Application/Rollback/ShooterPackedSnapshotRollbackProvider.cs) | Shooter 使用 packed snapshot 作为回滚状态 payload |
| [ShooterClientFrameSyncController.cs](../../../Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Client/Synchronization/ShooterClientFrameSyncController.cs) | Shooter 客户端侧创建 RollbackCoordinator 并支持恢复预测快照 |
| [RemoteDrivenRuntimeModuleFactory.cs](../../../Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Client/Session/Features/Sim/RemoteDrivenRuntimeModuleFactory.cs) | MOBA 远端驱动模式创建 `ClientPredictionDriverModule` |

---

## 3. 总体结构

```mermaid
flowchart TB
    subgraph Providers[业务状态 Provider]
        P1["Transform Provider"]
        P2["Command Log Provider"]
        P3["Packed Snapshot Provider"]
    end

    Registry["RollbackRegistry\n按 Key 注册并排序"]
    Coordinator["RollbackCoordinator\nCapture / Store / Restore"]
    Snapshot["WorldRollbackSnapshot\nFrame + Entries"]
    Buffer["RollbackSnapshotRingBuffer\nframe % capacity"]

    InputHistory["InputHistoryRingBuffer\n按帧保存输入"]
    HashHistory["WorldStateHashRingBuffer\n预测/权威哈希"]
    Reconciler["ClientPredictionReconciler\n哈希不一致触发回滚"]
    Driver["ClientPredictionDriverModule\n消费权威输入 + 本地预测 + 重演"]
    World["IWorld.Tick"]

    P1 --> Registry
    P2 --> Registry
    P3 --> Registry
    Registry --> Coordinator
    Coordinator --> Snapshot
    Snapshot --> Buffer
    Driver --> InputHistory
    Driver --> World
    Driver --> Coordinator
    Driver --> HashHistory
    HashHistory --> Reconciler
    Reconciler --> Driver
    Driver -->|"Restore(frame-1)"| Coordinator
    Driver -->|"Replay inputs"| World
```

核心思想是：框架不尝试理解业务对象结构，而是要求每个可回滚状态域提供自己的导出/导入实现。Coordinator 只处理“某一帧有哪些 payload、按什么顺序恢复、历史保存多久、失败如何上报”。

---

## 4. 回滚快照模型

### 4.1 Provider 条目

`WorldRollbackSnapshotEntry` 是单个状态域的序列化结果：

```csharp
public readonly struct WorldRollbackSnapshotEntry
{
    public readonly int Key;
    public readonly byte[] Payload;
}
```

其中：

- `Key` 来自 `IRollbackStateProvider.Key`，用于恢复时找到对应 Provider。
- `Payload` 是业务自定义字节数组，框架不解析。
- 空 payload 是合法的，典型例子是 `CommandRollbackStateProvider.Export` 返回 `Array.Empty<byte>()`，恢复时只让命令日志裁剪到指定帧之后。

### 4.2 世界快照

`WorldRollbackSnapshot` 聚合某一帧全部 Provider 条目：

```csharp
public readonly struct WorldRollbackSnapshot
{
    public readonly int Version;
    public readonly FrameIndex Frame;
    public readonly WorldRollbackSnapshotEntry[] Entries;
}
```

设计要点：

- `Version` 当前由 `WorldRollbackSnapshotCodec.CurrentVersion` 固定为 `1`。
- `Frame` 是快照对应的模拟帧。
- `Entries` 使用数组承载，捕获时通过共享 `ArrayPool<WorldRollbackSnapshotEntry>` 租用临时数组，并在存储完成后清理归还。
- `WorldRollbackSnapshotCodec` 用 `BinaryObjectCodec.Encode/Decode` 做二进制编解码，可用于跨模块传输或持久化。

---

## 5. Provider 注册与确定性顺序

`RollbackRegistry` 负责收集 Provider，并在 `RollbackCoordinator` 构造时被 `Seal()` 封存。

```mermaid
flowchart LR
    Build["业务构建 RollbackRegistry"] --> Register["Register(provider)"]
    Register --> CheckDup{"Key 已存在?"}
    CheckDup -->|"同一实例"| Ignore["忽略重复注册"]
    CheckDup -->|"不同实例"| Error["抛出重复 Key 异常"]
    CheckDup -->|"不存在"| Insert["按 Key 升序插入 Providers"]
    Insert --> Coordinator["new RollbackCoordinator(registry, buffer)"]
    Coordinator --> Seal["registry.Seal()"]
    Seal --> Runtime["运行期禁止继续注册"]
```

关键约束：

1. **Key 必须全局稳定**：同一业务状态域在客户端和服务器使用相同 Key。
2. **Key 不可冲突**：不同 Provider 使用同一 Key 会抛异常。
3. **顺序确定**：注册时按 Key 升序插入 `_providers`，捕获和恢复遍历顺序稳定。
4. **构造后封存**：`RollbackCoordinator` 构造函数会调用 `_registry.Seal()`，之后注册会抛异常，避免运行期 Provider 集合变化导致快照不兼容。

---

## 6. 捕获、缓存与恢复流程

### 6.1 捕获并存储

`RollbackCoordinator.Capture(frame)` 遍历所有 Provider：

1. 从 `RollbackRegistry.Providers` 取出按 Key 排序的 Provider。
2. 对每个 Provider 调用 `Export(frame)`。
3. 将返回 payload 包装成 `WorldRollbackSnapshotEntry`。
4. 通过 `ArrayPool<WorldRollbackSnapshotEntry>.Shared.Rent(count)` 租用条目数组。
5. 构造 `WorldRollbackSnapshot(CurrentVersion, frame, entries)`。
6. `CaptureAndStore(frame)` 再把快照写入 `RollbackSnapshotRingBuffer`。

```mermaid
sequenceDiagram
    participant Game as "Host/Gameplay"
    participant C as "RollbackCoordinator"
    participant R as "RollbackRegistry"
    participant P as "IRollbackStateProvider[]"
    participant B as "RollbackSnapshotRingBuffer"

    Game->>C: CaptureAndStore(frame)
    C->>R: Providers
    loop 每个 Provider
        C->>P: Export(frame)
        P-->>C: payload bytes
    end
    C->>C: new WorldRollbackSnapshot(version, frame, entries)
    C->>B: Store(snapshot)
    B->>B: idx = frame % capacity
    C-->>Game: RollbackOperationResult(Store/Succeeded)
```

### 6.2 环形缓存

`RollbackSnapshotRingBuffer` 使用固定容量数组保存快照：

```csharp
var idx = Mod(snapshot.Frame.Value, _capacity);
_frames[idx] = snapshot.Frame;
_snapshots[idx] = snapshot;
_has[idx] = true;
```

读取时必须精确匹配帧号：

```csharp
if (_has[idx] && _frames[idx].Value == frame.Value)
{
    snapshot = _snapshots[idx];
    return true;
}
```

因此它的语义是“最近 N 帧精确历史”，不是时间线数据库。超过容量的旧帧会被同余位置覆盖，覆盖时释放旧快照的 `Entries` 数组。

### 6.3 恢复

`RollbackCoordinator.TryRestore(frame)` 首先从 ring buffer 找快照；找不到会返回 `SnapshotNotFound`。找到后进入 `Restore(snapshot)`：

```mermaid
flowchart TB
    Start["TryRestore(frame)"] --> Lookup{"RingBuffer.TryGet(frame)"}
    Lookup -->|"否"| NotFound["SnapshotNotFound"]
    Lookup -->|"是"| Version{"Version == CurrentVersion?"}
    Version -->|"否"| Unsupported["UnsupportedVersion"]
    Version -->|"是"| Entries["遍历 Entries"]
    Entries --> Provider{"registry.TryGet(entry.Key)"}
    Provider -->|"否"| Missing["ProviderMissing\n继续处理其他条目"]
    Provider -->|"是"| Import["provider.Import(frame, payload)"]
    Import --> Done["Restore/Succeeded"]
```

恢复异常处理分两层：

- Provider 缺失：记录 `ProviderMissing`，但继续处理其他条目。
- Provider 导入抛异常：记录 `ProviderFailed` 并继续向上抛，让调用方知道恢复不可继续。

`RollbackOperationResult` 中包含：操作类型、状态、帧、ProviderKey、ProviderCount、PayloadBytes、Message、Exception，可用于日志和观测。

---

## 7. 客户端预测与重演

AbilityKit 中存在三层不同职责的客户端预测实现，不能仅因都包含“prediction”或“rollback”命名就视为同一套完整能力：

1. `PredictionCoordinator`：StateSync 包内的通用流程骨架，管理 `StateSlots`、按帧输入历史和字典快照；冲突分支会先提取完整帧批次，再清空旧历史并按原帧执行 replay。
2. `ClientPredictionRunner`：轻量封装，适合测试或简单客户端；其恢复入口直接尝试恢复分歧帧，不能替代 Host 的完整对账模块。
3. `ClientPredictionDriverModule`：Host Extension 的正式运行时模块，包含远端输入源、本地输入源、预测窗口、权威输入对账、哈希对账和 replay 超时保护。

正式业务接入应优先选择第 3 层或业务专用控制器，并单独核验输入保留、状态导入、回滚帧选择、重演终点和表现层刷新，而不能把通用 Coordinator 的 API 存在误写成完整 reconciliation。

通用 `PredictionCoordinator` 已提供 `ProcessInputs` 表达同一预测帧中的命令批次。`InputHistory.GetFrameBatches` 会保留服务器确认帧之后到原预测终点的每一个 Frame，包括空输入帧；重演时同帧命令使用相同帧号并只捕获一次帧末快照。命令对象本身不会由历史自动复制，调用方仍必须保证命令在保留期间不可变。

### 7.1 轻量 Runner

`ClientPredictionRunner.TickPredicted` 的流程：

```mermaid
sequenceDiagram
    participant App as "客户端调用方"
    participant Runner as "ClientPredictionRunner"
    participant Sink as "IWorldInputSink"
    participant World as "IWorld"
    participant Rollback as "RollbackCoordinator"
    participant Reconciler as "ClientPredictionReconciler"

    App->>Runner: TickPredicted(nextFrame, dt, inputs, computeHash)
    Runner->>Sink: Submit(nextFrame, inputs)
    Runner->>Runner: InputHistory.Store(nextFrame, inputs)
    Runner->>World: Tick(dt)
    Runner->>Rollback: CaptureAndStore(nextFrame)
    Runner->>App: computeHash(nextFrame)
    Runner->>Reconciler: RecordPredictedHash(nextFrame, hash)
```

收到权威哈希时：

```mermaid
sequenceDiagram
    participant App as "服务器哈希接收方"
    participant Runner as "ClientPredictionRunner"
    participant Reconciler as "ClientPredictionReconciler"
    participant Rollback as "RollbackCoordinator"
    participant Sink as "IWorldInputSink"
    participant World as "IWorld"

    App->>Runner: OnAuthoritativeStateHash(frame, authoritative)
    Runner->>Reconciler: OnAuthoritativeHash(frame, authoritative)
    alt predicted hash 缺失或一致
        Reconciler-->>Runner: false
    else hash 不一致
        Reconciler-->>Runner: OnRollbackRequested(frame)
        Runner->>Rollback: TryRestore(frame)
        loop frame+1 到 PredictedFrame
            Runner->>Sink: Submit(replayFrame, savedInputs)
            Runner->>World: Tick(fixedDelta)
            Runner->>Rollback: CaptureAndStore(replayFrame)
            Runner->>Reconciler: RecordPredictedHash(replayFrame, hash)
        end
    end
```

需要注意两个源码事实：轻量 `ClientPredictionRunner.HandleRollbackRequested` 当前直接 `TryRestore(rollbackFrame)`，其中 `rollbackFrame` 是 `ClientPredictionReconciler` 触发的哈希不一致帧；Host Extension 的完整模块则会回滚到 `mismatchFrame - 1`，再从分歧帧开始重演。正式游戏接入更应该参考 `ClientPredictionDriverModule` 的逻辑。

此外，StateSync 通用 `PredictionCoordinator.Reset()` 现在会同时重置帧游标、清空输入历史、snapshot store 和当前槽位；`Dispose()` 继续清理 handler/listener，并解除事件引用。`DictionarySnapshotStore.Record/Get` 两侧都返回隔离副本，可变引用槽位必须提供 `IStateSlotValueCloner`，否则在捕获或覆盖前快速失败。

`RollbackCoordinator` 的恢复路径会先解析全部 Provider，再开始 Import，并通过 `ArrayPool` 租用临时 Provider 数组；这避免“解析到一半才发现 Key 缺失”，但 Import 本身不是事务。若后续 Provider 导入失败，前面的 Provider 可能已经恢复，调用方必须把失败视为可能存在部分恢复，并进入全量基线或重建路径。`OperationCompleted` 观察者异常会被记录和隔离，不会改写回滚操作结果。

逻辑回滚失败后的“请求什么”与“如何执行”也应分层：`NetworkSessionRecoveryCoordinator` 聚合信号并给出决策，`NetworkSessionRecoveryActionRouter<TResult>` 把动作映射到项目 handler，`NetworkSessionRecoveryRuntime<TResult>` 再提供 Automatic/Manual 执行、generation、取消和陈旧完成抑制。框架不会替项目定义 full-state RPC、Room restore 或表现重建；这些 handler 必须由会话 owner 注册，并在切换 world/session 时显式 reset/dispose。

帧时钟也通过 `FrameTimeRollbackStateProvider` 进入 Provider 集合。其 payload v2 保存 Q32.32 raw `long`，用于恢复准确的累计帧时间；这不替代其它业务 Provider，也不证明所有业务计算均已定点化。

### 7.2 哈希对账

`ClientPredictionReconciler` 本身很薄：

- `RecordPredictedHash(frame, hash)` 将预测哈希写入 `WorldStateHashRingBuffer`。
- `OnAuthoritativeHash(frame, authoritative)` 查找同帧预测哈希。
- 查不到预测哈希时返回 `false`，调用方可稍后重试。
- 找到且值不同，触发 `OnRollbackRequested(frame)`。

这意味着框架只规定“何时认为分歧发生”，不规定“状态哈希如何计算”。状态哈希由业务传入，例如 Shooter 的 `ShooterStateHasher` 或 MOBA 的 actor transform hash。

---

## 8. Host Extension 集成

### 8.1 ClientPredictionDriverModule

`ClientPredictionDriverModule` 是当前最完整的客户端预测/回滚模块。它挂到 `HostRuntimeOptions.PreTick` 和 `PostTick`：

- `OnPreTick`：消费权威输入、推进预测输入或执行 replay。
- `OnPostTick`：定期捕获快照、计算预测哈希、处理延迟到达的权威哈希。

#### 创建 WorldContext

World 创建时模块会：

1. 从 `world.Services` 解析 `IWorldInputSink`。
2. 如果启用回滚，构建 `RollbackRegistry`，创建 `RollbackCoordinator` 和 `RollbackSnapshotRingBuffer`。
3. 捕获第 0 帧初始状态。
4. 如果提供 `buildComputeHash`，创建预测/权威哈希 ring buffer 和 `ClientPredictionReconciler`。
5. 注册 `OnRollbackRequested` 回调到 `RequestReconcileRollback`。

```mermaid
flowchart TB
    WorldCreated["OnWorldCreated(world)"] --> ResolveSink["Resolve IWorldInputSink"]
    ResolveSink --> EnableRollback{"enableRollback?"}
    EnableRollback -->|"是"| BuildRegistry["buildRollbackRegistry(world)"]
    BuildRegistry --> NewRollback["RollbackCoordinator + RingBuffer"]
    NewRollback --> Capture0["CaptureAndStore(frame 0)"]
    EnableRollback -->|"否"| NoRollback["Rollback = null"]
    Capture0 --> Hash{"buildComputeHash != null?"}
    NoRollback --> Hash
    Hash -->|"是"| Reconciler["PredictedHashes + AuthoritativeHashes + Reconciler"]
    Hash -->|"否"| Context["WorldContext"]
    Reconciler --> Context
```

#### 权威输入与预测输入

`OnPreTick` 的主循环可以理解为三个阶段：

1. **优先消费权威输入**：如果远端输入源已经有下一确认帧输入，则提交到 `IWorldInputSink`，保存到 `AuthoritativeInputs` 和 `AppliedInputs`，推进 `ConfirmedFrame`。
2. **如果发现预测输入和权威输入不同**：恢复到 `frame - 1`，进入 replay 模式。
3. **没有权威输入可消费时做本地预测**：根据预测窗口、本地输入队列和时间同步限制提交预测输入。

```mermaid
flowchart TB
    Tick["OnPreTick"] --> Remote{"有 nextConfirmed 权威输入?"}
    Remote -->|"是"| Compare{"该帧已预测且输入不同?"}
    Compare -->|"是"| Restore["Restore(frame - 1)"]
    Restore --> ReplayMode["Mode = Replaying\nReplayTo = PredictedFrame"]
    Compare -->|"否"| ApplyAuth["Submit 权威输入\nStore Authoritative/Applied"]
    ApplyAuth --> Confirm["ConfirmedFrame++"]
    Remote -->|"否"| Replay{"Mode == Replaying?"}
    ReplayMode --> Replay
    Replay -->|"是"| ReplayStep["用权威输入逐帧重演"]
    ReplayStep --> DoneReplay{"到达 ReplayTo?"}
    DoneReplay -->|"是"| Normal["Mode = Normal"]
    DoneReplay -->|"否"| WaitAuth{"缺少重演帧权威输入?"}
    WaitAuth -->|"是"| Timeout["等待，超时则禁用 reconcile"]
    Replay -->|"否"| Predict["按预测窗口提交本地预测输入"]
```

#### 哈希不一致触发回滚

权威状态哈希通过 `OnAuthoritativeStateHash(worldId, frame, hash)` 进入模块：

1. 写入 `AuthoritativeHashes`。
2. 如果同帧预测哈希还不存在，则跳过，等待 `OnPostTick` 记录预测哈希后再比较。
3. 如果预测哈希存在，调用 `ClientPredictionReconciler.OnAuthoritativeHash`。
4. 不一致时 `RequestReconcileRollback` 恢复到 `mismatchFrame - 1`，设置 replay 到当前 `PredictedFrame`。

`OnPostTick` 会捕获当前 `PredictedFrame` 的快照，并在存在 `ComputeHash` 时记录预测哈希。如果之前已经收到该帧权威哈希，会立刻做补偿比较。

### 8.2 ServerRollbackModule

`ServerRollbackModule` 面向服务端或权威模拟端。它依赖 `FrameSyncDriverModule` 暴露的 `IFrameSyncDriverEvents`：

- `AddInputsFlushed`：输入被提交到世界前后，保存该帧输入到 `InputHistoryRingBuffer`。
- `AddPostStep`：每 N 帧捕获一次回滚快照。

`TryRollbackAndReplay(worldId, rollbackFrame, replayToFrame, deltaTimePerFrame)` 的流程：

```mermaid
sequenceDiagram
    participant Caller as "调用方"
    participant Module as "ServerRollbackModule"
    participant Coord as "RollbackCoordinator"
    participant Inputs as "InputHistoryRingBuffer"
    participant Sink as "IWorldInputSink"
    participant World as "IWorld"

    Caller->>Module: TryRollbackAndReplay(worldId, rollbackFrame, replayToFrame, dt)
    Module->>Coord: TryRestore(rollbackFrame)
    alt restore failed
        Module-->>Caller: false
    else restore ok
        loop rollbackFrame+1 到 replayToFrame
            Module->>Inputs: TryGet(frame)
            Module->>Sink: Submit(frame, inputs)
            Module->>World: Tick(dt)
            Module->>Coord: CaptureAndStore(frame)
        end
        Module-->>Caller: true
    end
```

服务端回滚常用于延迟补偿、权威重演或调试确定性；客户端回滚则用于隐藏网络延迟并修正预测分歧。

---

## 9. Demo 接入：MOBA 与 Shooter

### 9.1 MOBA：RemoteDrivenRuntimeModuleFactory

MOBA 的远端驱动运行时通过 `RemoteDrivenRuntimeModuleFactory` 创建 `ClientPredictionDriverModule`：

- 开启客户端预测时：`enableRollback: true`、`rollbackHistoryFrames: 240`、`rollbackCaptureEveryNFrames: 1`。
- 回滚 Provider 由 `options.BuildRollbackRegistry` 注入。
- 状态哈希由 `options.BuildComputeHash` 注入。
- 远端输入来自 `options.ResolveRemoteInputs`。
- 本地输入来自 `options.ResolveLocalInputs`。
- 还可以通过 `ResolveIdealFrameLimit` 结合时间同步限制预测超前量。

实际 registry 构建会注册 MOBA actor transform 这类业务 Provider。测试用例中还会注册被动技能触发事件日志 Provider，说明回滚不只覆盖实体位置，也应覆盖会影响未来结果的事件/命令日志。

### 9.2 Shooter：packed snapshot 作为回滚状态

Shooter 侧使用 `ShooterPackedSnapshotRollbackProvider`：

```csharp
public byte[] Export(FrameIndex frame)
{
    return _runtime.ExportPackedSnapshotBytes(_worldId, isFullSnapshot: true, authorityOverride: false);
}

public void Import(FrameIndex frame, byte[] payload)
{
    _runtime.ImportPackedSnapshotBytes(payload);
}
```

这表示 Shooter 的回滚状态不是由多个细粒度 component Provider 组成，而是复用其状态同步 packed snapshot 编解码能力：

```mermaid
flowchart LR
    Runtime["ShooterBattleRuntimePort"] --> Export["ExportPackedSnapshotBytes"]
    Export --> Provider["ShooterPackedSnapshotRollbackProvider.Export"]
    Provider --> Coordinator["RollbackCoordinator.CaptureAndStore"]
    Coordinator --> Restore["RollbackCoordinator.TryRestore"]
    Restore --> Import["ShooterPackedSnapshotRollbackProvider.Import"]
    Import --> Runtime2["ImportPackedSnapshotBytes"]
```

`ShooterClientFrameSyncController` 构造时创建：

1. `RollbackRegistry`。
2. 注册 `ShooterPackedSnapshotRollbackProvider`。
3. 创建 `RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(rollbackBufferFrames))`。

它还提供 `TryRestorePredictedSnapshot(frame)`，用于恢复客户端预测快照并刷新表现层。收到 packed 权威快照后，控制器会记录预测帧/hash、权威帧/hash、导入后 runtime hash、重演 tick 数和 pending input 数量；发生 hash mismatch、导入失败、世界不匹配或帧距离超限时进入 `CatchUp`/`AwaitingFullSnapshot` 等恢复状态。`Full` 或 `AuthorityOverride` 标记的快照属于强恢复快照，不能与普通增量校正混为一谈。

这种设计复用了状态同步文档中描述的 packed/pure-state 快照能力：状态同步负责“接收权威状态”，回滚负责“保存本地历史状态并恢复”。但 Shooter 的 recovery/full snapshot 请求、重连触发和 reliable event baseline 恢复属于客户端同步与会话链路，不是 `RollbackCoordinator` 单独提供的能力。

---

## 10. 设计约束与查漏补缺

### 10.1 证据成熟度

| 能力 | 当前证据 | 结论 |
|------|----------|------|
| `RollbackCoordinator`、Provider registry、ring buffer | E0 源码与局部 E3 单元/契约测试 | 基础设施可复用，但不自动代表业务状态覆盖完整 |
| `ClientPredictionDriverModule` | E0 实现、MOBA 运行时工厂调用、Host 生命周期接线 | 已有 E2 采用；仍需按具体玩法验证 Provider 完整性和预算 |
| Shooter packed rollback/reconciliation | E2 Shooter 客户端运行链、状态 hash 与 rollback provider | 具备专用 recovery 逻辑；不能外推为通用 Coordinator 已完成 |
| Smoke、回放与 CI 发布门禁 | 由 Shooter smoke artifact、FrameRecord 测试和 `tools/test-gates.json` 分层提供 | E3/E4/E5 必须分开记录，局部测试不等于每次 PR 都执行的 gate |
| 本轮聚焦验证 | FrameSync 18/18、StateSync 12/12 | 捕获/恢复、帧时钟与预测局部契约通过；未运行真实网络故障 Smoke |

### 10.2 已落地设计

- `IRollbackStateProvider` 让框架与业务状态结构解耦。
- `RollbackRegistry` 通过 Key 排序和 Seal 保证 Provider 集合稳定。
- `RollbackSnapshotRingBuffer` 用固定容量控制内存上限。
- `RollbackCoordinator` 直接使用共享 `ArrayPool` 降低捕获和恢复阶段的临时数组分配。
- `RollbackOperationResult` 暴露捕获/恢复失败原因，便于日志和测试。
- `ClientPredictionDriverModule` 已包含输入不一致回滚、哈希不一致回滚、预测窗口、replay 等待超时和自动禁用 reconcile 的保护。
- `ServerRollbackModule` 提供服务端按历史输入重演的基础能力。
- Shooter 和 MOBA 分别展示了“packed snapshot provider”和“细粒度业务 provider”的两种落地方式。

### 10.3 使用约束

| 约束 | 原因 | 建议 |
|------|------|------|
| Provider 必须覆盖影响未来模拟的全部状态 | 恢复不完整会导致重演仍然分歧 | 不只保存位置/血量，也要保存随机数、冷却、事件日志、命令日志等 |
| 通用 Coordinator 按帧批次重演 | 同帧多命令必须共享帧号，空输入帧也必须保留 | 使用 `ProcessInputs` 提交帧批次；命令在进入历史后保持不可变，复杂 World 仍优先采用 Host 的输入数组驱动 |
| 引用型 StateSlot 必须可复制 | 静默浅复制会让历史快照随当前状态一起变化 | 为 Coordinator/StateSlots 注入 `IStateSlotValueCloner`；值类型和字符串无需额外策略 |
| `rollbackHistoryFrames` 必须大于最大网络抖动/预测窗口 | 旧帧被覆盖后无法恢复 | 结合 tick rate、RTT、重连窗口配置容量 |
| Key 必须稳定且不冲突 | 快照恢复依赖 Key 查找 Provider | 为每个模块分配固定 Key 段 |
| 捕获频率影响精度与性能 | 每帧捕获最准确但成本最高 | 客户端预测通常每帧捕获；服务端可按功能调整 `captureEveryNFrames` |
| 哈希必须确定且覆盖关键状态 | 哈希漏字段会掩盖分歧 | 业务应提供独立确定性测试 |
| 输入比较只比较 OpCode、Player、Payload | 当前 `InputsEqual` 不比较其他外部状态 | 输入命令 payload 应包含决定模拟结果的全部参数 |
| 恢复后表现层需要刷新 | 逻辑世界恢复不会自动重建所有 View 状态 | Shooter 在恢复后调用 `PublishRuntimeSnapshot()` |

### 10.4 相关专题边界

| 专题 | 与回滚预测的关系 | 源码锚点 |
|------|------------------|----------|
| `01-FrameSync.md` | 回滚预测依赖输入帧的稳定编号、聚合和分发语义 | `FramePacket`、`RemoteFrameAggregator`、`FrameSyncDriverModule`、Orleans battle host |
| `04-ReplaySystem.md` | 回放记录为回滚前后状态对账、复现和验收提供输入轨道 | `com.abilitykit.record`、MOBA view runtime、`BattleRecordReplayDesign.md` |
| `05-SessionCoordination.md` | 会话协调决定帧包如何跨客户端、Gateway、Room 和 BattleHost 流动 | `FramePacketNetAdapter`、Gateway、`RoomGrain`、`BattleLogicHostGrain`、Shooter smoke runner |
| 示例测试 | 示例测试验证预测窗口、输入不一致、哈希不一致和恢复后表现刷新 | MOBA `ClientPredictionTestHarness`、Shooter frame sync controller tests |

---

*文档版本：v3.2 | 最后更新：2026-08-16 | 文档类型：Canonical 设计*
