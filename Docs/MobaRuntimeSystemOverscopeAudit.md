# Moba.Runtime System 越界逻辑审计

## 目标

梳理 `moba.runtime` 中 `system` 层的越界逻辑，识别哪些内容应下沉到 `service`，以提升单元测试能力、可维护性和工业化程度。

## 当前原则

- `system`：负责调度、编排、生命周期推进、事件消费、轻量状态变更。
- `service`：负责规则判断、业务流程、可复用执行逻辑、跨模块统一实现。
- `dto/mo/config`：只承载数据和配置，不承载规则。
- `trigger/event`：统一入口和溯源上下文。

## 典型越界模式

以下情况通常说明 `system` 已经越界：

- 出现大量业务分支和条件树。
- 直接解析配置并决定业务规则。
- 直接执行效果逻辑，而不是调度 service。
- 同一类逻辑在多个 system 中重复出现。
- system 依赖过多外部上下文并进行复杂推导。

## 当前需要重点审计的 system 区域

### 1. AOE 生命周期相关 system

重点文件：
- [`MobaAreaSyncSystem`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Systems/Area/MobaAreaSyncSystem.cs#L1)
- [`SpawnAreaPlanActionModule`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Triggering/PlanActions/Skill/SpawnAreaPlanActionModule.cs#L1)

关注点：
- `area.spawn / area.enter / area.exit / area.tick` 的分发是否应进一步下沉到 `service`。
- `system` 是否只负责收集事件、查找实体、转交 payload。
- `area stay` 的间隔调度是否应迁移到统一的 area runtime service。

建议：
- 将 AOE 事件分发整理为 `IAreaTriggerService` 或现有 `MobaTriggerExecutionGateway` 的更高层封装。
- `system` 保留实体监听与事件消费，不直接持有太多规则分支。

### 2. Projectile 生命周期相关 system

重点文件：
- [`MobaProjectileSpawnSyncHandler`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Systems/Projectile/MobaProjectileSpawnSyncHandler.cs#L1)
- [`MobaProjectileTickSyncHandler`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Systems/Projectile/MobaProjectileTickSyncHandler.cs#L1)
- [`MobaProjectileHitSyncHandler`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Systems/Projectile/MobaProjectileHitSyncHandler.cs#L1)
- [`MobaProjectileExitSyncHandler`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Systems/Projectile/MobaProjectileExitSyncHandler.cs#L1)
- [`MobaProjectileStageTriggerExecutor`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Systems/Projectile/MobaProjectileStageTriggerExecutor.cs#L1)

关注点：
- 这些 handler 是否仍保留了过多业务执行细节。
- hit/spawn/tick/exit 的 trigger 执行是否应统一下沉为 `ProjectileRuntimeService`。
- `MobaProjectileStageTriggerExecutor` 目前更像 service helper，是否应改为显式 service。

建议：
- 把 stage trigger 处理封装为 service，system 只转发事件和同步实体状态。
- hit 事件 payload 的构造与 lineage 绑定也尽量由 service 承接。

### 3. Triggering / PlanAction system

重点文件：
- [`SpawnAreaPlanActionModule`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Triggering/PlanActions/Skill/SpawnAreaPlanActionModule.cs#L1)
- [`MobaTriggerExecutionGateway`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Triggering/MobaTriggerExecutionGateway.cs#L1)

关注点：
- 计划动作模块是否逐渐承担了过多 runtime 逻辑。
- `system` 是否应该只消费计划动作结果，而不参与效果决策。

建议：
- 保持 plan action 作为 service 层能力。
- runtime system 不要反向嵌入 plan 规则。

### 4. 配置校验与引用解析

重点文件：
- [`MobaBattleConfigReferenceValidator`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Validation/MobaBattleConfigReferenceValidator.cs#L1)
- [`MobaConfigDatabase`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Infrastructure/Config/Core/MobaConfigDatabase.cs)

关注点：
- 校验规则是否仍在 system 侧散落。
- 是否所有配置解释都已经集中在 service / infrastructure。

建议：
- 保持校验在 service / infrastructure。
- system 只依赖校验后的运行时模型。

## 建议的下沉优先级

### P0：必须尽快下沉

- 任何直接执行 trigger 的业务逻辑。
- 任何在 system 内部出现的复杂条件树。
- 任何跨多个实体/阶段复用的效果执行逻辑。

### P1：建议尽快下沉

- AOE 阶段事件的分发与 payload 构造。
- Projectile 阶段 trigger 的统一执行入口。
- 带有溯源/上下文组装的逻辑。

### P2：可逐步下沉

- 生命周期统计、埋点、日志格式化。
- 与表现层弱相关但重复出现的同步辅助逻辑。

## 单元测试收益

把越界逻辑从 `system` 下沉到 `service` 后，测试会更工业化：

- 可以直接对 service 做纯逻辑单测，减少对 world/system 的依赖。
- 可以更容易构造上下文和 payload，而不必启动完整同步流程。
- 可以把触发执行、阶段分发、配置解释拆成更稳定的测试边界。

## 结论

`system` 不应该承载最终业务规则。当前 `moba.runtime` 已经具备继续收敛的基础，下一步应持续把“触发执行、payload 构造、阶段规则分发”从 `system` 中抽离到 `service`，让 `system` 只保留调度职责。
