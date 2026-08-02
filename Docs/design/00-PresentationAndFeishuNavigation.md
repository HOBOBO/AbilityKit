# AbilityKit 内训 PPT 与飞书深入阅读导航

> 本文是公司内训 PPT、`Docs/design` 和飞书文档之间的稳定导航层。PPT 负责建立主线和展示关键证据，本文负责把每个讲解阶段路由到可继续阅读的设计文档。MOBA 与 Shooter 都是能力验证示例，不以完整游戏流程作为完成标准。

---

## 1. 讲解与文档的职责分工

| 载体 | 主要职责 | 不承担的职责 |
|------|----------|--------------|
| PPT | 结论、关键架构、真实运行画面、能力边界、采用决策 | 完整 API、全部源码入口、长篇成熟度说明 |
| 短视频 | 用 30 秒到 2 分钟证明关键能力可运行 | 长时稳定性、完整战局、全部异常矩阵 |
| Docs/design | 设计意图、源码入口、生命周期、扩展点、风险与测试证据 | 代替主讲人的叙事节奏 |
| 自动化产物 | 回归结果、性能报告、replay、trace、门禁摘要 | 对外展示的主要视觉 |

PPT 中每个技术阶段至少提供一个“深入阅读”入口。链接优先指向飞书中的设计文档页面，而不是本地源码路径或 Git 工作区路径。

---

## 2. PPT 阶段导航

| PPT 范围 | 讲解任务 | 深入阅读 |
|----------|----------|----------|
| Slide 1-4 | 为什么公司需要统一战斗能力 | [序章：为什么需要 AbilityKit](00-Prologue.md)、[公司级采用与模块治理](10-EngineeringQuality/04-CompanyAdoptionAndModuleGovernance.md) |
| Slide 5-8 | 框架定位、非目标和公司级资产 | [AbilityKit 是什么](01-OverviewAndGettingStarted/01-WhatIsAbilityKit.md)、[项目结构](01-OverviewAndGettingStarted/04-ProjectStructure.md) |
| Slide 9-13 | 四层架构、模块边界和基础协议 | [能力地图](01-OverviewAndGettingStarted/00-AbilityKitCapabilityMap.md)、[核心概念](01-OverviewAndGettingStarted/02-CoreConcepts.md)、[逻辑世界概述](02-LogicalWorldDesign/01-WorldOverview.md) |
| Slide 14-24 | 技能、触发、属性、Buff、Targeting、Projectile、Damage | [玩法能力地图](08-GameplayModules/00-GameplayCapabilityMap.md)、[Pipeline 与 Ability Runtime](08-GameplayModules/08-PipelineAndAbilityRuntime.md)、[触发器系统](08-GameplayModules/02-TriggeringSystem.md) |
| Slide 25-30 | 同步、快照、预测、回放和服务端宿主 | [同步能力地图](07-NetworkSynchronization/00-SynchronizationCapabilityMap.md)、[回滚预测](07-NetworkSynchronization/03-RollbackPrediction.md)、[服务端能力地图](12-ServerArchitecture/00-ServerCapabilityMap.md) |
| Slide 31-35 | MOBA 与 Shooter 示例分别证明什么 | [MOBA 专题总览](09-ImplementationExamples/MOBA/00-Overview.md)、[Shooter 专题总览](09-ImplementationExamples/Shooter/00-Overview.md) |
| Slide 36-41 | 测试、门禁、配置验证和性能纪律 | [正式测试流程](10-EngineeringQuality/01-TestingWorkflow.md)、[示例工业化流程](10-EngineeringQuality/03-MobaShooterIndustrializationFlow.md)、[性能与热路径治理](10-EngineeringQuality/05-CrossModulePerformanceAndHotPathGovernance.md) |
| Slide 42-47 | 项目接入、内部推广和决策 | [公司级采用与模块治理](10-EngineeringQuality/04-CompanyAdoptionAndModuleGovernance.md)、[文档治理路线图](11-DocumentationCompletionPlan.md) |

---

## 3. 核心能力深入阅读

| 主题 | 首选文档 | 扩展文档 |
|------|----------|----------|
| World / DI | [逻辑世界概述](02-LogicalWorldDesign/01-WorldOverview.md) | [服务容器](02-LogicalWorldDesign/05-ServiceContainer.md)、[系统设计](02-LogicalWorldDesign/04-SystemDesign.md) |
| Host / Session | [Host 运行时](03-LogicalWorldHostDesign/01-HostRuntime.md) | [Host 模块系统](03-LogicalWorldHostDesign/02-HostModules.md)、[会话协调](07-NetworkSynchronization/05-SessionCoordination.md) |
| Pipeline / Ability | [Pipeline 与 Ability Runtime](08-GameplayModules/08-PipelineAndAbilityRuntime.md) | [技能系统架构](08-GameplayModules/01-SkillSystemArchitecture.md) |
| Triggering | [触发器系统](08-GameplayModules/02-TriggeringSystem.md) | [MOBA Trigger 深潜](09-ImplementationExamples/MOBA/10-TriggerValidationPresentationDeepDive.md) |
| Buff / Continuous | [Buff 系统](08-GameplayModules/03-BuffSystem.md) | [Continuous 框架](08-GameplayModules/11-ContinuousFrameworkDesign.md)、[MOBA 持续行为组合](09-ImplementationExamples/MOBA/13-ContinuousCapabilityCompositionDesign.md) |
| Targeting | [Targeting 系统](08-GameplayModules/07-TargetingSystem.md) | [实体与技能索引](08-GameplayModules/09-EntityAndSkillIndexing.md) |
| Projectile / Damage | [投射物系统](08-GameplayModules/04-ProjectileSystem.md) | [伤害计算](08-GameplayModules/06-DamageCalculation.md)、[MOBA Projectile 与 Damage 深潜](09-ImplementationExamples/MOBA/08-ProjectileDamageDeepDive.md) |
| Snapshot / StateSync | [状态同步](07-NetworkSynchronization/02-StateSync.md) | [快照分发](04-PresentationLayerDesign/02-SnapshotDispatch.md) |
| Prediction / Replay | [回滚预测](07-NetworkSynchronization/03-RollbackPrediction.md) | [预测重整](07-NetworkSynchronization/03.1-PredictionReconciliationDesign.md)、[回放系统](07-NetworkSynchronization/04-ReplaySystem.md) |
| Orleans | [Orleans 运行时与部署](12-ServerArchitecture/01-OrleansRuntimeAndDeployment.md) | [Gateway、Room 与 Battle](12-ServerArchitecture/02-GatewayRoomBattleFlow.md) |

---

## 4. 两个示例的展示契约

### 4.1 MOBA：复杂技能机制展厅

MOBA 的主视频不需要完整战局。建议在 60 到 120 秒内连续展示三到四种有明显差异的技能机制，例如：

1. 主动技能经过 Pipeline 阶段执行，并产生可追踪的 Effect lineage；
2. Projectile 命中后应用 Buff 或触发二段效果；
3. owner-bound 被动、AOE 或持续行为响应事件；
4. 基础快照或表现事件把逻辑结果同步到 View。

首选深入阅读：[MOBA 主动、被动、Buff、Projectile 与 AOE](09-ImplementationExamples/MOBA/17-ActivePassiveBuffProjectileAoeTriggerEffects.md)、[技能 Flow 与 Pipeline 配置](09-ImplementationExamples/MOBA/18-SkillFlowPipelineConfigDesign.md)、[Trace、Context 与 Effect](09-ImplementationExamples/MOBA/09-TraceContextEffectDeepDive.md)。

### 4.2 Shooter：大量单位与同步方式实验场

Shooter 的主视频同样不需要完整战局。建议在 30 到 90 秒内完成密度和同步模式切换：

1. 展示 512、2048、8192 三档实体预算，其中 2K 作为 Unity 表现重点；
2. 展示批处理或 GPU 实例绘制前后的实体管理结果；
3. 切换权威插值、预测回滚、纯状态同步等代表性模式；
4. 在 HUD 或报告中展示实体数、快照预算、丢帧、状态 hash 或重整结果。

首选深入阅读：[Shooter Svelto 性能模式](09-ImplementationExamples/Shooter/09-SveltoPerformanceModeDeepDive.md)、[纯状态预算与兴趣范围](09-ImplementationExamples/Shooter/06-PureStateBudgetAndInterest.md)、[客户端同步策略](09-ImplementationExamples/Shooter/04-ClientSyncStrategies.md)、[表现 Session 与 View Pipeline](09-ImplementationExamples/Shooter/10-PresentationSessionAndViewDeepDive.md)。

长时 soak、完整战局 acceptance 和多进程 smoke 属于验证资产，可以在深入页或答疑中引用，但不应占据示例主视频。

---

## 5. PPT 图片选择规范

| 页面类型 | 推荐主视觉 | 不推荐 |
|----------|------------|--------|
| 定位与开场 | 实际 Unity 运行画面、代码与文档资产的克制组合 | 用复杂流程图充当标题页 |
| 架构与协议 | 一张简化结构图或调用链 | 同页放两到三张高密度 Mermaid |
| MOBA 示例 | 技能命中、Projectile、Buff、Trace 的真实截图或短视频封面 | 用完整战局流程图代替能力证据 |
| Shooter 示例 | 512/2K/8K 密度对比、同步模式 HUD、GPU 实例画面 | 只展示测试矩阵而没有大量单位画面 |
| 测试与工程化 | 门禁摘要、replay/trace 产物和一张分层图 | 终端长日志截图 |
| 接入与决策 | 简洁的模块选择或阶段路线 | 大段源码路径和成熟度备注 |

技术图仍保留为附录和飞书深入文档资产。主讲 PPT 应优先使用“真实画面 + 一句结论 + 深入阅读链接”的组合。

---

## 6. 飞书发布与 PPT 链接规则

1. `Docs/design` 是唯一正式文档源，`local/Docs` 只保存讲稿和本地素材。
2. 先执行飞书同步预览并核对目录，再完成正式上传。
3. PPT 不直接保存本地 Markdown 相对路径；正式链接从 `feishu-sync-state.local.json` 中对应源文件的 `feishuDocumentUrl` 获取。
4. 当前同步采用版本化重导入时 URL 可能变化，因此文档更新后需要重新生成或校验 PPT 链接。
5. 每个 PPT 阶段至少链接一篇入口文档；关键技术页可直接链接对应专题，不要求每页都堆放多个链接。
6. 飞书页面中的跨文档相对链接不能视为已自动改写，正式发布前应以本导航页和同步状态映射做一次点击验收。

建议把本文作为 PPT 的统一“深入阅读”入口，再为 MOBA、Shooter、同步、玩法模块和工程质量几个高频主题增加直接链接。
