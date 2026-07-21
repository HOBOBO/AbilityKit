# Editor 工具链

包：`com.abilitykit.demo.moba.editor`，asmdef `AbilityKit.Demo.Moba.Editor`（仅 Editor 平台，引用 21 个 asmdef 含 HotReload）。

## BattleDebug 14 个面板（`Editor/BattleDebug/Panels/`）

**帧同步面板（6 个）**：

- `BattleDebugFrameSyncPanel`（总览）
- `BattleDebugFrameSyncNetworkPanel`（jitter buffer）
- `BattleDebugFrameSyncPredictionPanel`（Order 51，预测）
- `BattleDebugFrameSyncReconcilePanel`（Order 53，对账，含按钮调 `IClientPredictionReconcileControl`）
- `BattleDebugFrameSyncRollbackPanel`（Order 52，回滚）
- `BattleDebugFrameSyncTimePanel`（Order 54，时间同步）

**其它面板（8 个）**：

- `BattleDebugOverviewPanel`（总览）
- `BattleDebugAttributesPanel`（属性）
- `BattleDebugBuffsPanel`（Buff）
- `BattleDebugEffectsPanel`（效果）
- `BattleDebugTagsPanel`（标签）
- `BattleDebugDiagnosticEventsPanel`（诊断事件）
- `BattleDebugDiagnosticStatePanel`（诊断状态）
- `BattleDebugDiagnosticTracePanel`（诊断追踪）

## BattleDebug 框架（非面板）

- `BattleDebugWindow`（主窗口）
- `BattleDebugContext` / `BattleDebugEntityFilter`
- `BattleDebugPanelRegistry` / `BattleDebugToolbarCommandRegistry`
- `IBattleDebugPanel` / `IBattleDebugToolbarCommand`
- 子目录：`Diagnostics/` / `Filtering/` / `Toolbar/` / `ViewModels/`

## Moba 配置 SO + 工具（`Editor/Moba/`，25 个）

### ScriptableObject 配置类

`CharacterSO` / `SkillSO` / `SkillFlowSO` / `SkillLevelTableSO` / `BuffSO` / `PassiveSkillSO` / `ProjectileSO` / `ProjectileLauncherSO` / `SummonSO` / `VfxSO` / `ModelSO` / `AttrTypeSO` / `BattleAttributeTemplateSO` / `AttributeTemplateSO` / `TagTemplateSO` / `SearchQueryTemplateSO` / `SkillButtonTemplateSO` / `SpawnSummonActionTemplateSO` / `ComponentTemplateSO` / `SkillFlowDefs` 等。

### 工具

- `ConfigValidator` — 配置校验
- `MobaConfigJsonExporter` — SO → JSON 导出
- `MobaConfigJsonFolderSync`（`Editor/Moba/`）— SO 与 JSON 双向同步
- `VfxJsonExporter` — VFX 配置导出
- `MobaConfigTableRegistry` / `IMobaConfigTableAsset` / `MobaConfigTableAssetSO`(+custom editor) — Luban 表 SO 包装
- `Hero/` — 英雄相关 SO

## 其它 Editor 工具

- **FrameSync/**：`FrameSyncTestWindow`（帧同步测试窗口）
- **HotReload/**：`HotReloadMenu` / `UnityHotfixLogger`
- **Preview/**：`EditorGameFlowPumpWindow` / `MobaDemoSceneMenu`
- **SceneGizmos/**：`ActorBuffGizmoDrawer` / `ActorCombatGizmoDrawer` / `MobaSceneGizmoSettings` / `MobaGizmoSettingsPersistence` / `SceneGizmoSettingsMenu`
- **CollisionDebug/**：`CollisionWorldGizmoDrawer`
- **DebugDraw/**
- **Document/**
- **Tests/**：23 个 Edit-mode 测试（asmdef `AbilityKit.Demo.Moba.Diagnostics.Core.Tests`）
