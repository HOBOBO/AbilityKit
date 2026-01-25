---
trigger: manual
---

# Skill（技能）规则

## 1) 核心对象

- **SkillExecutor / SkillPipelineRunner**：技能管线执行
- **MobaSkillTriggering**：派发 `skill.*` 事件（例如 `skill.cast.complete`）
- **SkillCastRequest**：常见 payload，包含 `CasterActorId`/`TargetActorId`/...

## 2) 被动技能（Passive Skills）

- **MobaPassiveSkillTriggerRegisterSystem**：按 `SkillLoadoutComponent.PassiveSkillRuntime.TriggerIds` 注册/反注册监听
- **默认自过滤**：对“可判定来源 actorId 的事件”，外部事件只执行 `AllowExternal=true` 的 trigger entry

## 3) 事件来源约定（用于 AllowExternal 过滤）

触发器若要支持“自身/外部”判定，事件必须在 `evt.Payload/evt.Args` 提供来源 actorId。

当前被动系统识别来源的 key：

- `MobaSkillTriggering.Args.CasterActorId`（`"caster.actorId"`）
- `EffectSourceKeys.SourceActorId`（`"effect.sourceActorId"`）
- 或 payload 为 `SkillCastRequest`（优先读取 `CasterActorId`）

## 4) AllowExternal 语义

- `AllowExternal=false`（默认）：外部来源事件不会执行该 trigger
- `AllowExternal=true`：允许处理外部来源事件（内部再通过条件判断关系/阵营等）
