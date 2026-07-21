# Entitas ECS Components（运行时状态总览）

位置：`Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Common/Shared/Entitas/Generated/`

5 个 Context：`Actor` / `Game` / `Input` / `Service` / `Feature`，外加 `Contexts.cs`。

## Actor Context（39 个 `Actor*Component`，按语义分组）

### 标识 / 元数据

- `ActorActorIdComponent` / `ActorEntityMainTypeComponent` / `ActorUnitSubTypeComponent` / `ActorModelIdComponent`
- `ActorTeamComponent` / `ActorOwnerLinkComponent` / `ActorOwnerPlayerIdComponent` / `ActorSummonMetaComponent`

### 属性 / 资源

- `ActorAttributeGroupComponent` / `ActorResourceContainerComponent`

### 战斗语义（Buff/Effect/碰撞/生命期）

- `ActorBuffsComponent` / `ActorEffectListenersComponent`
- `ActorCollisionLayerComponent` / `ActorCollisionIdComponent` / `ActorColliderComponent`
- `ActorLifetimeComponent` / `ActorActorDespawnRequestComponent`

### 移动 / 变换 / 资源

- `ActorTransformComponent` / `ActorMotionComponent` / `ActorMoveInputComponent`

### 投射物

- `ActorFlyingProjectileTagComponent` / `ActorProjectileLauncherComponent` / `ActorProjectileEffectSnapshotComponent`

### 技能施放（SkillCast 实体，14 个）

- `ActorSkillCastInstanceIdComponent` / `ActorSkillCastOwnerActorIdComponent`
- `ActorSkillCastSkillIdComponent` / `ActorSkillCastSkillLevelComponent` / `ActorSkillCastSlotComponent`
- `ActorSkillCastStageComponent` / `ActorSkillCastSequenceComponent` / `ActorSkillCastStartFrameComponent`
- `ActorSkillCastTargetActorIdComponent` / `ActorSkillCastAimComponent` / `ActorSkillCastTimelineRuntimeComponent`
- `ActorSkillCastRunningTagComponent` / `ActorSkillCastCancelRequestComponent` / `ActorSkillCastDestroyRequestComponent`
- 另：`ActorSkillLoadoutComponent`（技能装载，属 Actor 本体）

### 触发器 / 被动

- `ActorOngoingTriggerPlansComponent` / `ActorPassiveSkillTriggerListenersComponent`

### AI

- `ActorActorBrainComponent`

## Domain 层 vs 生成层

注意：Domain 层 `Domain/Components/` 还有手写的非生成组件定义（`ActorComponent`、`ActorBrainComponent`、`BuffComponent`、`ShieldComponents`、`SkillCastInstanceComponents`、`SkillRuntime`、`SkillLoadoutComponent`、`ProjectileLauncherComponent`、`MotionComponent`、`LifetimeComponent` 等）。

这些是 Domain 语义模型，与生成 Component 并存：
- **生成 Component**（`Actor*Component`）：运行时 Entitas ECS 用，自动生成
- **Domain Component**（无 `Actor` 前缀）：玩法语义层用，手工维护

## Entitas 集成

- `EntitasEcsWorld` / `EntitasWorldContext` / `EntitasUnitFacade`（`Common/Shared/ECS/Entitas/`）
- `MobaEntitasContextsFactory`（`Infrastructure/Entitas/`）— 实现 `IEntitasContextsFactory`（注意 I 前缀）
- `MobaEntitasContextsExtensions`（`Infrastructure/Entitas/`）

## System 基类

继承 `WorldSystemBase`（`com.abilitykit.world.entitas/Runtime/World/Base/`）：

- `OnInit()` / `OnExecute()` / `OnCleanup()`（每帧清理，基本不用）/ `OnTearDown()`（销毁释放）

或 `ReactiveWorldSystemBase<T>`（响应式，`OnCleanup` 空实现，资源释放在 `OnTearDown`）。

`[WorldSystem(order: ..., Phase = WorldSystemPhase.Execute|PostExecute)]` 声明系统顺序与执行阶段。
