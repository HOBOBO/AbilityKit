---
name: moba-demo
description: AbilityKit MOBA Demo（com.abilitykit.demo.moba.*）的包结构、逻辑与表现装配、Bootstrap、Entitas ECS、配置加载、DTO 到 MO 转换、MOBA Source Generator/Analyzer、测试门禁、导航碰撞和多人同步指南。用于定位或修改 MOBA runtime/view/host/editor/codegen 包，新增配置表、注册 PlanAction/事件/目标查询/发射器/快照/路由，处理生成代码或 Analyzer 诊断，以及运行 MOBA 验证门禁。
---

# moba-demo skill

基于源码核校（2026-08-04）。**覆盖 demo 装配/阶段机/ECS/视图/同步/网络/Editor/测试/配置/CodeGen，以及导航/寻路/碰撞墙体/PathFollowing/map 运行时/AI BT**。技能/触发器/BUFF/Passive 业务内容归 [ability-kit](../ability-kit/SKILL.md)。碰撞/移动/导航基础设施（`com.abilitykit.combat.{collision,motion,navigation}` 包）归 ability-kit 的 [combat_* 子目录](../ability-kit/SKILL.md)。

## 7 个包总览

| 包 | version | 职责 |
|----|---------|------|
| `com.abilitykit.demo.moba.share` | 0.0.1 | 平台无关共享接口/DTO/枚举/Flow 抽象 |
| `com.abilitykit.demo.moba.codegen` | 0.0.1 | **MOBA 专用 Roslyn Source Generator 与 Analyzer**；生成 runtime Manifest 并约束声明形状 |
| `com.abilitykit.demo.moba.view.abstractions` | **0.1.0** | view 与 logic 层之间的共享抽象（Hud/View/PresentationCue/插值契约） |
| `com.abilitykit.demo.moba.host` | **0.1.0** 🆕 | **Moba host adapter**（BattleLaunchSpec/RoomOrchestrator/HostRuntimeBuilder），从 host.extension 提取 |
| `com.abilitykit.demo.moba.runtime` | 0.0.1 | **逻辑运行时**（装配/Domain/Common/Infrastructure/Worlds/Bootstrap） |
| `com.abilitykit.demo.moba.view.runtime` | 0.0.1 | **表现/会话运行时**（`BattleSessionFeature`、View、Net、Sim） |
| `com.abilitykit.demo.moba.editor` | 0.0.1 | Editor 工具链（14 BattleDebug 面板、ConfigSync、SceneGizmos、HotReload） |

**不存在** `com.abilitykit.demo.moba.view.editor`。

## 启动链路（全链路）

```
Host/Session → WorldTypeRegistry → MobaWorldBlueprintsRegistration
    → MobaLogicWorldBlueprintBase → WorldCreateOptions
    → EntitasWorld → MobaWorldBootstrapModule
        ├─ Configure: MobaBootstrapFlow.Configure + World DI Modules + MobaServicesAutoModule
        └─ Install: AutoSystemInstaller + MobaBootstrapFlow.Install
    → Systems execute by MobaSystemOrder
```

## 关键边界（不重复 ability-kit skill）

本 skill **不覆盖**：
- `Application/Services/{Skill,Buffs,Triggering,Passive,Effect,Combat,Continuous}` 内部
- `Application/Systems/{Skill,Buffs,Triggering,Passive,Effects}` 内部
- `Domain/Ability/Impl/Moba/EffectSourceCompat`（已空）
- `Configs/ability/` 触发器规则

这些归 ability-kit skill。本 skill 聚焦**装配/编排/视图/工具链/测试/配置加载链**。

## Sections

- [packages_overview.md](packages_overview.md) — 7 包职责 + 编译期/运行时依赖图
- [codegen_analyzer.md](codegen_analyzer.md) — MOBA Generator/Analyzer 覆盖矩阵、DTO→MO 规则、fallback 边界与 P1 门禁
- [runtime_architecture.md](runtime_architecture.md) — runtime 包 Application/Domain/Common/Infrastructure/Worlds 分层 + 11 篇团队约定 Docs 导航
- [bootstrap_flow.md](bootstrap_flow.md) — 12 个 Bootstrap Stage + 拓扑排序 + 启动全链路（含 MapRuntime → nav.Build）
- [ecs_components.md](ecs_components.md) — 5 Context + 39 Actor*Component 分组清单 + Motion 初始化生命周期
- [view_runtime.md](view_runtime.md) — view.runtime 目录树 + BattleSessionFeature 37 partial + 双 Sim 模式
- [app_flow.md](app_flow.md) — Game/App/Flow 状态机 + GamePhase + IBattleSessionFeature
- [editor_toolchain.md](editor_toolchain.md) — 14 BattleDebug 面板 + ConfigSync + SceneGizmos + NavigationGizmoDrawer + HotReload
- [console_demo.md](console_demo.md) — src/ Console 项目结构 + Program 4 模式 + Phases/Steps
- [testing.md](testing.md) — .NET/Unity 测试体系与 MOBA 验证入口（含白盒验收 dotnet 判定层 + LiveSim 系列，2026-08-14）
- [configs.md](configs.md) — moba/luban/ability/battle_maps 四套配置分工 + 加载链
- [src_dotnet_projects.md](src_dotnet_projects.md) — MOBA 相关 .NET 项目职责与依赖图
- [navigation.md](navigation.md) — **NEW** 导航运行时：`com.abilitykit.combat.navigation` 纯包 + demo 烘焙 + Debug 状态
- [path_following.md](path_following.md) — **NEW** 寻路跟随系统：MobaPathFollowingSystem 读脑→查路径→Path 源
- [collision_and_walls.md](collision_and_walls.md) — **NEW** 碰撞世界/移动碰撞/墙体系统：sync/adapter/墙滑/per-skill 策略
- [map_runtime.md](map_runtime.md) — **NEW** 地图运行时服务：MobaMapRuntimeService + battle_maps.json
- [ai_bt.md](ai_bt.md) — **NEW** AI 行为树修复：DefaultApproachRange 0.5 + CreateSummon WithMoveInput
- [multiplayer_sync.md](multiplayer_sync.md) — 🆕 **v0.1.0** 多人同步完整度：BattleWorldWithFrameSync / CatchUp / Recording / Metrics / Spectator / BotAI / TickRate

## 相关 skill

- 技能/触发器/BUFF 业务 → [ability-kit](../ability-kit/SKILL.md)
- 帧同步预测 → [framesync-prediction-rollback](../framesync-prediction-rollback/SKILL.md)
- 会话协调器 → [coordinator](../coordinator/SKILL.md)
- host.extension（含 MobaHostRuntimeBuilder/MobaRoomOrchestrator）→ [host-extension](../host-extension/SKILL.md)
- Session 重构准则 → [state-handles-controllers](../state-handles-controllers/SKILL.md)
