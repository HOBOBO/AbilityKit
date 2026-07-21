---
name: moba-demo
description: AbilityKit MOBA Demo（com.abilitykit.demo.moba.*）——5 个包的总览与装配、Bootstrap 11 阶段机、39 个 Actor*Component ECS 状态、view.runtime 的 BattleSessionFeature 37 partial 与双 Sim 模式、Editor 14 个 BattleDebug 面板、Console Demo net10.0 入口、4 套测试体系、Configs/{moba,luban,ability} 三套配置。触发场景：定位 moba 包结构、修改 Bootstrap Stage、添加 Actor*Component、拆 BattleSessionFeature、加 BattleDebug 面板、加 Console Demo CLI 模式、跑 moba 自动测试、配 luban 表、查团队约定 Docs。
---

# moba-demo skill

基于源码核校（2026-07-20）。**只覆盖 demo 装配/阶段机/ECS/视图/同步/网络/Editor/测试/配置**——技能/触发器/BUFF/Passive 业务内容归 [ability-kit](../ability-kit/SKILL.md)。

## 5 个包总览

| 包 | version | 职责 |
|----|---------|------|
| `com.abilitykit.demo.moba.share` | 0.0.1 | 平台无关共享接口/DTO/枚举/Flow 抽象 |
| `com.abilitykit.demo.moba.view.abstractions` | **0.1.0** | view 与 logic 层之间的共享抽象（Hud/View/PresentationCue/插值契约） |
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

- [packages_overview.md](packages_overview.md) — 5 包职责 + 依赖图
- [runtime_architecture.md](runtime_architecture.md) — runtime 包 Application/Domain/Common/Infrastructure/Worlds 分层 + 11 篇团队约定 Docs 导航
- [bootstrap_flow.md](bootstrap_flow.md) — 11 个 Bootstrap Stage + 拓扑排序 + 启动全链路
- [ecs_components.md](ecs_components.md) — 5 Context + 39 Actor*Component 分组清单
- [view_runtime.md](view_runtime.md) — view.runtime 目录树 + BattleSessionFeature 37 partial + 双 Sim 模式
- [app_flow.md](app_flow.md) — Game/App/Flow 状态机 + GamePhase + IBattleSessionFeature
- [editor_toolchain.md](editor_toolchain.md) — 14 BattleDebug 面板 + ConfigSync + SceneGizmos + HotReload
- [console_demo.md](console_demo.md) — src/ Console 项目结构 + Program 4 模式 + Phases/Steps
- [testing.md](testing.md) — 4 套测试体系（xUnit Tests/NetworkCondition + Unity Tests + view.runtime 内联）
- [configs.md](configs.md) — moba/luban/ability 三套配置分工 + 加载链
- [src_dotnet_projects.md](src_dotnet_projects.md) — 7 个 .NET 项目职责+依赖图

## 相关 skill

- 技能/触发器/BUFF 业务 → [ability-kit](../ability-kit/SKILL.md)
- 帧同步预测 → [framesync-prediction-rollback](../framesync-prediction-rollback/SKILL.md)
- 会话协调器 → [coordinator](../coordinator/SKILL.md)
- host.extension（含 MobaHostRuntimeBuilder/MobaRoomOrchestrator）→ [host-extension](../host-extension/SKILL.md)
- Session 重构准则 → [state-handles-controllers](../state-handles-controllers/SKILL.md)
