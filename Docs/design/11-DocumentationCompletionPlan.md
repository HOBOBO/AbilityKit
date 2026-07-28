# 11.1 文档治理路线图：源码覆盖、设计意图与流程图规范

> 本文定义 `Docs/design` 的长期治理规则。目标是让设计文档持续对应真实源码、运行流程和验收入口，并为专题扩展、结构调整、流程图维护和版本记录提供统一标准。

---

## 1. 总目标

`Docs/design` 不只追求“有文档”，而是要求每篇设计文档都能回答读者最关心的五个问题：

| 问题 | 文档必须给出的答案 |
|------|--------------------|
| 这个模块解决什么问题 | 描述业务痛点、工程痛点和框架边界 |
| 为什么这样设计 | 说明核心抽象、生命周期、依赖方向和取舍 |
| 源码从哪里进入 | 列出 Unity package、.NET project、Demo、测试或 Server 入口 |
| 运行时怎么流动 | 至少提供结构图、时序图、状态图或数据流图之一 |
| 理解如何被验证 | 给出源码阅读路径、运行命令、测试入口或调试观察点 |

---

## 2. 文档完成标准

每个模块完成时，至少满足以下标准：

| 标准 | 要求 |
|------|------|
| 源码驱动 | 以真实类、接口、方法和目录为依据，不保留过期伪 API |
| 设计意图 | 解释模块要解决的问题、为什么拆这些对象、为什么保持这些约束 |
| 生命周期 | 说明创建、注册、运行、销毁、回收、取消或清理流程 |
| 流程图 | 至少提供覆盖结构、关键运行流程和生命周期的图；简单模块可用表格替代低价值图 |
| 源码路径 | 标明核心源码、相关 Demo、验证测试或运行命令 |
| 可执行入口 | 至少提供最小接入示例、模式决策表、验证命令或可复制模板之一，并写清所有权与失败路径 |
| 风险边界 | 写清线程、确定性、GC、跨端、回滚、热更新或服务端边界 |
| 证据状态 | 区分源码事实、示例验证、测试覆盖、场景基线、批准预算和强制门禁 |
| 索引接入 | 更新 `00-index.md` 的文档定位、说明和版本记录 |
| 讲解接入 | 能从 `00-PresentationAndFeishuNavigation.md` 路由到高频主题；PPT 只引用飞书页面 URL，不依赖本地路径 |

---

## 3. 总体治理顺序与覆盖矩阵

```mermaid
flowchart TD
    A[概览与源码地图] --> B[Core 与 World DI]
    B --> C[World 与 Host]
    C --> D[ECS 与系统调度]
    D --> E[同步、快照、回放]
    E --> F[Triggering、Pipeline、Ability]
    F --> G[Combat 领域模块]
    G --> H[配置、代码生成、Luban]
    H --> I[表现层与 Demo]
    I --> J[Orleans 服务端]
    J --> K[工程质量、测试、验收]

    A -. 基础覆盖 .-> E
    E -. 同步覆盖 .-> I
    I -. 示例覆盖 .-> K
    K -. 验收覆盖 .-> A
```

这组治理顺序遵循依赖方向：底座文档定义术语和生命周期，运行时文档解释装配与 Tick，玩法文档解释可组合能力，示例与服务端文档验证跨端落地。新文档可以引用前置概念，避免每篇都重复解释基础设施。

文档治理按覆盖范围记录，不以对话阶段或临时任务状态作为长期索引：

| 覆盖域 | 代表文档 | 源码验证重点 |
|------|------------|--------------|
| 入门与结构 | `01-OverviewAndGettingStarted/00-AbilityKitCapabilityMap.md`、`03-QuickStart.md`、`04-ProjectStructure.md` | Unity package、`src` 工程、Server/Orleans、Console Demo、构建与测试入口 |
| 网络同步 | `07-NetworkSynchronization/00-SynchronizationCapabilityMap.md`、`04-ReplaySystem.md`、`05-SessionCoordination.md` | `FramePacket`、`RecordSession`、`RemoteFrameAggregator`、`SessionCoordinator`、Local/Remote/Hybrid 成熟度、Gateway/Room/Battle Grain |
| 玩法运行时 | `08-GameplayModules/07-TargetingSystem.md`、`08-PipelineAndAbilityRuntime.md` | Targeting 稳定排序与查询所有权、Pipeline run 隔离与终止语义、Ability/MOBA 特化边界 |
| 配置与数据工具 | `05-CommonModules/04-ConfigurationSystem.md`、`07-CodeGenAndLubanProductionPipeline.md`、`08-ActionTimelineDataAndPlayback.md`、`09-ExcelScriptableObjectSync.md` | 运行时配置、生成发布、时间线协议和 Editor 双向同步的职责隔离、失败传播与验证门禁 |
| 示例体系 | `09-ImplementationExamples/02-ET Demo Analysis.md`、`03-MOBA Demo Analysis.md`、`04-Shooter Demo 与 Orleans Smoke.md` | ET Scene/Component/System、MOBA Blueprint/Bootstrap、Shooter Svelto runtime、Orleans smoke |
| 质量、采用与性能 | `10-EngineeringQuality/01-TestingWorkflow.md`、`04-CompanyAdoptionAndModuleGovernance.md`、`05-CrossModulePerformanceAndHotPathGovernance.md`、本文 | 测试门禁、成熟度证据、owner/rollback、benchmark artifact、预算与门禁晋升 |
| 内训与发布 | `00-PresentationAndFeishuNavigation.md`、`FeishuOfflineExportGuide.md` | PPT 阶段路由、示例展示定位、图片选择、飞书 URL 映射与点击验收 |

```mermaid
flowchart LR
    Foundation[入门结构] --> Sync[同步回放会话]
    Sync --> Examples[示例总览]
    Examples --> Quality[质量与测试]
    Quality --> Check[格式与图表检查]
    Check --> DeepDive[专题深潜]
```

---

## 4. 模块批次计划

### 4.1 Batch 0：流程图健康检查与文档规范

| 项目 | 内容 |
|------|------|
| 目标 | 修复明显显示异常的流程图和旧 ASCII 图，建立统一写法 |
| 源码目录 | 不读源码，优先扫描 `Docs/design` |
| 目标文档 | `00-index.md`、本文、已有旧文档中的 Mermaid 和 ASCII 图 |
| 必画图 | 文档补全总路线图、流程图修复流程图、文档验收流程图 |
| 重点问题 | Mermaid 是否含未转义泛型、裸反斜杠换行、过宽节点、ASCII 箱线图 |
| 验收方式 | `git diff --check`，抽样阅读 Mermaid 语法，确认链接入口存在 |

```mermaid
flowchart LR
    Scan[扫描 Markdown] --> Risk[标记风险图]
    Risk --> Fix[修复 Mermaid 或替换 ASCII]
    Fix --> Review[人工阅读语法]
    Review --> Check[格式检查]
```

### 4.2 Batch 1：概览、项目结构与源码入口

| 项目 | 内容 |
|------|------|
| 目标 | 说明 AbilityKit 是什么、源码在哪里、怎么运行、源码入口在哪里 |
| 源码目录 | `Unity/Packages`、`src`、`Server/Orleans`、`LubanConfig` |
| 目标文档 | `01-OverviewAndGettingStarted`、`00-Prologue.md`、`00-index.md` |
| 必画图 | 仓库结构图、包依赖图、Demo 启动路径图、源码路径图 |
| 设计意图 | 为什么源码集中在 Unity package，为什么保留 .NET project，为什么 Demo 和 Server 分层 |
| 验收方式 | 文档能支撑 build、Console Demo、至少一个测试入口的独立验证 |

### 4.3 Batch 2：Core 与通用基础设施

| 项目 | 内容 |
|------|------|
| 目标 | 覆盖事件、对象池、日志、标识、数值、配置基础能力 |
| 源码目录 | `Unity/Packages/com.abilitykit.core/Runtime` |
| 目标文档 | `05-CommonModules/01-EventSystem.md`、`02-ObjectPool.md`、`04-ConfigurationSystem.md`、Core 数值专题 |
| 必画图 | 事件派发时序图、对象池生命周期图、配置 provider 仲裁图、标识映射图 |
| 设计意图 | 降低 GC、统一事件解耦、稳定字符串 ID、支持配置覆盖和调试统计 |
| 验收方式 | 文档中的 API 与源码签名一致，移除 `Rent/Return`、`ITimerHandle` 等过期伪接口 |

### 4.4 Batch 3：World DI 与逻辑世界

| 项目 | 内容 |
|------|------|
| 目标 | 解释世界级依赖注入、世界生命周期、服务作用域和系统协作 |
| 源码目录 | `Unity/Packages/com.abilitykit.world.di/Runtime`、`Unity/Packages/com.abilitykit.world.ecs/Runtime` |
| 目标文档 | `02-LogicalWorldDesign/01-WorldOverview.md` 到 `05-ServiceContainer.md` |
| 必画图 | World 创建流程、Container/Scope 解析链路、System Tick 顺序、服务生命周期状态图 |
| 设计意图 | 为什么按 World 隔离服务，为什么需要 Scoped，如何避免跨战斗污染 |
| 验收方式 | 能从文档追到 `WorldContainer`、`WorldScope`、`WorldClock` 和系统注册入口 |

### 4.5 Batch 4：Host 运行时与扩展模块

| 项目 | 内容 |
|------|------|
| 目标 | 解释 HostRuntime 如何管理多世界、连接、广播、Tick 和扩展模块 |
| 源码目录 | `Unity/Packages/com.abilitykit.host/Runtime`、`Unity/Packages/com.abilitykit.host.extension/Runtime` |
| 目标文档 | `03-LogicalWorldHostDesign/01-HostRuntime.md` 到 `03-WorldManager.md` |
| 必画图 | HostRuntime Tick 时序图、World 创建销毁图、连接广播图、Blueprint/Module 装配图 |
| 设计意图 | 为什么 Host 不直接承载具体玩法，为什么通过 Hook/Feature/Module 扩展 |
| 验收方式 | 文档能解释 Demo 和 Orleans 如何复用同一 Host 抽象 |

### 4.6 Batch 5：ECS 适配与查询模型

| 项目 | 内容 |
|------|------|
| 目标 | 解释 AbilityKit 自有 ECS、Entitas、Svelto 三种模型的定位和取舍 |
| 源码目录 | `Unity/Packages/com.abilitykit.world.ecs/Runtime`、`com.abilitykit.world.entitas`、`com.abilitykit.world.svelto` |
| 目标文档 | `06-ECSArchitecture` 全目录 |
| 必画图 | EntityWorld 存储结构图、QueryImpl 流程图、Entitas 生命周期图、Svelto 提交流程图 |
| 设计意图 | 为什么保留多 ECS 适配，何时用轻量世界，何时接 Entitas 或 Svelto |
| 验收方式 | 文档能支撑查询模型选择，并解释 snapshot 遍历和版本校验 |

### 4.7 Batch 6：同步、快照、回滚与回放

| 项目 | 内容 |
|------|------|
| 目标 | 把多人同步链路拆成输入帧、快照、状态同步、预测回滚、录制回放 |
| 源码目录 | `com.abilitykit.world.framesync`、`com.abilitykit.world.snapshot`、`com.abilitykit.world.statesync`、`com.abilitykit.record`、`com.abilitykit.protocol` |
| 目标文档 | `07-NetworkSynchronization` 全目录 |
| 必画图 | 输入帧聚合图、快照封包图、预测回滚状态图、回放轨道图、重连恢复图 |
| 设计意图 | 为什么把输入、状态、表现分开，如何兼顾确定性、弱网、重连和验收 |
| 验收方式 | 能追踪一次客户端输入从采集到服务端推进再到表现层应用的完整链路 |

### 4.8 Batch 7：Triggering、Pipeline 与 Ability 核心玩法

| 项目 | 内容 |
|------|------|
| 目标 | 解释事件触发、条件判断、动作计划、技能阶段和运行上下文 |
| 源码目录 | `com.abilitykit.triggering`、`com.abilitykit.pipeline`、`com.abilitykit.ability`、`src/AbilityKit.Triggering` |
| 目标文档 | `08-GameplayModules/00-GameplayCapabilityMap.md` 到 `03-BuffSystem.md`、`08-PipelineAndAbilityRuntime.md` |
| 当前状态 | canonical Pipeline/Ability 深潜已建立；后续按测试、API 和示例变化持续校验 |
| 必画图 | TriggerPlan 执行图、条件表达式图、Pipeline 阶段图、Buff 生命周期图 |
| 设计意图 | 为什么用数据化 Plan，为什么用阶段管线，如何支持热更新、回放和测试 |
| 验收方式 | 文档能解释一个技能输入如何变成触发计划、效果执行和表现提示，并区分通用 Pipeline、Ability 服务与 MOBA runner |

### 4.9 Batch 8：Combat 领域模块

| 项目 | 内容 |
|------|------|
| 目标 | 覆盖目标搜索、投射物、伤害、实体索引、技能库、移动等战斗领域模块 |
| 源码目录 | `com.abilitykit.combat.*` |
| 目标文档 | `08-GameplayModules/04-ProjectileSystem.md` 到 `10-MotionPipeline.md` |
| 当前状态 | canonical Targeting、Projectile、Attribute、Damage、EntityManager/SkillLibrary 和 Motion 文档已建立；后续随领域接入、测试和确定性证据持续校验 |
| 必画图 | Targeting 管线图、Projectile 命中图、Damage Pipeline 图、实体索引更新图、移动来源合成图 |
| 设计意图 | 为什么把领域能力拆小包，如何组合成 MOBA/Shooter 不同战斗模型 |
| 验收方式 | 能通过文档追踪一次命中：搜索目标、生成投射物、命中判定、伤害结算、快照输出 |

### 4.10 Batch 9：配置、代码生成与数据链路

| 项目 | 内容 |
|------|------|
| 目标 | 区分运行时配置、CodeGen/Luban 生产链、ActionTimeline 协议与 Excel-ScriptableObject 编辑器同步 |
| 源码目录 | `Unity/Packages/com.abilitykit.codegen`、`com.abilitykit.actionschema`、`com.abilitykit.excel-sync`、`LubanConfig`、Demo Configs |
| 目标文档 | `05-CommonModules/04-ConfigurationSystem.md`、`07-CodeGenAndLubanProductionPipeline.md`、`08-ActionTimelineDataAndPlayback.md`、`09-ExcelScriptableObjectSync.md` |
| 当前状态 | 四条 canonical 责任线已建立；ActionTimeline 和 Excel Sync 均缺 package 自动测试，Excel Sync 仍需唯一主键、baseline 更新与编译阶段门禁 |
| 必画图 | 配置生成/加载图、时间线导出播放图、Excel 导入和三方冲突图 |
| 设计意图 | 避免把同名 Schema 或 Editor 工具误认为运行时配置能力，明确各数据资产的权威源、提交边界和失败传播 |
| 验收方式 | 能分别追踪运行时配置发布、logic JSON 导出播放和 Excel 双向同步，并定位每条链的测试与成熟度缺口 |

### 4.11 Batch 10：表现层、Demo 与工程示例

| 项目 | 内容 |
|------|------|
| 目标 | 解释逻辑表现分离、ViewEvent、Snapshot Dispatch、Console/MOBA/Shooter/ET Demo |
| 源码目录 | `com.abilitykit.demo.*`、`src/AbilityKit.Demo.*`、`Docs/design/09-ImplementationExamples` |
| 目标文档 | `04-PresentationLayerDesign`、`09-ImplementationExamples` |
| 必画图 | ViewEvent 转译图、Snapshot 分发图、Demo 启动图、跨平台适配图 |
| 设计意图 | 为什么表现层只消费事件和快照，如何支持 Unity、Console、ET、Server smoke 共用逻辑 |
| 验收方式 | 文档能解释同一个逻辑结果如何在不同端表现，并指出各端适配代码入口 |

### 4.12 Batch 11：Orleans 服务端与 Smoke 验收

| 项目 | 内容 |
|------|------|
| 目标 | 解释 Gateway、RoomGrain、BattleHost、FrameSyncGrain、Shooter Smoke 验收 |
| 源码目录 | `Server/Orleans/src`、`Server/Orleans/tools` |
| 目标文档 | Shooter/Orleans 示例文档、`12-ServerArchitecture` 服务端架构专题 |
| 必画图 | 网关请求图、房间生命周期图、战斗宿主推进图、Smoke 验收图 |
| 设计意图 | 为什么服务端只通过协议和 Host 组合接入，如何保证可测和可恢复 |
| 验收方式 | 能根据文档跑 smoke 或理解 smoke 输出中的帧、hash、reconnect、late join 结果 |

### 4.13 Batch 12：工程质量、测试与发布验收

| 项目 | 内容 |
|------|------|
| 目标 | 统一测试、采用治理、性能证据、DemoHarness、Unity 自动化、Smoke 和文档检查 |
| 源码目录 | `src/*Tests`、`Server/Orleans/src/*Tests`、`tools`、`.github` |
| 目标文档 | `10-EngineeringQuality/01-TestingWorkflow.md`、`04-CompanyAdoptionAndModuleGovernance.md`、`05-CrossModulePerformanceAndHotPathGovernance.md`，文档检查和发布验收专题 |
| 当前状态 | 测试、公司采用和性能治理 canonical 基线已建立；通用性能阻断 gate 尚未落地 |
| 必画图 | 测试金字塔图、CI 验收图、成熟度状态图、性能门禁晋升图、文档检查图 |
| 设计意图 | 为什么把纯逻辑测试、场景证据、成熟度、预算和发布阻断分层，如何降低回归与采用风险 |
| 验收方式 | 文档列出每类变更应运行的门禁、采用证据和性能声明边界 |

### 4.14 持续复核批次：既有 canonical 与最新源码对齐

| 项目 | 内容 |
|------|------|
| 目标 | 周期性选择 1 到 2 篇既有 canonical，按最新源码、生产调用和测试证据修正契约漂移，而不是只做措辞润色 |
| 本轮源码目录 | `com.abilitykit.core/Runtime/Event`、`Runtime/Generic/StableStringIdRegistry.cs`、`Runtime/Pooling`、Ability Triggering 生产适配与 Foundation 样例 |
| 本轮目标文档 | `05-CommonModules/01-EventSystem.md`、`05-CommonModules/02-ObjectPool.md` |
| 当前状态 | 已纠正事件退订与 Global facade API，补齐优先级/snapshot/once 重入、异常吞掉、字符串双重释放和 null 所有权；已补对象池锁内回调、异常非事务、manager 线程边界、旧 release handle、PooledObject 重复归还、首次配置固化和生产证据 |
| 剩余缺口 | Core Event/ObjectPool 均缺独立契约测试；字符串 Publish 双重释放和 null 不释放需修复，对象池需覆盖回调异常、Player 重复归还、manager 并发、scope 旧租约和 provider 锁内执行 |
| 验收方式 | Core 与引用样例工程构建、可用测试入口、Markdown/Mermaid/链接/fence 检查与限定路径 diff 审阅 |

持续复核不覆盖原有专题批次的历史状态。候选优先级由源码变更频率、生产调用深度、文档最后更新时间和独立测试缺口共同决定；发现状态传播、所有权、确定性或失败边界漂移时，应优先更新现有 canonical，不重复创建同主题文档。

Batch 12 的工程质量文档应覆盖测试流程与发布验收。可拆分的专题边界如下：

| 专题 | 内容边界 | 源码与脚本入口 |
|------|----------|----------------|
| 文档检查与发布验收 | Markdown fence、Mermaid 风险、索引定位、链接扫描、版本记录规范 | `Docs/design`、`.github`、现有验证脚本 |
| CI 分层 Job 设计 | fast/unit、matrix、smoke、nightly、artifact retention 的 job 拆分 | `.github/workflows`、`tools`、`Server/Orleans/tools` |

---

## 5. 专题优先级池

专题优先级由源码复用范围、文档缺口、验收风险和读者阻塞程度共同决定。已建立 canonical 基线的专题保留为持续校验项，不再描述为“尚无独立文档”：

| 优先级 | 方向 | 当前基线与后续工作 | 优先原因 |
|--------|------|--------------------|----------|
| P4 | Core 基础设施深潜 | 继续校验 StableStringId、EventDispatcher、PoolRegistry 与配置 provider | 这些能力被几乎所有模块依赖，且容易出现过期伪 API |
| P5 | Pipeline、Ability 与 Targeting | 已建立 `08-PipelineAndAbilityRuntime.md` 和 `07-TargetingSystem.md`；后续补独立契约测试与门禁 | 主链路边界已可追溯，验证资产仍需加强 |
| P6 | 配置与数据工具链 | 已建立运行时配置、CodeGen/Luban、ActionTimeline 和 Excel Sync canonical；后续补协议契约测试、唯一性和 baseline/发布门禁 | 四条执行面容易因 Schema、JSON 和生成概念重叠而误判职责 |
| P7 | Orleans 服务端专题 | 已有独立架构文档；继续补部署、容量与 Smoke 证据 | 服务端运行面需要持续维护验收和部署边界 |
| P8 | 采用、性能与发布验收 | 已建立治理规范和性能基线规则；后续统一 artifact schema、预算审批和 CI 观察任务 | 将人工判断沉淀为可重复执行的质量门禁 |
| P9 | 既有 canonical 周期复核 | 已复核 Flow/Svelto、Entitas/Query、Timer/HFSM 与 Event/ObjectPool 四组 canonical；后续按源码变更和测试缺口轮换候选 | 防止已建立文档因运行时能力增长再次漂移 |

```mermaid
flowchart TD
    Coverage[基础覆盖] --> Core[P4 Core 基础设施]
    Core --> Ability[P5 Pipeline 与 Ability]
    Ability --> Data[P6 CodeGen 与 Luban]
    Data --> Server[P7 Orleans 服务端]
    Server --> Release[P8 CI 与发布验收]
    Release --> Review[P9 canonical 周期复核]
```

---

## 6. 每篇设计文档模板

设计文档采用以下基础结构。章节可按模块复杂度合并，但“如何决策、如何接入、如何验证”不能只隐藏在结论中：

```md
# 模块编号 标题：能力、边界与源码入口

> 概述模块能力定位、源码入口、运行流程、设计取舍与边界判断。

## 1. 能力定位与选型速查
## 2. 解决的问题与非目标
## 3. 源码入口
## 4. 总体结构图
## 5. 关键运行流程
## 6. 生命周期或状态机
## 7. 最小接入示例或操作模板
## 8. 设计意图与取舍
## 9. 验证入口、证据状态与缺口
## 10. 源码阅读路径
## 11. 边界判断
## 12. 关联模块
```

---

## 7. 流程图修复规范

`Docs/design` 中有两类高风险图：旧 ASCII 箱线图，以及 Mermaid 节点标签中包含裸泛型、反斜杠换行或过长文字的图。流程图按以下规则处理。

| 风险 | 处理方式 |
|------|----------|
| ASCII 箱线图过宽或错位 | 优先改成 Mermaid `flowchart`、`sequenceDiagram`、`stateDiagram-v2` 或表格 |
| 节点标签包含泛型尖括号 | 用文字替代泛型，或写成 `EventKey of Args`，避免裸 `<T>` |
| 节点标签包含反斜杠换行 | 改成短标签，详细解释放到图下正文 |
| 单个节点文字太长 | 拆成多个节点，或把说明移到表格 |
| Mermaid 方向不清 | 结构图用 `TB`，数据流和调用链用 `LR`，状态流转用 `stateDiagram-v2` |
| 时序涉及多个参与者 | 使用 `sequenceDiagram`，参与者命名保持短句 |

```mermaid
flowchart TD
    A[发现异常图] --> B{图的类型}
    B -->|ASCII 箱线图| C[改为 Mermaid 或表格]
    B -->|Mermaid 语法风险| D[缩短标签并移除特殊字符]
    B -->|过宽说明| E[拆节点或移到正文]
    C --> F[检查 Markdown 格式]
    D --> F
    E --> F
```

---

## 8. 设计意图检查清单

文档治理前先对源码提出这些问题：

| 维度 | 需要回答的问题 |
|------|----------------|
| 边界 | 这个模块负责什么，不负责什么 |
| 抽象 | 核心接口为什么是这个粒度，是否隐藏了可替换实现 |
| 生命周期 | 谁创建，谁持有，谁 Tick，谁 Dispose 或 Release |
| 数据流 | 数据从哪里进入，经过哪些对象，最终产生什么输出 |
| 扩展点 | 业务项目可以在哪里接入，哪些点不建议扩展 |
| 确定性 | 是否影响帧同步、回放、预测、回滚或 hash |
| 性能 | 是否涉及对象池、索引、snapshot、批处理或 GC 约束 |
| 跨端 | 是否同时服务 Unity、Console、ET、Server 或纯 .NET 测试 |
| 调试 | 出问题时看哪个日志、测试、快照、trace 或 smoke 输出 |

---

## 9. 执行节奏

文档治理按固定节奏推进：

```mermaid
sequenceDiagram
    participant Reader as 源码阅读
    participant Notes as 设计笔记
    participant Docs as 设计文档
    participant Diagrams as 流程图
    participant Check as 检查验收

    Reader->>Notes: 记录真实类、接口、流程和约束
    Notes->>Docs: 更新能力定位、源码入口和设计意图
    Docs->>Diagrams: 补结构图、时序图和状态图
    Diagrams->>Check: 检查 Mermaid 风险和 Markdown 格式
    Check-->>Reader: 进入目标模块
```

单次治理聚焦 1 到 2 个模块，控制文档变更范围，避免源码依据变弱。治理结束时更新 `00-index.md` 的文档定位和版本记录。

验收顺序固定为：

```mermaid
flowchart LR
    Diff[查看文档 diff] --> Fence[检查 Markdown fence]
    Fence --> Mermaid[检查 Mermaid 高风险写法]
    Mermaid --> Index[确认 00-index 定位]
    Index --> Version[追加版本记录]
    Version --> Whitespace[运行 git diff --check]
```

这条验收线本身也要保持源码驱动：如果某个文档新增了测试命令、smoke 字段或类名，必须能在真实工程、测试或脚本中找到对应入口。
