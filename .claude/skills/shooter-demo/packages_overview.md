# 6 个包依赖图与职责

## 包清单（Shooter demo 自身均为 0.0.1；其依赖的 abilitykit core/world.*/combat.*/host.*/coordinator 等 25 个框架包已 0.1.0 Beta。Unity 2022.3）

| 包 | 文件数 | 职责 |
|----|-------|------|
| `com.abilitykit.demo.shooter.runtime` | ~80 | 逻辑世界 + Svelto 战斗模拟 + 同步导出 |
| `com.abilitykit.demo.shooter.share` | 1 | 共享常量 |
| `com.abilitykit.demo.shooter.view.runtime` | 121 | 客户端表现/同步/会话/PlayMode |
| `com.abilitykit.demo.shooter.editor` | 6 | Editor 窗口 + SceneView |
| `com.abilitykit.demo.shooter.ai` | 1 | ML-Agents 适配 |
| `com.abilitykit.protocol.shooter` | 7 | 线协议 |

## runtime 关键依赖（package.json）

```
core
host
world.di
world.svelto
share
protocol.shooter
```

**注意**：runtime 的 deps **不含** ability/combat.*/modifiers/attributes/triggering/pipeline——这是 shooter 刻意不复用技能栈的硬证据。

但 runtime **间接**复用了：
- `AbilityKit.Ability.World.DI/Abstractions/Services.Attributes`（基础 DI）
- `AbilityKit.Ability.Host.WorldBlueprints`（世界装配）
- `AbilityKit.Ability.StateSync.Aoi`（AOI 兴趣管理）
- `AbilityKit.World.Svelto`（Svelto 适配）
- `AbilityKit.Network.Runtime`（网络）

## 各包一句话

- **runtime**：战斗内核 + Svelto 适配 + 三类快照导出 + 9 端口 facade + Rollback/Recovery/LagCompensation
- **share**：`ShooterGameplay` 静态常量（RoomType/WorldType/GameplayId=2/TickRate=30/MaxPlayers=4/PlayerHp=1000）
- **view.runtime**：Client 网络（`NetworkSdkClient` + `ShooterRoomGatewayFlow`/`RoomGatewaySessionFlow`）+ Synchronization（三策略）+ Presentation + PlayMode + Replay + Acceptance。⚠️ **不经 coordinator**（view.runtime asmdef 不引用 `AbilityKit.Coordinator`）
- **editor**：`Tools/AbilityKit/Shooter Demo` 菜单 + `ShooterDemoWindow`（3 DriveMode）+ SceneView 渲染
- **ai**：`ShooterAiTrainingEnvironment : IAiEnvironment`（基于 `com.abilitykit.ai.abstractions`，ML-Agents 桥接）
- **protocol.shooter**：MemoryPack 结构体 + Codec（11 opcodes）

## 命名空间与目录树

runtime 根 `Runtime/`：

- `Worlds/` — 世界装配（`ShooterBattleWorldBlueprint` / `ShooterLogicWorld` / `ShooterWorldModule` / `ShooterWorldHost` / `ShooterWorldBlueprintsRegistration` / `ShooterBattleWorldSession` / `ShooterServicesAutoModule`）
- `Domain/Battle/` — 战斗内核（Simulation / State / Rules / Factories / Systems / AI）
- `Domain/Gameplay/Scenario/` — Svelto 性能/场景跑分
- `Infrastructure/Ecs/Svelto/` — Svelto 适配（World / Entities / 组件）
- `Application/` — 端口与同步（RuntimePort / Ports / Services/EntityManager / Synchronization / Rollback / Session）

view.runtime 根 `Runtime/`：

- `Client/` — 网络端点 + Session + Synchronization（三策略 Harness Carrier）+ Gateway
- `Presentation/` — PresentationSession + Snapshot/View + EntityViewModel + ViewEvents
- `PlayMode/` — PlaySessionRunner + LaunchOptions
- `Unity/PlayMode/` — Unity PlayMode 启动器（RemoteStateSync / FrameRecordReplay）
- ~~`Hosting/`~~ — ⚠️ **已删除**（原 coordinator 接入路径 `ShooterCoordinatorSessionHost` / `InputBridge` / `GatewayCoordinatorInputTransport` 均不存在；`ShooterRemoteCoordinatorInputContractTests` 断言其缺席）。网络接入在 `Client/`：`ShooterClientNetworkLauncher`（P2.2 后构建独立 battle `NetworkTransport` 连接）/ `ShooterClientConnectionFactory` / `ShooterRoomGatewayConnection`（P2.2 后 = 房间控制面 + battle-state facade）/ `ShooterClientGatewayLauncher`。战斗数据面（P2.2 两连接拓扑）：`ShooterBattleTransportGatewayClient`（输入上行，`NetworkTransport.SendInputAsync`）+ `ShooterBattleDataPlane`（推送/重连，`RawServerPushReceived`→`ApplyGatewayPush`，主线程 `Drain`）。详见 [coordinator/integration_recipes.md](../coordinator/integration_recipes.md) 的"shooter 真实路径"
- `Network/` — NetworkConditionProvider + PlayModeSessionRegistry
