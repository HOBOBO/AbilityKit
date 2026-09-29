# Tiny 包与工程总览

基于源码核校（2026-09-28）。行数为 `git ls-files` 口径（排除 `obj/`、`bin/`）。

## 三个 Unity 包 + 服务端

| 位置 | .cs / 行数 | version | 装配 |
|---|---|---|---|
| `Unity/Packages/com.abilitykit.demo.tiny.logic` | 7 / 323 | 0.0.1 | `AbilityKit.Demo.Tiny.Logic` |
| `Unity/Packages/com.abilitykit.demo.tiny` | 29 / 4452 | 0.0.1 | `AbilityKit.Demo.Tiny.FrameSync`、`AbilityKit.Demo.Tiny.View` |
| `Unity/Packages/com.abilitykit.demo.tiny.turn` | 6 / 1229 | 0.0.1 | `AbilityKit.Demo.Tiny.Turn.Logic`、`AbilityKit.Demo.Tiny.Turn.View` |
| `Server/Orleans/src/AbilityKit.Demo.Tiny.Server` | 6 / 551 | — | `AbilityKit.Demo.Tiny.Server`（**非 Grain**） |

`com.abilitykit.demo.tiny` 的 4452 行拆分为：`Runtime/` 1986、`Tests/` 2205、`Samples~/` 261。

**不存在** `com.abilitykit.demo.tiny.meta` 包。`Unity/Packages/com.abilitykit.demo.tiny.meta` 是一个**游离的 169 字节孤儿 `.meta`**（`folderAsset: yes`，无对应目录），已纳入版本库但无任何引用——属于待清理的死元数据。

## package.json 依赖

| 包 | 依赖 |
|---|---|
| `tiny.logic` | **无**（`dependencies: {}`，`verify-tiny-dependencies.ps1` 强制断言） |
| `tiny` | `demo.tiny.logic 0.0.1`、`demo.common 0.0.1`、`game.view.runtime 0.1.0`、`network.room 0.1.0`、`network.sdk 0.1.0`、`network.runtime 0.1.0`、`protocol.room 0.1.0`、`world.framesync 0.1.0`、`thirdparty.unsafe 5.0.0` |
| `tiny.turn` | `demo.common 0.0.1`、`network.room 0.1.0`、`network.runtime 0.1.0`、`network.sdk 0.1.0`、`protocol.room 0.1.0` |

边界由脚本强制：`tiny` **不得**依赖 `com.abilitykit.record`、不得含匹配 `skill|buff|projectile` 的依赖、不得依赖 `demo.tiny.turn`；`tiny.turn` 的依赖集必须**恰好**等于上表五项。

## asmdef 引用（全量）

```
AbilityKit.Demo.Tiny.Logic        → references: []            noEngineReferences: true
AbilityKit.Demo.Tiny.FrameSync    → ["AbilityKit.Demo.Tiny.Logic", "AbilityKit.World.FrameSync"]
                                    noEngineReferences: true
AbilityKit.Demo.Tiny.View         → ["AbilityKit.Demo.Tiny.Logic", "AbilityKit.Demo.Tiny.FrameSync",
                                     "AbilityKit.Demo.Common", "AbilityKit.Game.View.Runtime",
                                     "AbilityKit.Network.Room", "AbilityKit.Network.Sdk",
                                     "AbilityKit.Network.Runtime", "AbilityKit.Protocol.Room"]
AbilityKit.Demo.Tiny.Turn.Logic   → references: []            noEngineReferences: true
AbilityKit.Demo.Tiny.Turn.View    → ["AbilityKit.Demo.Tiny.Turn.Logic", "AbilityKit.Demo.Common",
                                     "AbilityKit.Network.Room", "AbilityKit.Network.Runtime",
                                     "AbilityKit.Network.Sdk", "AbilityKit.Protocol.Room"]
```

`FrameSync` 装配刻意 `noEngineReferences: true`——它要被投影进 .NET 测试与服务端，所以**不得**引入 `UnityEngine`。`Logic` 同理。

测试装配：
- `Tests/Editor`：`includePlatforms: ["Editor"]`，引用 View + FrameSync + Logic + Demo.Common(.Unity) + Game.View.Runtime + Network.Room/Runtime + Protocol.Room。
- `Tests/PlayMode`：无平台限制；引用 View，**不直接引用 FrameSync**（经 View 传递）。
- `Tests/NetworkPlayMode`：无平台限制；引用 View + Logic + Network.Room/Runtime + Protocol.Room（**不含** `Demo.Common.Unity`）。

`Runtime/View/AssemblyInfo.cs` 给三个测试装配开了 `InternalsVisibleTo`——所以测试能碰 `internal` 的 `TinyFrameReplication` / `TinyAssetPreparation` / `TinyHudPresenter` / `TinyInputBuffer` / `TinySyncModeConfiguration` / `TinyBattleSession.SnapshotRequestCount`。

## Runtime 文件清单（14 文件 / 1986 行）

| 文件 | 行 | 性质 |
|---|---|---|
| `View/TinyBattleSession.cs` | 344 | **正式会话**（继承 `RoomGatewayBattleSessionBase`；本文件只剩三模式同步策略 + 输入提交 + 表现读取。改造前 627 行，见 [framework_boundaries.md](framework_boundaries.md)） |
| `View/TinyGameplayRoot.cs` | 306 | `MonoBehaviour` 场景根：消费启动意图、单飞操作状态机、返回大厅 |
| `View/TinyViewModules.cs` | 208 | `TinyInputModule`（WASD/空格）+ `TinyActorViewModule`（程序化场地/相机/灯光 + 每 Actor 一个立方体） |
| `View/Sync/TinyFrameReplication.cs` | 184 | 权威帧/快照 → `TinyFrameSyncSession` 适配 + 本地预测 + 回滚记账 |
| `View/TinyGatewayClient.cs` | 147 | `ITinyBattleGateway` 实现，1:1 委派 `RoomGatewayConnectionSession` |
| `View/TinyProjectLaunch.cs` | 75 | 静态一次性交接，脱离 Starter 的入口 |
| `View/Presentation/TinyHudPresenter.cs` | 75 | IMGUI 面板（建房/加入/准备/开战/返回 + 遥测） |
| `View/Sync/TinySyncTelemetry.cs` | 52 | 只读遥测结构；**留了一个 11 参数旧构造（无消费者，可删）** |
| `View/Sync/TinySyncMode.cs` | 41 | `enum TinySyncMode { State, Frame, Hybrid }` + 标签映射 |
| `View/Loading/TinyAssetPreparation.cs` | 40 | 启动清单校验 + **有意为空的** 1 步 Loading 管线 |
| `View/Sync/TinyInputBuffer.cs` | 36 | 最新移动 + 粘性攻击缓冲 |
| `View/AssemblyInfo.cs` | 3 | `InternalsVisibleTo` |
| `FrameSync/TinyFrameSyncSession.cs` | 158 | 包 `RollbackCoordinator` + 三个环形缓冲 |
| `FrameSync/TinyBattleRollbackProvider.cs` | 34 | `IRollbackStateProvider` + Preflight |

**大头在框架侧**：`Runtime/FrameSync` 的 192 行只是 `com.abilitykit.world.framesync`（`RollbackCoordinator`、`RollbackSnapshotRingBuffer`、`InputHistoryRingBuffer`、`WorldStateHashRingBuffer`）的包装；`TinyGatewayClient` 同理是 `network.room` 的包装。真正的回滚机器不在 Tiny 里。

## Composition / Scenes

`Scenes/TinyDemoGameplayScene.unity` 只有一个根：`TinyGameplayBootstrap.prefab` 实例（无相机/灯光/其他对象）。链路完整且 GUID 全部可解析：

```
TinyDemoGameplayScene → TinyGameplayBootstrap.prefab（DemoGameplayBootstrap from demo.common.unity）
    → TinyGameplayCatalog.asset → TinyMultiplayerProfile.asset（profileId: tiny-multiplayer,
       gameplay: Tiny, mode: Multiplayer）→ TinyGameplayRoot.prefab（Runtime/View/TinyGameplayRoot）
```

场地（地面、正交相机、方向光、角色立方体）**全部在运行时由 `TinyActorViewModule` 程序化生成**——Tiny 无外部美术资源，所以 `TinyAssetPreparation` 的 Loading 步骤是有意的空实现（**别把它读成"加载系统"**）。

## 19 个 .NET 工程（`src/`）

| 工程 | 类型 | 职责 |
|---|---|---|
| `AbilityKit.Demo.Tiny.Core` | 投影库 | glob `tiny.logic/Runtime/Logic/*.cs`；**无 ProjectReference** |
| `AbilityKit.Demo.Tiny.FrameSync` | 投影库 | glob `tiny/Runtime/FrameSync/*.cs`；引用 Core + `AbilityKit.World.FrameSync` |
| `AbilityKit.Demo.Tiny.Turn.Core` | 投影库 | glob `tiny.turn/Runtime/Logic/*.cs`；无 ProjectReference |
| `AbilityKit.Demo.Tiny.ClientHarness` | 验收驱动库 | `TinyRecoveryEvidence` / `TinyHybridSnapshotFaultGateway` / `TinyHybridMismatchSmoke`；**依赖枢纽** |
| `AbilityKit.Demo.Tiny.Turn.ClientHarness` | 验收驱动库 | 回合制对应物；`Compile Include` 拉 `TinyTurnSession.cs` |
| `AbilityKit.Demo.Tiny.Client` | Exe | 多模式 TCP 驱动（`state`/`frame`/`hybrid`/`session-*`/`process-owner-*`/`process-guest-*`）；同时持有共享源 `TinyNetworkClient.cs`(138) / `TinySessionSmoke.cs`(180) / `TinyFrameSmoke.cs`(266) / `TinyProcessSmoke.cs`(200) |
| `Logic.Sample` | Exe / 3 行 | 章 01 |
| `RoomSample` | Exe / 60 | 章 02（含同命令 ID 重放断言） |
| `StateSample` | Exe / 87 | 章 03 |
| `FrameSample` / `HybridSample` | Exe / 各 24 | 章 04 / 05 |
| `RecoverySample` | Exe / 50 | 章 06（`history` / `overflow` / `mismatch`） |
| `ChapterSamples` | Exe / 5 行 | 聚合跑 04/05/06 三个可导入片段 |
| `Record.Sample` | Exe / 79 | 章 09 离线录制复演 |
| `LiveRecord.Sample` | Exe / 265 | 章 09 真实 Gateway 录制（六次输入、三个快照） |
| `ProtocolEvolution.Sample` | Exe / 89 | 章 09 V1/V2 封套 + schema 协商拒绝 |
| `BattleStyles.Sample` | Exe / 86 | 章 10 回合制 vs 实时并排 |
| `Turn.StateSample` | Exe / 222 | 章 10 双进程回合制（`owner`/`guest` 经 `room.json` 协调） |
| `Replication.Tests` | xunit | 复用真实 `TinyFrameReplication.cs`/`TinyInputBuffer.cs`；**6 例，本轮实测通过** |

**这 19 个工程不在任何受版本管理的 .sln 里**：`src/AbilityKit.sln`（140 工程）、`Server/Orleans/AbilityKit.Orleans.sln`（10 工程）、ET 的 `ET.sln` 的 `tiny` 命中数均为 0。`AbilityKit.Demo.Tiny.Server` 也不在其中，只经 `AbilityKit.Orleans.Host.csproj:12` 与 `AbilityKit.Orleans.Grains.Tests.csproj:12` 的 `ProjectReference` 传递构建。后果：没有 IDE 全量导航/重构覆盖，也不在解决方案级构建里。

> **别被 `Unity/Unity.sln` 误导**：它有 10 个 Tiny 条目，但都是 **Unity 按 asmdef 生成的 csproj**（`AbilityKit.Demo.Tiny.Logic`/`.View`/`.FrameSync`/`.Turn.Logic`/`.Turn.View` 及 5 个测试装配），且 `Unity/Unity.sln` **是生成物、已被 `Unity/.gitignore` 忽略**（未被跟踪）。它们与 `src/` 下手写的同名工程**不是一回事**——同名冲突最典型的是 `AbilityKit.Demo.Tiny.FrameSync`：`src/` 那份是投影 `Runtime/FrameSync/*.cs` 的库，Unity 那份是 `Runtime/FrameSync` 这个 asmdef 的产物。
