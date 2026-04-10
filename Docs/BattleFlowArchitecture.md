# AbilityKit 战斗流程架构设计文档

> 本文档描述 AbilityKit 框架中技能流程的整体数据流向，包括 Pipeline、Triggering、Modifiers 等核心模块的协作关系。

---

## 目录

1. [模块概览](#1-模块概览)
2. [模块依赖关系](#2-模块依赖关系)
3. [技能施法完整流程](#3-技能施法完整流程)
4. [Pipeline 层详解](#4-pipeline-层详解)
5. [Triggering 层详解](#5-triggering-层详解)
6. [Modifiers 层详解](#6-modifiers-层详解)
7. [Attributes 层详解](#7-attributes-层详解)
8. [Buff 层详解](#8-buff-层详解)
9. [数据流向总图](#9-数据流向总图)
10. [时序图](#10-时序图)
11. [关键类型速查](#11-关键类型速查)

---

## 1. 模块概览

### 1.1 模块列表

| 模块 | 路径 | 核心职责 |
|------|------|---------|
| `com.abilitykit.pipeline` | `Unity/Packages/com.abilitykit.pipeline` | 技能管线编排，按阶段和时间轴执行 |
| `com.abilitykit.triggering` | `Unity/Packages/com.abilitykit.triggering` | 触发器系统，条件评估 + 行为执行 |
| `com.abilitykit.modifiers` | `Unity/Packages/com.abilitykit.modifiers` | 修饰器计算，属性数值修改 |
| `com.abilitykit.attributes` | `Unity/Packages/com.abilitykit.attributes` | 属性系统，属性存储和查询 |
| `com.abilitykit.timer` | `Unity/Packages/com.abilitykit.timer` | 定时器框架，时间管理（使用方注入） |
| `com.abilitykit.demo.moba.runtime` | `Unity/Packages/com.abilitykit.demo.moba.runtime` | MOBA 业务实现 |

### 1.2 核心设计思想

```
┌─────────────────────────────────────────────────────────────┐
│                     技能施法流程                              │
├─────────────────────────────────────────────────────────────┤
│  Pipeline    │ 负责「什么时候做什么」—— 时间轴编排            │
│  Triggering  │ 负责「满足条件时执行什么」—— 行为执行          │
│  Modifiers   │ 负责「属性值怎么算」—— 数值计算                │
│  Attributes  │ 负责「属性值存在哪」—— 数据存储                │
└─────────────────────────────────────────────────────────────┘
```

---

## 2. 模块依赖关系

```mermaid
graph LR
    subgraph 业务层
        MobaRuntime["com.abilitykit.demo.moba.runtime"]
    end

    subgraph 框架层
        Pipeline["com.abilitykit.pipeline"]
        Triggering["com.abilitykit.triggering"]
        Modifiers["com.abilitykit.modifiers"]
        Attributes["com.abilitykit.attributes"]
        Timer["com.abilitykit.timer"]
        Core["com.abilitykit.core"]
    end

    MobaRuntime --> Pipeline
    MobaRuntime --> Triggering
    MobaRuntime --> Modifiers
    MobaRuntime --> Attributes

    Triggering --> Core
    Pipeline --> Core
    Modifiers --> Core
    Modifiers -.-> Timer
    Modifiers -.-> Attributes
    Attributes --> Core
    Timer --> Core
```

### 2.1 依赖说明

| 依赖方向 | 说明 |
|---------|------|
| MobaRuntime → Pipeline | 业务层使用 SkillPipelineRunner 启动技能 |
| MobaRuntime → Triggering | 业务层使用 TriggerRunner 执行 Effect |
| MobaRuntime → Modifiers | 业务层使用 MobaAttrs 操作属性 |
| Modifiers → Attributes | 修饰器计算需要读取属性值 |
| Modifiers → Timer | 时间衰减修饰器需要计时器（可选） |

---

## 3. 技能施法完整流程

```mermaid
flowchart TB
    subgraph 入口层["🎮 入口层"]
        Input[("玩家输入/AI触发")]
        SkillRequest[("SkillCastRequest\n技能施法请求")]
    end

    subgraph Pipeline层["📋 com.abilitykit.pipeline\n技能管线层"]
        SPR[("SkillPipelineRunner\n技能管线运行器")]

        subgraph PreCast["PreCast 阶段"]
            PreCastConfig[("PreCastConfig")]
            PreCastPhases[("PreCastPhases\n时间轴阶段")]
        end

        subgraph Cast["Cast 阶段"]
            CastConfig[("CastConfig")]
            CastPhases[("CastPhases\n时间轴阶段")]
        end

        TimelinePhase[("SkillTimelinePhase\n时间轴阶段")]
    end

    subgraph Effect触发层["⚡ Effect 触发层"]
        Invoker[("MobaEffectInvokerService\n效果调用服务")]
        EffectId[("EffectId\n效果ID")]
        EffectContext[("EffectPipelineContext\n效果上下文")]
    end

    subgraph Triggering层["🔔 com.abilitykit.triggering\n触发器层"]
        TriggerRunner[("TriggerRunner\n触发器运行器")]
        PlannedTrigger[("PlannedTrigger\n计划触发器")]

        subgraph TriggerPlan["TriggerPlan 触发计划"]
            Predicate[("Predicate\n条件判断")]
            Actions[("Actions\n行为列表")]
        end

        subgraph Executable["Executable 行为"]
            AtomicExe[("AtomicExecutable\n原子行为")]
            CompositeExe[("CompositeExecutable\n组合行为")]
            ScheduledExe[("ScheduledExecutable\n调度行为")]
        end
    end

    subgraph Modifiers层["🔧 com.abilitykit.modifiers\n修改器层"]
        ModCalculator[("ModifierCalculator\n修饰器计算器")]
        ModResult[("ModifierResult\n计算结果")]

        subgraph MagnitudeSource["MagnitudeSource 数值来源"]
            FixedValue[("FixedValue\n固定值")]
            TimeDecay[("TimeDecay\n时间衰减")]
            LevelCurve[("LevelCurve\n等级曲线")]
            AttributeRef[("AttributeRef\n属性引用")]
            Pipeline[("Pipeline\n修饰器管道")]
        end
    end

    subgraph Attributes层["💎 com.abilitykit.attributes\n属性层"]
        AttrGroup[("AttributeGroup\n属性组")]
        AttrContext[("AttributeContext\n属性上下文")]
        AttrInstance[("AttributeInstance\n属性实例")]
        AttrEffect[("AttributeEffect\n属性效果")]
    end

    subgraph Buff层["🛡️ Buff 层"]
        BuffSystem[("MobaBuffSystem\nBuff系统")]
        BuffApply[("ApplyBuffRequest\nBuff应用请求")]
        BuffTick[("MobaBuffTickSystem\nBuff周期Tick")]
        BuffRemove[("MobaBuffRemoveSystem\nBuff移除")]
    end

    subgraph Entity层["🏛️ Entity 层 (Entitas)"]
        ActorEntity[("ActorEntity\n角色实体")]
        SkillLoadout[("SkillLoadoutComponent\n技能栏组件")]
        PassiveTrigger[("OngoingTriggerPlansComponent\n被动技能计划")]
    end

    Input --> SkillRequest
    SkillRequest --> SPR
    SPR --> PreCastConfig
    SPR --> PreCastPhases
    PreCastConfig --> TimelinePhase
    PreCastPhases --> TimelinePhase
    TimelinePhase -->|触发时间点| EffectId

    EffectId --> Invoker
    Invoker --> EffectContext
    EffectContext --> TriggerRunner

    TriggerRunner --> PlannedTrigger
    PlannedTrigger --> TriggerPlan
    TriggerPlan --> Predicate
    TriggerPlan --> Actions

    Actions --> Executable
    Executable --> AtomicExe
    Executable --> CompositeExe
    Executable --> ScheduledExe

    AtomicExe -->|AttributeEffectComponent| AttrEffect
    AtomicExe -->|ApplyBuffRequest| BuffApply
    CompositeExe -->|多阶段Buff| BuffApply

    AttrEffect --> ModCalculator
    ModCalculator --> ModResult
    ModResult --> AttrGroup

    ModCalculator --> MagnitudeSource
    MagnitudeSource --> TimeDecay
    MagnitudeSource --> LevelCurve
    MagnitudeSource --> AttributeRef
    MagnitudeSource --> Pipeline

    AttrGroup --> AttrContext
    AttrContext --> AttrInstance

    BuffApply --> BuffSystem
    BuffTick -->|周期触发| Invoker
    BuffRemove -->|到期移除| AttrGroup

    ActorEntity --> AttrGroup
    ActorEntity --> SkillLoadout
    ActorEntity --> PassiveTrigger

    SkillLoadout -->|被动技能| PassiveTrigger
    PassiveTrigger -->|监听事件| TriggerRunner
```

---

## 4. Pipeline 层详解

### 4.1 模块路径

```
com.abilitykit.pipeline/Runtime/Core/
├── Interfaces/                    # 接口定义
│   ├── IAbilityPipeline.cs       # 管线接口
│   ├── IAbilityPipelineContext.cs # 上下文接口
│   ├── IAbilityPipelinePhase.cs  # 阶段接口
│   └── ...
├── Phase/                         # 阶段实现
│   ├── AbilityDelayPhase.cs      # 延迟阶段
│   ├── AbilitySequencePhase.cs   # 顺序执行阶段
│   ├── AbilityParallelPhase.cs   # 并行执行阶段
│   └── ...
└── AbilityPipeline.cs            # 核心抽象管线类
```

### 4.2 核心类型

| 类型 | 说明 |
|------|------|
| `SkillPipelineRunner` | 技能管线运行器，管理 PreCast → Cast 两阶段 |
| `SkillTimelinePhase` | 时间轴阶段，按时间点触发 EffectId |
| `SkillPipelineContext` | 管线执行上下文，存储施法者/目标/技能ID |
| `AbilityPipeline<TCtx>` | 核心抽象管线类 |
| `InstantAbilityPipeline<TCtx>` | 纯瞬时管线，同步执行完毕 |

### 4.3 阶段类型

| 阶段类型 | 说明 |
|---------|------|
| `IAbilityInstantPhase` | 瞬时阶段标记，Execute 后立即完成 |
| `IDurationalPhase` | 持续阶段标记，OnUpdate 驱动 |
| `IInterruptiblePhase` | 可中断阶段标记，支持暂停/继续 |

### 4.4 PreCast → Cast 流程

```mermaid
flowchart LR
    Start([技能开始]) --> PreCastStart[StartPreCast]
    PreCastStart --> PreCastPipeline[Pipeline.Start\nPreCastConfig]
    PreCastPipeline --> PreCastLoop{PreCast 完成?}

    PreCastLoop -->|否| TimelinePhase1[TimelinePhase.OnUpdate]
    TimelinePhase1 -->|elapsedMs >= AtMs| TriggerEffect1[触发 EffectId]
    TriggerEffect1 --> PreCastLoop

    PreCastLoop -->|是| CastStart[StartCast]
    CastStart --> CastPipeline[Pipeline.Start\nCastConfig]
    CastPipeline --> CastLoop{Cast 完成?}

    CastLoop -->|否| TimelinePhase2[TimelinePhase.OnUpdate]
    TimelinePhase2 -->|elapsedMs >= AtMs| TriggerEffect2[触发 EffectId]
    TriggerEffect2 --> CastLoop

    CastLoop -->|是| Complete([技能结束])
```

---

## 5. Triggering 层详解

### 5.1 模块路径

```
com.abilitykit.triggering/Runtime/
├── Executable/                    # 行为执行核心
│   ├── IExecutable.cs             # 核心接口
│   ├── AtomicExecutables.cs       # 原子行为实现
│   ├── CompositeExecutables.cs    # 组合行为实现
│   └── ScheduledExecutables.cs   # 调度行为实现
├── Plan/                         # 触发器计划层
│   ├── TriggerPlan.cs            # 触发器数据结构
│   └── PlannedTrigger.cs         # 触发器实现
└── Runtime/                      # 运行时核心
    └── TriggerRunner.cs          # 触发器运行器
```

### 5.2 核心类型

| 类型 | 说明 |
|------|------|
| `TriggerRunner<TCtx>` | 触发器运行管理器，接收事件并执行触发计划 |
| `PlannedTrigger<TArgs, TCtx>` | 基于计划的触发器实现 |
| `IExecutable` | 所有行为的基础接口 |
| `ExecutionResult` | 行为执行结果 |

### 5.3 行为类型层次

```mermaid
graph TB
    IExecutable["IExecutable"]
    Atomic["IAtomicExecutable"]
    Composite["ICompositeExecutable"]
    Scheduled["IScheduledExecutable"]
    Conditional["IConditionalExecutable"]

    Sequence["ISequenceExecutable"]
    Selector["ISelectorExecutable"]
    Parallel["IParallelExecutable"]

    NoOp["NoOpExecutable"]
    Delay["DelayExecutable"]
    ActionCall["ActionCallExecutable"]
    If["IfExecutable"]
    Timed["TimedExecutable"]
    Periodic["PeriodicExecutable"]

    IExecutable --> Atomic
    IExecutable --> Composite
    IExecutable --> Scheduled
    Composite --> Sequence
    Composite --> Selector
    Composite --> Parallel
    Composite --> Conditional

    Atomic --> NoOp
    Atomic --> Delay
    Atomic --> ActionCall
    Scheduled --> Timed
    Scheduled --> Periodic
    Conditional --> If
```

### 5.4 触发器执行流程

```mermaid
sequenceDiagram
    participant Invoker as MobaEffectInvokerService
    participant TR as TriggerRunner
    participant Trigger as PlannedTrigger
    participant Pred as Predicate
    participant Actions as Actions

    Invoker->>TR: Execute(EffectId, Context)
    TR->>TR: 创建 ExecCtx<TCtx>

    TR->>TR: 按优先级排序 Triggers

    loop 遍历每个 Trigger
        TR->>Trigger: Evaluate(ctx)
        Trigger->>Pred: Check(context)
        Pred-->>Trigger: bool result

        alt 条件满足
            Trigger->>Actions: Execute(context)
            Actions-->>Trigger: ExecutionResult
            Trigger-->>TR: continue/stop
        else 条件不满足
            Trigger-->>TR: Skip
        end
    end
```

---

## 6. Modifiers 层详解

### 6.1 模块路径

```
com.abilitykit.modifiers/Runtime/Core/
├── Source/                        # 数值来源
│   ├── IValueSource.cs           # 数值来源接口
│   └── MagnitudeSource.cs        # 统一数值来源结构
├── Data/                         # 数据结构
│   ├── ModifierData.cs          # 修改器数据
│   └── ModifierResult.cs        # 计算结果
├── Engine/                       # 计算引擎
│   └── ModifierCalculator.cs   # 修饰器计算器
└── Enums/
    └── ModifierOp.cs            # 操作类型枚举
```

### 6.2 核心类型

| 类型 | 说明 |
|------|------|
| `ModifierCalculator` | 修饰器计算器，支持缓存、来源追踪 |
| `ModifierData` | 修改器数据单元（Key + Op + MagnitudeSource） |
| `MagnitudeSource` | 统一数值来源，支持多种来源类型 |
| `ModifierResult` | 计算结果（BaseValue + AddSum + PercentProduct + MulProduct） |

### 6.3 数值来源类型

| 来源类型 | 说明 |
|---------|------|
| `Fixed` | 恒定值 |
| `Scalable` | 等级曲线插值 |
| `Attribute` | 属性引用 |
| `TimeDecay` | 时间衰减 |
| `Pipeline` | 修饰器管道组合 |

### 6.4 操作类型

| 操作 | 优先级 | 计算公式 |
|------|--------|---------|
| `Override` | 0 | → 直接替换 |
| `Add` | 10 | → Base + Value |
| `PercentAdd` | 15 | → Base × (1 + Value) |
| `Mul` | 20 | → Base × Value |

### 6.5 计算公式

```
FinalValue = OverrideFlag ? OverrideValue
                          : (BaseValue + AddSum) × PercentProduct × MulProduct
```

### 6.6 数据流向

```mermaid
flowchart TB
    subgraph Input["输入"]
        BaseValue[("BaseValue\n基础值")]
        Modifiers[("ModifierData[]\n修改器列表")]
        Context[("IModifierContext\n上下文")]
    end

    subgraph Calculator["ModifierCalculator"]
        CheckCache[("检测缓存")]
        Compute[("ComputeCore")]
        OperatorComp[("OperatorComposer")]
    end

    subgraph Output["输出"]
        Result[("ModifierResult")]
        FinalValue[("FinalValue")]
    end

    BaseValue --> CheckCache
    Modifiers --> CheckCache
    Context --> Compute
    CheckCache --> Compute
    Compute --> OperatorComp

    OperatorComp --> Result
    Result --> FinalValue
```

---

## 7. Attributes 层详解

### 7.1 模块路径

```
com.abilitykit.attributes/Runtime/Ability/Share/Common/AttributeSystem/
├── AttributeGroup.cs            # 属性组
├── AttributeContext.cs          # 属性上下文
├── AttributeInstance.cs         # 属性实例
├── AttributeEffect.cs           # 属性效果
└── ...
```

### 7.2 核心类型

| 类型 | 说明 |
|------|------|
| `AttributeGroup` | 属性组，管理一组属性实例 |
| `AttributeContext` | 属性上下文，提供属性访问接口 |
| `AttributeInstance` | 属性实例，存储基础值和计算器 |
| `AttributeEffect` | 属性效果，表示一次属性修改 |

### 7.3 层级关系

```mermaid
graph TB
    subgraph AttributeGroup["AttributeGroup"]
        Attr1["AttributeInstance 1"]
        Attr2["AttributeInstance 2"]
        Attr3["AttributeInstance N"]
    end

    subgraph AttributeInstance["AttributeInstance"]
        BaseValue[("BaseValue")]
        Calculator[("ModifierCalculator")]
        FinalValue[("FinalValue")]
    end

    AttributeGroup --> Attr1
    AttributeGroup --> Attr2
    AttributeGroup --> Attr3

    Attr1 --> Calculator
    Attr2 --> Calculator
    Attr3 --> Calculator

    Calculator --> FinalValue
```

---

## 8. Buff 层详解

### 8.1 模块路径

```
com.abilitykit.demo.moba.runtime/Runtime/Impl/Moba/
├── Services/
│   └── Buffs/
│       ├── BuffStageEffectExecutor.cs  # Buff 阶段效果执行器
│       └── BuffEventArgs.cs
└── Systems/
    └── Buffs/
        ├── MobaBuffApplySystem.cs      # Buff 应用系统
        ├── MobaBuffTickSystem.cs       # Buff 周期 Tick
        └── MobaBuffRemoveSystem.cs     # Buff 移除系统
```

### 8.2 核心类型

| 类型 | 说明 |
|------|------|
| `MobaBuffApplySystem` | 处理 ApplyBuffRequest，应用 Buff |
| `MobaBuffTickSystem` | 每帧 Tick，更新持续时间并触发周期效果 |
| `MobaBuffRemoveSystem` | 处理 Buff 到期移除 |
| `BuffStageEffectExecutor` | 执行 Buff 各阶段的效果 |

### 8.3 Buff 生命周期

```mermaid
flowchart LR
    subgraph Apply["应用阶段"]
        A1[添加 ApplyBuffRequest]
        A2[解析 Buff 配置]
        A3[创建 Buff 实例]
        A4[注册时间轴事件]
    end

    subgraph Tick["周期阶段"]
        T1[更新已用时间]
        T2[检测周期触发点]
        T3[执行周期效果]
    end

    subgraph Remove["移除阶段"]
        R1[到期/手动移除]
        R2[清理修饰器]
        R3[触发移除事件]
    end

    A1 --> A2 --> A3 --> A4
    A4 --> T1
    T1 --> T2 --> T3
    T3 --> T1
    T1 -->|到期| R1 --> R2 --> R3
```

---

## 9. 数据流向总图

```mermaid
flowchart TB
    subgraph Layer0["入口层"]
        Player[("🎮 玩家输入")]
        AI[("🤖 AI 触发")]
        Passive[("⚡ 被动触发")]
    end

    subgraph Layer1["请求层"]
        SkillRequest[("SkillCastRequest"))]
        BuffRequest[("ApplyBuffRequest"))]
        PassiveTrigger[("OngoingTriggerPlans"))]
    end

    subgraph Layer2["Pipeline 层"]
        PipelineRunner[("SkillPipelineRunner"))]
        TimelinePhase[("SkillTimelinePhase"))]
    end

    subgraph Layer3["Effect 层"]
        EffectInvoker[("MobaEffectInvokerService"))]
        EffectId[("EffectId")]
        EffectContext[("EffectPipelineContext"))]
    end

    subgraph Layer4["Triggering 层"]
        TriggerRunner[("TriggerRunner"))]
        PlannedTrigger[("PlannedTrigger"))]
        Executable[("Executable 行为链"))]
    end

    subgraph Layer5["业务行为层"]
        AttrEffect[("AttributeEffect"))]
        BuffAction[("BuffAction"))]
        DamageAction[("DamageAction"))]
        HealAction[("HealAction"))]
    end

    subgraph Layer6["Modifiers 层"]
        ModCalculator[("ModifierCalculator"))]
        ModResult[("ModifierResult"))]
    end

    subgraph Layer7["Attributes 层"]
        AttrGroup[("AttributeGroup"))]
        AttrContext[("AttributeContext"))]
    end

    subgraph Layer8["Entity 层"]
        Entity[("ActorEntity"))]
    end

    Player --> SkillRequest
    AI --> SkillRequest
    Passive --> PassiveTrigger

    SkillRequest --> PipelineRunner
    BuffRequest -.-> PipelineRunner

    PipelineRunner --> TimelinePhase
    TimelinePhase --> EffectId

    EffectId --> EffectInvoker
    EffectInvoker --> EffectContext
    EffectContext --> TriggerRunner

    TriggerRunner --> PlannedTrigger
    PlannedTrigger --> Executable

    Executable --> AttrEffect
    Executable --> BuffAction
    Executable --> DamageAction
    Executable --> HealAction

    AttrEffect --> ModCalculator
    BuffAction --> ModCalculator

    ModCalculator --> ModResult
    ModResult --> AttrGroup

    AttrGroup --> AttrContext
    AttrContext --> Entity
    Entity --> BuffRequest
```

---

## 10. 时序图

### 10.1 技能施法时序图

```mermaid
sequenceDiagram
    participant Player as 玩家输入
    participant Runner as SkillPipelineRunner
    participant Phase as SkillTimelinePhase
    participant Invoker as MobaEffectInvokerService
    participant TR as TriggerRunner
    participant Mod as ModifierCalculator
    participant Attr as AttributeGroup
    participant Entity as ActorEntity

    Player->>Runner: Start(SkillCastRequest)
    Runner->>Phase: Start(PreCastConfig)

    loop 时间轴推进 (每帧)
        Phase->>Phase: OnUpdate(deltaTime)

        alt 到达触发时间点 (elapsedMs >= AtMs)
            Phase->>Invoker: Execute(EffectId, Context)
            Invoker->>TR: Execute(EffectId, Context)
            TR->>TR: Evaluate(Triggers)

            alt 条件满足
                TR->>TR: Execute(Actions)

                alt 添加属性效果
                    TR->>Mod: AddModifier(AttributeEffect)
                    Mod->>Attr: Calculate()
                    Attr-->>Mod: ModifierResult
                end

                alt 应用 Buff
                    TR->>Entity: AddApplyBuffRequest
                end
            end
        end
    end

    Runner->>Runner: Start(CastConfig)
    loop Cast 阶段...
    end
```

### 10.2 Buff 周期触发时序图

```mermaid
sequenceDiagram
    participant Tick as MobaBuffTickSystem
    participant Stage as BuffStageEffectExecutor
    participant Invoker as MobaEffectInvokerService
    participant TR as TriggerRunner
    participant Mod as ModifierCalculator
    participant Attr as AttributeGroup

    loop 每帧
        Tick->>Tick: Update(deltaTime)

        alt 到达周期触发点
            Tick->>Stage: Execute(EffectIds, BuffId)

            loop 遍历每个 EffectId
                Stage->>Invoker: Execute(EffectId, BuffContext)
                Invoker->>TR: Execute(EffectId, Context)
                TR->>TR: Evaluate + Execute

                alt 属性修改
                    TR->>Mod: ApplyEffect()
                    Mod->>Attr: Calculate()
                end
            end
        end
    end
```

### 10.3 被动技能触发时序图

```mermaid
sequenceDiagram
    participant Event as 事件源
    participant Manager as PassiveSkillTriggerListenerManager
    participant Service as MobaOngoingTriggerPlanService
    participant Bus as IEventBus
    participant TR as TriggerRunner
    participant Mod as ModifierCalculator
    participant Attr as AttributeGroup

    Note over Manager,Service: 注册阶段
    Manager->>Service: StartTriggers(OwnerKey, Plans)
    Service->>Bus: Register(EventKey, TriggerPlan)

    Note over Event,TR: 触发阶段
    Event->>Bus: Publish(EventKey, Args)
    Bus->>TR: OnEvent(Args)
    TR->>TR: Evaluate(Triggers)

    alt 条件满足
        TR->>TR: Execute(Actions)

        alt 属性效果
            TR->>Mod: AddModifier()
            Mod->>Attr: Calculate()
        end
    end
```

---

## 11. 关键类型速查

### 11.1 Pipeline 模块

| 类型 | 命名空间 | 说明 |
|------|---------|------|
| `SkillPipelineRunner` | Moba.Services.Skill | 技能管线运行器 |
| `SkillPipelineContext` | Moba.Services.Skill | 管线上下文 |
| `SkillTimelinePhase` | Moba.Services.Skill | 时间轴阶段 |
| `AbilityPipeline<TCtx>` | AbilityKit.Pipeline | 核心抽象管线 |
| `IAbilityPipelinePhase` | AbilityKit.Pipeline | 阶段接口 |

### 11.2 Triggering 模块

| 类型 | 命名空间 | 说明 |
|------|---------|------|
| `TriggerRunner<TCtx>` | AbilityKit.Triggering | 触发器运行器 |
| `PlannedTrigger<TArgs, TCtx>` | AbilityKit.Triggering | 计划触发器 |
| `IExecutable` | AbilityKit.Triggering | 行为接口 |
| `ExecutionResult` | AbilityKit.Triggering | 执行结果 |

### 11.3 Modifiers 模块

| 类型 | 命名空间 | 说明 |
|------|---------|------|
| `ModifierCalculator` | AbilityKit.Modifiers | 修饰器计算器 |
| `ModifierData` | AbilityKit.Modifiers | 修改器数据 |
| `MagnitudeSource` | AbilityKit.Modifiers | 数值来源 |
| `ModifierOp` | AbilityKit.Modifiers | 操作类型 |

### 11.4 Attributes 模块

| 类型 | 命名空间 | 说明 |
|------|---------|------|
| `AttributeGroup` | AbilityKit.Attributes | 属性组 |
| `AttributeContext` | AbilityKit.Attributes | 属性上下文 |
| `AttributeEffect` | AbilityKit.Attributes | 属性效果 |

### 11.5 Moba 业务模块

| 类型 | 命名空间 | 说明 |
|------|---------|------|
| `MobaAttrs` | Moba.Attributes | 属性访问包装器 |
| `MobaEffectInvokerService` | Moba.Services.Effect | 效果调用服务 |
| `MobaBuffApplySystem` | Moba.Systems.Buffs | Buff 应用系统 |
| `SkillCastRequest` | AbilityKit.Ability | 技能施法请求 |

---

## 附录 A：名词解释

| 术语 | 解释 |
|------|------|
| Pipeline | 管线/流水线，将复杂流程拆分为多个阶段按序执行 |
| Trigger | 触发器，响应事件并执行相应行为 |
| Modifier | 修改器，对数值进行加成/乘法等操作 |
| Attribute | 属性，角色/单位的数值属性（如攻击力、血量） |
| Buff | 增益效果，通常有时间限制 |
| Effect | 效果，技能的具体表现（伤害、治疗、加 Buff 等） |
| Executable | 可执行行为，Triggering 模块的核心执行单元 |
| Context | 上下文，贯穿整个流程的共享数据容器 |

---

## 附录 B：设计原则

1. **接口驱动** - 核心模块通过接口解耦，便于测试和替换实现
2. **数据驱动** - 技能配置、Buff 配置通过表/资产定义，减少硬编码
3. **分层清晰** - Pipeline 管流程、Triggering 管行为、Modifiers 管数值
4. **可扩展** - 支持自定义行为、自定义修饰器、自定义条件
5. **零 GC** - 核心结构使用值类型，减少堆分配

---

> 文档版本：1.0
> 最后更新：2026-04-09
