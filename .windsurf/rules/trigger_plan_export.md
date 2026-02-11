---
trigger: manual
---

# Trigger Plan 导出规则（ability_trigger_plans.json）

## 1) 术语

- **TriggerStrong（强类型触发器）**：`ActionEditorConfigBase` / `ConditionEditorConfigBase` 的派生类（例如 `GiveDamageActionEditorConfig`）。
- **JsonActionEditorConfig（弱类型/兜底节点）**：仅包含 `TypeValue + Args(Dictionary)` 的节点，用于无法匹配强类型配置时兜底。
- **Plan 导出产物**：`Assets/Resources/ability/ability_trigger_plans.json`

## 2) 核心不变量

- **运行时 PlanActionModule 的签名必须与导出 Arity 对齐**
  - 例如 `GiveDamagePlanActionModule` 仅实现 `Action2(value, reasonParam)`，所以 plan 中必须导出 `Arity=2`。
- **强类型参数必须能在导出时稳定读取**
  - 如果导出器读到 `Args=null`/`Args empty`，则参数一定丢失，最终 `ConstValue` 会是 0。

## 3) 高风险点（会导致 value=50 导出为 0）

- **节点不是强类型**：实际节点为 `JsonActionEditorConfig` 且 `Args=null`。
  - 典型表现：Unity Inspector 里只看到 `Args`，看不到 `伤害值` 等字段。
- **导出链路中使用对象池容器**：`PooledDefArgs` / `PooledTriggerArgs` 这类 `Dictionary` 在 Release 时会 `Clear()`。
  - 规则：导出阶段如需长期使用 args，必须 copy（或直接从强类型节点取值）。

## 4) 推荐工作流

- 修改触发器后：
  - 立刻执行 `AbilityKit/Ability/Export Trigger Plan Json`
  - 立刻打开 `ability_trigger_plans.json` 校验 `TriggerId` 下对应 `Actions` 的 `Arity/ConstValue`

## 5) 定位指南（当只输出日志不造成伤害）

- **先看导出**：
  - `give_damage` 是否为 `Arity=2` 且 `Arg0.ConstValue` 为期望伤害值
- **再看运行时注册**：
  - `GiveDamagePlanActionModule` 是否被注册（`PlanActionModuleRegistry`）
  - `ActionRegistry` 是否能同时存多 arity delegate
- **最后看上下文解析**：
  - `PlanContextValueResolver.TryGetCasterActorId/TryGetTargetActorId` 是否能从 payload（如 `ProjectileHitArgs`）解析出 attacker/target
