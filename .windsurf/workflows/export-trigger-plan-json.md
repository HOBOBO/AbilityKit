---
description: Export Trigger Plan Json（Plan方式触发器）
---

# 目标

把编辑器里的 **TriggerStrong（强类型触发器）** 导出为 `Assets/Resources/ability/ability_trigger_plans.json`，并确保：

- `TriggerId` 对应的 `Actions` 的 **Arity / ConstValue** 正确
- 运行时 `PlanActionModule` 能正确执行（例如 `give_damage`）

# 前置条件

- Unity 已完成编译（无红色 compile errors）
- 你确认当前修改的触发器资源（`AbilityModuleSO`）已保存

# 操作步骤

## 1) 在 Unity 导出 plan

在 Unity 菜单执行：

- `AbilityKit/Ability/Export Trigger Plan Json`

如果你的触发器资源放在默认目录（`Assets/Configs/Ability`），也可以用：

- `AbilityKit/Ability/Export Trigger Plan Json (Configs/Ability)`

导出目标文件：

- `Assets/Resources/ability/ability_trigger_plans.json`

## 2) 立刻校验 json 是否更新

打开 `Assets/Resources/ability/ability_trigger_plans.json`，定位你的 `TriggerId`，重点检查：

- `Actions[i].ActionId`
- `Actions[i].Arity`
- `Actions[i].Arg0.Kind / ConstValue`
- `Actions[i].Arg1.Kind / ConstValue`

### 常见正确形态

- `debug_log(messageId, dump)`
  - `Arity=2`
  - `Arg0.ConstValue` 为 `Strings` 表的 key（`str:*`）
- `give_damage(value, reasonParam)`
  - `Arity=2`
  - `Arg0.ConstValue` 为伤害值（例如 50）
  - `Arg1.ConstValue` 为 reasonParam（整数）

## 3) 若出现 Arity 对但 ConstValue=0（高频坑）

这通常意味着 **导出阶段 action 参数丢失**（不是运行时问题）。排查顺序：

- 3.1 在 Unity Inspector 里确认该节点是否为强类型节点
  - 强类型：看到 `伤害值/原因参数/...` 等字段（例如 `GiveDamageActionEditorConfig`）
  - 非强类型：只看到 `Args(Dictionary)`（`JsonActionEditorConfig`）

- 3.2 若是 `JsonActionEditorConfig` 且 `Args` 为空
  - 需要把该节点替换为强类型节点
  - 或在 `Args` 手动补 `value` / `reasonParam`

- 3.3 导出时 Console 搜索关键字（用于定位是哪条节点丢参）
  - `AbilityTriggerJsonExporter`（导出器诊断日志）

## 4) 运行时验证（命中触发伤害）

- 触发器能执行不代表伤害一定生效，至少确认：
  - 运行时 `TriggerId=20005`（或你的 TriggerId）执行时没有 `Action not found or signature mismatch`
  - `give_damage` 走到 `GiveDamagePlanActionModule.Execute2(...)`

# 常见故障 & 快速定位

## A) json 里 EventId 为空

导出器会提示：

- `Empty EventId triggers (active by TriggerId)`

这是允许的：意味着运行时通过 `TriggerId` 直接执行（例如 projectile hit 时直接 `ExecuteTriggerId(20005, payload)`）。

## B) Plan 执行时报 Action not found / signature mismatch

优先排查：

- ActionRegistry 是否支持同一 `ActionId` 多签名（Action0/1/2）并存
- PlanActionModule 是否被安装并注册

# 相关关键文件

- 导出器：
  - `Unity/Packages/com.abilitykit.ability.editor/Editor/Triggering/Utilities/AbilityTriggerJsonExporter.cs`
- 导出产物：
  - `Unity/Assets/Resources/ability/ability_trigger_plans.json`
- 运行时 plan action：
  - `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Impl/Moba/Systems/Bootstrap/PlanActions/*PlanActionModule.cs`
