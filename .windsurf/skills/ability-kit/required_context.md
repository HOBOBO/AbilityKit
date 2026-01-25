---
name: ability-kit
section: required-context
---

# Required context from the user

为了高效定位问题/实现功能，你最好提供：

- **事件名**：例如 `skill.cast.complete`
- **触发器 triggerId / 配置来源**：ability_triggers.json / AbilityModuleSO
- **触发对象**：caster/target actorId（或对应实体）
- **期望时序**：同帧生效 vs 下一帧生效
