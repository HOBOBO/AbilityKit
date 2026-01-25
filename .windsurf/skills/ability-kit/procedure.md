---
name: ability-kit
section: procedure
---

# Procedure (how to work on a skill-related task)

1. **确认事件源**
   - 找到事件派发处（通常是 `MobaSkillTriggering.Publish` 或具体 system/service）。
   - 确认 `IEventBus` 实例来自 DI（避免 publish/subscribe 不一致）。

2. **确认 payload 与 args 约定**
   - `TriggerEvent.Payload` 是否为 `SkillCastRequest` / `SkillPipelineContext`？
   - `evt.Args` 是否携带 `caster.actorId` / `effect.sourceActorId`？（影响被动外部过滤）

3. **确认订阅是否建立且生命周期正确**
   - 被动：`MobaPassiveSkillTriggerRegisterSystem` 是否为对应实体注册了 listener？
   - 反注册是否只发生在 entity destroy / system TearDown？

4. **确认 trigger 执行路径**
   - `TriggerRunner.RunOnce` 是否被调用？
   - 对外部事件：`AllowExternal=false` 的 entry 应被过滤。

5. **性能与池化检查**
   - 高频路径是否引入 `new List/Dictionary`、LINQ、闭包？
   - args 是否能复用并避免复制（临时注入/恢复优先）。
