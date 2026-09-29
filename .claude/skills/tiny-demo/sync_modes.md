# Tiny 三种同步模式

## 客户端语义

| 模式 | 客户端行为 | 全量快照用途 |
|---|---|---|
| **State** | 只展示服务端权威状态快照；**没有**本地状态预测 | 常规状态同步 |
| **Frame** | 按权威输入帧推进确定性规则；迟到输入触发重放 | 订阅基线、断线或失配恢复 |
| **Hybrid** | 提交本地输入时**立即预测**，再用权威帧确认或回滚校正 | 订阅与恢复，并**每 5 个服务端帧**周期校验 |

Hybrid 的准确表述是"**帧输入预测 + 周期状态快照**"，**不是**独立的状态同步预测回滚。若将来要演示 `acceptedSeq` 裁剪与状态重演，那是**新增能力**，要单独实现与验收，不能当作 Hybrid 已有。

- State 模式**不做**本地状态预测或状态回滚。别把 05 的帧预测说成 State 已具备。
- Hybrid 收到与历史哈希一致的周期快照时**保留**后续预测；不一致时校正到权威状态。
- 帧哈希失配或回滚历史缺失 → 请求新的全量快照，恢复期间**暂停提交**帧输入。

## 服务端模板映射

模板 id 定义在**唯一一份** `TinySyncTemplates.cs`（`tiny.logic` 包，服务端经 `Compile Include` 共编译）：

| 常量 | 值 |
|---|---|
| `WorldType` | `tiny-battle` |
| `State` | `tiny-state-authority` |
| `Frame` | `tiny-frame-authority` |
| `Hybrid` | `tiny-hybrid-authority` |

`TinyServerGameplayModule.Create()` 用 `ServerSyncCapabilityDeclaration.FromTemplates(...)` 声明，**默认模板 = State**（第一个参数）：

| 模板 id | `ServerBattleSyncMode` | `ServerBattleRuntimeMode` | 快照间隔 (snapshot/full) | 协商出的 `NetworkSyncModel` |
|---|---|---|---|---|
| `tiny-state-authority` | `StateSync` | `BattleWorld` | 1 / 1 | `AuthoritativeInterpolation` |
| `tiny-frame-authority` | `FrameSync` | `BattleWorldWithFrameSync` | 1000000 / 1000000（等于关） | `Lockstep` |
| `tiny-hybrid-authority` | `FrameSync` | `BattleWorldWithFrameSync` | 5 / 5 | `HybridHeroPrediction` |
| 其他 | — | — | — | 注册过但未映射 → `ArgumentException("Unsupported Tiny sync template.")` |

三者都声明 schema 范围 **1..1**。回合制只有一条模板：`tiny-turn-state-authority` → `StateSync` / `BattleWorld` / 1,1，能力 lambda 与模板无关，恒为 `AuthoritativeInterpolation`。

## 三方声明必须同时改（**核心陷阱**）

模板 id 只有一份常量（`TinySyncTemplates`），但**服务端与客户端各有一份解释**，没有共享枚举强制一致：

```
唯一共享的 TinySyncTemplates（模板 id 字符串）
        │
        ├─→ 服务端 ServerSyncCapabilityDeclaration.FromTemplates（2026-09-28 起为唯一声明入口）
        │     一次声明同时给出：模板集合（ServerBattleSyncProfile）
        │                      + 每模板的档案与 schema 区间
        │     守卫：AbilityKit.Orleans.Grains.Tests/Rooms/TinySyncModeProfileTests.cs（[Theory]×3）
        │
        └─→ 客户端 Runtime/View/Sync/TinySyncMode.cs
              按字符串 → 自己的 enum TinySyncMode { State, Frame, Hybrid }
              守卫：TinyBattleSessionTests.CreateRoomSelectsServerSyncTemplate
```

**改造前**服务端是"模板列表 + 按字符串 switch"两份独立声明，加模板要改两处且能悄悄不一致；现在 `ServerGameplayModule` 只接收一个 `ServerSyncCapabilityDeclaration`，`SyncProfile` 是它的派生属性，**服务端内部不可能漂移**。仍未消除的是**跨端**那一份：客户端 `TinySyncMode` 是服务端没有的镜像类型（服务端没有等价枚举），加模式仍要同时改服务端声明与客户端枚举。

模式选择通过**房间创建标签**传递：`CreateTags[RoomGatewaySyncTagKeys.SyncTemplateId] = TinySyncTemplates.{State|Frame|Hybrid}`（`TinySyncModeConfiguration.CreateTags`）。加入方根据服务端快照协商同步能力。

## 能力协商与 schema

- 正式会话用 `NetworkSyncSessionBinding` + `NetworkSyncProfiles.*` 协商，schema 版本**钉死 1..1**。
- 不兼容版本会在 `TinyBattleSession` **订阅之前**被拒：`InvalidOperationException`，且 `SubscribeCalls == 0`、`Requests == 0`、`BattleId` 为空、`CanSubmitInput == false`。定向证据 `IncompatibleSchemaStopsBeforeSubscriptionAndInput`。
- 会话还会在 **Loading 与战斗订阅前**校验启动清单哈希，防客户端规则/场地标识与服务端不一致。

## 启动清单与 RulesKey

`TinyAssetPreparation.Validate(room)` 调 `RoomGatewayLaunchManifestCompatibility.Require(room, 1, new[] { TinyBattle.AssetKey, TinyBattle.RulesKey }, new { ["players"] = room.Players.Count })`。

- 清单哈希由服务端 `RoomLaunchManifestBuilder.Build` 生成，客户端**同样算法**校验。`metadata` 里带 `players` 计数，所以**第二个玩家加入时清单哈希就会变**（在开战之前）。
- **改规则常量必须同步升级 `RulesKey`**（`tiny:rules.v1` → `v2`）。这是**纯人工约定**，没有任何自动检查；不改则本地测试全绿、线上启动清单校验拒绝。
- 在途改动正在把清单哈希实现统一到一个共享助手 `RoomLaunchManifestHash`（`com.abilitykit.protocol.room`），服务端 `RoomLaunchManifestBuilder.ComputeHash` 改为委派它；客户端孪生 `RoomGatewayLaunchManifestCompatibility`（`com.abilitykit.network.room`）也是新增未跟踪文件。**这些文件当前未提交**。

## 输入提交的服务端帧语义

- 正式会话**观察帧输入响应的服务端帧号**。
- Frame 模式只对**已处理**的旧帧做**有界重排**；过早输入转入全量恢复，限速保留当前基线。
- Hybrid 已预测的输入若被**拒绝**（`FrameAlreadyProcessed`）→ **请求全量基线**，**不重试**、也不静默迁移到另一帧（`FrameInputRetriesProcessedFrameUsingServerFrame` 对 Frame 断言"恰好重试 1 次"，`HybridRejectedPredictionRequestsSnapshotWithoutRetry` 对 Hybrid 断言"0 次重试"）。
- 输入模块每帧采样，并在网络请求等待期间**保留最新移动 + 一次攻击短按**（`TinyInputBuffer`）。
