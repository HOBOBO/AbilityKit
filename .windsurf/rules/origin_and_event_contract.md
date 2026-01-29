---
trigger: manual
---

# 事件溯源（Origin）与事件契约

## 1) 目标

任意 TriggerEvent 链式触发（skill -> effect -> damage -> buff -> projectile -> …），都能追溯到同一个 `origin.contextId`。

- `contextId` 是对外统一语义
- `rootId` 仅为溯源模块内部概念（“无父即根”），不要写入 `origin.contextId`

## 2) Rule 1：禁止丢失溯源（No Lost Origin）

任何会发布 TriggerEvent 的模块/系统/服务，必须保证事件携带溯源字段：

- 根事件必须显式写入 `origin.*`
- 非根事件必须继承（透传）上游的 `origin.*`

## 3) Rule 2：继承发布必须使用统一入口（Recommended）

当你从已有事件（存在 parentArgs）再发布新事件时，必须继承溯源字段。

至少继承：

- `origin.source` / `origin.target` / `origin.kind` / `origin.configId` / `origin.contextId`

推荐使用统一 helper：`TriggerEventPublishExtensions.PublishInherited(...)`

## 4) Rule 3：内部流水线事件必须带 Args（No args:null）

禁止发布 `args: null` 的 TriggerEvent（会导致溯源链路中断）。

## 5) Rule 4：origin.kind 类型统一为 enum

`origin.kind` 必须统一使用 `EffectSourceKind`（enum），禁止使用字符串。

## 6) 统一实现

统一使用 `EffectOriginArgsHelper`：

- `EffectOriginArgsHelper.FillFromRegistry(args, sourceContextId, registry)`
- `EffectOriginArgsHelper.FillFromServices(args, sourceContextId, services)`

语义约束：

- `origin.contextId` 永远写 `contextId`（snapshot.ContextId / sourceContextId）
- `rootId` 只在溯源系统内部维护/使用

## 7) EffectSource（溯源树）落地入口（推荐）

当你需要把“技能/效果/BUFF/飞行物”等运行时派生链路串成可追溯的树，并保证可回收/可排查时，请使用 `EffectSourceRegistry`。

- 规则：`.windsurf/rules/effect_source.md`
- 速查：`.windsurf/skills/ability-kit/effect_source.md`

关键实现文件：

- `Unity/Packages/com.abilitykit.ability.runtime/Runtime/Ability/Share/Impl/Moba/EffectSource/EffectSourceRegistry.cs`
- `Unity/Packages/com.abilitykit.ability.runtime/Runtime/Ability/Share/Impl/Moba/EffectSource/EffectSourceKeys.cs`

常用模式（建议）：

- root：在技能入口创建（例如 skill cast），并在流程逻辑结束处 End
- child：在派生对象创建处创建，并在对象生命周期结束时 End
