# MOBA 运行时启动链指南

本文档说明 `com.abilitykit.demo.moba.runtime` 的逻辑世界启动链路。它关注从外部宿主请求创建世界，到 Blueprint 配置、World Module 装配、World DI 注册、Bootstrap Flow 执行、Entitas System 安装与帧内顺序生效的完整路径。

如果只需要新增一个 Bootstrap Stage，请先看 `BootstrapFlowGuide.md`。如果只需要新增系统顺序，请先看 `SystemOrderGuide.md`。本文档负责解释这些机制如何串成一次完整启动。

## 总览

当前推荐启动链路如下：

```text
Host / Session
    -> WorldTypeRegistry
    -> MobaWorldBlueprintsRegistration
    -> MobaLogicWorldBlueprintBase
    -> WorldCreateOptions
    -> EntitasWorld
    -> MobaWorldBootstrapModule
        -> Configure
            -> MobaBootstrapFlow.Configure
                -> Bootstrap Stage Configure
                -> World DI Modules
                -> MobaServicesAutoModule
        -> Install
            -> AutoSystemInstaller
            -> MobaBootstrapFlow.Install
                -> Bootstrap Stage Install
        -> Systems execute by MobaSystemOrder
```

这条链路分为两次装配：

1. 创建前装配：Blueprint 写入 `WorldCreateOptions`，决定 World 类型、模块、服务构建器和上下文工厂。
2. 创建中装配：World Module 执行 `Configure` 与 `Install`，注册服务、安装系统、执行初始化阶段。

## 宿主入口

外部宿主不应直接 new 内部系统，而应通过 World Type 与 Blueprint 创建逻辑世界。

当前默认入口在 `MobaSessionCoordinatorHost`：

```text
CreateLogicWorldHost
    -> new WorldTypeRegistry
    -> MobaWorldBlueprintsRegistration.RegisterAll
    -> new WorldManager(new RegistryWorldFactory(worldTypeRegistry))
    -> new HostRuntime(worldManager, ...)
```

`ConfigureLogicWorldOptions` 会设置基础创建参数：

1. `WorldCreateOptions.Id`：逻辑世界 ID。
2. `WorldCreateOptions.WorldType`：默认是 Battle World。
3. `WorldCreateOptions.ServiceBuilder`：默认 World DI Builder。
4. 宿主注入的配置加载、配置表注册和碰撞服务。

这里适合做宿主级替换，例如控制台模拟、服务端托管、Unity 表现层或 ET Demo 注入自己的加载器。这里不适合写具体技能、Buff 或系统初始化逻辑。

## Blueprint 阶段

`MobaWorldBlueprintsRegistration` 注册默认世界类型：

1. `MobaBattleWorldBlueprint.Type`
2. `MobaLobbyWorldBlueprint.Type`

注册到 `WorldTypeRegistry` 时，会通过 `BlueprintToWorldFactoryAdapter` 包装基础工厂。创建世界时，Blueprint 先修改 `WorldCreateOptions`，再交给基础工厂创建 `EntitasWorld`。

`MobaLogicWorldBlueprintBase.Configure` 的默认步骤是：

```text
ConfigureCommon
    -> ensure ServiceBuilder
    -> set Entitas contexts factory when enabled

ConfigureBlueprintOptions
    -> store MobaLogicWorldBlueprintOptions into options.Extensions

ConfigureModules
    -> append world modules for concrete world type
```

Blueprint 的职责是描述 World 轮廓：

1. WorldType：大厅、战斗或未来的其他逻辑世界。
2. Profile：运行画像，例如 Lobby 或 Battle。
3. Features：是否启用 Entitas、BootstrapFlow、输入、快照、配置、技能、投射物、触发器等能力。
4. Modules：最终会进入 World 创建流程的 `IWorldModule` 列表。

新增 World 类型时，优先新增 Blueprint，而不是在宿主里硬编码分支。

## World Module 阶段

`MobaWorldBootstrapModule` 是当前 MOBA 逻辑世界的默认装配模块。它同时实现：

1. `IWorldModule`：参与 World DI 的 `Configure`。
2. `IEntitasSystemsInstaller`：参与 Entitas Systems 的 `Install`。

它的静态初始化会调用 `MobaBootstrapFlowModule.EnsureInitialized`，确保所有 Bootstrap Stage 已经注册。

### Configure

`MobaWorldBootstrapModule.Configure` 只做一件事：

```text
MobaBootstrapFlow.Configure(builder)
```

具体服务注册交给各个 Stage 和 Module，避免 Bootstrap 类膨胀。

### Install

`MobaWorldBootstrapModule.Install` 做两件事：

```text
AutoSystemInstaller.Install(...)
MobaBootstrapFlow.Install(contexts, systems, services)
```

顺序含义：

1. 先自动安装带 `WorldSystem` 标记的系统。
2. 再执行 Bootstrap Stage 的 Install 逻辑，用于启动末段初始化和需要服务解析器的工作。

新增系统应优先走 `WorldSystem` 标记与 `MobaSystemOrder`，不要手动塞进 Bootstrap 类。

## Bootstrap Flow 阶段

`MobaBootstrapFlow` 从 `MobaBootstrapStageRegistry` 获取 Stage，并分别执行：

1. `Configure` 阶段：注册服务、模块、配置加载器、运行时 Registry。
2. `Install` 阶段：需要 `IContexts`、`Systems`、`IWorldResolver` 后才能执行的初始化。

Stage 通过 `MobaBootstrapStageAttribute` 标记，并由 `MobaBootstrapStageInitializer` 自动发现和注册。

当前主要 Stage 职责：

| Stage | 阶段 | 职责 |
| --- | --- | --- |
| Config | Configure | 注册配置表、DTO 反序列化、配置加载 Profile、`MobaConfigDatabase` |
| CoreState | Configure | 注册默认 World 服务、清理属性注册缓存、替换确定性随机数 |
| WorldModules | Configure | 注册事件总线、触发器 Registry、Entitas 模块、Projectile 模块、MOBA 自动服务模块 |
| Tags | Configure | 注册 GameplayTags 相关数据 |
| TriggerPlans | Configure | 注册触发计划加载与索引 |
| TargetingAndSkills | Configure | 注册事件订阅、触发器索引、技能条件 Registry |
| PlanTriggering | Configure / Install | 注册 Plan Action 及触发执行相关能力 |
| WorldInit | Install | 解析 `WorldInitData`，设置随机种子，应用进入游戏数据 |

Stage 设计原则：

1. 一个 Stage 只表达一个启动阶段。
2. Configure 阶段不依赖已构建完成的服务实例之外的运行时上下文。
3. Install 阶段可以使用 `IWorldResolver` 和 Entitas Contexts。
4. Stage 名称应稳定，因为后续会用于文档、诊断和依赖排序。
5. 临时初始化不要绕过 Stage 直接堆回 `MobaWorldBootstrapModule`。

## 服务注册链路

服务注册来源主要有三类：

1. 宿主在 `ConfigureLogicWorldOptions` 中注入的服务。
2. Bootstrap Stage 通过 `WorldContainerBuilder` 注册的服务。
3. `MobaServicesAutoModule` 扫描 `[WorldService]` 标记注册的服务。

`MobaServicesAutoModule` 当前会组合三个扫描模块：

1. `MobaApplicationServicesModule`：扫描 `AbilityKit.Demo.Moba.Services`。
2. `MobaApplicationSystemsServicesModule`：扫描 `AbilityKit.Demo.Moba.Systems`。
3. `MobaInfrastructureServicesModule`：扫描 `AbilityKit.Demo.Moba.Util` 与 `AbilityKit.Demo.Moba.Util.Generator`。

新增服务的推荐路径：

1. 普通战斗服务：写入业务目录，添加 `[WorldService]`。
2. 需要初始化组合的服务：写一个小型 `IWorldModule`，再由 Stage 加入。
3. 宿主替换服务：在 `WorldCreateOptions.ServiceBuilder` 上注入，或通过 Blueprint/Module 替换。
4. 配置加载、协议、资源适配：放在 Infrastructure 侧，由 Stage 或宿主注入。

避免路径：

1. 在 System 构造函数中 new 复杂服务图。
2. 通过全局静态状态传递战局数据。
3. 在 View 层直接修改运行时服务内部状态。

## 系统安装与排序

系统安装由 `AutoSystemInstaller` 负责扫描并安装。MOBA 系统通过 `WorldSystem` 标记声明：

1. `order`：使用 `MobaSystemOrder` 常量。
2. `Phase`：使用 `WorldSystemPhase.PreExecute`、`Execute` 或 `PostExecute`。

当前业务基准是：

```text
MobaSystemOrder.Base = WorldSystemOrder.CoreBase + 1000
```

主要阶段：

| 阶段 | 代表顺序 | 语义 |
| --- | --- | --- |
| PreExecute / Early | EntityManagerSync、MotionInit | 帧前同步、基础状态准备 |
| Execute / Normal | Motion、PassiveSkillTriggers、SkillPipelines、Effects、Buffs、ContinuousPeriodic | 帧内核心玩法执行 |
| PostExecute / Late | EntityManagerCleanup、ProjectileSync、SummonLifecycle | 帧后同步、清理和生命周期收束 |

新增系统检查：

1. 先判断它是帧前准备、帧内规则还是帧后清理。
2. 选择已有 `MobaSystemOrder` 区间，必要时预留小范围常量。
3. 确认是否依赖其他系统输出。
4. 确认写入状态是否需要快照、回放或状态同步。
5. 更新 `SystemOrderGuide.md`，让顺序变化有文档可追。

## WorldInitData 阶段

`WorldInitStage` 是当前启动链路里最晚的逻辑初始化阶段。

它会：

1. 安装 MemoryPack 序列化适配。
2. 从服务中解析 `WorldInitData`。
3. 反序列化进入游戏请求。
4. 用请求里的随机种子初始化 `RollbackWorldRandom`。
5. 构造 `MobaGameStartSpec`。
6. 调用 `MobaEnterGameFlowService.ApplyGameStartSpec`。
7. 将 `MobaGamePhaseService` 切到 InGame。

这意味着玩家出生、初始队伍、随机种子等开局数据，应通过 World 初始化数据或等价宿主流程进入，而不是由某个系统在第一帧临时猜测。

## 推荐扩展路径

| 目标 | 推荐改动点 |
| --- | --- |
| 新增 Battle/Lobby 之外的 World 类型 | 新增 Blueprint，并注册到 `MobaWorldBlueprintsRegistration` |
| 替换宿主资源加载 | 在 `MobaSessionCoordinatorHost.ConfigureLogicWorldOptions` 或外部宿主注入 `ITextAssetLoader` |
| 新增配置加载阶段 | 扩展 Config Stage 或新增独立 Stage |
| 新增 World 级服务 | `[WorldService]` + `MobaServicesAutoModule` 可扫描命名空间 |
| 新增需要显式组合的服务组 | 新增 `IWorldModule`，并由 Stage 添加 |
| 新增帧系统 | `[WorldSystem]` + `MobaSystemOrder` |
| 新增开局数据应用逻辑 | 扩展 `WorldInitStage` 或抽出独立 Install Stage |
| 新增技能/触发器初始化 | 优先放入 TargetingAndSkills、TriggerPlans 或 PlanTriggering 对应阶段 |

## 禁止绕过的边界

1. 不在 View 包里直接安装逻辑 System。
2. 不在单个技能里创建或替换 World DI 容器。
3. 不在 `MobaWorldBootstrapModule` 里继续追加大量具体业务注册。
4. 不绕过 `MobaSystemOrder` 手动插入帧系统。
5. 不把宿主差异写成 `if Unity / if Server` 的业务分支。
6. 不让临时兼容代码成为新的默认启动路径。

## 排查顺序

启动失败时，按以下顺序排查：

1. WorldType 是否已注册到 `MobaWorldBlueprintsRegistration`。
2. Blueprint 是否把必要 Module 写入 `WorldCreateOptions.Modules`。
3. `MobaBootstrapFlowModule.EnsureInitialized` 是否触发 Stage 注册。
4. Stage 是否带 `MobaBootstrapStageAttribute`，且构造函数可被自动创建。
5. 服务是否在正确阶段注册，生命周期是否正确。
6. System 是否带 `WorldSystem`，所在程序集和命名空间是否被扫描。
7. `MobaSystemOrder` 是否与依赖系统顺序一致。
8. `WorldInitData` 是否存在，Payload 是否能被正确反序列化。

## 与其他文档的关系

1. `RuntimeArchitectureGuide.md`：定义目录职责和分层边界。
2. `BootstrapFlowGuide.md`：定义 Stage 写法和扩展方式。
3. `ServiceRegistrationGuide.md`：定义服务注册约定。
4. `SystemOrderGuide.md`：定义系统排序约定。
5. `SnapshotGuide.md`：定义状态快照与恢复约定。
6. `EventGuide.md`：定义事件发布和订阅约定。
