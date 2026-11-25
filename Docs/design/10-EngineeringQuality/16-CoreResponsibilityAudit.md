# Core 职责归属与补强审计

> 2026-11-21。工程盘点与工单，不替代 [Core 边界](../../../Unity/Packages/com.abilitykit.core/Documentation~/CoreBoundary.md)、[框架包工程规范](10-FrameworkPackageEngineeringStandards.md)或[发布检查清单](06-BetaStabilizationAndReleaseChecklist.md)，不批准新机制或兼容 API 退役。

## Core 应该是什么

Core 是战斗工具集的**跨运行时基础层**：通用原语由 Core 提供，使用这些原语作出的战斗决策仍由各领域持有。当前纯运行时只直接依赖 Deterministic；不引用 Unity 引擎，Editor 与 Runtime 独立。核心工具集不等于单一 Core 包；Pipeline、Triggering、World、Ability 都可以是核心模块，但它们的状态与策略不因此进入 Core。

参考成熟框架时，比较的是职责，不是复制目录或功能：

| 参考 | 对 AbilityKit 的设计启示 |
| --- | --- |
| Epic [Unreal Gameplay Ability System](https://dev.epicgames.com/documentation/en-us/unreal-engine/gameplay-ability-system-for-unreal-engine) 将技能、属性、效果作为独立框架能力描述。 | 技能编排、触发执行、属性结算应由领域包组合，不给 Core 添一个通用战斗管理器。 |
| Unity [Entities 1.0 System groups](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/systems-update-order.html) 在 World 的系统组内表达更新顺序。 | Core 允许提供稳定排序、同步派发；模拟时钟、阶段、同帧顺序和跨世界隔离仍由 World 与执行包定义。 |
| Photon Quantum 3 [Fixed Point](https://doc.photonengine.com/quantum/current/manual/quantum-ecs/fixed-point) 与 [RNG Session](https://doc.photonengine.com/quantum/current/manual/rngsession) 分别定义确定性数值和随机流；共享随机状态会让消费顺序影响结果。 | 数值算法与随机流契约归 Deterministic，随机状态的归属、保存与恢复归 World/同步层。Core 只保留已有桥接，不再提供第二套实现。 |

## 现有代码是否合适

使用证据以仓库生产源码的实际符号为准，排除第三方同名类型、样例、测试与生成副本。同一机制的多个调用点、同一 MOBA 链的多个包均不作为独立需求。下表是分组抽样，不是外部消费者证明；少于三个使用方不能作为直接删除已发布 API 的理由。

| 区域 | 使用证据或契约 | 处置 |
| --- | --- | --- |
| `Pooling`、`Buffers/PooledBufferOwner`、`Lifetime` | Core `ObjectPool<T>` 被 Motion、Projectile、Targeting 等使用；`PooledBufferOwner<T>` 用于 TCP、WebSocket 和 FrameSync 快照存储，前两者是同类收包需求；`DisposableRegistration` 用于 Triggering、World.ECS 和 World.Snapshot。 | 保留原语。池容量、对象复位、施用范围和 World 停机顺序由使用者持有；不能因存在 `PoolScope` 就认为所有调用都已隔离。 |
| `Collections`、`Logging`、`Identifiers`、`Timing` | 属于已定义的基础契约。`StableHashV1` 跨 Record 与 Triggering 两种标识用途，旧 `StableStringId` 适配仍在 Triggering；`MonotonicTime` 用于 Flow 耗时、Network.Host 超时和 Network.Runtime 条件模拟。 | 保留现有契约。不统一各种 ID 的作用域、碰撞策略或编码入口；不把真实耗时当模拟 Tick。 |
| `Math`、`Mathematics` | 向量与几何基础及 Deterministic 转换桥，转换误差和范围有独立契约。 | 保留基础及桥接；碰撞形状归 Collision，定点与随机算法归 Deterministic。不要仅凭历史命名空间判断实际所有者。 |
| `Eventing` | 有同步实例分发、类型化键和进程共享入口；Triggering 与 World 各有事件设施。按 `EventDispatcher`/`GlobalEventDispatcher` 抽查，未找到三个独立生产使用方，不能把键的使用计作分发器的使用。 | 保持现有 API；类型化键、实例分发和全局入口分开评审。不合并各领域总线，不要求新 World 使用全局入口。 |
| `Buffers/FrameIndexedBuffer` | 当前生产符号引用集中在 StateSync 的输入与快照历史；FrameSync 使用独立的回滚快照环。 | 暂保留既有接口与淘汰契约，不扩展为同步框架。只有后续证明消费者与兼容边界之后，才评审是否由同步层承接。 |
| `Continuous`、`DebugDraw`、旧 `Config` 与反射、`DisposeUtils` | Continuous 与 Diagnostics 已承接对应主线；配置文件、兼容反射与释放工具已有替代策略。 | 已明确不属于长期职责；现有旧入口按各自兼容窗口冻结，不能套用已批准的 Unity 引导类退役决定。 |
| `Markers` | Targeting 的 rule/scorer/selector 注册仍显式调用 `MarkerScanner`，三处属于同一所有者；MOBA 启动的使用另行核对。 | 优先做宿主发现能力与领域显式注册的对照方案；反射扫描本身不要求 Unity，不能因为可能迁出 Core 就归入 Unity 包。 |
| `Observability` | [现有准入复核](../../../Unity/Packages/com.abilitykit.core/Documentation~/Observability.md)将生产引用归为 MOBA 诊断链，符号仍在 `PublicAPI.Unshipped`。 | 不承诺为 Core 正式基础面；若仍只有同一需求，评审归实际采集领域，Diagnostics 只有在存在跨域共享需求时才是承接候选。 |

## 稳定化优先级

目前没有证据表明 Core 缺少一个必须新增的运行时设施。定点与随机已有 Deterministic，计划计时已有 Timer，World 和同步包承担帧推进与快照，Record、Diagnostics、GameplayTags、Attributes、Ability 均已有各自职责。缺口主要在这些能力如何被验证、组合，以及哪些包可以对外承诺什么，而不是再加 Guard、Result、Service Locator 或一套通用 ECS。

基线：稳定标识已从 `Runtime/Generic` 归到 `Runtime/Identifiers`，源码和 Unity GUID 未变。最近一次整理通过 531 项 Core 测试，77 个 Runtime 文件的中文多行注释检查无问题；后续按变更执行门禁，不再安排一轮无目的的全包注释或目录重写。

| 优先级 | 工单 | 完成标准 |
| --- | --- | --- |
| **P0-1 · 已通过** | Collision 和 Record 的两处超额声明已迁回所属命名空间；项目直链检查改按 Git 源码范围枚举。 | 白名单未扩大；领域回归通过，完整边界审计已能在含临时工程副本的当前工作区直接通过。 |
| **P0-2 · 划定发版承诺** | 核对稳定 API、兼容入口和暂不承诺的 API；明确 Core 对 Deterministic 的版本与平台支持范围。 | Public API 基线、文档和制品声明一致。`Observability` 维持 `PublicAPI.Unshipped` 待评审状态，不当成正式基础 API；如拟随稳定制品交付，须明确其支持状态。不把 Windows Player 最小运行列为本轮工单，也不将未验证的平台写成已支持。 |
| **P1-1 · 核心代码打磨** | 先看 `ObjectPool` 的租用、归还、容量和失败补偿，再看 `EventDispatcher` 的订阅、顺序、重入和参数所有权；分两批实施。 | 对照 `PoolingContractTests` 和 `EventDispatcherContractTests`，只补实际缺项，例如多实例隔离或失败后的可观察状态；有明确职责切点再拆内部实现。保留公开 API、调用顺序、异常和默认值；若触及热路径，记录分配与吞吐对比。仅文件偏长不构成拆分理由。 |
| **P1-2 · 按候选验收** | 固定待发布的 Core 与 Deterministic 源码和依赖制品，在所选发布渠道完成独立安装、最小调用与回退。正式发版必须完成本项；若前一项不改实现，不必为等重构推迟验收。 | 重跑严格输入校验、API/源码门禁、对应测试与实际制品消费，归档制品哈希和签核；按计划发布渠道验证依赖解析。本地同版本替换不等于正式旧版升级；无正式旧版时标为不适用，不声称已验证。 |
| **P2 · 兼容债与后续提案** | 旧 Continuous、DebugDraw、配置与反射入口按兼容窗口维护。正式 Continuous 继续在现有独立包；`ReflectionInvokeUtils` 不另建包；`MarkerScanner` 尚有消费者，现阶段保留。`FrameIndexedBuffer`、观测原语和多 World 所有权有实际需求再做归属评审。 | 不以目录整洁为由提前退役、回迁或新建公用包。确需迁移时，先列明生产消费者、依赖方向、兼容路径和验收，再按仓库机制治理实施。 |

**条件性发版门禁：** 只有首发承诺 Unity 与 .NET 跨目标位一致锁步时，才把 `Fixed64` 的舍入与范围差异升为 P0，由 Deterministic 单独评审，定义舍入、溢出与旧回放兼容，再验证同组 raw 黄金向量。不承诺时，保留[候选支持限制](14-CoreDeterministicBetaCandidate.md)，不在 Core 桥接层修补差异。

对新原语继续执行三个独立使用方准入；对已有类型先做支持分级，再按兼容契约处置。单个 Unity 适配应靠近所有者；本次没有证据支持建立统一的 Unity 基础包。只有多个独立领域出现同一引擎生命周期契约、且现有适配归属不能满足时再立项。

## P0-1 定位与迁移结果

Core 的 Runtime 源码及 Public API 基线没有 Collision 或 Record 的旧副本。边界门禁统计的是其他包新增的 `AbilityKit.Core.*` 命名空间声明，不是 Core 内的重复文件。对照原门禁基线，超额的两个文件都是当前领域实现，有实际调用方：

| 原文件与公开面 | 已核实的使用方 | 迁移落点 |
| --- | --- | --- |
| Collision 的 `Runtime/Math/IColliderBoundsSource.cs`：`IColliderBoundsSource`、`ColliderBounds` | `NaiveCollisionWorld`、`GridCollisionWorld` 实现接口；Projectile 的 `AreaWorld` 用它读取边界并执行扫掠。 | 留在 Collision 包，改用 `AbilityKit.Combat.Collision`；Core 只提供 `Vec3` 等基础数学。 |
| Record 的 `Runtime/Record/FrameRecord/Analysis/CanonicalStateDiffAnalyzer.cs`：分析器与同文件的规范状态 DTO、枚举 | Record.Tools、Record.Tests 及 MOBA 规范状态导出器和测试。 | 留在 Record 包，改用 `AbilityKit.Record.FrameRecord`；不改变 JSON 字段、默认值、枚举值和格式版本。 |

经用户确认，迁移只覆盖上表类型和仓库调用方，不保留旧全名；升级时需调整 `using` 或完全限定名并重新编译。Collision 边界源移至 `Runtime/Collision`，保留原 `.meta` GUID。程序集、碰撞算法、回放语义和既有命名空间白名单不变；其余历史类型不纳入此批。

审计：排除 Git 忽略的临时工程后，未修改的 `tools/audit_core_boundaries.ps1` 在隔离源码快照上完整通过；Collision、Record 的历史命名空间文件回到既有限额 5 和 56。直接检查现有工作区时，这两处已放行，但根目录 `temp` 下的旧 Shooter 工程副本仍触发配置/反射源文件直链门禁。临时工程和审计脚本未改动，不能把隔离通过写成本地工作区直接运行通过。

.NET 回归：Collision 13 项、Projectile 97 项、Record 29 项（含命令行工具端到端）、MOBA 规范状态导出及受命名空间影响的测试 53 项通过。未运行 Unity Player，未改包版本或发布状态。

## P1-1 对象池批量清理整理

将 `Clear`、裁剪及其批量销毁实现收拢至 `ObjectPool.BulkCleanup.cs`。仍由同一 `ObjectPool<T>` 持有锁、空闲栈、所有权转换和计数，不增加运行时对象；迁移的方法体以及 `Get`、`Release`、`Prewarm` 的方法体未变。不调整公开 API、回调顺序、失败语义或容量策略，也不宣称吞吐提升。

新增两条行为回归，分别覆盖销毁式清空与强制裁剪：销毁回调前提交整批移除和计数；拒绝重新归还尚待销毁的对象，保留回调期间归还的其他对象；多个失败按销毁顺序聚合。下一批评审 `EventDispatcher` 的订阅快照、参数所有权与失败隔离。

## P1-1 事件派发与键文件整理

Git 历史中非泛型 `EventKey` 与泛型 `EventKey<TArgs>` 已同时存在，原泛型文件名中的反引号和数字对应单泛型参数的元数据写法，不是名为 `EventKey1` 的重复类型。泛型键仍被 Triggering、Ability 等使用。本批仅将泛型源文件改名为 `EventKey.Generic.cs`，保留原 `.meta`、类型和 API。

将私有通道与监听器实现原样归入 `EventDispatcher.Channel.cs`，仍属同一分发器类型。未改变订阅次序、快照、重入、参数释放或快照池；不合并 `GlobalEventDispatcher` 与领域总线。新增用例覆盖无监听器时整数和字符串发布的自动释放优先级、释放失败隔离，以及同键的两个分发器实例分别订阅和清空。

## 边界门禁的源码范围

项目直链检查此前会把工作区中 Git 忽略的临时 Unity 工程当成生产输入。现在在 Git 工作区枚举已跟踪和未忽略的 `.csproj`，读取当前文件内容；无 Git 元数据的源码副本剪枝构建和临时目录。只调整项目文件枚举，不改命名空间白名单、托管生产源码扫描或直链拒绝规则。

当前工作区完整边界审计通过，检查了 79 个 Core Runtime 文件、2323 个托管生产源码文件，80 个历史命名空间文件仍受限额约束。隔离夹具验证了忽略临时副本、拒绝未跟踪正式项目、拒绝已跟踪但匹配忽略规则的项目，以及在没有 Git 元数据和 `rg` 时的通过与拒绝路径。Core 538 项测试通过；未清理临时工程，也未运行 Unity Player 或修改发布状态。

## 首发支持清单与本地候选复验（2026-11-21）

包内[首发支持清单](../../../Unity/Packages/com.abilitykit.core/Documentation~/FirstReleaseSupport.md)已经区分稳定推荐、高级保留、历史兼容与待评审项。README、文档索引、兼容基线说明、变更日志均以该清单为当前候选口径；历史 Beta 记录不再作为现行承诺。Observability 保持可用但不进入首发稳定面。未改运行时、签名基线、版本或批次状态。

本地复验以 `bcc9cea4a2b051baf387d58b7b569d8522227945` 为基线，以带首发文档的工作区哈希快照生成候选。归档位于 `local/Logs/core-stabilization/first-release-20261121-100457/`；`consumer/report.json` 记录输入来源、制品哈希与各阶段结果，`sha256.json` 索引归档副本。确认首发清单已进入实际 Core 候选 tarball，且与输入文件哈希一致。

| 验收项 | 结果 |
| --- | --- |
| 工作区 Release 回归 | Core 538 项、Deterministic 90 项通过，零失败、零跳过；Core 构建启用公共签名门禁，其基线未变。 |
| 边界和依赖 | Core 边界审计通过；Core、Deterministic 生产程序集的直接依赖审计通过。扩展审计另发现 MOBA View 缺六项直接依赖，并报告 `AbilityKit.HFSM` 所有者未识别。本轮未修改 MOBA 声明，也未据此宣布完整 `core-stability` 通过。 |
| 本地 `.NET 10` | 基线安装、候选替换、基线回退均通过，每阶段 8 项最小调用；只声明 Core 时能解析 Deterministic，缺内核的负向校验以 `NU1101` 拒绝。 |
| Unity `2022.3.62f1` Editor | 从无 `Library` 的隔离工程验证三个阶段，每阶段 8 项最小调用、3 项包归属检查，核对锁文件和 C# 源码哈希；候选不解析 `UnityWebRequest`，也不编译 `AbilityKit.Core.Unity`。两包均显式安装，不证明注册表可以自动解析传递依赖。 |
| 严格发布预览 | 七包批次在新首发清单仍是未提交输入时按预期拒绝，未通过正式输入门禁。没有提交、打标签或发布。 |

P0-2 的支持分级已落文档，P1-2 的本地制品复验已完成；正式签核仍需固定并复验实际发布输入、通过严格预览、按计划渠道验收传递依赖安装并归档证据。当前没有可用于正式升级验证的历史发行版；按本轮范围不补 Windows Player，不扩张 Mono、IL2CPP 或跨目标锁步承诺。

### 固定 Core 与 Deterministic 的本地验收输入

在 `local/Logs/core-stabilization/frozen-inputs-20261121-101134/` 归档 `release-input-lock.json`、`validated-source.tar.gz`、`core-deterministic.patch` 和严格预览日志。逐文件核对 251 项源码与构建输入、两项探针输入及八份既有制品的哈希。源码归档保留已验收的原始字节；补丁按照 Git 入库规则生成，从基线 `bcc9cea4a2b051baf387d58b7b569d8522227945` 在独立索引重放得到同一棵树 `c95c2384d3a1828e0ffa5318f2b059043b16c7e9`，不修改实际暂存区。本次是输入固定和重放检查，消费结果沿用相同字节的前次复验。

七包批次没有一起冻结：GameplayTags、Diagnostics、AI Abstractions 只记录当前基线树，不扩张验收结论；Protocol 的两份生成目录改动与 Network Runtime 的快照准入改动未纳入。两者应由各自所有者确认内容及相应回归，不替换为旧版或擅自补进发布候选。现有严格预览仍因未提交输入退出；无新提交、标签或发布。正式签核仍需确认最终批次范围和提交，再复核制品与计划发布渠道的依赖解析。
