# 1.3 快速开始：从源码到第一个可运行闭环

> 文档类型：源码接入与验证指南
> 事实基线：2026-08-16
> 文档版本：v3.0
>
> 本文用于建立 AbilityKit 从源码位置、构建入口、Console Demo、测试项目到第一个功能改动的最短运行闭环。本文结论来自 `README.md`、`Unity/Packages/README.md`、`.cursor/rules/src-unity-packages-relation.mdc`、`src/AbilityKit.sln`、Console Demo 启动链路和 P0 相关测试工程。

---

## 1. 快速开始的定位

AbilityKit 不是单个 Unity 插件，而是一组可以在 Unity、纯 .NET、服务端和 Demo 中复用的框架包。仓库中同时存在三类工程入口：

| 入口 | 作用 | 使用边界 |
|------|------|----------|
| `Unity/Packages` | framework/package runtime 的权威源码，也是 Unity Package 入口 | 阅读和修改共享框架源码从这里开始 |
| `src` | .NET SDK 工程，用于构建、测试、Console Demo | 先用它跑通最小闭环 |
| `Server/Orleans` | Orleans 网关、房间、战斗宿主和 Smoke 验证 | 理解多人服务端链路时再进入 |

最小闭环目标：

1. 知道源码和工程引用关系。
2. 跑通一个 Console Demo 或测试。
3. 能顺着一次 Tick 看见配置、阶段、输入、逻辑世界、同步适配器、表现输出的流向。
4. 能定位一个基础模块的源码，例如事件、DI、Host、ECS 查询。

---

## 2. 最小闭环路径

```mermaid
flowchart TD
    A["克隆仓库并打开根目录"] --> B["确认共享 package runtime 以 Unity/Packages 为权威源"]
    B --> C["阅读能力地图和项目结构"]
    C --> D["运行 dotnet build 或 Console Demo"]
    D --> E["观察配置、世界、阶段、自动测试日志"]
    E --> F["按源码入口追一条闭环"]
    F --> G{当前目标}
    G -- 学框架底座 --> H["Core + World.DI + Host"]
    G -- 学玩法表达 --> I["Triggering + Ability + Combat"]
    G -- 学联机同步 --> J["FrameSync + Snapshot + StateSync + Record"]
    G -- 学示例落地 --> K["MOBA / Shooter / ET / Orleans"]
```

这条路径的核心思想是：先跑通运行闭环，再沿一条源码链路扩展到相关模块。

---

## 3. 环境与工程入口

### 3.1 工程入口清单

| 文件/目录 | 说明 |
|-----------|------|
| `README.md` | 仓库级定位、端到端能力管线、文档阅读路径 |
| `Unity/Packages/README.md` | Package 分级、能力组合、模块文档索引 |
| `.cursor/rules/src-unity-packages-relation.mdc` | 明确共享 package runtime 以 `Unity/Packages` 为权威源，镜像工程通过项目文件引用源码 |
| `.cursor/rules/ability-package-structure.mdc` | Runtime、Editor、Samples、package 命名和包结构约束 |
| `src/AbilityKit.sln` | .NET 解决方案入口，包含框架、Demo、测试项目 |
| `Docs/design/00-index.md` | 当前设计文档总索引和源码入口索引 |

### 3.2 为什么先从 `src` 跑

Unity Editor 启动慢，且很多逻辑模块不依赖 Unity 场景。AbilityKit 把核心逻辑放在 `Unity/Packages`，再由 `src/*.csproj` 引用这些源码，所以可以用 .NET 快速验证：

```mermaid
flowchart LR
    P["Unity/Packages<br/>共享 package 权威源码"] --> U["Unity Editor/Player"]
    P --> S["src/*.csproj<br/>Compile Include"]
    S --> T["dotnet build/test"]
    S --> C["Console Demo"]
    P --> O["Server/Orleans<br/>服务端宿主"]
```

这意味着：

- 读源码时看 `Unity/Packages`。
- 跑测试和 Console Demo 时进 `src` 对应工程。
- 不要在 `src` 里复制一份框架源码。
- 如果 `src` 和 Unity 行为不一致，优先检查 `.csproj` 的 `<Compile Include>` 是否覆盖了真正的 package 源码。

---

## 4. 最小运行路径

### 4.1 构建解决方案

在仓库根目录执行：

```powershell
dotnet build src/AbilityKit.sln
```

这个命令会验证 `.NET` 工程引用的 package 源码能否编译。它适合做第一轮总体验证；如果只想看某个能力，可以直接运行具体测试项目。

### 4.2 运行 MOBA Console Demo

```powershell
dotnet run --project src/AbilityKit.Demo.Moba.Console/AbilityKit.Demo.Moba.Console.csproj
```

该 Demo 的入口是 `src/AbilityKit.Demo.Moba.Console/Program.cs`。默认模式设计为创建 `ConsoleBattleBootstrapper`，初始化配置和运行时世界，进入战斗阶段，再由 `AutoTestRunner` 驱动一段完整战斗脚本。它使用 `BootstrapStrict`，所以配置错误会在 World 创建阶段直接终止，不会为了展示流程而降级跳过。

2026-08-16 当前工作区中，默认命令会被 `trigger 10060201 / action[2]` 的 SpawnArea 严格校验阻断：action 覆盖 `duration_ms=300`，Console 配置中的 Area `40060201` 仍有 `delay_ms=400`。因此下面的模式表描述 CLI 入口和设计用途，不表示每种模式当前都能越过启动门禁。

Console Demo 支持几类 CLI 模式：

| 模式 | 命令 | 作用 |
|------|------|------|
| 默认自动测试 | `dotnet run --project src/AbilityKit.Demo.Moba.Console/AbilityKit.Demo.Moba.Console.csproj` | 运行 FullBattleScenario，验证初始化、阶段切换、自动输入 |
| 技能测试 | `dotnet run --project src/AbilityKit.Demo.Moba.Console/AbilityKit.Demo.Moba.Console.csproj -- --skill` | 开启 Trace/Debug 日志，重复执行技能释放场景 |
| 录制 | `dotnet run --project src/AbilityKit.Demo.Moba.Console/AbilityKit.Demo.Moba.Console.csproj -- --record` | 进入记录模式，输出 replay 文件 |
| 回放 | `dotnet run --project src/AbilityKit.Demo.Moba.Console/AbilityKit.Demo.Moba.Console.csproj -- --replay Records/replay_xxx.akrec` | 从指定记录文件回放 |
| 列表 | `dotnet run --project src/AbilityKit.Demo.Moba.Console/AbilityKit.Demo.Moba.Console.csproj -- --list` | 列出现有记录文件 |

同步模式也可以通过环境变量覆盖：

```powershell
$env:SYNC_MODE="SnapshotAuthority"
dotnet run --project src/AbilityKit.Demo.Moba.Console/AbilityKit.Demo.Moba.Console.csproj
```

当前 `ConsoleConfigLoader` 支持的值包括 `Lockstep`、`SnapshotAuthority`、`Hybrid`。如果设置为 `SnapshotAuthority`，`ConsoleBattleBootstrapper.Start()` 会在配置允许时连接 `StateSyncAdapter`。

### 4.3 Console Demo 启动后真实发生什么

源码链路不是“Main 直接创建角色并执行技能”，而是由 bootstrapper、runtime world、phase flow 和 feature host 分层完成：

```mermaid
sequenceDiagram
    participant Main as Program.Main
    participant Boot as ConsoleBattleBootstrapper
    participant Config as ConsoleConfigLoader
    participant DI as WorldContainerBuilder
    participant Runtime as WorldManager/IWorld
    participant Flow as BattleFlow/PhaseHost
    participant Phase as InMatchPhase
    participant Test as AutoTestRunner

    Main->>Boot: new ConsoleBattleBootstrapper(config, mobaConfig, modules, recordConfig)
    Boot->>Config: LoadBattleStartConfig()
    Boot->>Boot: InitializeShareComponents()
    Main->>Boot: Initialize()
    Boot->>DI: ConsoleConfigModule.Configure(builder)
    Boot->>DI: Resolve MobaConfigDatabase / TriggerPlanJsonDatabase
    Boot->>Runtime: CreateRuntimeWorld()
    Runtime-->>Boot: runtime services + input port
    Main->>Boot: Start()
    Boot->>Flow: SetBattleContext(context)
    Boot->>Phase: SetContext + ConfigureFeatures(view/sync/input/hud)
    Boot->>Boot: CreateBattleSession()
    Boot->>Flow: Start() -> Prepare
    Main->>Boot: SetupBattle()
    Boot->>Flow: Connect -> CreateOrJoinWorld -> LoadAssets -> InMatch
    Phase->>Phase: Attach FeatureHost and BattleEntityFeature
    Test->>Boot: TransitionTo(InMatch) / Tick / submit auto input
```

进入 `InMatch` 后，阶段内部还有四步初始化：

```mermaid
flowchart TD
    A["InMatchPhase.OnEnter"] --> B["FeatureHost.Attach"]
    B --> C["BattleEntityFeature 创建并注入 ECS World"]
    C --> D["Step 1: RegisterPlayerEntities"]
    D --> E["按 BattleStartConfig.Players 生成角色实体"]
    E --> F["Step 2: RegisterLocalPlayer"]
    F --> G["Step 3: InitializeBattleState"]
    G --> H["Step 4: NotifyBattleStarted"]
    H --> I["FeatureHost.Tick + runtimeWorld.Tick + SyncAdapter.Tick"]
```

运行后关注三类输出：

| 输出 | 用来判断什么 |
|------|--------------|
| 配置加载日志 | `battle_start`、角色、技能、TriggerPlan 是否加载成功 |
| Runtime world 初始化日志 | `WorldManager`、`MobaWorldBootstrapModule`、服务扫描和输入端口是否装配成功 |
| 阶段与自动测试结果 | `Prepare/Connect/CreateOrJoinWorld/LoadAssets/InMatch` 是否正常推进，脚本、初始化、阶段测试是否通过 |

### 4.4 运行指定测试项目

例如只验证 World DI：

```powershell
dotnet test src/AbilityKit.World.DI.Tests/AbilityKit.World.DI.Tests.csproj
```

例如只验证网络运行时：

```powershell
dotnet test src/AbilityKit.Network.Runtime.Tests/AbilityKit.Network.Runtime.Tests.csproj
```

例如只验证 Shooter Runtime：

```powershell
dotnet test src/AbilityKit.Demo.Shooter.Runtime.Tests/AbilityKit.Demo.Shooter.Runtime.Tests.csproj
```

测试项目适合用来学习模块边界，因为它们通常比 Demo 更小，失败信息也更直接。

### 4.5 当前可执行证据

| 入口 | 2026-08-16 当次结果 | 结论边界 |
|------|---------------------|----------|
| `AbilityKit.Continuous.Tests` | `2/2` | 默认 Manager 的 owner 索引与基本生命周期；不代表 MOBA 五类 runtime 完整闭环 |
| `AbilityKit.Context.Tests` | `5/5` | Registry 事件异常隔离、Clear/Destroy 与 enum 默认值；尚未覆盖 Snapshot/Resolver |
| `AbilityKit.BTCore.Tests` | `3/3` | Rebuild 与重复 Enable 生命周期；不证明随机节点确定性 |
| `BehaviorManagerLifecycleTests` | `2/2` | 外部完成/中断后的 Decision 单次释放；构建仍有既有依赖和可空性警告 |
| `AbilityKit.Demo.Moba.Tests` | `279/305` | 279 项通过；26 项共同被同一个严格配置错误阻断，不能写成 Console 完整 World 当前可运行 |

快速开始应以“最小聚焦测试通过”为第一闭环，再把 Console 作为项目配置、应用装配与失败门禁的综合入口。只有主 MOBA 配置恢复有效并重新执行成功后，才能把默认 Console 路径写成已跑通证据。

---

## 5. 源码阅读链路

### 5.1 Core 事件链路

源码入口：`Unity/Packages/com.abilitykit.core/Runtime/Event/EventDispatcher.cs`、`Unity/Packages/com.abilitykit.core/Runtime/Generic/StableStringIdRegistry.cs`。

```mermaid
sequenceDiagram
    participant User as 调用方
    participant Dispatcher as EventDispatcher
    participant Registry as StableStringIdRegistry
    participant Channel as Channel<TArgs>
    participant Listener as Listener<TArgs>
    participant Pool as Pools

    User->>Dispatcher: Subscribe(eventId, handler, priority, once)
    Dispatcher->>Registry: GetOrRegister(eventId)
    Registry-->>Dispatcher: stable int id
    Dispatcher->>Channel: 按 priority/order 插入 listener
    User->>Dispatcher: Publish(eventId, args)
    Dispatcher->>Channel: Publish(args)
    Channel->>Listener: try Invoke(args)
    Channel->>Channel: 移除 once listener
    Dispatcher->>Pool: autoRelease args / IDisposable / IPoolable
```

这条链路体现了 AbilityKit 底座的几个倾向：稳定 ID、低分配、容错派发、生命周期自动释放。

### 5.2 World DI 链路

源码入口：`Unity/Packages/com.abilitykit.world.di/Runtime/World/DI/WorldContainer.cs`、`Unity/Packages/com.abilitykit.world.di/Runtime/World/DI/WorldScope.cs`。

```mermaid
flowchart TD
    A["IWorldModule.Configure"] --> B["WorldServiceCollection"]
    B --> C["WorldContainer"]
    C --> D{Resolve lifetime}
    D -- Singleton --> E["Root 创建并缓存"]
    D -- Transient --> F["每次创建"]
    D -- Scoped --> G{从哪里解析}
    G -- Root --> H["拒绝: 不能从 Root 解析 Scoped"]
    G -- Scope --> I["WorldScope.GetOrCreate"]
    I --> J["Scope Dispose 时逆序释放"]
    C --> K["CreateScope(seeder)"]
    K --> L["播种 per-battle/per-stage 输入"]
```

`WorldContainer` 管根生命周期，`WorldScope` 管一次世界或一次阶段内的局部生命周期，播种对象由外部持有，不由 scope 释放。

### 5.3 Host Tick 链路

源码入口：`Unity/Packages/com.abilitykit.host/Runtime/Host/Framework/HostRuntime.cs`。

```mermaid
sequenceDiagram
    participant RuntimeLoop as 外部循环
    participant Host as HostRuntime
    participant Options as HostRuntimeOptions
    participant Worlds as IWorldManager
    participant World as IWorld
    participant Client as IServerConnection

    RuntimeLoop->>Host: CreateWorld(options)
    Host->>Options: BeforeCreateWorld
    Host->>Worlds: Create(options)
    Host->>Options: WorldCreated
    Host->>Client: Broadcast(WorldCreatedMessage)
    RuntimeLoop->>Host: Tick(deltaTime)
    Host->>Options: PreTick
    Host->>Worlds: Tick(deltaTime)
    Worlds->>World: Tick(deltaTime)
    Host->>Options: PostTick
```

Host 不直接写业务战斗逻辑，它提供世界生命周期、连接管理、消息广播和 Tick Hook。业务模块通过 World、DI、Host.Extension 或 Demo Runtime 接入。

### 5.4 ECS 查询链路

源码入口：`Unity/Packages/com.abilitykit.world.ecs/Runtime/AbilityKit.World.ECS/Core/EntityQuery.cs`、`Unity/Packages/com.abilitykit.world.ecs/Runtime/AbilityKit.World.ECS/Impl/EntityWorld.cs`。

```mermaid
flowchart TD
    A["world.Query<T1,T2>()"] --> B["ComponentRegistry.GetId<T>()"]
    B --> C["EntityQuery 保存 typeId + world"]
    C --> D["ForEach(visitor)"]
    D --> E["EntityWorld.QueryImpl"]
    E --> F["从第一个组件索引取候选实体"]
    F --> G["复制候选快照，避免遍历中修改集合"]
    G --> H["验证 alive + version"]
    H --> I["检查其他组件是否存在"]
    I --> J["调用 visitor(entity, components)"]
    J --> K["释放快照列表到对象池"]
```

这条链路适合理解 AbilityKit 自研轻量 ECS 的查询思路：用第一个组件索引收窄候选，再在实体组件数组中做类型和存活校验。

### 5.5 Console Demo 一帧 Tick 链路

源码入口：`src/AbilityKit.Demo.Moba.Console/Bootstrap/ConsoleBattleBootstrapper.cs`、`src/AbilityKit.Demo.Moba.Console/Battle/Flow/BattleFlow.cs`、`src/AbilityKit.Demo.Moba.Console/Battle/Flow/Phases/InMatchPhase.cs`。

```mermaid
flowchart TD
    A["ConsoleBattleBootstrapper.Tick"] --> B["更新 LogicTimeSeconds 和 LastFrame"]
    B --> C["BattleFlow.Tick"]
    C --> D["PhaseHost.Tick 当前阶段"]
    D --> E["InMatchPhase.OnTick"]
    E --> F["FeatureHost.Tick view/sync/input/hud"]
    B --> G["runtimeWorld.Tick"]
    G --> H["Moba runtime services/systems"]
    B --> I["cuePresenter.Tick"]
    B --> J["battleView.Tick"]
    B --> K["syncAdapter.Tick"]
    K --> L["actor snapshots"]
    L --> M["ConsoleViewBinder.SyncActor + TickRender"]
    B --> N["ReplayRecorder 可选记录快照"]
```

这条链路把“框架源码”和“示例表现”连起来：阶段和 Feature 管 Console 侧流程，Runtime World 管可复用战斗逻辑，SyncAdapter/快照/表现层把逻辑结果投射到可观察输出。

---

## 6. 边界判断

| 概念 | 设计边界 | 容易混淆的判断 |
|------|----------|----------------|
| `Unity/Packages` | 框架源码唯一位置 | 以为 `src` 也有一份源码可以直接改 |
| `src` | 构建、测试、Console Demo 工程 | 以为它是 Unity 运行时源码根目录 |
| `World` | 逻辑世界和系统执行容器 | 以为它等同 Unity Scene |
| `Host` | 世界运行时外壳和连接外壳 | 以为业务逻辑都写在 `HostRuntime` 里 |
| `WorldScope` | 一段生命周期内的服务解析上下文 | 以为所有服务都应该 Singleton |
| `PhaseHost` | 示例层阶段流，不是通用业务状态机的唯一选择 | 以为所有项目都必须复制 Console 的阶段名 |
| `Snapshot` | 逻辑状态到表现/网络的输出 | 以为表现层可以直接读取逻辑对象 |
| `Demo` | 框架落地参考和验收样板 | 以为业务项目必须完整复制 Demo |

---

## 7. 修改第一个功能时怎么选入口

```mermaid
flowchart TD
    A["我要改一个功能"] --> B{属于哪类}
    B -- 基础事件/对象池/标识 --> C["com.abilitykit.core"]
    B -- 服务装配/生命周期 --> D["com.abilitykit.world.di"]
    B -- 世界运行/连接/Tick --> E["com.abilitykit.host 或 host.extension"]
    B -- 技能触发/条件/动作 --> F["com.abilitykit.triggering"]
    B -- 属性/Buff/伤害/投射物 --> G["combat.* / ability / attributes"]
    B -- 输入帧/快照/同步/回放 --> H["world.framesync / snapshot / statesync / record"]
    B -- 示例接入问题 --> I["demo.moba / demo.shooter / Server/Orleans"]
```

功能改动前需要定位三类入口：

1. 对应 Package 的 `Runtime` 源码。
2. 对应 `src` 工程或测试工程。
3. `Docs/design` 中同能力域的设计文档。

如果只是为了验证框架底座，不要从完整 MOBA Demo 开始改。优先选择 `AbilityKit.World.DI.Tests`、`AbilityKit.Network.Runtime.Tests`、`AbilityKit.Demo.Shooter.Runtime.Tests` 这类更小的测试入口。

---

## 8. 分阶段阅读路径

| 阶段 | 阅读目标 | 关联文档 |
|------|----------|----------|
| 第 1 天 | 建立全局地图，跑通 Demo | `Docs/design/00-index.md`、`Docs/design/01-OverviewAndGettingStarted/00-AbilityKitCapabilityMap.md`、本文 |
| 第 2 天 | 读 World、DI、Host | `Docs/design/02-LogicalWorldDesign/*`、`Docs/design/03-LogicalWorldHostDesign/*` |
| 第 3 天 | 读 Core、事件、对象池、配置 | `Docs/design/05-CommonModules/*` |
| 第 4 天 | 读 Triggering、Skill、Buff、Attribute | `Docs/design/08-GameplayModules/*` |
| 第 5 天 | 读 Snapshot、StateSync、Rollback、Replay | `Docs/design/07-NetworkSynchronization/*` |
| 第 6 天 | 读 MOBA 示例闭环 | `Docs/design/09-ImplementationExamples/03-MOBA%20Demo%20Analysis.md`、`Docs/design/09-ImplementationExamples/MOBA/*` |
| 第 7 天 | 读 Shooter/Orleans 服务端闭环 | `Docs/design/09-ImplementationExamples/04-Shooter%20Demo%20与%20Orleans%20Smoke.md`、`Docs/design/09-ImplementationExamples/Shooter/*` |

---

## 9. 快速开始小结

快速开始的核心路径是从可运行闭环进入源码，再回到设计文档校验边界：

```mermaid
flowchart LR
    A["跑 Demo 或测试"] --> B["看日志"]
    B --> C["选一条链路"]
    C --> D["读源码入口"]
    D --> E["回到设计文档"]
    E --> F["改一个小点并跑测试"]
```

理解 `Unity/Packages`、`src`、`World`、`DI`、`Host`、`Snapshot`、`Demo` 这七个入口后，再阅读 Triggering、Ability、Combat、Network 会更容易对齐源码边界。

---

*文档版本：v3.0 | 最后更新：2026-08-16*
