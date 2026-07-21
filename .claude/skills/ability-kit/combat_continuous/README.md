# Combat / Damage Pipeline 与 Continuous 模块

> 旧 ability-kit skill 完全未覆盖的两个独立业务域。基于当前源码。

## Combat / Damage Pipeline（moba demo）

位置：`Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Combat/`

### 核心类

- `MobaCombatEffectService.cs` — 战斗效果服务
- `Damage/DamagePipelineService.cs` — 9 个 damage.* 阶段事件
- `Damage/DamagePipelineEvents.cs` — 事件名常量
- `Damage/DamagePipelineStages.cs` — 阶段定义
- `Damage/MobaDamageMitigationService.cs` — 减伤服务
- `Damage/MobaShieldService.cs` — 护盾服务

### 9 个 damage 阶段事件

1. `damage.attack.created`
2. `damage.before_calc`
3. `damage.calc.begin`
4. `damage.after_base`
5. `damage.after_mitigate`
6. `damage.after_shield`
7. `damage.final`
8. `damage.apply.before`
9. `damage.apply.after`

事件经第二套 `IEventBus` 派发（强类型 args）。

## Continuous（持续行为）

位置：`Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Continuous/`

### 核心类

- `MobaContinuousManager.cs` — 持续行为管理器
- `MobaContinuousModifierProjectorRegistry.cs` — 修改器投射器注册表
- `MobaContinuousModifierEntry.cs` — 修改器条目
- `MobaContinuousModifierQueryService.cs` — 查询服务
- `MobaContinuousLifecycleBinder.cs` — 生命周期绑定器
- `MobaEffectiveTagQueryService.cs` — 基于 GameplayTags 的 effective-tag 查询

### 用途

- BUFF 的 duration/interval 持续行为
- Skill pipeline 的 continuous 效果
- 标签的有效性查询（考虑持续修改器叠加后的实际标签）

## 属性与修改器（demo 侧）

- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Domain/Attributes/ActorEntityMobaAttrs.cs`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Domain/Attributes/MobaAttributeIds.cs`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Domain/Attributes/MobaAttrs.cs`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Modifiers/MobaSkillParamGroupResolver.cs`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Modifiers/MobaModifierResolveContext.cs`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Modifiers/MobaSkillParamModifierService.cs` — 技能参数修改器（如普攻换技能 ID）

## Summon（召唤物）

- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Summon/MobaSummonTriggering.cs`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Summon/OwnerLinkUtil.cs`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Summon/MobaSummonDeathSubscriber.cs`

## Trace / 诊断体系（贯穿全业务）

- `MobaTraceRegistry` / `TraceOrigin` / `TraceLifecycleReason`
- `MobaSkillCastRuntimeService` / `MobaSkillCastRuntimeHandle`
- `IMobaBattleDiagnosticsService` / `MobaBattleDiagnosticEventDraft`
- `IMobaBattleExceptionPolicy` / `MobaBattleExceptionContext` / `Domain` / `Severity`

Skill/Buff 全链路都接了 trace context 与异常策略。

## 关键约定

- 所有事件走**第二套** EventBus（`AbilityKit.Triggering.Eventing.IEventBus`）
- Damage 阶段事件可被订阅者拦截/修改（带 ExecutionControl）
- 持续行为通过 Continuous 模块统一管理，避免散落在各处
