# 渐进式技能系统示例

## 概述

本系列展示如何从最朴素的需求出发，逐步演进为一个完整的技能系统。每个 Phase 代表一种"需求复杂度级别"，展示框架在该复杂度下提供的抽象能力。

**设计理念**: 从需求出发，而非从框架出发。每个 Phase 都从一个真实需求开始，然后演进到能优雅解决该需求的框架能力。

## 演进路线

```
Phase0: 纯 C# Tick 循环           (零框架依赖，基准)
Phase1: IContinuous               (com.abilitykit.core)
Phase2: Flow                     (com.abilitykit.flow)
Phase3: Triggering               (com.abilitykit.triggering)
Phase4: Pipeline                 (com.abilitykit.pipeline)
Phase5: HFSM                    (com.abilitykit.hfsm)
```

## 框架模块对应

| Phase | 框架模块 | 源码位置 |
|-------|---------|---------|
| Phase0 | 无 | 纯 C# 实现 |
| Phase1 | IContinuous | `com.abilitykit.core/Runtime/Continuous/` |
| Phase2 | Flow | `com.abilitykit.flow/Runtime/` |
| Phase3 | Triggering | `com.abilitykit.triggering/Runtime/` |
| Phase4 | Pipeline | `com.abilitykit.pipeline/Runtime/` |
| Phase5 | HFSM | `com.abilitykit.hfsm/Runtime/` |

## JSON 配置驱动

每个 Phase 的配置都存储在代码中作为嵌入的 JSON（实际项目中可从外部 JSON 文件加载），展示如何将配置与逻辑分离。

## 各 Phase 详解

### Phase0: 朴素 Tick (~170行)

**需求**: 每秒对目标造成 10 点火焰伤害，持续 5 秒。

**实现**: 零框架依赖，纯 C# 手动时间累积。

**问题暴露**: 如果需要同时管理多个定时行为（灼烧 + 减速 + 中毒），需要手动遍历、手动管理生命周期。

### Phase1: IContinuous (~430行)

**需求**: 管理多个持续行为，支持暂停、恢复、中断。

**框架模块**: `com.abilitykit.core` 的 `IContinuous` + `IContinuousManager`。

**配置**: 嵌入的 `Phase1Config` JSON 定义 effect 参数。

**问题暴露**: 如果需要在特定时机触发效果（如造成伤害时），Continuous 无法处理。

### Phase2: Flow (~190行)

**需求**: 引导技能（等待 1.5 秒）+ 并行播放特效 + 连击分支。

**框架模块**: `com.abilitykit.flow` 的 `FlowSession` + `IFlowNode` + `SequenceNode` + `IfNode` + `RaceNode`。

**配置**: 嵌入的 `Phase2Config` JSON 定义技能参数。

**问题暴露**: 无法响应战斗事件。

### Phase3: Triggering (~360行)

**需求**: 造成伤害时，30% 几率触发灼烧；击杀目标时，触发连击加成。

**框架模块**: `com.abilitykit.triggering` 的 `EventBus` + `TriggerRunner` + `ITrigger`。

**配置**: 嵌入的 `Phase3Config` JSON 定义触发器参数。

**问题暴露**: 不知道技能执行阶段。

### Phase4: Pipeline (~360行)

**需求**: 完整技能流程：验证 → 消耗 → 引导 → 效果 → 冷却，支持打断。

**框架模块**: `com.abilitykit.pipeline` 的 `AbilityPipeline` + `AbilityInstantPhaseBase` + `IAbilityPipelineContext`。

**配置**: 嵌入的 `Phase4Config` JSON 定义技能和管线参数。

**问题暴露**: 需要状态机管理角色行为。

### Phase5: HFSM (~280行)

**需求**: 角色行为状态：Idle → Combat → Dead，支持状态转换。

**框架模块**: `com.abilitykit.hfsm` 的 `StateMachine` (AbilityKit.HFSM)。

**配置**: 嵌入的 `Phase5Config` JSON 定义状态和转换。

**集成**: 展示如何与 Pipeline/Triggering/Continuous 协作。

## 完整系统架构

```
┌─────────────────────────────────────────────────────────┐
│                    完整技能系统架构                       │
├─────────────────────────────────────────────────────────┤
│                                                         │
│   ┌─────────┐                                          │
│   │   HFSM  │  角色行为状态 (Idle/Combat/Dead)         │
│   └────┬────┘                                          │
│        │                                               │
│        ↓                                               │
│   ┌─────────┐                                          │
│   │ Pipeline │  执行技能流程                              │
│   │ Validation/Consume/Channeling/Effect/Cooldown       │
│   └────┬────┘                                          │
│        │ 派发事件                                        │
│        ↓                                                │
│   ┌─────────┐                                          │
│   │Triggering│  响应战斗事件                            │
│   │ Predicate / Action                                │
│   └────┬────┘                                          │
│        │ 添加 Buff                                       │
│        ↓                                                │
│   ┌─────────────┐                                       │
│   │ Continuous │  管理 Buff 生命周期                     │
│   │ DOT / HOT / Buff / Debuff                          │
│   └─────────────┘                                       │
│                                                         │
│   ┌─────────────┐                                       │
│   │   Flow     │  复杂流程编排 (引导/并行/分支)         │
│   └─────────────┘                                       │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

## 各模块职责

| 模块 | 职责 | 关键接口/类 |
|------|------|-------------|
| **HFSM** | 角色行为状态 | `StateMachine<TStateId, TEvent>` |
| **Pipeline** | 技能执行流程 | `AbilityPipeline<TCtx>`, `IAbilityPhase<TCtx>` |
| **Triggering** | 战斗事件响应 | `EventBus`, `TriggerRunner<TCtx>`, `ITrigger<TArgs, TCtx>` |
| **Continuous** | Buff/DOT/HOT 生命周期 | `IContinuous`, `IContinuousManager` |
| **Flow** | 异步流程编排 | `FlowSession`, `IFlowNode`, `SequenceNode` |

## 运行方式

```powershell
cd src/AbilityKit.Samples.Logic
dotnet build
dotnet run
```

选择 `ProgressiveSkill` 子菜单下的各个 Phase 运行。

## 设计原则

1. **使用框架实际模块** - 每个 Phase 直接使用框架包提供的类和接口
2. **JSON 配置驱动** - 配置与代码分离，JSON 定义所有数据
3. **渐进式演进** - 每个 Phase 只引入一个新模块
4. **代码简洁** - 每个 Phase 控制在一屏内可读完
5. **无重复实现** - 不再自己实现简化版，统一使用框架
