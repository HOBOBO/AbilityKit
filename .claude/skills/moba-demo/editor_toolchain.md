# Editor 工具链

包：`com.abilitykit.demo.moba.editor`，asmdef `AbilityKit.Demo.Moba.Editor`（仅 Editor 平台，引用 22 个 asmdef 含 `AbilityKit.Combat.Navigation` 和 HotReload）。

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
- **SceneGizmos/**：`ActorBuffGizmoDrawer` / `ActorCombatGizmoDrawer` / `NavigationGizmoDrawer`（**NEW**，导航网格+路径线） / `MobaSceneGizmoSettings` / `MobaGizmoSettingsPersistence` / `SceneGizmoSettingsMenu`
- **CollisionDebug/**：`CollisionWorldGizmoDrawer`
- **Active AOE Gizmo**：`ActiveAoeGizmoDrawer` 从 `MobaAreaRuntimeService` 读取当前逻辑世界的活动区域，绘制实际中心/半径；菜单 `Moba/Gizmos/Active AOE Areas`，上限 `MobaSceneGizmoSettings.MaxActiveAoes`。
- **DebugDraw/**
- **Document/**
- **Tests/**：23 个 Edit-mode 测试（asmdef `AbilityKit.Demo.Moba.Diagnostics.Core.Tests`）

## Navigation Gizmo（NEW 2026-07-29）

`NavigationGizmoDrawer.cs` — `[InitializeOnLoad]` 注册 `IDebugDrawContributor`，在 Scene View 绘制：
- **导航网格**：读 `NavigationDebugState.Grid`，绿框=free cell / 红框=blocked cell（上限 `MaxNavCells=2048`）
- **寻路路径线**：读 `NavigationDebugState.ActivePaths`，蓝线连 waypoints + 目标小球标记

`MobaSceneGizmoSettings` 新增：
- `NavigationBit = 1 << 6` / `PathBit = 1 << 7`
- `MaxNavCells` / `NavFreeColor`(绿) / `NavBlockedColor`(红) / `PathColor`(蓝)
- 菜单 `Moba/Gizmos/Navigation Grid` + `Path Lines` toggle

## 战斗几何可视化开关

`Tools/AbilityKit/Demos/Moba/Gizmos/` 下的开关使用 EditorPrefs 持久化：

- `Collision World`：逻辑碰撞世界中的角色和地图碰撞体。
- `Attack Range`：角色技能选择范围预览。
- `Buff Range`：当前 Actor Buff 表现模板半径。
- `Active AOE Areas`：已经生成、仍在生命周期内的 AOE 实例实际半径。
- `Navigation Grid` / `Path Lines`：导航网格和活动寻路路径。

AOE 绘制只读逻辑服务，不参与模拟 Tick；技能选择范围、Buff 半径和活动 AOE 使用地面圆环，活动 AOE 另有中心十字。碰撞绘制覆盖 Sphere/Capsule/AABB/OBB，单位、投射物和世界层使用不同颜色。所有绘制都受 `MaxActiveAoes`、`MaxActors`、`MaxNavCells` 等上限约束。
