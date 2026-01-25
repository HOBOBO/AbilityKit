---
trigger: manual
---

# 召唤（Spawn Summon）规则

## 1) 模板化召唤（spawn_summon_action_templates）

- `SpawnSummon` 运行时必须通过 Resolver 解析最终 Spec：
  - 从 `spawn_summon_action_templates` 读模板 DTO/MO
  - 使用 trigger args 对模板字段做覆盖（overrides）
  - Action 只负责执行（不负责解析复杂参数）

## 2) TriggerStrong 层约束

- TriggerStrong 侧只存 Share 层枚举/数值
- Runtime ActionDef 的 `args key` 必须保持兼容（老配置不破）

## 3) 配表与导出约束

- 必须存在：`Assets/Resources/moba/spawn_summon_action_templates.json`
- 必须可被 `MobaConfigDatabase.LoadFromResources("moba")` 加载
- ScriptableObject 表资产使用 `MobaConfigTableAssetSO` 子类接入统一导出器

## 4) 实现步骤（模板化 TriggerAction 通用范式）

输入：

- TriggerAction type string
- Share 层 TriggerStrong RuntimeConfig + EditorConfig 字段
- Impl 层 Action + Factory +（Parser/Resolver 如需）
- MobaConfig 表（可选）

输出：

- TriggerStrong 可编辑、可编译为 ActionDef
- 运行时 Action 通过 Factory 注册并可执行
- 如用模板表：Resources 可加载、Editor 可导出

步骤规范：

- 在 `TriggerActionTypes` 增加 type 字符串常量（例如 `spawn_summon`）
- TriggerStrong：新增 `*ActionConfig : ActionRuntimeConfigBase`
  - `Type => TriggerActionTypes.*`
  - `ToActionDef()` 写入 args（key 命名稳定）
- TriggerStrong：新增 `*ActionEditorConfig : ActionEditorConfigBase`
  - 用 `[TriggerActionType(...)]` 注册 Odin 菜单
  - `ToRuntimeStrong()` 产出 runtime config
- Impl：Factory
  - 在 Create(type)/注册表中注册新 Action
- Impl：Action
  - 不做复杂解析：只做执行与事件/服务调用
  - 复杂参数交给 Resolver/Selection
