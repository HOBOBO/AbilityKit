# MOBA Runtime 战斗逻辑层设计：职责边界、输入输出、System/Service 拆分与单元测试

> 本文以 moba.runtime 源码为准，系统性解释逻辑世界的内部结构：职责边界、输入流入 ECS Component 的路径、快照流出到表现层的方式、System 与 Service 的分工规则、世界级 DI 的注册体系，以及用轻量测试环境做单元测试的方法。阅读本文的前提是已理解 ECS 基本概念（Entity、Component、System）和 World 是什么。如果只关心"怎么用"，可以直接跳到第五节和第六节。
>
> 关联文档：本文与 [12-DI 与 System/Service 协作深潜](./12-DIAndSystemServiceCollaborationDeepDive.md) 是互补关系——后者侧重 DI 机制和协作模式，本文侧重逻辑层的职责边界、输入输出链路和测试策略。

---

## 1. 设计目标与能力定位

moba.runtime 包是 AbilityKit 中游戏逻辑最集中的层。接入方看到的只是一个 World 实例：调用 `Submit` 提交输入，通过快照回调接收结果——但 World 内部由几十个 Service 和 System 组成，它们之间有明确的分工规则和依赖关系。

**moba.runtime 解决的六个核心问题：**

| 维度 | 核心问题 | moba.runtime 的答案 |
|---|---|---|
| **L1 职责边界** | 逻辑层和表现层的边界在哪里 | 逻辑层管所有游戏规则，表现层管渲染和反馈 |
| **L2 输入管理** | 玩家输入怎么流入 ECS 世界 | `IWorldInputSink` → `MobaInputCoordinator` → Entitas Component |
| **L3 输出管理** | 游戏状态怎么流出到表现层 | `MobaSnapshotRouter` → 多个 `MobaSnapshotEmitter` → 回调 |
| **L4 System vs Service** | 哪些逻辑放 System，哪些放 Service | System 管帧驱动（Tick），Service 管跨帧状态和单次操作 |
| **L5 DI 体系** | 依赖注入在这里怎么用 | `[WorldService]` 特性 + 扫描注册，三种生命周期 |
| **L6 单元测试** | 怎么测试逻辑层代码 | `MobaRuntimeTestEnvironment` + `BattleTestScript` |

---

## 2. 解决的问题与非目标

### 2.1 moba.runtime 负责什么

逻辑层的职责是**完整的游戏规则执行**。它拥有所有游戏状态的权威副本，负责：

- 接受输入，计算结果，修改状态
- 维护所有实体的生命周期（创建、移动、销毁）
- 执行技能、Buff、投射物、伤害计算
- 发布游戏事件（伤害、死亡、技能开始等）
- 管理帧同步时钟和快照

### 2.2 moba.runtime 不负责什么

- 直接操作 GameObject（这是表现层的职责）
- 网络传输（由 Host / Coordinator 处理）
- 持久化存储（由外部系统处理）
- UI 渲染（由表现层处理）

### 2.3 边界图

```mermaid
flowchart TB
    subgraph Logic["逻辑层（moba.runtime）"]
        L1["接受输入 → 执行规则 → 发布快照"]
        L2["❌ 绝对不：直接操作 GameObject"]
    end

    Logic -.->|"快照回调"| View

    subgraph View["表现层（moba.view / ET Demo）"]
        V1["接收快照 → 创建/更新/销毁 GameObject"]
        V2["❌ 绝对不：修改 HP、位置、Buff 等游戏状态"]
    end

    style L2 fill:#ffcdd2
    style V2 fill:#ffcdd2
    style Logic fill:#e1f5fe
    style View fill:#e8f5e9
```

---

## 3. 源码入口

| 类型 | 源码 | 说明 |
|---|---|---|
| Service 自动注册模块 | `Application/Systems/Bootstrap/MobaServicesAutoModule.cs` | 按命名空间批量注册 Service |
| 世界引导模块 | `Application/Systems/MobaWorldBootstrapModule.cs` | 进入 Flow Bootstrap 并安装 System |
| System 顺序定义 | `Application/Systems/MobaSystemOrder.cs` | 规定 System 执行顺序 |
| System 协作辅助 | `Application/Systems/MobaWorldSystemExecution.cs` | 统一 Resolve / Warn / Require / HandleException |
| 服务基类 | `Application/Services/Templates/GameServiceBase.cs` | 统一日志、生命周期、resolver 持有和事件发布 |
| 技能调度 System | `Application/Systems/Skill/MobaSkillPipelineStepSystem.cs` | 遍历实体并调用 `SkillCastCoordinator.Step()` |
| 技能协调 Service | `Application/Services/Skill/Cast/SkillCastCoordinator.cs` | 处理技能输入、释放准备、策略、runner 生命周期 |
| Buff 调度 System | `Application/Systems/Buffs/MobaBuffCommandDrainSystem.cs` | 触发 `MobaBuffService.DrainPending()` |
| Buff Service | `Application/Services/Buffs/MobaBuffService.cs` | 维护命令队列、生命周期执行器、重入保护、诊断和异常 |
| 玩法 Tick System | `Application/Gameplay/Systems/MobaGameplayTickSystem.cs` | 读取时钟和运行门禁，驱动玩法服务 Tick |
| 玩法 Service | `Application/Gameplay/Core/MobaGameplayService.cs` | 维护玩法阶段、配置解析、触发绑定和生命周期事件 |
| 进场流程 Service | `Application/Services/Lifecycle/MobaEnterGameFlowService.cs` | 处理开局校验、Actor 生成、玩家映射、索引注册和 gameplay 启动 |
| 移动 Tick System | `Application/Systems/Motion/MobaMotionTickSystem.cs` | 移动管线 Tick，含 ECS 组件写回（例外边界） |
| 快照路由 | `Application/Services/Snapshot/MobaSnapshotRouter.cs` | 聚合所有快照发射器，实现 `IWorldStateSnapshotProvider` |
| 输入协调 | `Application/Services/Input/MobaInputCoordinator.cs` | 实现 `IWorldInputSink`，OpCode → Handler 路由 |
| 伤害管线 | `Application/Services/Combat/Damage/DamagePipelineService.cs` | 4 阶段伤害计算 |
| 测试环境 | `Runtime/Testing/MobaRuntimeTestEnvironment.cs` | 聚合测试所需组件 |
| 测试脚本运行器 | `Runtime/Testing/BattleTestScriptRunner.cs` | 脚本化多帧测试 |

---

## 4. 总体结构图

moba.runtime 内部按功能分为八个子域：

```mermaid
flowchart TB
    subgraph Core["核心"]
        EM["MobaEntityManager<br/>实体管理器 + 多索引"]
        AR["MobaActorRegistry<br/>ActorId → Entity 映射"]
        AI["ActorIdIndex<br/>ID 生成器"]
        PA["MobaPlayerActorMapService<br/>PlayerId ↔ ActorId 映射"]
    end

    subgraph IO["输入输出"]
        IC["MobaInputCoordinator<br/>IWorldInputSink 实现"]
        SR["MobaSnapshotRouter<br/>聚合所有快照发射器"]
        IOP["MobaBattleIOPort<br/>对外 IO 端口"]
    end

    subgraph Combat["战斗"]
        DP["DamagePipelineService<br/>伤害管线（4阶段）"]
        DS["MobaDamageService<br/>伤害应用"]
        SS["MobaShieldService<br/>护盾管理"]
        CR["MobaCombatRulesService<br/>战斗规则"]
    end

    subgraph Skill["技能"]
        SCC["SkillCastCoordinator<br/>施放协调器"]
        SP["TableDrivenMobaSkillPipelineLibrary<br/>表驱动技能管线"]
        SL["MobaSkillLoadoutService<br/>技能栏位"]
        SPM["MobaSkillParamModifierService<br/>参数修饰"]
    end

    subgraph Buff["Buff / 持续行为"]
        BS["MobaBuffService<br/>Buff 命令队列"]
        CM["MobaContinuousManager<br/>持续行为管理器"]
        CT["MobaContinuousTagRuleService<br/>标签规则"]
    end

    subgraph Projectile["投射物"]
        PS["MobaProjectileService<br/>投射物协调"]
        PM["MobaProjectileEmitterManager<br/>发射器管理"]
    end

    subgraph Lifecycle["生命周期"]
        EG["MobaEnterGameFlowService<br/>开局实体创建"]
        GP["MobaGameplayService<br/>游戏流程（Phase 管理）"]
        SU["MobaSummonService<br/>召唤物管理"]
    end

    subgraph Snapshot["快照"]
        TS["MobaActorTransformSnapshotService<br/>位置快照（缓冲模式）"]
        SG["MobaActorSpawnSnapshotService<br/>出生快照"]
        SD["MobaDamageEventSnapshotService<br/>伤害快照"]
        SH["MobaStateHashSnapshotService<br/>状态哈希快照"]
        SP["MobaPresentationCueSnapshotService<br/>表现提示快照"]
    end

    EM & AR --> IO
    IC --> Skill
    IC --> Combat
    SP --> Skill
    BS --> Buff
    DP --> Combat
    EG --> Core
    SR --> Snapshot
    CM --> Buff

    style Core fill:#e1f5fe
    style IO fill:#e8f5e9
    style Combat fill:#fff9c4
    style Skill fill:#fff9c4
    style Buff fill:#fff9c4
    style Projectile fill:#fff9c4
    style Lifecycle fill:#e8f5e9
    style Snapshot fill:#f3e5f5
```

---

## 5. 关键运行流程

### 5.1 单一入口原则

moba.runtime 有三个必须经过的单一入口：

| 入口 | 位置 | 禁止的绕过方式 |
|---|---|---|
| **实体创建** | `MobaEnterGameFlowService.ApplyGameStartSpec()` | 直接调用 `ActorArchetypeFactory.Create()` |
| **伤害计算** | `DamagePipelineService.Execute()` | 直接修改 HP 属性 |
| **技能施放** | `SkillCastCoordinator.TryCastBySlot()` | 直接调用 `MobaBuffService.ApplyBuffImmediate()` |

这个原则的验证方法是 grep 源码——如果找到了绕过这些入口的代码，就说明违反了设计。

### 5.2 输入管理：从按键到 ECS Component

#### 5.2.1 入口：IWorldInputSink

表现层调用 `IWorldInputSink.Submit()` 将玩家输入送入逻辑层。这是逻辑层唯一的输入接收口：

```csharp
public interface IWorldInputSink
{
    void Submit(FrameIndex frame, PlayerInputCommand[] commands);
}
```

在 moba.runtime 中，这个接口由 `MobaInputCoordinator` 实现：

```csharp
public sealed class MobaInputCoordinator : LogicWorldInputCoordinatorBase<MobaInputCommandContext>, IWorldInputSink
{
    public void Submit(FrameIndex frame, PlayerInputCommand[] commands)
    {
        // 不在这里处理逻辑，只路由
    }
}
```

#### 5.2.2 OpCode → Handler 路由

输入命令是扁平数组，每条命令有一个 OpCode。`MobaInputCommandContractRegistry` 维护 OpCode → Handler 的映射：

```csharp
public static MobaInputCommandContractRegistry CreateDefault()
{
    var registry = new MobaInputCommandContractRegistry();
    registry.Require(MobaOpCodes.Input.Move, typeof(MobaMoveInputCommandHandler), "Move");
    registry.Require(MobaOpCodes.Input.SkillInput, typeof(MobaSkillInputCommandHandler), "SkillInput");
    registry.Require(MobaOpCodes.Input.DebugSpawnUnit, typeof(MobaDebugSpawnUnitInputCommandHandler), "DebugSpawnUnit");
    registry.Require(MobaOpCodes.Input.DebugReplaceHero, typeof(MobaDebugReplaceHeroInputCommandHandler), "DebugReplaceHero");
    return registry;
}
```

这个注册表是**可扩展的**：新增一个命令只需要注册新的 Handler，不需要修改 `MobaInputCoordinator`。

#### 5.2.3 完整路由链路

```mermaid
sequenceDiagram
    participant View as 表现层
    participant Host as HostRuntime
    participant Driver as MobaBattleDriverHost
    participant IOPort as MobaBattleIOPort
    participant Coord as MobaInputCoordinator
    participant Handler as Moba*InputCommandHandler

    View->>Host: 玩家按键
    Host->>Driver: SubmitInputs(inputs)
    Driver->>Driver: Convert(inputs → commands)
    Driver->>IOPort: Submit(frame, commands)
    IOPort->>Coord: TrySubmit(frame, inputs)
    Note over Coord: OpCode → Handler 查找
    Coord->>Handler: Handle(context, frame, cmd)
    Handler->>Handler: 写入 Entitas Component
    Note over Handler: 写入 MoveInput / SkillInput<br/>由 System 在下一帧消费
```

#### 5.2.4 Move 命令：写入 Component 由 System 消费

Move Handler 不直接修改 Transform，而是写入一个 Component：

```csharp
public void Handle(LogicWorldCommandContext context, FrameIndex frame, PlayerInputCommand cmd)
{
    var move = cmd.Payload.Deserialize<MoveInputPayload>();
    if (!context.TryGetEntity(cmd.ActorId, out var entity))
        return; // 单位不存在，拒绝

    // 写入 MoveInput Component（Entitas Component）
    entity.AddMoveInput(move.TargetX, move.TargetY, frame);
}
```

写入 Component 而不是直接修改 Transform，是 ECS 的基本原则：**数据读写分离**。`MobaMotionTickSystem` 在下一帧读取 `MoveInput` Component，计算出新的 Transform 并回写：

```csharp
// MobaMotionTickSystem.OnExecute
protected override void OnExecute()
{
    var entities = _group.GetEntities(); // hasMotion + hasTransform + hasActorId
    for (int i = 0; i < entities.Length; i++)
    {
        var e = entities[i];
        var m = e.motion;
        var t = e.transform.Value;

        // 读取 MoveInput Component，计算新 Transform
        var state = m.State;
        state.Position = t.Position;
        var result = m.Pipeline.Tick(e.actorId.Value, ref state, dt, ref output);

        // 回写 Transform Component
        e.ReplaceTransform(new Transform3(state.Position, newRot, t.Scale));
    }
}
```

#### 5.2.5 Skill 命令：多阶段处理

Skill 命令比 Move 复杂，因为它有 Press / Hold / Release / Cancel 四个阶段：

```mermaid
flowchart TD
    A["SkillInputEvent"] --> A1["evt.Phase?"]
    A1 -->|"Press"| B1["HandlePressInput<br/>开始施法"]
    A1 -->|"Hold"| B2["HandleHoldInput<br/>持续施法"]
    A1 -->|"Release"| B3["HandleReleaseInput<br/>释放技能"]
    A1 -->|"Cancel"| B4["HandleCancelInput<br/>取消技能"]
    A1 -->|"其他"| B5["MobaSkillCastResult.NotMyTurn<br/>非技能阶段"]
    B1 & B2 & B3 & B4 --> C["SkillCastCoordinator 内部协调"]
    C --> D["最终调用 SkillPipeline 执行"]

    style B1 fill:#c8e6c9
    style B2 fill:#fff9c4
    style B3 fill:#c8e6c9
    style B4 fill:#ffcdd2
    style D fill:#e1f5fe
```

### 5.3 输出管理：快照路由

#### 5.3.1 唯一的输出端口：MobaSnapshotRouter

逻辑层不主动推送数据，表现层通过轮询或回调获取快照。`MobaSnapshotRouter` 是所有快照发射器的聚合器：

```csharp
public sealed class MobaSnapshotRouter : ..., IWorldStateSnapshotProvider
{
    private readonly List<IMobaSnapshotEmitter> _emitters;

    public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
    {
        foreach (var emitter in _emitters)
        {
            if (emitter.TryGetSnapshot(frame, out snapshot))
                return true;
        }
        snapshot = default;
        return false;
    }
}
```

#### 5.3.2 快照发射器的注册

所有快照发射器通过 `[MobaSnapshotEmitter]` 特性标记，由 `MobaSnapshotEmitterRegistry` 扫描注册：

```csharp
[MobaSnapshotEmitter(opcode: 80, priority: 100)]
public sealed class MobaActorTransformSnapshotService : ..., IMobaSnapshotEmitter
{
    // 实现 TryGetSnapshot
}
```

注册表按 OpCode 和优先级组织：

| OpCode | 发射器 | 类型 |
|---|---|---|
| 80 | `MobaActorTransformSnapshotService` | 位置快照（缓冲模式） |
| ~ | `MobaActorSpawnSnapshotService` | 出生快照 |
| ~ | `MobaActorDespawnSnapshotService` | 死亡快照 |
| ~ | `MobaDamageEventSnapshotService` | 伤害事件快照 |
| ~ | `MobaSkillStateSnapshotService` | 技能状态快照 |
| ~ | `MobaStateHashSnapshotService` | 状态哈希快照 |
| ~ | `MobaPresentationCueSnapshotService` | 表现提示快照 |
| ~ | `MobaProjectileEventSnapshotService` | 投射物快照 |
| ~ | `MobaAreaEventSnapshotService` | 区域事件快照 |

#### 5.3.3 快照获取链路

```mermaid
sequenceDiagram
    participant Driver as MobaBattleDriverHost
    participant Dispatcher as MobaTransformSnapshotDispatcher
    participant IOPort as MobaBattleIOPort
    participant Router as MobaSnapshotRouter
    participant Emitter as MobaActorTransformSnapshotService

    Note over Driver: 每帧调用
    Driver->>Dispatcher: TryDispatch(frame, callback)
    Dispatcher->>IOPort: CollectSnapshots(frame, snapshots)
    IOPort->>Router: CollectSnapshots(frame, snapshots)
    Router->>Emitter: TryGetSnapshot(frame)
    Emitter-->>Router: MobaActorTransformSnapshotEntry[]
    Router-->>IOPort: snapshots 填入 Transform + Spawn + Despawn + Damage + ...
    IOPort-->>Dispatcher: snapshots
    Dispatcher-->>Driver: callback(frame, entries)
    Note over Driver: 回调给 HostRuntime → 表现层
```

#### 5.3.4 快照的两种模式

快照发射器有两种工作模式：

| 模式 | 基类 | 特点 | 用途 |
|---|---|---|---|
| **缓冲模式** | `LogicWorldSnapshotBufferEmitterBase<T>` | 按帧缓冲，`Add()` 积累，`TryGetSnapshot` 消费 | 伤害事件、技能状态（事件型快照） |
| **立即模式** | `LogicWorldSnapshotEmitterBase<T>` | 每次查询实时构建 | 位置快照（数据型快照） |

缓冲模式的典型用法：

```csharp
public void ReportDamage(FrameIndex frame, in DamageReport report)
{
    Add(new MobaDamageEventSnapshotEntry(in report));
}

// 由 MobaDamageService.ApplyDamage 内部调用
// 帧末 MobaSnapshotRouter.CollectSnapshots 时一次性取出
```

---

## 6. 生命周期或状态机

### 6.1 System 的执行顺序

所有 System 通过 `[WorldSystem(order: MobaSystemOrder.*)]` 声明执行顺序：

```mermaid
flowchart TB
    subgraph PreExecute["PreExecute 阶段"]
        E1["MobaEntityManagerSyncSystem<br/>同步实体管理器"]
    end

    subgraph Init["Init 阶段"]
        I1["MobaMotionInitSystem<br/>移动初始化"]
    end

    subgraph Execute["Execute 阶段"]
        S1["MobaSkillCastCancelRequestSystem<br/>技能取消请求处理"]
        S2["MobaMotionTickSystem<br/>移动 Tick（调用 Pipeline）"]
        S3["MobaPassiveSkillTriggerRegisterSystem<br/>被动技能触发器注册"]
        S4["MobaSkillPipelineStepSystem<br/>技能管线步进（调用 _skills.Step）"]
        S5["MobaEffectsStepSystem<br/>效果步骤执行"]
        S6["MobaBuffLifecycleReconcileSystem<br/>Buff 生命周期对账"]
        S7["MobaBuffCommandDrainSystem<br/>Buff 命令队列消费（maxCommands=256）"]
        S8["MobaContinuousTickSystem<br/>持续行为 Tick（调用 _continuousManager）"]
    end

    subgraph PostExecute["PostExecute 阶段"]
        P1["MobaEntityManagerCleanupSystem<br/>实体清理"]
        P2["MobaActorDespawnCleanupSystem<br/>出生/死亡快照同步"]
        P3["MobaProjectileSyncSystem<br/>投射物同步"]
        P4["MobaSummonLifecycleSystem<br/>召唤物生命周期"]
        P5["MobaAreaSyncSystem<br/>区域同步"]
        P6["MobaDiagnosticStateSampleSystem<br/>诊断状态采样"]
    end

    PreExecute --> Init --> Execute --> PostExecute

    style Execute fill:#fff9c4
    style Init fill:#e8f5e9
    style PostExecute fill:#e8f5e9
```

### 6.2 启动链路：从 World 创建到第一帧

理解完整的启动链路，有助于在调试时定位问题。

```mermaid
flowchart TB
    A["HostRuntime.CreateWorld"] --> B["WorldTypeRegistry.GetBlueprint<MobaBattleWorldBlueprint>"]
    B --> C["MobaLogicWorldBlueprintBase.Configure"]
    C --> C1["ConfigureCommon<br/>注册 CollisionService<br/>设置 EntitasContextsFactory"]
    C --> C2["ConfigureBlueprintOptions<br/>设置 MobaLogicWorldBlueprintOptions"]
    C --> C3["ConfigureModules<br/>安装 MobaWorldBootstrapModule"]
    C3 --> D["EntitasWorld.Create"]
    D --> E["MobaWorldBootstrapModule.Configure"]
    E --> F["MobaBootstrapFlow.Configure"]
    F --> F1["Config Stage<br/>加载配置数据库"]
    F --> F2["CoreState Stage<br/>注册核心服务"]
    F --> F3["WorldModules Stage<br/>注册事件总线、触发器 Registry"]
    F --> F4["Tags Stage<br/>注册 GameplayTags"]
    F --> F5["TriggerPlans Stage<br/>注册触发计划"]
    F --> F6["PlanTriggering Stage<br/>注册 Plan Action"]
    F --> F7["WorldInit Stage<br/>解析 WorldInitData，应用开局数据"]
    F1 & F2 & F3 & F4 & F5 & F6 & F7 --> G["MobaWorldBootstrapModule.Install"]
    G --> G1["AutoSystemInstaller.Install<br/>扫描 [WorldSystem] 注册 Systems"]
    G --> G2["MobaBootstrapFlow.Install<br/>Stage Install 执行"]
    G1 & G2 --> H["World Ready<br/>Systems 按 MobaSystemOrder 执行"]
    H --> I["MobaEnterGameFlowService.ApplyGameStartSpec()<br/>第一帧：创建所有 Actor"]
```

**Bootstrap Stage 顺序：**

| Stage | 时机 | 做什么 |
|---|---|---|
| **Config** | Configure | 反序列化配置 DTO，构建 `MobaConfigDatabase` |
| **CoreState** | Configure | 注册核心状态 Service，初始化确定性 RNG |
| **WorldModules** | Configure | 注册 Entitas Contexts、事件总线、触发器 Registry |
| **Tags** | Configure | 加载 GameplayTags 数据 |
| **TriggerPlans** | Configure | 加载并索引触发计划 |
| **TargetingAndSkills** | Configure | 注册技能条件 Registry、事件订阅 |
| **PlanTriggering** | Configure + Install | 注册 Plan Action 及执行相关能力 |
| **WorldInit** | Install | 从 `WorldInitData` 恢复世界状态（重连恢复） |

**第一帧的特殊性**：World 创建后不会立即有游戏实体。第一帧的实体创建由 `MobaEnterGameFlowService` 触发：

```csharp
public bool TryApplyGameStartSpec(in MobaGameStartSpec spec)
{
    // 1. 验证开局请求
    ValidateStartRequest(spec);
    // 2. 构建 Actor 列表
    var actors = BuildEnterGameActors(spec);
    // 3. 通过 ActorSpawnPipeline 创建所有实体
    ActorSpawnPipeline.BuildActorsFromEnterGameReqAndInitialize(
        actorContext, actorIds, registry, entities, effectiveReq,
        initializer: (entity, loadout) => { /* 初始化属性、技能 */ },
        onActorBuilt: (entity, loadout) => { /* 发布 Spawn 快照 */ });
    // 4. 绑定 Player 和 Actor 的关系
    BindPlayerActors(spec);
    // 5. 准备游戏流程
    PrepareGameplay(gameplayId);
    // 6. 发布 EnterGame 快照
    PublishEnterGameSnapshots();
    // 7. 切换 Phase 到 Running
    _phase.SetInGame();
}
```

---

## 7. System 和 Service 的分工

### 7.1 核心区别

这是 moba.runtime 里最重要的设计区分：

| 维度 | System | Service |
|---|---|---|
| **驱动方式** | 每帧自动执行（帧驱动） | 由 System 或外部调用（请求驱动） |
| **状态** | 无状态（只读 Component，执行计算） | 有状态（持有字典、队列、注册表等） |
| **生命周期** | 绑定 ECS 生命周期 | 独立生命周期（通过 DI 注入） |
| **典型职责** | 移动 Tick、技能管线步进、Buff 生命周期驱动 | 伤害计算、快照发射、输入路由、配置数据库 |
| **并发** | 多 System 并行遍历 Entities | 通常单例，线程安全由调用方保证 |

### 7.2 System 获取 Service 的方式

System 通过三种方式获取 Service：

```csharp
// 方式 1：构造函数注入（推荐）
public sealed class MobaMotionTickSystem : WorldSystemBase
{
    public MobaMotionTickSystem(global::Entitas.IContexts contexts, IWorldResolver services)
        : base(contexts, services)
    {
    }

    protected override void OnInit()
    {
        Services.TryResolve(out _clock);
        Services.TryResolve(out _hitTriggers);
        _group = Contexts.Actor().GetGroup(ActorMatcher.AllOf(...));
    }
}

// 方式 2：特性注入（字段注入）
public sealed class MobaBuffLifecycleReconcileSystem : WorldSystemBase
{
    [WorldInject] private MobaBuffService _buffs = null;
}

// 方式 3：在 OnInit 中 Resolve
protected override void OnInit()
{
    Services.TryResolve(out _diagnostics);
}
```

### 7.3 Service 持有状态，System 操作状态

Service 持有跨帧状态，System 驱动状态变化：

```csharp
// Service：持有状态
public sealed class MobaBuffService : ..., IWorldService
{
    private readonly Queue<PendingBuffCommand> _pendingCommands = new Queue<PendingBuffCommand>(256);
    private readonly MobaContinuousManager _continuousManager;
    // 跨帧状态：命令队列、持续行为管理器
}

// System：驱动变化
public sealed class MobaBuffCommandDrainSystem : WorldSystemBase
{
    protected override void OnExecute()
    {
        _buffs.DrainPending(maxCommands: 256); // 消费队列
    }
}
```

### 7.4 典型反模式

以下代码是反模式——Service 内部遍历实体：

```csharp
// ❌ 反模式：Service 内部遍历实体
public void Tick()
{
    var entities = _group.GetEntities();
    for (int i = 0; i < entities.Length; i++)
    {
        // Service 内部做 System 的事：遍历并修改
    }
}
```

正确做法是让 System 遍历，Service 只负责"单次操作"：

```csharp
// ✅ 正确：System 遍历，Service 只做单次操作
protected override void OnExecute()
{
    var entities = _group.GetEntities();
    for (int i = 0; i < entities.Length; i++)
    {
        _buffs.TickBuffs(entity, actorId, frame); // 单次操作
    }
}
```

### 7.5 System 不是越薄越好

"MobaRuntime + ECS System"不是要求所有 System 都只能调用一个服务方法。MOBA 里至少有两类合理例外：

- `MobaMotionTickSystem` 会在系统内读取 `motion` 和 `transform` 组件，执行 motion pipeline tick，并写回 `ReplaceTransform` / `ReplaceMotion`。这些逻辑高度贴近 Entitas group 和组件局部性，留在 System 里更直接。
- `MobaProjectileSyncSystem` 会从 `IProjectileService` drain spawn/tick/exit/hit 事件，然后分发给内部 handler，并联动 `MobaEntityManager`、`MobaActorRegistry` 等服务。它是 PostExecute 阶段的事件路由器，不是纯一行 wrapper。

判断边界可以用一个简单规则：**跨系统复用、需要配置/诊断/生命周期、需要单元测试的业务规则放进 Service**；**只服务于当前 phase/group/component 写回的局部流程可以留在 System**。

---

## 8. DI 的作用和注册体系

### 8.1 为什么需要 DI

moba.runtime 有 40+ 个 Service，如果用手动 `new` 创建：

- 依赖关系写死在代码里，无法替换（无法 mock 测试）
- 所有 Service 都要写构造逻辑，重复代码
- 修改一个 Service 的依赖要改所有构造点

DI 解决了三个问题：

| 问题 | DI 解决方案 |
|---|---|
| 依赖谁 | 构造函数声明，按类型注入 |
| 什么时候创建 | 首次解析时创建（默认），或启动时预创建 |
| 怎么管理生命周期 | 三种生命周期：`Singleton` / `Scoped` / `Transient` |

### 8.2 三种注册方式

**方式 1：特性扫描（主要方式）**

```csharp
// [WorldService] 特性标记，框架自动扫描并注册
[WorldService]
public sealed class MobaBuffService : ..., IWorldService { ... }

// [WorldService(typeof(接口))] 注册多个接口
[WorldService(typeof(IMobaBattleInputPort))]
public sealed class MobaBattleIOPort : ..., IWorldService { ... }

// [WorldService(..., Singleton|Scoped)] 指定生命周期
[WorldService(typeof(IMobaGameplayService), Lifetime.Scoped)]
public sealed class MobaGameplayService : ..., IWorldService { ... }
```

**方式 2：特性参数（显式多接口）**

```csharp
[WorldService(typeof(IMobaGameStartPort))]
[WorldService(typeof(IMobaBattleInputPort))]
[WorldService(typeof(IMobaBattleOutputPort))]
public sealed class MobaEnterGameFlowService : ..., IWorldService { ... }
```

**方式 3：手动注册（少量特殊场景）**

```csharp
// 在 MobaLogicWorldBlueprintBase.ConfigureCommon 中
options.ServiceBuilder.Register<ICollisionService>(
    WorldLifetime.Singleton,
    _ => new CollisionService());
```

### 8.3 三种生命周期

| 生命周期 | 含义 | 典型使用 |
|---|---|---|
| `Singleton` | 每个 World 一个实例，全生命周期共享 | `MobaConfigDatabase`、`MobaSnapshotRouter` |
| `Scoped` | 每个 World 一个实例，World 销毁时释放 | `MobaGameplayService`、`MobaBuffService` |
| `Transient` | 每次解析创建新实例 | 极少使用 |

### 8.4 扫描命名空间

Service 的自动扫描由 `MobaServicesAutoModule` 组合三个扫描模块：

```csharp
MobaServicesAutoModule =
    MobaApplicationServicesModule      // AbilityKit.Demo.Moba.Services
  + MobaApplicationSystemsServicesModule // AbilityKit.Demo.Moba.Systems
  + MobaInfrastructureServicesModule   // AbilityKit.Demo.Moba.Util + Util.Generator
```

只要类标记了 `[WorldService]` 且在上述命名空间下，就会被自动注册。

### 8.5 System 的自动安装

System 通过 `[WorldSystem]` 特性标记：

```csharp
[WorldSystem(order: MobaSystemOrder.MotionTick, Phase = WorldSystemPhase.Execute)]
public sealed class MobaMotionTickSystem : WorldSystemBase { ... }
```

`AutoSystemInstaller.Install()` 扫描所有 `[WorldSystem]` 特性，按 `MobaSystemOrder` 排序后安装到 Systems 容器中。

### 8.6 依赖链的构建

以 `MobaMotionTickSystem` 为例，它的完整依赖链是：

```mermaid
flowchart TD
    S["MobaMotionTickSystem<br/>(WorldSystem)"]
    S --> C["构造函数 IWorldResolver"]
    C --> R1["Services.TryResolve"]
    C --> R2["Services.TryResolve"]
    C --> R3["Services.TryResolve"]
    C --> R4["Services.TryResolve"]

    R1 --> CL["MobaClockService<br/>[WorldService] 注册"]
    R2 --> HT["MobaMotionHitTriggerService<br/>[WorldService] 注册"]
    R3 --> AL["MobaActorLookupService<br/>[WorldService] 注册"]
    R4 --> EM["MobaEntityManager<br/>[WorldService] 注册"]

    HT --> DB["依赖 MobaConfigDatabase"]
    HT --> AL2["依赖 MobaActorLookupService"]
    AL --> EM2["依赖 MobaEntityManager"]
    AL --> AR["依赖 MobaActorRegistry"]

    style S fill:#e1f5fe
    style CL fill:#c8e6c9
    style HT fill:#c8e6c9
    style AL fill:#c8e6c9
    style EM fill:#c8e6c9
```

DI 容器自动按依赖顺序构建这条链，不需要调用方手动指定。

---

## 9. 单元测试

### 9.1 测试挑战

逻辑层测试的挑战在于：**游戏状态复杂、外部依赖多、不确定性高**。

- ECS 世界有 40+ 个 Service，互相依赖
- 很多 Service 依赖配置数据（`MobaConfigDatabase`）
- 时间驱动系统（Tick）需要可控的时钟

moba.runtime 的解法是**轻量级测试环境 + 脚本化测试**。

### 9.2 核心测试工具：MobaRuntimeTestEnvironment

`MobaRuntimeTestEnvironment` 聚合了测试所需的所有组件：

```csharp
public sealed class MobaRuntimeTestEnvironment<TCtx>
{
    public MobaTestConfigBuilder ConfigBuilder { get; }
    public MobaConfigDatabase ConfigDatabase { get; private set; }
    public TriggeringTestHarness<TCtx> Triggering { get; }
    public BattleTestScriptRunner BattleRunner { get; }

    public MobaRuntimeTestEnvironment<TCtx> LoadConfig(bool strict = false)
    {
        ConfigDatabase = ConfigBuilder.BuildDatabase(strict);
        return this;
    }
}
```

使用方式：

```csharp
var env = new MobaRuntimeTestEnvironment<ActorContext>()
    .WithConfig(builder => builder
        .AddDtos(Heroes.All)
        .AddDtos(Skills.BattleBasic))
    .LoadConfig(strict: false);

var db = env.ConfigDatabase;
// 用 db 测试配置查询逻辑
```

### 9.3 轻量配置：MobaTestConfigBuilder

不需要真实的配置文件，`MobaTestConfigBuilder` 提供纯内存配置：

```csharp
var builder = new MobaTestConfigBuilder()
    .AddDtos(heroes)   // HeroDto[]
    .AddDtos(skills)   // SkillDto[]
    .AddDtos(buffs)    // BuffDto[]
    .AddDtos(projectiles); // ProjectileDto[]

var database = builder.BuildDatabase(strict: false);
```

内部通过 `MobaConfigDatabase.ReloadFromDtoProvider()` 加载，不需要磁盘文件。

### 9.4 脚本化测试：BattleTestScript

对于复杂的多帧交互，使用 `BattleTestScript` 脚本化测试：

```csharp
public sealed class BattleTestScript
{
    public string Name { get; }
    public List<BattleTestStep> Steps { get; }
}

public sealed class BattleTestStep
{
    public FrameIndex Frame { get; set; }
    public List<PlayerInputCommand> Inputs { get; set; }
    public Action<World, FrameIndex> Assertion { get; set; }
}
```

测试运行器按帧执行步骤，帧末执行断言：

```csharp
public BattleTestScriptRunResult Run(BattleTestScript script, IBattleTestScriptDriver driver)
{
    foreach (var step in script.Steps)
    {
        // 1. 注入输入
        driver.InjectInputs(step.Frame, step.Inputs);
        // 2. 推进一帧
        driver.StepFrame(step.Frame);
        // 3. 执行断言
        step.Assertion(driver.World, step.Frame);
    }
}
```

### 9.5 Mock Service 的策略

对于需要 mock 的 Service（如时钟、网络回调），通过 DI 替换：

```csharp
// 手动注册 mock 时钟
var mockClock = new MockMobaClockService();
env.WithServiceOverride<IMobaClockService>(mockClock);

// 手动推进时钟
mockClock.SetTime(100f); // 100ms
env.StepFrame(new FrameIndex(3));
```

### 9.6 典型测试场景

| 测试场景 | 工具 | 说明 |
|---|---|---|
| 配置数据查询 | `MobaTestConfigBuilder` | 验证 HeroDto / SkillDto 反序列化 |
| 技能施放流程 | `BattleTestScript` | 验证 Press → Hold → Release 完整流程 |
| 伤害计算 | `MobaRuntimeTestEnvironment` + Mock | 验证护盾减免、伤害上限 |
| Buff 叠加 | `BattleTestScript` | 验证同 Buff 叠加层数、时长刷新 |
| 移动碰撞 | Mock 物理 + `BattleTestScript` | 验证移动命中触发 |

### 9.7 测试金字塔

```mermaid
flowchart TD
    T1["端到端测试（真实 World）<br/>完整启动链路，覆盖全路径<br/>覆盖：10%"] --> T2
    T2["单元测试（MobaRuntimeTestEnvironment + Mock）<br/>单 Service 逻辑：DamagePipeline、BuffApply<br/>覆盖：20%"] --> T3
    T3["集成测试（BattleTestScript + World）<br/>多 Service 交互：技能 + Buff + 伤害<br/>覆盖：70%"]

    style T1 fill:#ffcdd2
    style T2 fill:#fff9c4
    style T3 fill:#c8e6c9
```

---

## 10. 设计约束

### 10.1 System 的三条禁令

| 禁令 | 原因 | 正确做法 |
|---|---|---|
| System 禁止持有跨帧状态 | System 每次 Tick 都是新的实例或重置，状态会丢失 | 跨帧状态放 Service |
| System 禁止创建另一个 System | 依赖关系由 DI 管理，手动创建破坏拓扑 | 通过 DI 注入 |
| System 禁止直接修改其他 Entity 的 Component | ECS 的组件修改应在同一 System 内批量完成 | 通过 Service 方法修改 |

### 10.2 Service 的三条禁令

| 禁令 | 原因 | 正确做法 |
|---|---|---|
| Service 禁止直接遍历 Entities | 这是 System 的职责，Service 遍历会绕过 System 顺序保证 | System 遍历后调用 Service 单次操作 |
| Service 禁止持有 ECS Context 引用 | ECS Context 属于 System 层，Service 应该无感知 | 只通过 Entity ID 操作 |
| Service 禁止直接发布快照 | 快照发布应由 MobaSnapshotRouter 统一管理 | 通过快照发射器机制 |

### 10.3 禁止的绕过方式

```mermaid
flowchart LR
    subgraph Banned["禁止的绕过方式"]
        B1["❌ 绕过 MobaEnterGameFlowService 创建实体"]
        B2["❌ 绕过 DamagePipelineService 计算伤害"]
        B3["❌ 绕过 SkillCastCoordinator 施放技能"]
        B4["❌ 绕过 MobaSnapshotRouter 订阅快照"]
    end

    B1 --> R1["直接调用 ActorArchetypeFactory.Create()"]
    B2 --> R2["直接修改 HP：entity.attributes.Hp -= damage"]
    B3 --> R3["直接调用 _skillRuntime.Start()"]
    B4 --> R4["直接读取 ECS Component 作为快照"]

    style B1 fill:#ffcdd2
    style B2 fill:#ffcdd2
    style B3 fill:#ffcdd2
    style B4 fill:#ffcdd2
    style R1 fill:#ffcdd2
    style R2 fill:#ffcdd2
    style R3 fill:#ffcdd2
    style R4 fill:#ffcdd2
```

---

## 11. 验证入口与证据状态

### 11.1 源码阅读路径

1. `MobaInputCoordinator.Submit()` → `MobaInputCommandContractRegistry` → Handler 路由
2. `MobaSkillPipelineStepSystem.OnExecute()` → `SkillCastCoordinator.Step()` → SkillPipeline
3. `MobaSnapshotRouter.CollectSnapshots()` → 各 `MobaSnapshotEmitter` → 快照输出
4. `MobaBuffService.DrainPending()` → `BuffLifecycleExecutor` → Buff 生命周期
5. `MobaEnterGameFlowService.TryApplyGameStartSpec()` → `ActorSpawnPipeline` → 实体创建
6. `MobaRuntimeTestEnvironment` → `BattleTestScriptRunner` → 测试入口

### 11.2 证据状态

| 证据类型 | 状态 | 说明 |
|---|---|---|
| 源码事实 | ✅ 已验证 | 所有类名、方法名、常量与源码一致 |
| 运行验证 | ✅ 已验证 | Console Demo 可完整运行输入→技能→快照流程 |
| 单元测试 | ⚠️ 需加强 | `MobaRuntimeTestEnvironment` 已建立，测试覆盖需扩展 |
| 集成测试 | ⚠️ 需加强 | `BattleTestScript` 模式已建立，覆盖场景有限 |

---

## 12. 关联文档

| 文档 | 关系 |
|---|---|
| [12-DI 与 System/Service 协作深潜](./12-DIAndSystemServiceCollaborationDeepDive.md) | 互补：侧重 DI 机制和协作模式 |
| [21-MOBA 战斗逻辑层实战指南](../../../AbilityKit战斗逻辑层设计草稿.md) | 互补：侧重实战指南（能力组合、扩展模式、错误清单、7 步上手） |
| [01-世界启动与运行时装配](./01-WorldAndBootstrap.md) | 深入理解 World 创建和 Bootstrap Stage |
| [05-技能执行深潜](./05-SkillExecutionDeepDive.md) | 深入理解技能施放的四阶段处理 |
| [03-Buff、Projectile 与 Damage 管线](./03-BuffProjectileDamage.md) | 深入理解 Buff 命令链路和伤害计算 |
| [04-快照、表现层与预测回滚](./04-SnapshotPresentationPrediction.md) | 深入理解快照路由和表现层去重 |
| [06-配置、实体索引与生成深潜](./06-ConfigEntitySpawnDeepDive.md) | 深入理解 ActorSpawnPipeline 和实体生成 |
| [08-玩法能力地图](../08-GameplayModules/00-GameplayCapabilityMap.md) | 横向看 Triggering、Ability、Combat 等能力组合 |

---

*文档版本：v1.0 | 状态：canonical | 最后更新：2026-07-22 | 基于 AbilityKit moba.runtime 源码 v2026-Q3*
