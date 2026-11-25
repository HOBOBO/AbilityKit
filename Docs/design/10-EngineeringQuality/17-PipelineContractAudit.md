# Pipeline 职责与公开契约盘点

2026-11-21｜本轮盘点现状与整改顺序，不修改运行时、签名、样例、机制或发布状态。支持分级是稳定化建议，不是正式发版承诺。

## 职责边界

Pipeline 负责一次能力执行的阶段定义、Run 推进、阶段组合和运行时观测。宿主负责什么时候 Tick、给多少步长、何时结算、持有何种 Context、如何捕获和重绑领域状态；Pipeline 不负责世界调度、技能规则或完整快照。Run ID 用于当前进程内观测，诊断图标识也不是持久化业务身份或玩法配置版本。参见[运行时设计](../../../Unity/Packages/com.abilitykit.pipeline/Document/PipelineRuntimeDesign.md)和[状态与恢复审计](13-PipelineContinuousStateReview.md)。

Unity 生产程序集直接依赖 `com.abilitykit.core@0.1.0`，Editor 是独立程序集。但 Runtime 的 `Pipeline.cs` 含 Unity 场景初始化钩子，asmdef 尚非无引擎引用；[.NET 工程](../../../src/AbilityKit.Pipeline/AbilityKit.Pipeline.csproj)排除该文件，改用[同名门面](../../../src/AbilityKit.Pipeline/Runtime/Pipeline.cs)，后者另有公开 `Initialize()`。不能把共用大部分 Runtime 源码写成两端 API 或引导时序完全一致，也不能直接开启 `noEngineReferences`。

## 公开入口建议

| 分级 | 入口 | 证据与约束 |
| --- | --- | --- |
| 推荐稳定 | `IAbilityPipeline<TCtx>`、`AbilityPipeline<TCtx>`、`IAbilityPipelineRun<TCtx>`、配置/Context/Phase 契约、阶段 ID 和现有基础组合阶段 | `Start → Run → Tick(deltaTime)` 为普通接入。MOBA 技能绑定实际构建管线与自定义阶段；领域结算继续由调用方做，不从观察事件推导结算。 |
| 推荐显式装配 | `PipelineRuntime`、`IPipelineRegistry` 及实例注册表 | 若需要独立注册表，须显式传入实例。`new PipelineRuntime()` 默认仍取 `PipelineRegistry.Instance`，仅换 Runtime 或 trace recorder 不形成世界隔离；现有 MOBA 诊断装配就是只传 recorder 的例子。Pool 内部也仍用 Core 的共享登记，不承诺进程状态全隔离。 |
| 高级保留 | `AttachRun`、`PipelineExecutionProgress<TCtx,TNode>`、`SetExecutionPosition`、`Release`、`IAbilityPipelinePhaseInstanceFactory<TCtx>`，以及已有扩展点 | [MOBA 恢复适配](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Skills/Pipeline/SkillPipelineRollback.cs)已调用这些入口。Attach 不发普通 Start 事件及 RunStart trace，但初始化成功后仍注册并通知诊断；Release 不走普通完成/失败的 `ReleaseContext` 路径。阶段工厂隔离阶段实例，不保证深复制配置、委托或闭包；无工厂的阶段兼容复用并重置，不能承诺并发 Run 隔离或任意插件节点可恢复。 |
| 高级保留 | `IPipelineTraceRecorder`、`PipelineDebugHooks`、图/Trace/诊断快照、池租用查询与 `InstantAbilityPipeline<TCtx>` | Trace recorder 有真实 MOBA 消费者，Editor 依赖观测接口；诊断不持有战斗生命周期。瞬时入口有独立执行与失败测试，不等同普通 Tick 管线。不得把诊断异常隔离推及 `AbilityPipelineEvents<TCtx>` 的业务回调。 |
| 兼容保留 | 静态 `Pipeline`、`PipelineRegistry.Instance`、`TimeProvider` 和旧默认装配 | 现有 `AbilityPipeline<TCtx>` 默认使用静态 Runtime，不直接删除或标记弃用。`TimeProvider` 供 `AAbilityPipelineContext` 的 `StartTime` / `ElapsedTime` 使用，其默认值来自 `DateTime.UtcNow` 的差值，与 Run 接收的推进步长不是同一契约，不作单调或模拟时钟承诺。Unity 引导与 .NET 镜像如何收敛另案处理。 |

## 已证实的覆盖与缺口

- `.NET Release` 现有 20 项测试全通过，覆盖恢复挂接和挂接失败、复合阶段实例及进度、Repeat 续跑、恢复预检、诊断和瞬时管线的双异常。生产 asmdef 直接依赖审计通过。
- `.NET` 测试还直编两份纯托管的 Editor 诊断文件；这不等同于运行 Unity EditMode 测试。此前[独立 Editor 和样例验证](15-PipelineTriggeringPackageReadiness.md)使用本地 tarball，并显式补过夹具依赖；当前 Core 已退役旧 Unity 引导，不过本轮没有重跑完整 Pipeline 安装、Editor 或 Player 验证。
- 初次盘点时没有包级签名清单；.NET 门禁与 Unity 产物对照已补，见下方 P2 实施记录。签名检查不替代行为和候选制品验证。
- 普通 Run 的退出语义需要优先补证据。源码里 `Cancel` 设置标志，`Interrupt` 调用阶段中断，`Release` 是单独清理入口；不要合并为同一操作。普通 Run 中 `ShouldExecute` 在阶段执行的局部 `try` 外，完成/失败通知与清理也不是统一的异常处理路径。先确认回调、注册表异常和资源归还的既定契约；缺失或冲突的语义单列方案，不在重排时顺手修正。

## 下一批实施顺序

| 优先级 | 工作 | 验收 |
| --- | --- | --- |
| **P0 · 先锁住执行行为** | 对普通 Run 补成功、失败、Cancel 后推进、Pause/Resume、中断、恢复丢弃等特征测试。重点核对回调顺序、资源与上下文所有权、诊断观察者与业务委托的不同失败边界；对晚失败和恢复不重入补证据。 | Trace/回调序列、注册/注销和上下文释放次数可比较。已有恢复、瞬时管线、MOBA 适配不改变；若预期与实现冲突，提交证据及方案而不是改断言迎合当前实现。 |
| **P1 · 小步规范化** | 更新包内运行契约与注释，先处理 `AbilityPipeline<TCtx>` 的外层定义、Run 执行和诊断投影之间的边界。优先统一现有相关文件和私有实现；`Data`、`Impl`、`Interface` 等按归属逐批整理，不做全包一次搬迁。 | 保持命名空间、程序集、公开/受保护签名、阶段顺序和 `.meta` GUID；保留已迁移的 `Samples~`。Pipeline 测试及受影响的 MOBA 恢复/诊断、Unity Editor 回归按实际改动补验。 |
| **P2 · 候选消费** | 建立平台 API 对照和签名门禁，以实际 Pipeline 候选制品复验 .NET 与独立 Unity Editor，复查 Pipeline → Core → Deterministic 闭包、样例导入与最小调用。 | 区分本地制品显式安装与计划渠道传递解析；不扩大到未验证的 Player 或完整回滚能力，不更改当前发布批次。 |

第一刀先做 P0 的 Run 生命周期契约和测试，不先搬文件、不统一全局运行时，也不改变回滚语义。工程整理在工作区完成，留待操作者后续统一提交。

## P0 实施记录：普通 Run 生命周期

[生命周期测试](../../../src/AbilityKit.Pipeline.Tests/PipelineRunLifecycleTests.cs)新增 9 个用例。以独立注册表和记录器观察业务回调、Trace、注册/注销与 Context 释放的次序，不依赖全局观察者，也不读取已经归还的阶段列表。覆盖成功、执行异常、已进入阶段的更新与错误处理器双异常、暂停续跑、普通/暂停状态下延迟取消、复合阶段对活动子节点的单次中断、恢复挂接失败和静默丢弃。普通终态检查 Context 释放一次；恢复丢弃只验证注销、无业务回调和不释放宿主 Context，不据此声称覆盖完整回滚或所有池归还路径。

Release .NET 回归从 20 项增至 29 项，全部通过。未修改 Runtime、API、样例或 MOBA 恢复链，未重跑 Unity Editor/Player。

仍需单独确认的失败边界：`OnPipelineComplete` 在改为终态之后、`Cleanup` 之前调用，委托抛错可能跳过清理；自定义注册表的 `Unregister` 若抛错，也可能跳过后续阶段列表归还。诊断 hook 的订阅者隔离不自动覆盖自定义 `IPipelineTraceRecorder` 或业务回调。本轮不将这些行为写成契约。建议下一批只做已验证切片的无行为变化拆分；如果要调整上述失败边界，先明确原异常传播、资源归还和回调重入规则并提交方案。

## P1 实施记录：定义、执行与诊断拆分

`AbilityPipeline.cs` 保留管线定义、启动/挂接及 Context 释放契约；`AbilityPipeline.Run.cs` 承载私有 Run 的推进、控制和终态清理。`AbilityPipeline.Debug.cs` 投影定义图，`AbilityPipeline.Run.Debug.cs` 投影运行状态。仍是同一公开泛型类型和同一私有 Run，通过 partial 按职责编排文件，不引入新的对象或执行链。逐段核对了原成员、字段次序和方法体，保留原 `.meta`，三个新源文件各有独立 GUID。P0 记录的异常传播、回调和清理边界未修改。

验证：Pipeline Release 29 项、MOBA 技能流程与机制收敛定向回归 54 项通过；Unity `2022.3.62f1` 从空工程显式安装 Pipeline、Core、Deterministic 本地 tarball，Runtime 与 Editor 编译通过，核实四份 Pipeline 源码及对应 `.meta`。证据位于 `local/Logs/package-readiness/pipeline-run-split/report.json`。此轮未运行 Unity EditMode、导入样例或构建 Player，不代表注册表传递解析已验证。

## P2 实施记录：公开 API 基线与平台对照

- [.NET 候选基线](../../../src/AbilityKit.Pipeline/PublicAPI.Unshipped.txt)登记 641 条去重的签名，包含包外派生类可见的受保护成员；[已发布基线](../../../src/AbilityKit.Pipeline/PublicAPI.Shipped.txt)不登记本批候选。清单记录现有签名，不改变前述支持分级，不宣告发版。
- .NET 构建中 `RS0016` 拒绝未登记签名，`RS0017` 拒绝清单中找不到的签名。独立夹具验证了未登记的公开成员新增与候选基线中受保护成员的移除均会导致构建失败。现有多可选参数重载 `RS0026` 仅在 Pipeline 保留为可见告警，待单独评估重载兼容性，不在本批调整签名。
- 使用 P1 独立 Unity 工程的实际 Runtime DLL，比对前确认包缓存中 65 个 Runtime `.cs` / `.asmdef` 文件与工作区逐一同哈希。与 .NET Release DLL 对照对外类型、成员签名和可见性、泛型约束及可选参数默认值，唯一发现的公开差异是 .NET 多了静态 `Pipeline.Initialize()`。未比较任意自定义属性或程序集引用身份；这不是运行时行为或完整二进制兼容证明。平台入口见[运行时设计](../../../Unity/Packages/com.abilitykit.pipeline/Document/PipelineRuntimeDesign.md#unity-与-net-的公开入口)。
- Pipeline Release 首轮有一项 Trace 断言收到两条匹配事件；当时监听全局 Hook，未按当前 Run 过滤。单独复跑和随后三次全量复跑通过；后续修复见下方诊断测试记录。

本批只完成签名基线和平台对照，未重跑 Unity 测试、样例导入或 Player，未验证正式渠道的传递依赖解析。候选消费验收仍按实际待发布快照执行。

## P2 补充记录：诊断测试按 Run 隔离

[测试订阅](../../../src/AbilityKit.Pipeline.Tests/RunDiagnosticSubscription.cs)仍监听真实的全局诊断 Hook：在启动通知中按测试专用的配置实例识别目标 Run，之后仅转发对应 `OwnerId` 的 Trace 和结束通知。离开测试作用域时退订；没有关闭测试并行或修改 Runtime、Editor 实现。

在[诊断契约测试](../../../src/AbilityKit.Pipeline.Tests/PipelineDiagnosticsContractTests.cs)中注入同一管线定义下、使用不同配置实例的无关 Run。修复前，错误 Trace 断言收到两条匹配事件，Editor 历史容量测试也错误收录无关 Run；修复后，错误消息及唯一性、历史容量和淘汰规则的原有断言保留，诊断测试 10/10，.NET Release 完整套件连续 10 轮均为 29/29。本轮未运行 Unity EditMode 或候选消费验证。

## P2 补充记录：候选制品消费

- Unity `2022.3.62f1` 从无 `Library` 的工程安装工作区打包出的 UPM 制品。工程只声明 `com.abilitykit.pipeline@0.1.0`，由本地测试注册表解析 Core（深度 1）和 Deterministic（深度 2），无需手动补 UnityWebRequest、MemoryPack 或其他包。安装后按哈希核对三份制品中的文件。
- 通过 Package Manager 样例导入接口导入 `Pipeline Usage`，核对导入的脚本和 `.meta`，重启 Editor 编译并执行延迟、中断、技能正常完成及三次效果回调。删除无人引用的 Pipeline 空目录 `Runtime/Impl/Behaviors.meta`，消除了该包的孤立目录告警；Core 的 `Documentation~.meta` 提示仍在，不影响本次消费验证。
- 只在独立 EditMode 阶段加入 Test Framework。首轮 61/73：两项集合计数断言依赖反射、一项视口居中断言精确比较浮点数、九项误创建第二个 `ScriptableSingleton`。改用实际 `Count`、沿用相邻视口用例的 `0.001` 像素精度、使用真实 singleton 并在测试后恢复内存设置且不写盘。重新打包、安装和导入后，**73/73 通过**；未改 Runtime 或 Editor 实现。
- .NET Release 回归 29/29。独立控制台只引用本地 `AbilityKit.Pipeline` 验证用 NuGet 制品，从隔离目录解析 Core 和 Deterministic，验证 `Start → Tick → Completed`、动作与 Context 释放各一次。验证版本仅用于本地制品，不新增发布承诺。

本机证据在 `local/Logs/package-readiness/pipeline-candidate/`，其中 `recheck/unity-report.json` 记录最终 UPM 制品哈希、包文件清单、安装锁文件和 EditMode 结果。未验证正式注册表、升级或回退、Player/IL2CPP，也未更改发布批次。统一提交后仍须根据实际待发布提交核对制品输入。

## 回滚边界：现状契约与特征测试

[包内契约](../../../Unity/Packages/com.abilitykit.pipeline/Document/PipelineRuntimeDesign.md#执行进度与回滚边界)明确内置进度、自定义阶段、定义和宿主 Context/世界的归属。当前验收限于同一有效定义、独立值状态及宿主可静默重绑的子集，不宣告跨版本恢复、自动阶段隔离或事务式导入。

| 已新增的边界证据 | 现有结果 |
| --- | --- |
| 同 ID、同类型的 Delay 修改时长 | 可通过结构预检并装回进度，但相同步长后的完成结果不同。验证的是定义校验缺口，不把跨配置恢复变成兼容承诺。 |
| 同一定义重叠运行无工厂的有状态阶段 | 第二个 Run 将第一个 Run 共享的计时重置；此前独立捕获的数值不受影响。 |
| 原位写回后段抛错 | 预检已通过并执行准备回调，前段已写入，Run 标志还没应用；异常原样传播，不能继续使用半写入的 Run。 |
| MOBA 新挂接重绑抛错 | 测试阶段已建立一项绑定，适配器随后解绑并注销失败的新 Run，不发普通启动事件；不推导原位恢复或世界级补偿。 |

本批只更新文档和测试，不变更 Runtime、MOBA 恢复机制、包版本或快照格式。定义修订校验、可恢复运行的实例隔离准入、恢复失败补偿分别评审，不在本批扩大范围。

验证：Pipeline 进度专项 7/7、Release 全量 32/32；MOBA 事件等待与正式预测重放定向回归 7/7。本轮未重跑 Unity EditMode 或重新验证 UPM 制品。

## MOBA 回滚定义修订校验

**现状。** [Pipeline 进度预检](../../../Unity/Packages/com.abilitykit.pipeline/Runtime/Core/Pipeline/PipelineExecutionProgress.cs) 校验身份、阶段类型、有序拓扑及进度字段，不校验行为参数。[现有回归](../../../src/AbilityKit.Pipeline.Tests/PipelineExecutionProgressTests.cs) 证实 `Delay` 从 `0.5f` 改为 `1f` 后，旧进度仍能装入，但相同步长后的完成结果不同。这是复用旧进度的问题，不是进度存储与执行逻辑拆分失败。

[表驱动库](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Skills/Pipeline/TableDrivenMobaSkillPipelineLibrary.cs) 缓存编译出的 PreCast/Cast 定义，管线配置 ID 由技能 ID 和阶段决定；当前在 PreCast 的执行后续仍可能推进到 Cast。房间 `ConfigVersion` 是启动配置传入的值，诊断 `ConfigHash` 是导出器的可选输入，未找到任一个与实际编译定义及其引用内容绑定的证据；诊断 `StructureId` 也不代表行为版本。均不宜直接用作恢复凭证。

**实现范围。** MOBA 宿主编译并绑定一对 PreCast/Cast 定义，从私有阶段参数副本与配置开关生成带格式/实现版本的 SHA-256 修订。哈希输入按键名排序，保留有序数组，不用 CLR 类型、委托、对象哈希或调试图推断行为。引用标签模板使用注册表实际可读的内容修订；RulePlan、窗口触发和 Timeline 效果引用使用加载后计划库的内容修订，整体版本可保守拒绝无关计划改动。内置阶段和内置等待条件实现由 `moba-skill-pipeline:1` 标识；实现契约变化须提升该版本。动作及函数的注册代次仅用于同一世界发现注册修改，不是可跨世界比较的委托版本。

捕获从活跃 runner 已绑定的定义对取修订，不按技能 ID 重查当前缓存。在 [runner 预检](../../../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Skills/Pipeline/SkillRunnerRollback.cs) 中，与本次实际用于恢复的定义对比较，并保留现有结构校验。正式 lifecycle group 与直接导入共用该预检；定义不匹配或缺少凭证时，在解绑旧 Run、挂接新 Run、写回进度或提交组内状态前明确拒绝，不按新定义尝试继续执行。预检与挂接必须使用同一冻结定义，不能通过两次可能返回不同结果的查询绕过比较。

**载荷策略。** MOBA runner 外层从 v4 升 v5，为每个活动条目携带定义修订；v4 runner 载荷不做兼容读取，不用当前定义反填修订。升级前的回滚历史不可跨版本重用，需从新版本重新建立基线；普通技能执行不受此限制。通用 Pipeline API、MOBA 内层进度 v1、其他 provider 格式不变。本项不扩展到热更迁移、跨进程兼容承诺或世界级事务补偿。

| 状态与所有权 | 捕获、使用与失效 |
| --- | --- |
| 私有阶段定义、配置和编译修订由表驱动定义库持有；每次施法拥有自己的阶段实例 | runner 记录实际绑定的修订，恢复预检从同一组定义生成待挂接实例。当前阶段的业务进度仍由内层 v1 持有。 |
| 规则计划和标签模板由原服务持有，不进入 runner 快照 | 绑定时记录可验证内容与同世界修改代次；依赖改动导致捕获或恢复拒绝，不使用当前值冒充旧绑定。未版本化的程序化计划、自定义定义、未知等待条件、会开启独立施法的派生阶段不纳入本批回滚范围，普通执行不变。 |
| lifecycle group 持有恢复与既有局部补偿次序 | 在解绑前为目标和操作前快照分别准备 runner 定义；提交与补偿复用校验过的定义，不再次按技能 ID 查找。 |

| 验收用例 | 预期 |
| --- | --- |
| 等价定义独立构建；同修订捕获、恢复并逐帧重放 | 摘要一致，正式预测重放结果一致；保留内层 v1 固定字节向量，增加外层 v5 编解码覆盖。 |
| 同 ID、同拓扑，仅改 Delay/Repeat 参数，或替换同 ID 的已覆盖规则/模板内容 | 结构校验仍可能通过，但修订预检明确拒绝。 |
| 从 PreCast 捕获，仅修改后续 Cast；运行中替换可查询定义后再捕获 | 前者拒绝；后者仍捕获实际 Run 绑定的旧修订，不误标为新定义。 |
| v4、缺失或无法验证的修订；直接导入和正式组预检拒绝 | 验证旧 Run、既有订阅、推进能力及其他组内状态保持原样，无解绑、挂接或写回。 |

实施前基线：.NET Release Pipeline 全量 32/32；MOBA 事件等待、预测重放与 lifecycle group 预检定向回归 264/264。

实施后验证：.NET Release Pipeline 全量 32/32、Triggering 全量 63/63、MOBA 技能流程、正式预测重放、生命周期预检和补偿等定向回归 325/325。覆盖未来 Cast 的 Delay/Repeat 参数变更、同 ID 计划和标签模板内容替换、活跃 Run 的修订不被新查询结果覆盖，以及直接导入与正式组拒绝 v4 或无效凭证后检查点保持不变。MOBA 测试构建通过临时 MSBuild `Using` 补齐无关测试文件缺失的 Motion 命名空间，未修改该文件；未运行 Unity EditMode、Player 或重新打包 UPM。

## Runner 旧格式分支清理

Runner 在读取条目前就拒绝非 v5 载荷，因此条目和上下文解码不再传递版本参数，也不再保留不可达的 v2/v3 默认值分支。v5 字节布局、版本号及拒绝规则不变；本批不退役其他 provider 仍可达的旧格式，也不改变录制或网络协议。

本批 .NET Release 定向回归 297/297：覆盖技能流程、联合生命周期预检与补偿，以及录制输入后的快照恢复和重放。未运行 Unity EditMode 或 Player。
