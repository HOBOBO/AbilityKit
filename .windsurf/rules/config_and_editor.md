---
trigger: manual
---

# 配置 / Editor / DTO 规则

## 1) 触发器 JSON / Editor / DTO

- DTO：`TriggerDTO`（JSON）包含 `AllowExternal`
- Editor：`AbilityModuleSO.TriggerEditorConfig` 使用 Core: `TriggerHeaderDTO` 存头部字段
- `AllowExternal` 必须写在 `TriggerHeaderDTO` 才能在 Inspector 序列化/展示
- 导出：`AbilityTriggerJsonExporter` 负责把 EditorConfig 导出成 `TriggerDTO`

## 2) 新增表（MobaConfigDatabase / Registry）流程

新增表必须同步做三件事：

- `MobaConfigPaths` 增加 File 常量
- `MobaRuntimeConfigTableRegistry` 注册 DTO/MO
- `Resources` 下补齐 `{file}.json`（或确保导出流程生成）

Editor 导出统一走：

- `MobaConfigJsonExporter`（菜单 + Inspector 按钮）
- `MobaConfigTableAssetSO + IMobaConfigTableAsset` 自动发现
