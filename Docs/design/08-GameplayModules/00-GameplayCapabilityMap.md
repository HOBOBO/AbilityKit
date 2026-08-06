# 玩法能力地图

AbilityKit 的玩法层不是一个单体技能系统，而是一组可组合的战斗表达原语：Triggering 负责“事件-条件-动作”执行，Ability 负责 GameplayEffect 生命周期，Combat 包提供投射物、目标搜索、伤害、运动和实体管理等领域原语，Config/CodeGen 负责把配置转为可执行计划。

---

## 1. 玩法层总体结构

```mermaid
flowchart TB
    subgraph Input["玩法输入"]
        Config["配置/Luban/ActionSchema"]
        Code["手写 C# 逻辑"]
        Event["运行时事件"]
    end

    subgraph Triggering["Triggering 执行层"]
        EventBus["EventBus"]
        Runner["TriggerRunner"]
        Plan["TriggerPlan"]
        ExecCtx["ExecCtx"]
        Func["FunctionRegistry"]
        Action["ActionRegistry"]
        Scheduler["ActionSchedulerManager"]
    end

    subgraph Ability["Ability 效果层"]
        EffectService["EffectService"]
        EffectContainer["EffectContainer"]
        EffectSpec["GameplayEffectSpec"]
        EffectInstance["EffectInstance"]
        Component["IEffectComponent"]
        Cue["IGameplayEffectCue"]
    end

    subgraph Combat["战斗原语"]
        Targeting["TargetSearchEngine"]
        Projectile["ProjectileWorld"]
        Damage["DamageCalculationPipeline"]
        Attribute["Attributes/Modifiers"]
        Motion["Motion"]
        Entity["EntityManager"]
    end

    subgraph Runtime["运行时承载"]
        World["World/DI"]
        ECS["ECS Systems"]
        Frame["FrameSync/Rollback"]
        Snapshot["Snapshot/View"]
    end

    Config --> Plan
    Code --> Runner
    Event --> EventBus
    EventBus --> Runner
    Plan --> Runner
    Runner --> ExecCtx
    ExecCtx --> Func
    ExecCtx --> Action
    Runner --> Scheduler
    Runner --> EffectService
    EffectService --> EffectContainer
    EffectContainer --> EffectSpec
    EffectContainer --> EffectInstance
    EffectContainer --> Component
    EffectContainer --> Cue
    Component --> Attribute
    Component --> Projectile
    Component --> Damage
    Action --> Targeting
    Action --> Projectile
    Action --> Damage
    World --> EffectService
    ECS --> EffectContainer
    Projectile --> Frame
    Damage --> Snapshot
```

---

## 2. Triggering：通用事件条件动作执行器

### 2.1 设计定位

Triggering 解决的是“玩法逻辑如何被数据化、可排序、可观察、可中断地执行”。它不内置 Buff、投射物或伤害语义，而是提供通用执行主线：

1. 订阅事件。
2. 将多个触发器按 `Phase -> Priority -> Order` 排序。
3. 为每次事件创建 `ExecCtx`。
4. 评估条件。
5. 执行动作。
6. 处理中断、生命周期回调、观察者、Cue 和延迟 ActionScheduler。

关键源码：

| 类型 | 源码 | 职责 |
|------|------|------|
| TriggerRunner | `Unity/Packages/com.abilitykit.triggering/Runtime/Triggering/Runner/TriggerRunner.cs` | 运行时主线编排器 |
| TriggerRunnerRuntimeServices | `Unity/Packages/com.abilitykit.triggering/Runtime/Triggering/Runner/TriggerRunnerRuntimeServices.cs` | 创建 ExecCtx 所需的 registry 和 resolver |
| TriggerRunnerEntry | `Unity/Packages/com.abilitykit.triggering/Runtime/Triggering/Runner/TriggerRunnerEntry.cs` | 保存 phase/priority/order 和 trigger |
| TriggerPlan | `Unity/Packages/com.abilitykit.triggering/Runtime/Plans` | 数据化触发器计划 |
| ActionSchedulerManager | `Unity/Packages/com.abilitykit.triggering/Runtime/Runtime/ActionScheduler` | 延迟动作/计划内调度 |

### 2.2 执行流程

```mermaid
sequenceDiagram
    participant Game as 游戏逻辑
    participant Bus as IEventBus
    participant Runner as TriggerRunner<TCtx>
    participant List as Trigger Entries
    participant Ctx as ExecCtx<TCtx>
    participant Trigger as ITrigger<TArgs,TCtx>
    participant Action as ActionRegistry/Actions
    participant Life as Lifecycle/Observer/Cue

    Game->>Bus: Publish(EventKey<TArgs>, args)
    Bus->>Runner: Dispatcher.OnEvent(args, control)
    Runner->>List: 读取并按 Phase/Priority/Order 遍历
    Runner->>Ctx: CreateExecCtx(control)
    loop each trigger
        Runner->>Life: OnBeforeEvaluate
        Runner->>Trigger: Evaluate(args, ctx)
        alt 条件失败
            Runner->>Life: OnConditionFailed / ShortCircuit Cue
        else 条件通过
            Runner->>Life: OnConditionPassed
            Runner->>Trigger: Execute(args, ctx)
            Trigger->>Action: 调用数据化或手写 Action
            alt Stop/Cancel
                Runner->>Life: OnShortCircuit
                Runner-->>Bus: 结束后续触发器
            end
        end
    end
    Runner->>Life: OnEventDispatched
```

### 2.3 设计边界

- Triggering 管“何时执行、是否执行、按什么顺序执行”。
- Ability/Combat 管“执行的业务含义是什么”。
- `ExecCtx` 是扩展点聚合对象，承载上下文、函数注册表、动作注册表、黑板、数值域和执行控制。
- `ExecutionControl` 把 StopPropagation/Cancel 等控制语义从具体 Action 中抽出，避免 Action 直接操作 Runner 内部结构。

---

## 3. Ability：GameplayEffect 生命周期

### 3.1 设计定位

Ability 包把 Triggering 和战斗原语组合成“可施加、可持续、可周期触发、可移除”的 GameplayEffect 体系。

关键源码：

| 类型 | 源码 | 职责 |
|------|------|------|
| EffectService | `Unity/Packages/com.abilitykit.ability/Runtime/Ability/Effect/EffectService.cs` | 对外发布 Effect 事件、一次性评估/执行 Trigger |
| EffectContainer | `Unity/Packages/com.abilitykit.ability/Runtime/Ability/Effect/EffectContainer.cs` | 管理 active effects、Apply/Step/Remove 生命周期 |
| GameplayEffectSpec | `Unity/Packages/com.abilitykit.ability/Runtime/Ability/Effect/GameplayEffectSpec.cs` | 效果配置规格：时长、周期、Tag、组件、Cue |
| EffectInstance | `Unity/Packages/com.abilitykit.ability/Runtime/Ability/Effect/EffectInstance.cs` | 效果运行时实例状态 |
| IEffectComponent | `Unity/Packages/com.abilitykit.ability/Runtime/Ability/Effect/IEffectComponent.cs` | OnApply/OnTick/OnRemove 组件扩展点 |

### 3.2 Apply/Step/Remove 生命周期

```mermaid
flowchart TD
    A["EffectContainer.Apply(spec, context)"] --> B{ApplicationRequirements 满足?}
    B -- 否 --> X["返回 null"]
    B -- 是 --> C["创建 EffectInstance"]
    C --> D["Publish Apply TriggerEvent"]
    D --> E["向 TargetTags 添加 GrantedTags"]
    E --> F["逐个组件 OnApply"]
    F --> G["Cue.OnActive"]
    G --> H["加入 _active"]
    H --> I{Instant?}
    I -- 是 --> R["Remove(instanceId)"]
    I -- 否 --> J{ExecutePeriodicOnApply?}
    J -- 是 --> K["TickInstance"]
    J -- 否 --> L["设置 NextTickInSeconds"]
    K --> L

    M["EffectContainer.Step(context)"] --> N["ElapsedSeconds += dt"]
    N --> O["Cue.WhileActive"]
    O --> P{Duration?}
    P -- 是 --> Q["RemainingSeconds -= dt"]
    P -- 否 --> S["保持"]
    Q --> T{Period 到达?}
    S --> T
    T -- 是 --> U["TickInstance: Publish Tick + OnTick"]
    T -- 否 --> V{"Remaining <= 0?"}
    U --> V
    V -- 是 --> R
    V -- 否 --> W["继续存活"]

    R --> R1["从 _active 移除"]
    R1 --> R2["Publish Remove TriggerEvent"]
    R2 --> R3["Cue.OnRemove"]
    R3 --> R4["组件 OnRemove"]
    R4 --> R5["移除 GrantedTags"]
```

### 3.3 设计要点

- Apply 前先检查 Tag 需求，避免无效效果进入 active 列表。
- Apply、Tick、Remove 都会发布默认 TriggerEvent，使效果生命周期可以继续驱动 Triggering。
- 组件模型把属性修改、触发事件、投射物联动等能力从 EffectContainer 中剥离。
- `IEffectTriggeringSwitch` 允许在特定上下文关闭默认触发事件，避免递归或测试噪音。

---

## 4. Projectile：确定性投射物世界

### 4.1 设计定位

Projectile 模块提供逻辑层投射物模拟：生成、飞行、碰撞、穿透/命中策略、返回施法者、退出事件、回滚导入导出。

关键源码：

| 类型 | 源码 | 职责 |
|------|------|------|
| ProjectileWorld | `Unity/Packages/com.abilitykit.combat.projectile/Runtime/Projectile/Runtime/ProjectileWorld.cs` | 投射物集合、Tick、碰撞、回滚 |
| ProjectileSpawnParams | `Unity/Packages/com.abilitykit.combat.projectile/Runtime/Projectile/Runtime/ProjectileSpawnParams.cs` | 生成参数 |
| IProjectileHitPolicy | `Unity/Packages/com.abilitykit.combat.projectile/Runtime/Projectile/Policies/IProjectileHitPolicy.cs` | 命中后是否退出、剩余命中次数等策略 |
| IProjectileHitFilter | `Unity/Packages/com.abilitykit.combat.projectile/Runtime/Projectile/Filters/IProjectileHitFilter.cs` | 命中过滤 |
| ProjectileRollbackProvider | `Unity/Packages/com.abilitykit.combat.projectile/Runtime/Projectile/Rollback/ProjectileRollbackProvider.cs` | 接入 RollbackCoordinator |

### 4.2 Tick 流程

```mermaid
flowchart TD
    A["ProjectileWorld.Tick(frame, fixedDelta)"] --> B{active 是否为空?}
    B -- 是 --> Z["结束"]
    B -- 否 --> C["遍历 active projectile"]
    C --> D{"LifetimeFramesLeft <= 0?"}
    D -- 是 --> E["产生 Lifetime ExitEvent 并移除"]
    D -- 否 --> F{ReturnAfterFrames 到达?}
    F -- 是 --> G["标记 IsReturning"]
    F -- 否 --> H["计算速度与移动距离"]
    G --> G1{能找到返回目标?}
    G1 -- 否 --> G2["ReturnTargetLost ExitEvent 并移除"]
    G1 -- 是 --> H
    H --> I{"move <= 0?"}
    I -- 是 --> J["LifetimeFramesLeft--"]
    I -- 否 --> K["沿方向 Raycast"]
    K --> L{命中?}
    L -- 否 --> M["移动到终点"]
    L -- 是 --> N["HitFilter/冷却/去重"]
    N --> O{允许命中?}
    O -- 否 --> K
    O -- 是 --> P["产生 ProjectileHitEvent"]
    P --> Q["HitPolicy 更新剩余命中/是否退出"]
    Q --> R{退出?}
    R -- 是 --> S["ExitEvent 并移除"]
    R -- 否 --> K
```

### 4.3 回滚设计

ProjectileWorld 自身支持 `ExportRollback(frame)` 与 `ImportRollback(frame,payload)`，导出内容包含 active projectile 列表和 `_nextId`。这使投射物可以参与帧同步回滚：

```mermaid
sequenceDiagram
    participant Rollback as RollbackCoordinator
    participant Provider as ProjectileRollbackProvider
    participant World as ProjectileWorld
    participant Codec as BinaryObjectCodec

    Rollback->>Provider: Export(frame)
    Provider->>World: ExportRollback(frame)
    World->>Codec: Encode(SnapshotPayload)
    Codec-->>World: byte[]
    World-->>Provider: payload

    Rollback->>Provider: Import(frame,payload)
    Provider->>World: ImportRollback(frame,payload)
    World->>World: Clear active projectiles
    World->>Codec: Decode<SnapshotPayload>
    World->>World: 恢复 nextId 和 active projectiles
```

---

## 5. Targeting：候选-过滤-评分-选择流水线

Targeting 模块用于把“找目标”拆成可组合流水线：候选来源、规则过滤、评分器、选择器、结果映射。

关键源码：

| 类型 | 源码 | 职责 |
|------|------|------|
| TargetSearchEngine | `Unity/Packages/com.abilitykit.combat.targeting/Runtime/SearchTarget/Execution/TargetSearchEngine.cs` | 搜索主流程 |
| SearchQuery | `Unity/Packages/com.abilitykit.combat.targeting/Runtime/SearchTarget/Queries/SearchQuery.cs` | 查询描述 |
| SearchContext | `Unity/Packages/com.abilitykit.combat.targeting/Runtime/SearchTarget/Execution/SearchContext.cs` | 框架能力属性与包外强类型扩展数据租约 |
| ICandidateProvider | `Unity/Packages/com.abilitykit.combat.targeting/Runtime/SearchTarget/Providers/ICandidateProvider.cs` | 候选目标来源 |
| ITargetRule | `Unity/Packages/com.abilitykit.combat.targeting/Runtime/SearchTarget/Rules/ITargetRule.cs` | 过滤规则 |
| ITargetScorer | `Unity/Packages/com.abilitykit.combat.targeting/Runtime/SearchTarget/Scorers/ITargetScorer.cs` | 评分规则 |
| ITargetSelector | `Unity/Packages/com.abilitykit.combat.targeting/Runtime/SearchTarget/Selectors/ITargetSelector.cs` | 完整命中选择策略 |
| IStreamingTopKByScoreSelector | `Unity/Packages/com.abilitykit.combat.targeting/Runtime/SearchTarget/Selectors/ITargetSelector.cs` | 声明接受引擎严格 Top-K 语义的融合能力接口 |

```mermaid
flowchart TD
    A["SearchIds(query, context)"] --> B["清空 results 并获取 stats/keyProvider"]
    B --> C{Provider 是否存在?}
    C -- 否 --> X["返回空"]
    C -- 是 --> D{Selector 实现 Streaming Top-K 能力且 MaxCount 大于 0?}
    D -- 是 --> E["租借 K 个命中槽和评分缓冲"]
    E --> F["Provider.ForEachCandidate"]
    F --> G["规则与评分成功后提交去重键并 Offer"]
    G --> H["直接写出最多 K 个结果"]
    D -- 否 --> I["租借完整命中与评分缓冲"]
    I --> J["Provider.ForEachCandidate"]
    J --> K["规则与评分成功后提交去重键并 Collect"]
    K --> L{query.Selector 存在?}
    L -- 是 --> M["Selector.Select(完整 SearchHitView)"]
    L -- 否 --> N["按多排序项和稳定键完整排序"]
    M --> O["Writer 按 MaxCount 写出结果"]
    N --> O
```

设计要点：

- 位置、稳定键和统计是 `SearchContext` 的显式强类型属性；位置由具体 Rule 或 Scorer 按需读取，引擎不做全局 `RequiresPosition` 预检。
- 包外单次查询数据使用静态 `SearchContextKey<T>` 并由业务 facade 管理；上下文不提供通用服务定位器或整数键黑板。
- `ClearData()` 只清扩展数据，完整清理和池化归还还会清空框架能力引用，避免跨租约残留。普通构造的 Context 在 Dispose 时只清理自身，池化 Context 才归还全局池。
- 两条执行路径都只消费一次 Provider 最终推送的候选流。复合 Provider 仍可能为集合语义遍历多个来源。
- Selector 实现 `IStreamingTopKByScoreSelector` 且 `MaxCount > 0` 时，Rule、Scorer 与固定 Top-K 维护融合在候选回调中，不保存完整命中。
- 自定义 Selector 保留完整只读命中视图，用额外的 `O(H × M)` 评分存储换取加权随机、分组、采样等全局后处理能力；所有输出路径都由 Writer 强制 `MaxCount` 硬上限。
- 查询级去重键只在规则通过且全部评分有效后提交，失败候选不会抑制后续同键候选。
- 融合路径当前使用有序小数组插入，最坏约 `O(H × K × M)`，适合 K 远小于命中数的场景；并非堆式 `O(H log K)`。
- 多个排序项按声明顺序严格字典序比较，每项独立升降序，全部同分时按稳定键升序决胜。
- 池保留常规峰值容量，超阈值列表缩回初始容量，超阈值命中与评分数组释放底层存储。

---

## 6. Damage：Dataflow 伤害计算管线

Damage 模块把伤害计算拆成 Dataflow processor 链。默认管线顺序是：验证、暴击、基础伤害、加成、护甲、魔抗、最终伤害、溢出。

关键源码：

| 类型 | 源码 | 职责 |
|------|------|------|
| DamageCalculationPipeline | `Unity/Packages/com.abilitykit.combat.damage/Runtime/Damage/Processor/DamageProcessors.cs` | 默认伤害 processor 链 |
| DamageSlots | `Unity/Packages/com.abilitykit.combat.damage/Runtime/Damage/Processor/DamageProcessors.cs` | 强类型 DataflowSlot，避免魔法字符串 |
| DamageCalculationContext | `Unity/Packages/com.abilitykit.combat.damage/Runtime/Damage/Data/DamageCalculationContext.cs` | 伤害上下文与中间结果 |
| DamageData/DamageRequest/DamageResult | `Unity/Packages/com.abilitykit.combat.damage/Runtime/Damage/Data/DamageData.cs` | 输入输出数据结构 |

```mermaid
flowchart LR
    Req["DamageRequest"] --> V["ValidateDamageProcessor"]
    V --> Crit["CalculateCriticalProcessor"]
    Crit --> Base["CalculateBaseDamageProcessor"]
    Base --> Bonus["ApplyDamageBonusProcessor"]
    Bonus --> Armor["ApplyArmorReductionProcessor"]
    Armor --> MR["ApplyMagicResistReductionProcessor"]
    MR --> Final["CalculateFinalDamageProcessor"]
    Final --> Overkill["CalculateOverkillProcessor"]
    Overkill --> Result["DamageResult"]
```

设计要点：

- 暴击随机值通过 `DamageSlots.CritRoll` 从上层注入，便于确定性、回放和测试。
- Processor 之间通过 `DamageCalculationContext.Result` 传递中间结果。
- `context.Abort()` 能在请求无效时停止后续计算。

---

## 7. Record/Replay 与玩法验证

Record 模块不是玩法层的一部分，但它是玩法问题复现和确定性验证的重要支撑。

关键源码：

| 类型 | 源码 | 职责 |
|------|------|------|
| BasicReplayController | `Unity/Packages/com.abilitykit.record/Runtime/Record/Core/Replay/BasicReplayController.cs` | 按 ReplayClock 消费指定帧事件并交给 handler |
| RecordContainer | `Unity/Packages/com.abilitykit.record/Runtime/Record/Core/Container/RecordContainer.cs` | 记录容器 |
| EventTrack | `Unity/Packages/com.abilitykit.record/Runtime/Record/Core/Tracks/EventTrack.cs` | 事件轨道 |
| FrameRecordSink | `Unity/Packages/com.abilitykit.record/Runtime/Record/FrameRecord/FrameRecordSink.cs` | 按帧输入记录 |

回放 Tick 流程：

```mermaid
sequenceDiagram
    participant RuntimeLoop as 外部 Tick
    participant Controller as BasicReplayController
    participant Clock as IReplayClock
    participant Reader as IEventTrackReader
    participant Handler as IReplayEventHandler

    RuntimeLoop->>Controller: Tick(deltaTime)
    alt IsPlaying
        loop TryConsume(deltaTime)
            Controller->>Clock: TryConsume(deltaTime, out nextFrame)
            Clock-->>Controller: nextFrame
            Controller->>Reader: TryGetEvents(nextFrame)
            Reader-->>Controller: events
            loop each event
                Controller->>Handler: Handle(event)
            end
            Controller->>Controller: deltaTime = 0
        end
    else paused
        Controller-->>RuntimeLoop: return
    end
```

---

## 8. 从一次技能释放看模块协作

```mermaid
sequenceDiagram
    participant Input as 玩家输入/AI 决策
    participant Skill as 技能逻辑
    participant Targeting as TargetSearchEngine
    participant Effect as EffectContainer
    participant Trigger as TriggerRunner
    participant Projectile as ProjectileWorld
    participant Damage as DamagePipeline
    participant Snapshot as Snapshot/View

    Input->>Skill: CastSkill
    Skill->>Targeting: Search(query, context)
    Targeting-->>Skill: targets
    Skill->>Effect: Apply(GameplayEffectSpec, context)
    Effect->>Trigger: Publish Apply Event
    Trigger->>Trigger: Evaluate + Execute actions
    Trigger->>Projectile: Spawn projectile action
    loop 每个逻辑帧
        Projectile->>Projectile: Tick frame
        Projectile-->>Trigger: ProjectileHitEvent
        Trigger->>Damage: Calculate Damage
        Damage-->>Trigger: DamageResult
        Trigger->>Effect: Apply secondary effect or remove
        Trigger->>Snapshot: 输出 view/sync snapshot
    end
```

这条链路说明：AbilityKit 的技能能力来自多个小模块协作，而不是由单个 Skill 类承担所有职责。这样设计的收益是：每个战斗原语都可以独立测试、独立替换，并接入帧同步、回滚和服务端运行。
