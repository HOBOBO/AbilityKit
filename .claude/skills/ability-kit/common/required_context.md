# Required context from the user

为了高效定位问题/实现功能，最好提供以下信息：

## 基础信息

- **事件名 / EventKey**：例如 `MobaSkillTriggering.Events.CastComplete` 或 `MobaBuffTriggering.Events.Apply`；新引擎下若用 `EventKey<TArgs>`，需要知道 `TArgs` 类型（如 `BuffEventArgs`）
- **触发器 triggerId / 配置来源**：
  - moba JSON：`Configs/moba/skills.json` / `skill_flows.json` / `passive_skills.json`
  - Plan JSON：`Configs/ability/trigger_*.json` / `TriggerPlanJsonDatabase`
  - Luban：`Configs/luban/*.json`
- **触发对象**：caster/target actorId 或 ActorEntity
- **期望时序**：同帧生效 vs 下一帧生效（影响 EventBus 模式选择：Immediate vs Queued+Flush）

## 决策类信息（必须先回答）

- **使用哪套触发器？**
  - 改 moba 业务（技能/被动/BUFF/伤害/投射/区域）→ **第二套** `com.abilitykit.triggering`
  - 改 ability 包 Effect 层（EffectService/EffectContainer）→ 第一套 `com.abilitykit.ability/Runtime/Ability/Triggering/`
  - 不确定时优先看 moba 调用链的 IEventBus 命名空间：`AbilityKit.Triggering.Eventing.IEventBus` = 第二套
- **Plan 还是直接 Subscribe？**
  - 配置驱动（数据驱动技能）：`TriggerPlan` + JSON + `TriggerPlanJsonDatabase`
  - 代码驱动（系统级监听）：直接 `EventBus.Subscribe<TArgs>(key, handler)`
- **是否在 Pipeline 上下文中？**
  - 若是 → 通过 `SkillPipelineContext` 携带的 EventBus 引用派发
  - 若否 → 通过 `IWorldResolver` 解析 `IEventBus`
