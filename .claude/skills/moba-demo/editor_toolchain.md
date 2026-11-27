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

## Moba 配置 SO + 工具（`Editor/Moba/`）

### ScriptableObject 配置类

`CharacterSO` / `SkillSO` / `SkillFlowSO` / `SkillLevelTableSO` / `BuffSO` / `PassiveSkillSO` / `ProjectileSO` / `ProjectileLauncherSO` / `SummonSO` / `VfxSO` / `ModelSO` / `AttrTypeSO` / `BattleAttributeTemplateSO` / `AttributeTemplateSO` / `TagTemplateSO` / `SearchQueryTemplateSO` / `SkillButtonTemplateSO` / `SpawnSummonActionTemplateSO` / `ComponentTemplateSO` / `SkillFlowDefs` 等。

### 工具

- `ConfigValidator` — 配置校验
- `MobaConfigJsonExporter` — SO → JSON 导出
- `MobaConfigJsonFolderSync`（`Editor/Moba/`）— SO 与 JSON 双向同步
- `MobaPresentationTemplateInspector` — 从 `Tools/AbilityKit/Demos/Moba/Vfx/Inspect Published Templates` 只读检查已发布模板和运行时实例。特效配置维护与发布见 [Luban MOBA 配置入口](../../../LubanConfig/Moba/README.md)，不新增 Unity 导出菜单。
- HUD Prefab 结构生成和离线审计分开管理：前者从 MOBA 的 `HUD` 菜单预检后确认；Lux 表现与角色动画时序审计用无菜单批处理，运行时显式指定 `local/Logs/` 下的独立采样目录。用法、兼容行为与写入边界见[包内说明](../../../Unity/Packages/com.abilitykit.demo.moba.editor/Document/README.md#表现资源维护与离线复核)。
- Karthus、Lux、Gragas 的资源 `Inspect` 仅作历史复现，不纳入日常发布或默认门禁；报告路径见[历史英雄资源检查](../../../Unity/Packages/com.abilitykit.demo.moba.editor/Document/README.md#历史英雄资源检查)。复现旧迁移时显式传入报告文件，不回读固定旧路径。
- `MobaStateMachineAuthoringWindow` — 项目正式工作台，从 `Window/AbilityKit/MOBA 状态机编辑器` 保存、只读校验和预检后发布；不另建直写菜单。批处理保留 `MobaStateMachinePublisher.Publish` / `Check`；创作和发布细节见 [MOBA 状态机创作与发布](../../../Docs/MobaStateMachineAuthoring.md)。
- `MobaCharacterAnimationSyncWindow` — 正式动画绑定工作台，集中扫描、漂移检查和同步。保留状态机编辑器带角色打开的入口；不另设漂移检查菜单。完整 Luban 发布须确认，自动化继续调用原方法；见 [正式角色动画同步](../../../Docs/MobaStateMachineAuthoring.md#正式角色动画同步)。
- `SkillFlowSOEditor` — 只有生产 `SkillFlowCO.asset` 的 Inspector 显示全量校验与预检发布，不再新增单英雄同步或直接发布菜单。批处理沿用 `MobaSkillPipelineExporter.Check` / `Apply`；操作和报告位置见 [MOBA 技能管线发布](../../../Docs/MobaSkillPipeline.md#编辑器入口与写入范围)。
- Annie、Jinx、Veigar 的专属菜单已退役；旧同步方法只作为历史迁移兼容和内部资源派生支撑保留。日常修改使用各权威源和对应发布器，按 [MOBA 技能管线发布顺序](../../../Docs/MobaSkillPipeline.md#发布顺序) 操作，不把旧方法重新登记成菜单或门禁。
- `MobaConfigTableRegistry` / `IMobaConfigTableAsset` / `MobaConfigTableAssetSO`(+custom editor) — Luban 表 SO 包装
- `Hero/` — 英雄相关 SO

## 其它 Editor 工具

- **FrameSync/**：`FrameSyncTestWindow`（战斗调试 → 数据与工具 → 同步诊断 → 帧同步测试；启动和连接仍是窗口内的显式操作）
- **HotReload/**：`HotReloadMenu` / `UnityHotfixLogger`
- **Preview/**：`EditorGameFlowPumpWindow`（战斗调试 → 数据与工具 → 预览 → 编辑器驱动；打开不启动）／`MobaDemoSceneMenu`（`Open Demo Scene` / `Play Existing Demo Scene` 只使用既有场景，不生成资产）。跨 MOBA、Shooter 的装配写入只从 `Tools/AbilityKit/Demos/Build Package-Owned Gameplay Composition` 预检确认；不在项目菜单重复暴露，不从预览或播放触发。
- **示例维护**：战斗调试 → 数据与工具 → 示例维护。P1/P2 先用 `MobaShowcaseMaintenance` 查看输入和写入范围，再显式确认；保留原 `SyncBatch`，不另建直写菜单，不与生产发布混用。
- **SceneGizmos/**：`ActorBuffGizmoDrawer` / `ActorCombatGizmoDrawer` / `NavigationGizmoDrawer` / `MobaSceneGizmoSettings` / `MobaGizmoSettingsPersistence`；设置入口由 `BattleDebugSceneOverlayPanel` 提供。
- **CollisionDebug/**：`CollisionWorldGizmoDrawer`
- **Active AOE Gizmo**：`ActiveAoeGizmoDrawer` 从 `MobaAreaRuntimeService` 读取当前逻辑世界的活动区域，绘制实际中心/半径；上限 `MobaSceneGizmoSettings.MaxActiveAoes`。
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
- 分别由「场景叠加」页的「导航网格」和「寻路路径」开关控制。

## 战斗几何可视化开关

统一入口为 `Tools/AbilityKit/Demos/Moba/Battle/战斗调试` →「场景叠加」。开关沿用已有 `EditorPrefs` 设置；编辑模式可调整，收起面板不会关闭任何叠加层。后续新增 MOBA 场景叠加开关集中在此，不新增逐项菜单。

- 「碰撞世界」：逻辑碰撞世界中的角色和地图碰撞体。
- 「攻击范围」：角色技能选择范围预览。
- 「Buff 范围」：当前 Actor Buff 表现模板半径。
- 「出生区域」：围绕缓存的出生位置绘制区域。
- 「活动 AOE 区域」：已经生成、仍在生命周期内的 AOE 实例实际半径。
- 「导航网格」／「寻路路径」：导航网格和活动寻路路径。

AOE 绘制只读逻辑服务，不参与模拟 Tick；技能选择范围、Buff 半径和活动 AOE 使用地面圆环，活动 AOE 另有中心十字。碰撞绘制覆盖 Sphere/Capsule/AABB/OBB，单位、投射物和世界层使用不同颜色。所有绘制都受 `MaxActiveAoes`、`MaxActors`、`MaxNavCells` 等上限约束。
