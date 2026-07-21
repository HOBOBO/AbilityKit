# When to use

## 启用本 skill 的典型场景

- **技能施放链路**：定位 `MobaSkillTriggering.Publish` / `SkillCastCoordinator.TryCastSkill` / `SkillPipelineRunner.Start` / `SkillRulePlanPhase` / `MobaTriggerPlanExecutor.ExecuteRulePlan` 的实际入口与调用顺序
- **触发器/事件**：理解 `EventKey<TArgs>` 订阅、`TriggerRunner<TCtx>` phase+priority 派发、`TriggerPlan` 行为树执行；排查"事件收不到"（首先怀疑是不是用错了 EventBus）
- **被动技能**：`MobaPassiveSkillTriggerRegisterSystem` + `MobaPassiveSkillLifecycleService` 订阅/反注册；`AllowExternal` 在 Source/Plan DTO 层的配置
- **BUFF**：`MobaBuffService.ApplyBuffImmediate` → `BuffLifecycleExecutor` → `BuffEventPublisher` 向第二套 EventBus 发 `buff.apply/remove/interval/tick/stack/...`
- **Pipeline**：`com.abilitykit.pipeline` 独立包的 `IAbilityPipeline<TCtx>` / `IAbilityPipelineRun<TCtx>` / Phase 基类 / `EditorPipelineRegistry` 调试
- **Effect 系统**：`EffectService` / `EffectInstance` / `GameplayEffectSpec` / `EffectTriggering.Events` 的 apply/tick/remove 事件
- **Host 装配**：`HostRuntime` + `HostRuntimeModuleHost` + `WorldBlueprintRegistry` + `WorldTypeRegistry` + `WorldManager` 的标准组合
- **Combat / Damage**：`DamagePipelineService` 的 9 个 damage.* 阶段事件、`MobaShieldService`、`MobaDamageMitigationService`
- **Console Demo**：`dotnet run`（net10.0）、3 项自动测试、`Configs/moba|luban|ability` 三套配置、CLI 录制/回放、14 个 LogLevel

## 不要在本 skill 找的内容

- **客户端预测/回滚/reconcile** → 见 framesync-prediction-rollback
- **Session/Flow 类代码重构（State/Handles/Controllers 拆分）** → 见 state-handles-controllers
- **EffectSource（已删除）** → 改查 [effect_system.md](../effect/README.md)
- **SkillExecutor / BattleServices（已删除）** → 改查 `SkillCastCoordinator` / `SkillPipelineRunner`
