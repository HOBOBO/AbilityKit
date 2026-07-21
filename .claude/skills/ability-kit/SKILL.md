---
name: ability-kit
description: AbilityKit MOBA 技能框架速查与实现约束。涵盖两套触发器引擎（旧字符串键引擎 + 新 Plan 行为树引擎）、技能施放管线（SkillPipelineRunner/SkillCastCoordinator）、Pipeline（com.abilitykit.pipeline 独立包）、Effect 系统（替代旧 EffectSource）、BUFF 生命周期、Combat/Damage Pipeline、Continuous、Host/WorldBlueprints 多包装配、Console Demo 运行调试。触发场景：用户提到 abilitykit、技能 cast/pipeline、触发器 trigger、event bus、被动 Passive、Plan 行为树、BUFF apply/remove/tick、EffectService/EffectInstance、PipelineRuntime/EditorPipelineRegistry、HostRuntime/WorldBlueprintRegistry、IWorldModule、Console Demo、dotnet run、AutoTestRunner、Configs/moba|luban|ability、帧同步日志、DamagePipeline、Continuous、MobaTriggerPlanExecutor。
---

# ability-kit skill

基于当前源码核校（2026-07-20）。本目录按业务域分子目录，避免扁平化难维护。

## 最重要的架构事实（必读）

### 1. 存在两套完全独立、互不依赖的触发器

| 维度 | 旧引擎（第一套） | 新引擎（第二套，moba 实际生产用） |
|------|-----------------|--------------------------------|
| 位置 | `com.abilitykit.ability/Runtime/Ability/Triggering/` | `com.abilitykit.triggering/Runtime/` |
| EventBus 键 | `string eventId` + `TriggerEvent(Id, payload, args:IReadOnlyDictionary)` | 强类型 `EventKey<TArgs>` |
| TriggerRunner | 非泛型 `TriggerRunner`，`Compile(TriggerDef)→Register/RunOnce` | 泛型 `TriggerRunner<TCtx>`，phase+priority 排序，Cue/Lifecycle/Observer/Tracer |
| 行为定义 | `TriggerDef/ConditionDef/ActionDef` + `TriggerCompiler` | `TriggerPlan` 行为树（Sequence/Selector/Parallel/If/Repeat/Until/Scheduled/ActionCall...） |
| 数据来源 | `AbilityTriggerJsonDatabase`（弱类型 JSON） | `TriggerPlanJsonDatabase`（Plan JSON，多形态 + 校验器） |
| 服务范围 | 仅 ability 包自己的 Effect 层 | moba demo 的技能/被动/BUFF/伤害/投射/区域 全部事件 |

**陷阱**：订阅旧 EventBus、在新 EventBus 上 Publish（或反过来）事件永远收不到。两者命名空间也不同：`AbilityKit.Ability.Triggering.IEventBus` vs `AbilityKit.Triggering.Eventing.IEventBus`。详见 [triggering/two_engines.md](triggering/two_engines.md)。

### 2. EffectSource 已被完全删除

旧 skill 提到的 `EffectSourceRegistry/EffectSourceSnapshot/EffectSourceLiveRegistry/EffectSourceDebuggerWindow/ContextId/RootId/ParentId` 全部从代码中消失。替代方案是新的 Effect 系统：`com.abilitykit.ability/Runtime/Ability/Effect/`（`EffectService` + `EffectInstance` + `GameplayEffectSpec` + `EffectTriggering` + `IEffectEventSink`）。详见 [effect/README.md](effect/README.md)。

### 3. Pipeline 已独立成单独的包

`com.abilitykit.pipeline/` 是独立 UPM 包（不再在 `Runtime/Ability/Share/Pipeline/` 下）。Pipeline 的接口全部泛型化 `<TCtx>`；调试基础设施改名：旧 `AbilityPipelineLiveRegistry` → `EditorPipelineRegistry`；旧 `PipelineGraphAsset`（ScriptableObject）不存在了，现在是 `PipelineGraph` 静态工厂类。详见 [pipeline/README.md](pipeline/README.md)。

### 4. Host 装配拆分到 4 个包

| 包 | 职责 |
|----|------|
| `com.abilitykit.host` | WorldBlueprints 装配、HostRuntime、WorldHostBuilder |
| `com.abilitykit.world.di` | World 抽象（IWorld/WorldId/WorldCreateOptions）+ DI 容器 + WorldManager |
| `com.abilitykit.world.entitas` | Entitas 适配（EntitasWorld/IEntitasContextsFactory） |
| `com.abilitykit.host.extension` | FrameSync/BattleHost/RoomSync/CatchUp/GameStartSource 等扩展 |

旧 skill 把 `LogicWorldServer` 当框架类，**实际它只是示例** `LogicWorldServerExample`。详见 [host/README.md](host/README.md)。

### 5. 旧 skill 引用过的已删除/已改名类（精选）

| 旧引用 | 当前状态 |
|--------|---------|
| `SkillExecutor` | **已删除**，无替代同名类。技能执行入口是 `SkillCastCoordinator` + `SkillPipelineRunner` |
| `BattleServices`（Console Demo） | **已删除** |
| `MobaBuffApplySystem/MobaBuffTickSystem/MobaBuffRemoveSystem` | **已删除**，入口改为 `MobaBuffService.ApplyBuffImmediate/RemoveBuffImmediate` + `BuffLifecycleExecutor` + `BuffEventPublisher` |
| `TriggerDef.AllowExternal` | `AllowExternal` 已下移到配置/DTO 层（`TriggerSourceConfig`、`TriggerPlanJsonDatabase`、`ExecutableDto`） |
| `PipelineGraphAsset/PipelineGraphDto` | **不存在**，现为 `PipelineGraph` 静态类 |
| `AbilityPipelineRunDebuggerWindow`（EditorWindow） | **不存在**；调试通过 `EditorPipelineRegistry` + `EditorPipelineTraceRecorder` API |
| `EffectSourceRegistry/EffectSourceSnapshot/...` | **全部删除** |
| `EntitasContextsFactory` | 实际接口名是 `IEntitasContextsFactory`（实现：`MobaEntitasContextsFactory`） |
| `LogicWorldServer` | 仅示例类 `LogicWorldServerExample` |

## 目录结构（按业务域细分）

```
ability-kit/
├── SKILL.md                          ← 本文件（主入口）
├── common/                           ← 跨业务通用
│   ├── when_to_use.md                ← 何时启用本 skill
│   ├── required_context.md           ← 改动前必须明确的信息
│   ├── output_expectations.md        ← 输出应包含的内容
│   ├── invariants.md                 ← 已验证的工程约定
│   ├── procedure.md                  ← 标准排查步骤
│   ├── upm_asmdef_notes.md           ← UPM/asmdef 注意事项
│   └── examples_and_troubleshooting.md  ← 跨模块排查实例
├── triggering/                       ← 触发器引擎
│   ├── README.md                     ← 触发器总览 + key_files + 调用链
│   └── two_engines.md                ← 两套引擎深度对比
├── skill_buff/                       ← 技能施放 + BUFF 业务
│   └── README.md                     ← 调用链 + key_files + Plan 动词
├── pipeline/                         ← com.abilitykit.pipeline 模块
│   └── README.md
├── effect/                           ← Effect 系统（替代旧 EffectSource）
│   └── README.md
├── combat_continuous/                ← 战斗伤害 + 持续行为
│   └── README.md
├── host/                             ← Host/WorldBlueprints 装配
│   └── README.md
└── console_demo/                     ← Console Demo 运行调试
    └── README.md
```

## 章节索引

### common/（跨业务通用）

- [common/when_to_use.md](common/when_to_use.md) — 何时启用本 skill
- [common/required_context.md](common/required_context.md) — 改动前必须明确的事件名/triggerId/对象/时序/EventBus 类型
- [common/output_expectations.md](common/output_expectations.md) — 输出应包含的内容
- [common/invariants.md](common/invariants.md) — 已验证的工程约定（中文注释、池化、WorldSystemBase.OnTearDown、**两个 IEventBus**、性能、asmdef）
- [common/procedure.md](common/procedure.md) — 标准排查步骤（基于新调用链）
- [common/upm_asmdef_notes.md](common/upm_asmdef_notes.md) — UPM/asmdef 注意事项（含 moba.runtime 34 条引用实例）
- [common/examples_and_troubleshooting.md](common/examples_and_troubleshooting.md) — 跨模块排查实例

### 业务域

- [triggering/README.md](triggering/README.md) — 触发器总览（含 key_files + 调用链）
- [triggering/two_engines.md](triggering/two_engines.md) — 两套触发器深度对比与选择
- [skill_buff/README.md](skill_buff/README.md) — 技能施放 + BUFF + 被动 + Plan 动词清单
- [pipeline/README.md](pipeline/README.md) — Pipeline 模块完整介绍
- [effect/README.md](effect/README.md) — 新 Effect 系统
- [combat_continuous/README.md](combat_continuous/README.md) — Combat/Damage Pipeline + Continuous + Summon + Trace
- [host/README.md](host/README.md) — Host 4 包装配范式
- [console_demo/README.md](console_demo/README.md) — Console Demo 运行/调试/配置

## 相关 skill

- 客户端预测/回滚/reconcile 见 [framesync-prediction-rollback](../framesync-prediction-rollback/SKILL.md)
- Session/Flow 类代码重构（State/Handles/Controllers）见 [state-handles-controllers](../state-handles-controllers/SKILL.md)
