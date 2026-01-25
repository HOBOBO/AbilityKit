---
trigger: manual
---

# Buff（BUFF）规则

## 1) 模块与职责

- **System 处理**：`MobaBuffApplySystem` / `MobaBuffTickSystem` / `MobaBuffRemoveSystem`
- **Service 处理**：`MobaBuffService`
- **公共事件常量**：`MobaBuffTriggering`（不要在系统内再定义私有常量）

## 2) 事件常量

- `Events.ApplyOrRefresh = "buff.apply"`
- `Events.Remove = "buff.remove"`

Args 常用字段：

- `BuffId`/`EffectId`/`StackCount`/`DurationSeconds`/`RemoveReason`

## 3) Buff 多阶段效果协议（OnAdd/OnRemove/OnInterval）

- Buff 配置不再使用单一 `EffectId`，改为：
  - `OnAddEffects` / `OnRemoveEffects` / `OnIntervalEffects`
  - `IntervalMs`（<=0 表示不触发 interval）

事件约定：

- `buff.apply` / `buff.apply.<effectId>`
- `buff.remove` / `buff.remove.<effectId>`
- `buff.interval` / `buff.interval.<effectId>`

必须包含 args（用于被动 AllowExternal 外部过滤通用逻辑）：

- `effect.sourceActorId`
- `effect.targetActorId`
- `buff.id` / `buff.effectId` / `buff.stage` / `buff.stackCount`
- （remove）`buff.removeReason`

`buff.stage` 取值：

- `add` / `remove` / `interval`
