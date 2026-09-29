# Tiny 服务端与 Host 接入

`Server/Orleans/src/AbilityKit.Demo.Tiny.Server`（6 .cs / 551 行 / 14 行 csproj）——**不是新 Grain**，而是插进共享 Orleans 玩法框架的一个**玩法模块**。

```
TinyServerGameplayModule.cs            50   ns AbilityKit.Demo.Tiny.Server
TinyTurnGameplayModule.cs              33   ns AbilityKit.Demo.Tiny.Server
Gameplays/Tiny/TinyGameplay.cs         13   ns AbilityKit.Orleans.Grains.Gameplays.Tiny  ← 命名空间错位
Gameplays/Tiny/TinyRoomGameplayAdapter.cs     155
Gameplays/Tiny/TinyBattleRuntimeAdapter.cs    154
Gameplays/Tiny/TinyTurnBattleRuntimeAdapter.cs 146
```

## 命名空间错位（已知陷阱）

三个 adapter 与 `TinyGameplay` 声明 `AbilityKit.Orleans.Grains.Gameplays.Tiny`，但**编译进 `AbilityKit.Demo.Tiny.Server.dll`**。所以在 Grains 工程里 `using AbilityKit.Orleans.Grains.Gameplays.Tiny;` 必须额外引用 demo 程序集——这就是 `AbilityKit.Orleans.Grains.Tests.csproj` 必须引用 `AbilityKit.Demo.Tiny.Server.csproj` 的原因。**别按命名空间推断程序集。**

## 注册方式

`ServerGameplayModuleCatalog.Default` **硬编码只有 MOBA + Shooter**（`static readonly`）。Tiny 永远不在 Default 里，而是组合期追加：

```csharp
var catalog = ServerGameplayModuleCatalog.Default
    .WithModule(TinyServerGameplayModule.Create());
```

- 仓库内 Host：`Server/Orleans/src/AbilityKit.Orleans.Host/Program.cs`。**在途改动**（未提交）把 Turn 变成可开关：`builder.Configuration.GetValue("AbilityKit:Tiny:EnableTurn", true)`，**默认 true**，用 `AbilityKit__Tiny__EnableTurn=false` 关闭。
- 服务端发布包的组合入口：`tools/tiny-server-template/composition/Program.cs`，多一个 `ConfigureGameplay(catalog)` 钩子给项目注册自己的模块；它**先于**仓库 Host 就有该开关。
- 只注册 Tiny 的目录也可构造；**未注册默认 `battle` 类型时用首个模块作默认玩法**。

`WithModule` 是**不可变 copy-on-write**（返回新目录）。DI 注册的单例目录才真正到达 Grain：`RoomGrain` 与 `BattleLogicHostGrain` 构造函数接收 `ServerGameplayModuleCatalog`，再由 `RoomGrain` 传给 `RoomFrameSyncRoute.ResolveStartRoute(...)` 与 `RoomNetworkSyncCapabilityResolver.Resolve(...)`。

> **潜在坑**：这两个方法有 `modules = null` 的**回退到 `ServerGameplayModuleCatalog.Default`**。调用方忘记传目录时，**Tiny 会静默消失**而不是报错。排查"Tiny 房间起不来"时先确认目录传到位。

`ServerGameplayModule` 有三条**抛异常**（非静默）的不变式：`CreateRoomAdapter()` / `CreateBattleRuntimeAdapter()` 的返回类型必须匹配 `Descriptor.RoomType`；`CreateWorldBlueprints()` 至少一个 blueprint 的 `WorldType == Descriptor.DefaultWorldType`。`ServerGameplayCatalog.EnsureRegistered(roomType)` 对未知 roomType 抛 `InvalidOperationException`。

## 装配可见性与能力声明（2026-09-28 已改造）

**改造前**：adapter 的 4 个高级能力接口是 `internal`（`IObserverAwareBattleRuntimeSession` / `IReliableBattleEventProducer` / `IBattleRuntimeInputDiagnostics` / `IBattleRuntimeStageDiagnostics`），而 `AbilityKit.Orleans.Grains` 的 `InternalsVisibleTo` 只给了自己的测试装配。后果：MOBA/Shooter 在装配内（`internal sealed`）能用，**Tiny 这类装配外玩法结构上拿不到**——不是取舍，是编译期不可达。

**现状（改造后）**：全部公开，且 dispatch 本来就是按接口模式匹配（`BattleLogicHostGrain` 里几处 `_runtimeSession is XxxDiagnostics` / `is IObserverAwareBattleRuntimeSession`），所以装配外玩法**实现即生效**，无需改框架。

| 接口 | 可见性 | 解锁 |
|---|---|---|
| `IBattleRuntimeStateHashProvider` | public | 每 tick 状态哈希快路径 |
| `IBattleRuntimeInputDiagnostics` | public | 最后一次输入提交诊断（`BattleLogicHostGrain` 在批次未全量接受时写进告警） |
| `IBattleRuntimeStageDiagnostics` | public | 阶段耗时 sink |
| `IObserverAwareBattleRuntimeSession` | public | **按观察者推送**（AOI/旁观者的前提） |
| `IReliableBattleEventProducer` | public | 可靠事件源 |

配套去掉了通用装配里的玩法泄漏：原先 `BattleLogicHostGrain.AttachConsumedCommandAcknowledgements` 会在**广播路径**上反序列化 Shooter 的 packed/pure-state 载荷来塞 `ShooterCommandAcknowledgement`——那段对 Shooter 永远不执行（Shooter 的 session 是 observer-aware，走的是另一条路径），对其他玩法也是空转，已删除；回执改为契约层的 **`BattleCommandAcknowledgement(Frame, CommandId)`**（玩法无关），Shooter 在自己的 adapter 边界投影成线格式类型。

**Tiny 自己已是这条契约的第一个装配外消费者**：`TinyBattleRuntimeAdapter.Session` 现在实现 `IBattleRuntimeInputDiagnostics` 并汇报 `frame=… accepted=… rejected=… first=…`，由 `TinyBattleIntegrationTests.OutOfTreeGameplaySession_ReportsInputDiagnostics` 守住（该程序集没有 `InternalsVisibleTo`，能编译本身就是"装配外可达"的证据）。

## 同步能力声明只有一个入口

**改造前**三种写法并存：MOBA 硬编码常量、Shooter 走 `NetworkSyncProfileRegistry`、Tiny 手写 `if (string.Equals(templateId, …))` 链；而且"模板列表"（`ServerBattleSyncProfile`）与"能力解析"（模块的 delegate）是**两份独立声明**，靠 `ServerGameplayModule.ResolveSyncCapabilities` 里的一次 `SupportsTemplate` 兜底才不至于互相打架。

**现状**：新增公开的 `ServerSyncCapabilityDeclaration`，一次声明同时产出模板集合与能力解析器：

```csharp
ServerSyncCapabilityDeclaration.FromTemplates(
    defaultTemplateId,
    new ServerSyncTemplateDeclaration(templateId, mode, runtimeMode,
        snapshotIntervalFrames, fullSnapshotIntervalFrames,
        profile, minimumSchemaVersion, maximumSchemaVersion));
```

- `ServerGameplayModule` 的构造参数从 `(syncProfile, syncCapabilities)` 合并为 `syncDeclaration`；`SyncProfile` 变成派生属性 → **漂移在结构上不可能**。
- 档案名由 `NetworkSyncProfileRegistry.GetName(profile.CompatibilityModel)` 派生，不再手写字符串。
- 动态场景（Shooter 要按请求的 `SyncModel` 覆盖档案）走 `FromResolver(syncProfile, resolve)`。
- 同时删掉了不再可达的 `ServerBattleSyncProfile.StateSync/FrameSync` 三个工厂与 `ServerGameplaySyncCapabilityProfiles.ForMoba`（**唯一构造入口是 `FromTemplates`**），MOBA 的模板 id 改为取自 `ServerGameplayDescriptors.Moba.DefaultSyncTemplateId`（顺带消掉了该文件里 `AKS0002` 的 `'frame-sync-authority'` 魔法串告警）。

遗留的同类问题（见 [framework_boundaries.md](framework_boundaries.md)）：`ServerGameplayDescriptors` 仍是 `internal`（装配外玩法只能自己 `new GameplayRoomDescriptor`），网关侧的 `GatewayGameplayCatalog` 仍是手维护镜像。

## Tiny 明确不支持什么（这一部分是**显式结果对象**，不是 TODO）

下面这些"不支持"都是显式结果对象而非抛异常，且是有意的最小化（与上面的可见性无关）：

| 能力 | MOBA | Shooter | Tiny / Turn |
|---|---|---|---|
| adapter 文件大小 | 476 | 1030 | **154 / 146** |
| `SubmitCommand` | 真实（loadout 校验） | 全接受（no-op） | **全拒绝**：`InvalidGameplayCommand` / `"Tiny has no room commands."` |
| Bot（`MountBotAi`） | 真实 | 有 | **硬不支持**：`"Tiny has no bots."` / `"Tiny Turn has no bots."` |
| 诊断事件（`QueryDiagnosticEvents`） | 真实 | 真实 | **桩**：`"Unavailable"` / `"NotProduced"` / 空数组 |
| blueprint | 2 个（Lobby + Battle） | 1 个真实 | **1 个只设 `WorldType` 的 `DelegateWorldBlueprint`**（不注册任何 system/module） |
| 协议 mapper | `DefaultOrleansBattleProtocolMapper` | 专用 codec | **无**（载荷是不透明字节） |
| 启动清单 assets | map + 英雄/技能引用 | map + shooter assets | **恰好两个**（`tiny:arena`+`tiny:rules.v1` 或 `tiny:turn`+`tiny:turn.rules.v1`）+ `metadata {players}` |
| 迟到加入 | 完整 | 完整 | `BuildLateJoinPlayer` 会返回该玩家，但 `JoinPlayer` 只报 `"AlreadyJoined"`/`"RoomFull"`，**从不改动战斗** |
| 玩家 loadout | 必需，最多 10 人 | 否 | **否，最多 2 人** |

**其他有意简化**：

- `CreateStateSyncPush` **忽略 `isFullSnapshot` 参数**，恒置 `IsFullSnapshot = true`，`SchemaVersion` 硬编码 1。服务端测试 #9 与回合制测试 #2 **断言了这个强制行为**——是"由测试固定的有意设计"，不是疏漏。
- 两个 adapter 都会 `_worldManager.CreateBattleWorld(...)` + `_world.Tick(deltaTime)` **额外维护一个完整 `IWorld`**，只为满足 `ServerBattleWorldManager` 的生命周期要求；而权威其实是 `TinyBattle` / `TinyTurnBattle`。这个世界对状态**没有任何贡献**——是"契约税"，读代码时别误以为它参与模拟。
- `Tick` 返回 `_battle.Frame >= frame`，是 Tiny 唯一做帧记账的地方。
- `TinyRoomGameplayAdapter` **一个类同时服务 Tiny 与 Tiny Turn**（internal 6 参构造）；持久化 `Format` 分别是 `tiny.room.v1` 与 `tiny.turn.room.v1`（互不兼容，好事）。
- `StableWorldId` = `RoomId` 的 FNV-1a 64 位哈希，强制 ≥ 1（`0 → 1`）。
- `TinyTurnBattleRuntimeAdapter.ActorSnapshots()` **忽略状态**，位置恒为 `X = -1 / +1`、`Z = 0`，只有 HP 变化。实时版 `TinyBattleRuntimeAdapter` 才读真实 X/Z。

## 依赖闭包

`AbilityKit.Demo.Tiny.Server.csproj`：`net10.0`，**零 `PackageReference`**，5 条 `ProjectReference`——`AbilityKit.Orleans.Grains`、`AbilityKit.Demo.Tiny.Core`、`AbilityKit.Demo.Tiny.Turn.Core`、`AbilityKit.Host`、`AbilityKit.Network.Runtime`。

注意它引用 `Tiny.Core`/`Turn.Core` 这两个**只有 csproj 的投影工程**——所以**服务端构建依赖 Unity 目录树存在**，改 `Unity/Packages/**` 下的规则会静默改变服务端。`Server/Orleans/Directory.Build.props` 给每个 Orleans 工程注入 `AbilityKit.Server.Analyzers` 分析器（`ReferenceOutputAssembly=false`）。

## 服务端测试（`AbilityKit.Orleans.Grains.Tests`，全部已提交）

- `Battle/TinyBattleIntegrationTests.cs`（9 `[Fact]`）：迟到输入重放收敛、哈希失配停止预测直到全量恢复、历史耗尽、空输入确定性、`AttackDamage` 与冷却、只注册 Tiny 的目录自洽、Tiny + Shooter 共存时 Tiny 为默认、房间槽位/准备往返、真实 `ServerBattleWorldManager` 上的权威全量状态。
- `Battle/TinyTurnBattleIntegrationTests.cs`（3 `[Fact]`）：越权拒绝 + 回合跨快照保持、回合模块独立性（清单含 `tiny:turn.rules.v1` 且**不含** `tiny:rules.v1`）、排队输入只由权威状态确认。
- `Rooms/TinySyncModeProfileTests.cs`（1 `[Theory]` × 3）：**模板 → `NetworkSyncModel` 映射的 canonical 守卫**。
- `Rooms/RoomNetworkSyncCapabilityResolverTests.cs` 里有一个**合成的内联 "tiny" 模块**（roomType `tiny`、模板 `tiny-state-authority`、profile 名 `"TinyState"`、schema **2..2**），factory 全部抛 `NotImplementedException`。它**不是** `TinyServerGameplayModule`，是刻意用不同值造第三方玩法样例——**别把两者混淆**。

## 已知缺口（框架级，影响 Tiny 的对外可见性）

1. **`Gateway/HttpApi/GatewayGameplayCatalog.cs` 是硬编码 2 项数组（`moba`、`shooter`）**，由 `GatewayHttpApi` 的玩法列表端点使用。它**不派生自 `ServerGameplayModuleCatalog`**，所以 HTTP 端点**永远不会列出** `tiny` / `tiny-turn`；且 `GatewayGameplayCatalog.Resolve` 对未知 roomType **回退到 `All[0]`（moba）**——**静默给出错误描述符而不是报错**。房间创建本身正常（`RoomGrain` 走 DI 目录）。这是**任何第三方玩法都会踩的框架缺口**，不只是 Tiny。
2. **Tiny 相关工程全部不在任何 .sln**（见 [packages_overview.md](packages_overview.md)），`AbilityKit.Demo.Tiny.Server` 只经 Host 与 Grains.Tests 的 `ProjectReference` 传递构建。
3. **没有 Tiny 专属协议目录/解码器注册**。Tiny 的 31001/31002 作为不透明字节由通用 Room 协议承载，所以**不注册也能跑**——代价是服务端除 adapter 自身外**没有任何东西校验快照载荷形状**。
