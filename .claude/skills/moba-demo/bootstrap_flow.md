# Bootstrap 阶段机

入口：`MobaWorldBootstrapModule`（`IWorldModule` + `IEntitasSystemsInstaller`）→ `MobaBootstrapFlow` → `MobaBootstrapStageRegistry.GetSortedStages()`（**已实现完整拓扑排序**）。

注意：`BootstrapFlowGuide.md` 第 161 行声称 "Dependencies 属性存在但未被使用，Stage 按注册顺序执行"——**此文档已过时**。实际 `MobaBootstrapStageBase.cs` 第 141-202 行的 `GetSortedStages`/`Visit` 已实现拓扑排序，循环依赖会被检测并 Warning。

## 11 个 Stage（位于 `Application/Systems/Bootstrap/Flow/Stages/`）

标 `[MobaBootstrapStage]` 由反射自动注册：

| 顺序 | Stage 类 | Name 常量 | 阶段 | 依赖 | 职责 |
|------|---------|----------|------|------|------|
| 1 | `CoreStateStage` | `CoreState` | Configure | — | 默认 World 服务、清理属性注册缓存、确定性随机数 |
| 2 | `ConfigStage` | `Config` | Configure | CoreState | 配置表、DTO 反序列化、`MobaConfigDatabase` |
| 3 | `WorldModulesStage` | `WorldModules` | Configure | Config | 事件总线、触发器 Registry、Entitas 模块、`MobaServicesAutoModule` |
| — | `TagsStage` | `"Tags"` | Configure | — | GameplayTags |
| — | `TemplateFeatureStage` | `"TemplateFeature"` | Configure+Install | — | 组件模板特性 |
| 4 | `TriggerPlansStage` | `TriggerPlans` | Configure | WorldModules | 触发计划加载与索引 |
| 4 | `TargetingAndSkillsStage` | `TargetingAndSkills` | Configure | WorldModules | 事件订阅、触发器索引、技能条件 Registry |
| 5 | `WorldInitStage` | `Install.WorldInit` | Install | TargetingAndSkills, TriggerPlans | MemoryPack 序列化、`WorldInitData`、`RollbackWorldRandom`、`MobaGameStartSpec`、切 InGame |
| 6 | `PlanTriggeringStage` | `Install.PlanTriggering` | Configure+Install | TriggerPlans, WorldInit | `TriggerPlanJsonDatabase`、计划触发系统 |
| 7 | `StartGameStage` | `StartGame` | Install | WorldInit | 启动末段初始化 |

## 拓扑

- Configure 链：`CoreState → Config → WorldModules → {TriggerPlans, TargetingAndSkills}`
- Install 链：`{TargetingAndSkills, TriggerPlans} → WorldInit → {PlanTriggering, StartGame}`

## 辅助类型（Bootstrap/Flow 根）

`MobaBootstrapFlow`（`InitOpCode=2000`）、`MobaBootstrapStageBase`、`MobaBootstrapStageRegistry`、`MobaBootstrapStageInitializer`（反射自动注册）、`MobaBootstrapStageAttribute`、`MobaBootstrapFlowModule`（静态 ctor 触发注册）、`IPlanActionModule`、`MobaServicesAutoModule`、`PlanTextLoaderAdapter`、`UnityResourcesTextLoader`、`TriggerScope`、`TriggeringConstants`。

## 写新 Stage 的步骤

1. 新建类继承 `MobaBootstrapStageBase`
2. 标 `[MobaBootstrapStage(name)]`
3. override `Dependencies` / `Phase`（如需要）
4. override `ConfigureAsync` / `InstallAsync`（如需要）
5. 注册到 `MobaBootstrapStageNames` 常量
