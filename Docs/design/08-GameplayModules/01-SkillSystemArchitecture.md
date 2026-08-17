# 8.1 技能系统架构

> 文档类型：示例分析（MOBA 技能应用层）
> 事实基线：2026-08-16
> 文档版本：v3.0
>
> 本文从源码出发说明 AbilityKit 中“技能”能力的真实组成方式：框架核心并不把技能实现为一个巨型 `SkillExecutor`，而是通过输入、配置、Pipeline、Triggering、GameplayEffect、Combat 原语、Trace/Runtime 快照协作完成一次技能释放。

---

## 目录

- [8.1 技能系统架构](#81-技能系统架构)
  - [目录](#目录)
  - [1. 能力定位](#1-能力定位)
    - [1.1 职责归属与复用边界](#11-职责归属与复用边界)
  - [2. 源码入口](#2-源码入口)
  - [3. 总体设计](#3-总体设计)
  - [4. 技能输入到释放的主流程](#4-技能输入到释放的主流程)
  - [5. 配置到 Pipeline 的构建流程](#5-配置到-pipeline-的构建流程)
    - [5.1 Pipeline 运行态不是直接复用配置阶段](#51-pipeline-运行态不是直接复用配置阶段)
  - [6. PreCast/Cast 双阶段执行模型](#6-precastcast-双阶段执行模型)
  - [7. 技能运行时上下文与生命周期追踪](#7-技能运行时上下文与生命周期追踪)
  - [8. 与 Triggering、Effect、Combat 的协作边界](#8-与-triggeringeffectcombat-的协作边界)
  - [9. 扩展点与约束](#9-扩展点与约束)
    - [9.1 扩展点](#91-扩展点)
    - [9.2 关键约束](#92-关键约束)
  - [10. 证据与关联文档](#10-证据与关联文档)
    - [10.1 证据状态与已知限制](#101-证据状态与已知限制)

---

## 1. 能力定位

AbilityKit 的技能系统不是单个包中的封闭系统，而是一个跨模块组合能力：

- `com.abilitykit.ability` 提供 GameplayEffect、Triggering 适配、Effect 生命周期等通用玩法基础。
- `com.abilitykit.pipeline` 提供可推进、可中断、可组合的执行 Pipeline 抽象。
- `com.abilitykit.triggering` 提供事件-条件-动作的正式化执行主线。
- `com.abilitykit.combat.*` 提供目标搜索、投射物、伤害、实体、运动等战斗原语。
- `com.abilitykit.demo.moba.runtime` 给出一套完整 MOBA 技能落地路径，包含输入、配置库、释放协调器、运行时追踪和 Entitas 实体同步。

因此，本文中的“技能系统架构”主要描述当前工程中最完整的 MOBA 技能闭环，而不是虚构一个框架内不存在的 `SkillConfig -> SkillExecutor` 固定结构。

### 1.1 职责归属与复用边界

| 层级 | 稳定职责 | 不在该层承诺的内容 |
|------|----------|--------------------|
| 框架包 | Pipeline 的阶段推进与终态、Triggering 的计划执行、Effect 生命周期以及 Targeting/Projectile/Damage 等战斗原语 | 技能槽、资源消耗、冷却事务、蓄力/引导规则、角色状态和整套施法事务 |
| 项目应用层 | 把输入、角色、配置、资源、并发策略、Pipeline 和清理路径编排成项目自己的技能释放协议 | 不应把项目 DTO、阶段名称或失败码反向声明为框架公共 API |
| MOBA 示例 | `SkillCastPreparationService`、PreCast/Cast、Timeline、RulePlan、Trace 与 runtime handle 的一套高接入参考 | 不是所有战斗项目开包即用的统一 Skill Runtime；其他项目可以替换整个应用层编排 |

这一区分是工具集定位的直接结果：框架降低稳定机制的重复实现成本，项目保留多变战斗规则的最终所有权。只有当多个项目对某段逻辑的语义、失败路径、依赖方向和生命周期均达成一致时，才考虑继续下沉。

---

## 2. 源码入口

| 能力 | 关键类型 | 源码 |
|------|----------|------|
| 输入协调 | `MobaInputCoordinator` | `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Input/MobaInputCoordinator.cs` |
| 释放协调 | `SkillCastCoordinator` | `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Cast/SkillCastCoordinator.cs` |
| 释放准备 | `SkillCastPreparationService` | `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Cast/SkillCastPreparationService.cs` |
| 释放上下文 | `SkillCastContext` | `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Cast/SkillCastContext.cs` |
| 通用 Pipeline 运行态 | `AbilityPipeline<TCtx>` / `AbilityPipelinePhaseRuntime` | `Unity/Packages/com.abilitykit.pipeline/Runtime/Core/Pipeline/AbilityPipeline.cs` / `Unity/Packages/com.abilitykit.pipeline/Runtime/Core/Pipeline/AbilityPipelinePhaseRuntime.cs` |
| Pipeline 库 | `IMobaSkillPipelineLibrary` | `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Pipeline/IMobaSkillPipelineLibrary.cs` |
| 配置驱动 Pipeline | `TableDrivenMobaSkillPipelineLibrary` | `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Pipeline/TableDrivenMobaSkillPipelineLibrary.cs` |
| Pipeline 运行器 | `SkillPipelineRunner` | `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Pipeline/SkillPipelineRunner.cs` |
| MOBA 时间线阶段 | `SkillTimelinePhase` | `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Phases/SkillTimelinePhase.cs` |
| MOBA 规则计划阶段 | `SkillRulePlanPhase` | `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Phases/SkillRulePlanPhase.cs` |
| 运行器注册表 | `SkillRunnerRegistry` | `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Cast/SkillRunnerRegistry.cs` |
| Pipeline 上下文 | `SkillPipelineContext` | `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Pipeline/SkillPipelineContext.cs` |
| 通用 Skill Pipeline Builder | `SkillPipelineBuilder` | `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Domain/Ability/Pipeline/Skill/SkillPipelineBuilder.cs` |
| Effect 生命周期 | `EffectContainer` | `Unity/Packages/com.abilitykit.ability/Runtime/Ability/Effect/EffectContainer.cs` |
| Trigger 主线 | `TriggerRunner<TCtx>` | `Unity/Packages/com.abilitykit.triggering/Runtime/Triggering/Runner/TriggerRunner.cs` |

---

## 3. 总体设计

技能能力由“输入入口 + 释放协调 + 配置构建 + Pipeline 执行 + 原语动作”分层组成。

```mermaid
flowchart TD
    Input[玩家输入/AI/脚本] --> Sink[IWorldInputSink]
    Sink --> Coordinator[MobaInputCoordinator]
    Coordinator --> Handlers[MobaInputCommandHandlerRegistry]
    Handlers --> SkillCast[SkillCastCoordinator]

    SkillCast --> Prep[SkillCastPreparationService]
    Prep --> Loadout[MobaSkillLoadoutService]
    Prep --> Units[IUnitResolver]
    Prep --> Actors[MobaActorLookupService]
    Prep --> Library[IMobaSkillPipelineLibrary]
    Prep --> Trace[MobaTraceRegistry]
    Prep --> Runtime[MobaSkillCastRuntimeService]

    Library --> Config[MobaConfigDatabase]
    Library --> Flow[SkillFlowMO / SkillPhaseDTO]
    Flow --> PhaseDef[PhaseDefinition]
    PhaseDef --> PipelinePhase[IAbilityPipelinePhase]

    SkillCast --> Registry[SkillRunnerRegistry]
    Registry --> Runner[SkillPipelineRunner]
    Runner --> PreCast[PreCast Pipeline]
    Runner --> Cast[Cast Pipeline]

    Cast --> RulePlan[SkillRulePlanPhase]
    Cast --> Timeline[SkillTimelinePhase]
    RulePlan --> Triggering[TriggerRunner / ActionRegistry]
    Triggering --> Effect[GameplayEffect / EffectContainer]
    Triggering --> Combat[Targeting / Projectile / Damage]
```

设计拆分的主要原因：

1. **输入与技能释放解耦**：`MobaInputCoordinator` 只负责命令上下文与分发，实际释放由 `SkillCastCoordinator` 处理。
2. **准备与执行解耦**：`SkillCastPreparationService` 专注校验、解析实体、创建上下文和运行时记录；`SkillPipelineRunner` 专注推进 Pipeline。
3. **配置与运行时解耦**：`TableDrivenMobaSkillPipelineLibrary` 把表配置转为 `IAbilityPipelinePhase<SkillPipelineContext>`，运行器只消费已构建阶段。
4. **技能与效果/战斗原语解耦**：技能 Pipeline 不直接写死伤害、Buff、投射物，而是通过 RulePlan、Timeline、Triggering 动作调用 Effect 与 Combat 能力。

---

## 4. 技能输入到释放的主流程

`MobaInputCoordinator` 在世界服务准备好后绑定输入处理器，并解析 `SkillCastCoordinator`。输入到达时，它构建 `MobaInputCommandContext`，再交给命令处理器注册表分发。

```mermaid
sequenceDiagram
    participant Net as FrameSync/Input
    participant Sink as MobaInputCoordinator
    participant Handlers as MobaInputCommandHandlerRegistry
    participant Skills as SkillCastCoordinator
    participant Registry as SkillRunnerRegistry
    participant Runner as SkillPipelineRunner

    Net->>Sink: Apply frame inputs
    Sink->>Sink: CreateContext(frame, inputs)
    Sink->>Handlers: TryHandle(context, frame, command)
    Handlers->>Skills: TryHandleInputResult(actorId, SkillInputEvent)
    Skills->>Skills: ValidateSkillInput(actorId, event)
    alt Press
        Skills->>Registry: TryUpdateRunningInput(slot, aim, target)
        alt 已有运行中技能
            Registry-->>Skills: updated
        else 无运行中技能
            Skills->>Skills: TryStartCastFromInput
        end
    else Hold
        Skills->>Registry: TryUpdateRunningInput(slot, aim, target)
    else Release
        Skills->>Registry: TryUpdateRunningInputAndRelease(slot, aim, target)
        alt 无运行中技能
            Skills->>Skills: TryStartCastFromInput
        end
    else Cancel
        Skills->>Registry: TryCancelBySlot(slot)
    end
    Skills-->>Handlers: MobaSkillInputHandleResult
    Handlers-->>Sink: command result
```

关键规则：

- `Press`：优先更新运行中的同槽技能；不存在时尝试启动释放。
- `Hold`：只更新运行中技能输入，不会新建释放。
- `Release`：优先标记运行中技能释放；不存在时可启动释放，适配点击释放类技能。
- `Cancel`：按槽位取消运行中技能。

---

## 5. 配置到 Pipeline 的构建流程

`TableDrivenMobaSkillPipelineLibrary` 是当前 MOBA 示例中最完整的配置驱动 Pipeline 构建器。它从 `MobaConfigDatabase` 读取 `SkillMO` 与 `SkillFlowMO`，并将 `SkillPhaseDTO` 编译为运行时 PhaseDefinition。

```mermaid
flowchart TD
    A[TryGet skillId] --> B{skillId 有效?}
    B -- 否 --> X[返回 false]
    B -- 是 --> C{缓存存在?}
    C -- 是 --> D[BuildPhases from cached definitions]
    C -- 否 --> E[MobaConfigDatabase.TryGetSkill]
    E --> F{Skill.CastFlowId > 0?}
    F -- 否 --> X
    F -- 是 --> G[TryGetSkillFlow CastFlow]
    G --> H{PreCastFlowId > 0?}
    H -- 是 --> I[构建 PreCast SkillPipelineConfig]
    H -- 否 --> J[跳过 PreCast]
    I --> K[BuildFlowDefinitions PreCast]
    J --> L[构建 Cast SkillPipelineConfig]
    K --> L
    L --> M[BuildFlowDefinitions Cast]
    M --> N[写入 SkillPipelineCacheEntry]
    N --> D
    D --> O[返回 preCastConfig/preCastPhases/castConfig/castPhases]
```

`SkillPhaseDTO` 支持的主要阶段：

| 阶段 | 运行时定义 | 说明 |
|------|------------|------|
| Timeline | `TimelinePhaseDefinition` -> `SkillTimelinePhase` | MOBA 自己的时间线阶段，按毫秒推进事件，当前只接受 `EffectExecuteMode.InternalOnly` 并调用 `MobaEffectInvokerService.Execute` |
| RulePlan | `RulePlanPhaseDefinition` -> `SkillRulePlanPhase` | 瞬时阶段，按配置 triggerId 调用 `MobaTriggerPlanExecutor.ExecuteRulePlan`，失败且 `AbortOnFailure` 时写入 `FailReason` 并中止 Pipeline |
| Sequence | `SequencePhaseDefinition` -> `AbilitySequencePhase<T>` | 子阶段顺序执行，运行时通过 `CreateRunPhase` 复制复合阶段和子阶段 |
| Parallel | `ParallelPhaseDefinition` -> `AbilityParallelPhase<T>` | 子阶段并行执行，维护 active phase 索引，全部完成后结束 |
| Repeat | `RepeatPhaseDefinition` -> `AbilityRepeatPhase<T>` | 重复执行子阶段，可设置间隔 |
| Delay | `DelayPhaseDefinition` -> `AbilityDelayPhase<T>` | 等待指定毫秒数 |
| WaitUntil | `WaitUntilPhaseDefinition` -> `AbilityWaitUntilPhase<T>` | 当前支持 `ObservedSlotsIdle`，通过 `SkillCastCoordinator.TryGetRunningBySlot` 等待指定技能槽空闲 |

源码中特别标注：旧的 `Checks` 和 `Handlers` 阶段已经废弃，`TableDrivenMobaSkillPipelineLibrary.BuildPhaseDefinition` 会直接抛出异常，要求迁移到 RulePlan 的 Trigger 条件与动作。这体现了设计方向：技能校验和效果执行尽量收敛到统一的触发器/动作体系中。

### 5.1 Pipeline 运行态不是直接复用配置阶段

通用 `AbilityPipeline<TCtx>.Start` 会先调用 `AbilityPipelinePhaseRuntime.CreateRunPhases(_phases)`，再创建一次 `Run`。这个步骤有两个含义：

1. 如果阶段实现了 `IAbilityPipelinePhaseInstanceFactory<TCtx>`，每次运行会通过 `CreateRunPhase()` 创建运行实例。
2. 如果阶段没有实现工厂契约，则保持原实例兼容，但仍会调用 `Reset()`。

```mermaid
flowchart TD
    Config[TableDrivenMobaSkillPipelineLibrary.TryGet] --> Defs[PhaseDefinition cache]
    Defs --> Build[BuildPhases creates phase objects]
    Build --> Runner[SkillPipelineRunner StartPreCast or StartCast]
    Runner --> Pipeline[AbilityPipeline AddPhase]
    Pipeline --> Start[AbilityPipeline Start]
    Start --> Runtime[AbilityPipelinePhaseRuntime CreateRunPhases]
    Runtime --> Factory{phase implements factory}
    Factory -- yes --> Clone[CreateRunPhase]
    Factory -- no --> Same[reuse phase instance]
    Clone --> Reset[Reset]
    Same --> Reset
    Reset --> Run[Run Tick / ExecutePipeline]
```

`AbilitySequencePhase<TCtx>` 和 `AbilityParallelPhase<TCtx>` 都实现了运行实例创建；`SkillTimelinePhase` 当前没有实现工厂契约，因此 `TableDrivenMobaSkillPipelineLibrary.TryGet` 每次会从缓存的 `PhaseDefinition` 重新 `CreatePhase()`，避免多个技能释放共享同一个时间线阶段状态。

---

## 6. PreCast/Cast 双阶段执行模型

一次技能释放分为可选 `PreCast` 与必需 `Cast` 两段：

- `PreCast`：可用于前摇、吟唱、预检查、提前表现提示。
- `Cast`：正式执行技能效果、时间轴、RulePlan、投射物、伤害等。

`SkillPipelineRunner.Start` 会检查并发策略、上下文、Cast 配置和阶段列表，然后记录起始帧。如果没有 PreCast，会直接进入 Cast。源码里还有一个容易漏掉的约束：`triggerContext` 必须存在；缺失时会以 `SkillFailureCodes.Start.ContextMissing` 拒绝启动，因为后续 PreCast/Cast 事件、Trace 和运行时句柄都依赖释放级上下文。

```mermaid
flowchart TD
    A[SkillPipelineRunner.Start] --> B{AllowParallel?}
    B -- 否且已有运行中 --> C{InterruptRunning?}
    C -- 是 --> D[CancelAll]
    C -- 否 --> R[Reject skill.start.alreadyRunning]
    B -- 是或无运行中 --> E{triggerContext 存在?}
    D --> E
    E -- 否 --> R2[Reject contextMissing]
    E -- 是 --> F{castConfig/castPhases 有效?}
    F -- 否 --> R3[Reject cast config/phases missing]
    F -- 是 --> G[解析 IFrameTime 作为 StartFrame]
    G --> H{有 PreCast?}
    H -- 否 --> I[StartCast]
    H -- 是 --> J[StartPreCast]
    J --> K{PreCast 状态}
    K -- Executing --> L[加入 running]
    K -- Completed --> I
    K -- Failed/Interrupted --> M[Publish PreCastFail + Cleanup]
    I --> N{Cast 状态}
    N -- Executing --> L
    N -- Completed --> O[Publish CastComplete + EndTrace + Cleanup]
    N -- Failed/Interrupted --> P[Publish CastFail + EndTrace + Cleanup]
```

PreCast 与 Cast 都会创建新的 `SkillCastPipeline` 和 `SkillPipelineContext`：

```mermaid
sequenceDiagram
    participant Runner as SkillPipelineRunner
    participant Pipeline as SkillCastPipeline
    participant Ctx as SkillPipelineContext
    participant Bus as MobaSkillTriggering
    participant Trace as Trace/Runtime

    Runner->>Pipeline: AddPhase(...)
    Runner->>Ctx: Initialize(abilityInstance, request, triggerContext)
    Runner->>Ctx: SetFrame(start/current frame)
    Runner->>Pipeline: Start(config, context)
    Runner->>Bus: Publish PreCastStart or CastStart
    Runner->>Pipeline: Tick(0f)
    alt Executing
        Runner->>Runner: keep in _running
    else Completed
        Runner->>Bus: Publish Complete
        Runner->>Trace: EndContext / MarkPipelineEnded
        Runner->>Ctx: RunAndClearCleanups
    else Failed
        Runner->>Bus: Publish Fail
        Runner->>Trace: Cancel/Fail
        Runner->>Ctx: RunAndClearCleanups
    end
```

---

## 7. 技能运行时上下文与生命周期追踪

`SkillCastPreparationService` 在执行前创建两类上下文：

1. `SkillCastContext`：释放级上下文，保存技能 ID、槽位、等级、序号、施法者、目标、瞄准、WorldServices、EventBus、运行时句柄和 Trace 根上下文。
2. `SkillPipelineContext`：Pipeline 推进上下文，保存当前阶段、状态、输入释放状态、帧号、时间、共享数据、清理回调，并实现多个 MOBA Combat/Trigger 接口。

准备流程如下：

```mermaid
flowchart TD
    A[Prepare SkillCastPreparationInput] --> B{actorId/skillId 有效?}
    B -- 否 --> Fail[Failed code]
    B -- 是 --> C[IUnitResolver.TryResolve caster]
    C -- 失败 --> Fail
    C -- 成功 --> D[ResolveCasterTransform]
    D --> E{targetActorId > 0?}
    E -- 是 --> F[IUnitResolver.TryResolve target]
    E -- 否 --> G[跳过目标]
    F -- 失败 --> Fail
    F -- 成功 --> H[IMobaSkillPipelineLibrary.TryGet]
    G --> H
    H -- 失败 --> Fail
    H -- 成功 --> I[构造 SkillCastRequest]
    I --> J[ResolveSkillLevel + NextCastSequence]
    J --> K[SkillCastContextBuilder.Build]
    K --> L[MobaTraceRegistry.CreateRootContext]
    L -- 失败 --> Fail
    L -- 成功 --> M[MobaSkillCastRuntimeService.Create]
    M -- handle 无效 --> Fail
    M -- 成功 --> Ready[Ready request/context/runtime/config/phases]
```

`SkillPipelineContext` 在初始化时会把核心事实同步到通用 `IAbilityPipelineContext` 扩展字段：技能信息、参与者、瞄准、上下文类型、SourceContextId、SkillRuntimeHandle。这保证了 Pipeline Phase、Trigger Action、Effect 执行器可以通过统一上下文读取技能事实，而不是直接依赖 MOBA 专用类型。

时间线阶段还会把 `_nextEventIndex` 同步到 `SkillPipelineContext.SetTimelineNextEventIndex`。这不是表现层字段，而是运行时快照字段：`SkillPipelineRunner.RunningSnapshot` 会暴露 `ElapsedMs` 与 `NextEventIndex`，用于同步、诊断或恢复当前技能释放进度。

---

## 8. 与 Triggering、Effect、Combat 的协作边界

技能 Pipeline 本身只负责“何时执行什么阶段”，不负责把所有玩法规则写死在技能类里。实际效果通常通过两条路径触发：

```mermaid
flowchart LR
    SkillPipeline[Skill Pipeline] --> Timeline[SkillTimelinePhase]
    SkillPipeline --> RulePlan[SkillRulePlanPhase]

    Timeline --> EffectInvoker[MobaEffectInvokerService]
    RulePlan --> PlanExecutor[MobaTriggerPlanExecutor]
    PlanExecutor --> Triggering[TriggerRunner / ActionRegistry]

    Triggering --> Effect[EffectContainer / GameplayEffectSpec]
    Triggering --> Targeting[TargetSearchEngine]
    Triggering --> Projectile[ProjectileWorld]
    Triggering --> Damage[DamageCalculationPipeline]
    Triggering --> Presentation[Presentation Cue / Snapshot]
```

边界说明：

- **技能输入层**：处理按下、持续、释放、取消等操作语义。
- **技能释放层**：创建上下文、检查并发策略、启动/取消 Pipeline。
- **技能配置层**：把表配置转换为可执行 Phase，不直接修改战斗状态。
- **Triggering 层**：承载条件判断和动作执行，是数据化玩法逻辑主线。
- **Effect 层**：管理 Buff/GameplayEffect 生命周期，包括 Apply、Tick、Remove。
- **Combat 层**：提供 Targeting、Projectile、Damage 等可测试战斗原语。
- **表现/同步层**：通过快照、Trace、Cue、ViewEvent 把逻辑结果投递到客户端表现或网络同步。

---

## 9. 扩展点与约束

### 9.1 扩展点

| 扩展点 | 方式 | 适用场景 |
|--------|------|----------|
| 新技能阶段 | 扩展 `SkillPhaseDTO` 到 `PhaseDefinition` 的映射 | 新增特殊阶段类型 |
| 新技能动作 | 注册 Triggering `ActionRegistry` / PlanActionModule | 数据化技能效果、位移、召唤、投射物 |
| 新条件 | 注册 Triggering `FunctionRegistry` 或条件 Factory | 冷却、资源、距离、Tag、状态判断 |
| 新 Combat 原语 | 新增 `combat.*` 模块服务，再由 Action 调用 | 新目标搜索、新弹道、新伤害类型 |
| 新释放策略 | 扩展 `SkillCastPolicyResolver` | 并发、打断、蓄力、引导技能规则 |
| 新输入语义 | 扩展输入命令 Handler 或 `SkillInputPhase` 处理 | 长按、拖拽、二段释放、取消确认 |

### 9.2 关键约束

1. **不要把技能写成巨型 Executor**：复杂逻辑应拆到 Pipeline Phase、Trigger PlanAction、Effect Component、Combat Service。
2. **不要在配置构建阶段修改世界状态**：`TableDrivenMobaSkillPipelineLibrary` 只构建 PhaseDefinition 和 Phase，状态修改应发生在运行时动作中。
3. **需要可回放/可同步的逻辑必须确定性**：随机值、时间、帧号应由上层注入或来自 `IFrameTime`。
4. **运行时必须可追踪**：正式技能释放应创建 `MobaTraceRegistry` 根上下文和 `MobaSkillCastRuntimeService` runtime handle。
5. **清理必须集中处理**：阶段订阅、临时对象、持续效果绑定应注册到 `SkillPipelineContext` cleanup 中。
6. **旧 Checks/Handlers 阶段不应继续扩散**：源码已明确要求迁移到 RulePlan 条件与动作。
7. **不要把通用 `AbilityTimelinePhase.cs` 当成当前技能时间线实现**：该文件当前只是停用占位，MOBA 正在使用的是 `SkillTimelinePhase`。
8. **长生命周期阶段要确认运行实例隔离**：有状态阶段应实现 `IAbilityPipelinePhaseInstanceFactory<TCtx>`，或者像 MOBA 配置库一样每次从 PhaseDefinition 创建新阶段。
9. **清理回调不是事务边界**：通用 `AbilityPipeline<TCtx>` 在完成、失败和中断时会调用 `ReleaseContext`，但当前实现会吞掉该回调异常，再注销 Registry 并归还阶段列表。宿主需要在自己的 cleanup 中记录失败并保证幂等，不能依靠异常向上传播判断资源是否释放完整。
10. **Registry 只提供观察索引**：`PipelineRegistry` 未初始化时 `Register` 静默跳过，`Shutdown()` 只清空 owner 列表，不会中断仍在运行的 Pipeline。项目 runtime 的 ForceTerminate/Clear 责任不能转交给 Registry。

---

## 10. 证据与关联文档

### 10.1 证据状态与已知限制

- **E0 实现**：Pipeline、Ability、Triggering 与 Combat 包提供可组合原语；MOBA 包提供完整的技能应用层实现。
- **E1 示例**：纯 C# Pipeline 示例与 MOBA 配置/装配展示了即时、跨帧和表驱动组合方式。
- **E2 集成**：MOBA 技能释放、被动生命周期、Trigger/Effect 与 Combat 动作存在真实消费者和集成测试。
- **E3 契约**：2026-08-16 当次 `AbilityKit.Pipeline.Tests` 为 `3/3`，覆盖 PhaseId 和执行异常与 error handler 双失败聚合；`AbilityKit.Ability.Tests` 为 `4/4`，覆盖 TriggerEvent 构造和 Effect 定点时间累计。它们没有覆盖 Registry shutdown、ReleaseContext 异常、全部组合阶段或 MOBA 施法事务。
- **E4 场景**：历史 P0 Console/Unity artifact 只能证明其日期化覆盖路径。当前 MOBA 主工程为 `279/305`，26 项在 World 创建前被同一个 SpawnArea 严格配置错误阻断，不能写成 2026-08-16 技能主路径已重新通过。
- **E5 门禁**：当前没有把“完整通用技能应用层”作为发布能力的独立预算和强制门禁；这是有意保留的非目标，而非待补齐的单一框架类。

已知限制是 MOBA 施法准备、槽位、冷却、资源、失败码和清理策略仍然项目专用。采用方应复制其组织思路并重新定义领域协议，而不是直接依赖示例命名形成跨项目兼容承诺。

- [玩法能力地图](00-GameplayCapabilityMap.md) - 玩法层整体能力与模块协作。
- [触发器系统](02-TriggeringSystem.md) - 事件、条件、动作、计划化执行。
- [Buff 系统](03-BuffSystem.md) - GameplayEffect 生命周期与 Buff 表达。

---

*文档类型：示例分析（MOBA 技能应用层） | 事实基线：2026-08-16 | 证据等级：公共包局部 E3；E4 仅限日期化历史 artifact | 文档版本：v3.0*
