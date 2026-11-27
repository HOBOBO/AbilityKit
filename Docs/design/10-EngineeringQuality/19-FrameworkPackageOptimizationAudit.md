# 框架包结构优化盘点

> 文档类型：工程审计与分批路线。初始盘点基线：2026-11-24；本轮预测驱动拆分核校：2026-11-25。记录本地工作区现状，不替代[框架包工程规范](10-FrameworkPackageEngineeringStandards.md)、[模块治理](04-CompanyAdoptionAndModuleGovernance.md)或发布清单。

## 排查范围与判定

扫描 `Unity/Packages` 下直接包含 `package.json` 的 105 个包：22 个 `demo.*`、6 个 `thirdparty.*`、其余 77 个进入归属筛查。77 个包含示例集合、平台适配、项目协议和编辑器工具，**不是框架发布包数，也不是 77 个已确认有缺陷的包**。其中 30 个未见根目录 README、61 个未见根目录 CHANGELOG；这是补查线索，不是发版判定。本轮盘点以 `batch-1-leaves` 的七个候选为基线；后续批次以[发布清单](../../../tools/publish/release-manifest.json)为准。

逐包预筛包声明、程序集、目录、包内文档和 `.NET` 源码链接；对候选与已治理的包再核对专项记录和现有测试入口。本轮是 E0 静态盘点，引用的历史测试只对当时输入和环境有效；未重跑运行时、干净消费或发布验证。没有 `.NET` 镜像不等于 Unity 专用包有缺陷，`noEngineReferences: false` 也不是平台污染的充分证据。优先查有行为影响的错位和依赖闭包，再整理公开 API、生命周期、目录和注释；不为统一外观批量改命名空间、程序集或资源 GUID。

## P0：七个首发候选

| 包 | 当前证据与优先排查项 | 下一次验收 |
| --- | --- | --- |
| `core`、`deterministic` | [本地候选](14-CoreDeterministicBetaCandidate.md)已有固定输入的纯 C# 与 Unity Editor 消费证据（对应本地 E4），[Core 专项](16-CoreResponsibilityAudit.md)已有职责和兼容面裁剪。作为基线保留，不重新铺开目录整改；签核前复跑候选闭包和旧边界告警。Windows Player 最小运行不排入本轮结构优化。 | 固定候选输入，逐项记录覆盖和未承诺的环境；不把 Editor 当 Player 或注册表验收。 |
| `protocol` | 已移除多余 Core 引用，MemoryPack 保留可选入口；`Runtime/Generated` 仍由现行 catalog 工具生成和校验。 | 本地源包闭包已消费；发布验收仍需对固定候选输入运行严格预览，不能用 `-Check` 替代。 |
| `diagnostics` | 采集原语与 Unity 取样已分程序集，补齐 Editor JSON 工件导出的直接依赖。 | 本地 Unity Editor 采样探针已过；候选签核须记录外部 asmdef 的迁移约束与支持环境。 |
| `network.runtime` | Runtime asmdef 已禁止引擎引用，有独立 `.NET` 契约测试入口；依赖 `core` 与 `protocol`。已有 `Abstractions`、`Protocol`、`Runtime` 分层，不适合盲目大搬；优先评审会话与传输接口、连接关闭和资源所有权。 | 协议闭包收口后，跑本地回环、断开与重连契约和独立消费；只对 runtime 给结论，不外推到 lobby、room、battle 或 KCP 适配。 |
| `gameplaytags` | Runtime 禁止引擎引用，Editor 单独编译；层级、查询、移除条件、名称序列化与 Ability owner/source 清理已复验（E3）。 | 用独立 Unity 工程确认安装本包后 Runtime 消费无需引用 Editor；不把 NetIndex 当作跨版本身份。 |
| `ai.abstractions` | 单个纯 C# Runtime 源码文件；已明确借用观测、动作所有权、环境生命周期和策略快照的边界，现有推理测试工程覆盖缓冲与 Shooter 环境消费（E3）。 | 用独立 Unity 工程验证最小消费；目前推理测试未接入 `foundation-units`，不等同发布门禁。 |

**P0 实施批次：先处理 `protocol` 与 `diagnostics`，再收紧 `network.runtime` 会话契约，随后补齐 `gameplaytags` 和 `ai.abstractions` 的候选说明与本地回归。** 首发仍需七包整体签核。

## 其余 70 个包：按工作线分流

下表列的是工程整理归属，不是依赖层级、成熟度或发布顺序。名称均省略 `com.abilitykit.`。

| 批次 | 包 | 工作重点与验收 |
| --- | --- | --- |
| P1 宿主与协调（4） | `host`、`host.extension`、`host.network`、`coordinator` | 延续现有整理，不重做。基于 [Host Extension 专项](18-HostExtensionContractAudit.md)清理剩余原语与项目装配边界；`SpectatorWorldDriver` 先核两种消费入口。连接、世界、会话所有者分别验证关闭/释放与失败语义。 |
| P1 网络组合与适配（12） | `network.client`、`network.host`、`network.sdk`、`network.room`、`network.battle`、`network.battle.config`、`network.host.kcp`、`network.transport.inmemory`、`network.transport.kcp`、`network.transport.litenet`、`network.transport.websocket`、`protocol.room` | 按 runtime → 单端装配 → room/battle → 可选传输审计。`network.client` 的依赖包含 SDK、room、battle 和 config；先确认这是明确的聚合入口还是不必要的默认绑定，再评估是否拆分。统一以自有源码和本地回环查所有权、退出/重连与错误传播，不访问外部服务。 |
| P2 同步、快照、录制（6） | `world.framesync`、`world.statesync`、`world.snapshot`、`world.networkfragments`、`record`、`record.memorypack` | `world.snapshot` 经 fragments 间接依赖 `host` 和帧同步；先查片段数据与宿主缓冲是否可分，不能仅凭一条引用认定架构错误。`record` 是已有的录制所有者；把来源、格式编解码、存储与恢复责任写清，不移回 Core。分开测试两种同步模式，有共享改动时分别核验真实调用路径。 |
| P3 世界装配与技能基础（10） | `world.di`、`pipeline`、`triggering`、`continuous`、`ability`、`ability.explain`、`attributes`、`modifiers`、`context`、`timer` | `world.di` 先复核既有容器生命周期与已声明 MemoryPack 闭包。`pipeline` 延续 [运行与回滚专项](17-PipelineContractAudit.md)；不重复清理 Runner 已退役格式，其他仍可达 provider 按消费者分别评审。`triggering` 以[首轮证据](15-PipelineTriggeringPackageReadiness.md)为起点，继续收口公开面、注释和直接依赖；`continuous` 保留独立通用领域包，不塞回 Core。后续用 `SkillCore` 最小组合验证数据、推进逻辑与宿主适配，避免框架反向依赖 Demo。 |
| P3 流程、执行与诊断周边（9） | `compute`、`dataflow`、`behavior`、`behaviortree`、`flow`、`hfsm`、`environment`、`trace`、`hotreload` | `behavior` / `behaviortree` / `hfsm` 优先区分状态实例、执行和 Editor/生成入口；`trace` / `hotreload` 优先查生产消费者和可选性。不要仅凭相似命名合并。 |
| P3 战斗领域（8） | `combat.collision.abstractions`、`combat.damage`、`combat.entitymanager`、`combat.motion`、`combat.navigation`、`combat.projectile`、`combat.skilllibrary`、`combat.targeting` | 按目标/碰撞端口 → 移动与投射物 → 伤害/实体组合查。对外明确快照、确定性和对象寿命，补独立测试；项目策略不上移，不借结构整理修改共享玩法机制。 |
| P3 高层组合（7） | `battlescenario`、`scenario`、`actionschema`、`game.battle.runtime`、`game.view.runtime`、`gameframework`、`gameframework.network` | 先追踪实际消费，区分场景描述、可复用端口、客户端表现与旧框架装配。`game.battle.runtime` 被 `network.battle` 直接依赖，不宜按名字归 Demo；`gameframework` 优先做公开面和兼容入口抽查，避免一次性搬动大包。 |
| P4 归属优先（14） | `samples`、`world.ecs`、`world.entitas`、`world.svelto`、`compute.unityjobs`、`ai.mlagents.bridge`、`unity.pool`、`protocol.moba`、`protocol.shooter`、`protocol.editor`、`excel-sync`、`analyzer`、`actioneditor.impl`、`base.editor` | 分为示例、轻量表现层、可选后端、项目协议及开发期工具。`world.ecs` 维护其轻量表现用途，不升为首发基础件；`world.entitas`、`world.svelto`、`base.editor`、`actioneditor.impl` 直接依赖 `thirdparty.*`，独立发布前先定依赖策略，不自动迁包。生成源码改模板，项目协议有消费者时先核构建链和部署归属，不能仅凭前缀删除。 |

本次未找到名为 `com.abilitykit.world.extension` 的 UPM 包，不另排同名项目；现有世界扩展按包声明、asmdef 与实际消费者归入对应工作线。

## 各批交接清单

1. **归属**：记录用途、非职责、实际消费者和可用环境；类先明确所有者，再移动文件。对历史入口区分仍在使用、可达但未承诺、完全不可达；删除前核源码、序列化入口、生成输入和 Unity 资产引用。
2. **闭包**：对照 `package.json`、asmdef、`.csproj`、运行时与 Editor 源码，确保单一源码来源、生产包不依赖 Demo，生成产物有权威工具和可复验输入。Unity 资产移动保留 `.meta`。
3. **契约**：先锁定现有成功、错误、排序和释放行为；再按规范整理中文多行注释、包内使用说明和变更记录。保持 API、存档及协议的行为不变，除非另行批准切换契约。
4. **证据**：优先复用 `tools/test-gates.json` 中现有门禁与测试入口；记录实际执行范围、输入和结果到 `local/Logs/`。分别记录 `.NET`、Unity Editor、Player 和候选制品消费，不能互相替代。首发仍以发布清单和独立签核为准。

## P0 实施记录：协议闭包与诊断适配

- `protocol`：Unity 与 `.NET` 均已移除未使用的 Core 引用；Unity 只显式引用 Newtonsoft DLL，并禁止引擎引用。MemoryPack 反射入口仍为可选，未变更其行为、协议格式或生成物。唯一 catalog 生成入口的 `-Check` 校验通过（4 个目录、91 条消息）。
- `diagnostics`：`AbilityKit.Diagnostics` 已禁止引擎引用，`UnityRuntimeDiagnostics` 移入同包的 `AbilityKit.Diagnostics.Unity`，保持命名空间、方法、指标与现有生命周期。检索未发现仓内生产调用；外部自定义 asmdef 调用方需引用新程序集。隔离 Unity 编译还暴露 Editor 工件导出缺少 Newtonsoft 声明，已在包清单与 Editor asmdef 补齐；通用运行时不引用 Newtonsoft。
- 本地 `.NET` Release：Protocol 171 项、Network Runtime 264 项通过；Diagnostics 首跑一个并发采样用例失败，复跑 133 项通过。完整 `foundation-units` 中止于 GameplayTags 的 `ShouldRemove_IgnoresBlockOnlyRemovalRequirements`，未修改该包。详见本机 `local/Logs/framework-protocol-diagnostics/20261124-095010/`。
- Unity `2022.3.62f1`：两个隔离项目使用本地源包。Protocol 项目只解析本包和 Newtonsoft，验证 JSON 往返与未安装 MemoryPack 的失败契约；Diagnostics 项目解析本包及声明闭包，验证 Unity 适配程序集归属、停用时不写入、帧耗时单位与负内存值跳过，并调用真实 Unity 采样入口。两项通过；未验证 Player、注册表或正式候选制品。

## P0 实施记录：网络请求与协议会话释放

- `RequestClient` 将请求登记与终止移出统一到同一请求表边界。连接报告断开或错误时摘走当批待响应请求；释放同时停止新登记；已经取得完成权的响应或失败不被随后发生的发送异常覆盖。连接不会由请求客户端关闭。
- `NetworkProtocolSession` 串行化订阅变更与释放。回调可重入释放，后续尚未执行的回调跳过；会话不等待已在运行的回调。`RawConnection` 仍由调用方持有。未调整 `ConnectionManager.Close()` 的事件合同或重连策略，也未更改传输、线协议、帧同步或状态同步契约。
- `runtime-contracts` 的 `Network runtime tests` 步骤 Release 通过 270 项（含 6 个新增用例）。新增覆盖断开后重新请求、发送中释放、先终止后发送失败、回调中释放与路由/事件解绑；使用自有测试数据和本地测试替身。未运行 Unity Editor/Player 或同步实战链，不能据此声明双链已验证。日志在 `local/Logs/network-runtime-session/20261124-100431/`。
- 下游 `network-sdk` 门禁仅运行两个步骤：Host 网络集成 11 项通过；SDK 148 项通过、1 项失败。失败用例只对照内置 catalog 与解码器注册表，报告 `abilitykit.moba.battle/shield-state.push` 缺少解码器注册；不执行请求或会话释放，本批不修改该注册链。

## P0 实施记录：主动关闭与旧回调失效

- `NetworkSession` 每次启动使用独立接收周期及解码器。在调度器执行时核对周期，旧 I/O 不进入解码，旧通知及延迟管线结果不跨入新周期。关闭后不重用半帧，传输仅释放一次。已经进入的外部回调不强制中断。
- `ConnectionManager` 解绑前撤销旧会话回调，主动关闭时使二次排队的通知失效。旧断开回调重入关闭并打开新连接后，不再对新连接安排重连；保留主动关闭不产生 `Disconnected` 的旧合同。未改线协议、重连退避或任何同步/玩法机制。
- `.NET` Release：`runtime-contracts` 网络运行时 278 项通过（新增 8 项）；`network-sdk` 的宿主网络集成 11 项通过。证据在 `local/Logs/network-runtime-close/20261124-102129/`。仅执行本地测试，未运行 SDK 全门禁、Unity 或帧同步／状态同步实战链。

## P0 实施记录：标签与 AI 抽象契约

- `gameplaytags`：运行时已有「只配置禁止标签时，标签均不存在即移除」的约定，MOBA 标签作者化文档也已明确该行为。更正反向断言的旧测试，覆盖层级禁止、空容器／null、必需／禁止／查询的组合。核实 `IGameplayTagService` 接口与 Ability 实现的归属，并用 Ability 消费测试确认最后来源移除与 `ClearOwner` 聚合通知。未改变玩法机制、数据格式或 API。
- `ai.abstractions`：补中文 API 注释和包文档，区分逻辑元素类型与 `float[]` 存储、借用观测与调用方动作、策略自有状态与环境恢复。保留原接口及文件组织；在现有推理测试工程验证清零复用及 Shooter 环境的重置、步进与截断，无新依赖和测试工程。
- `.NET` Release：完整 `foundation-units` 10 步均通过（869 个用例，含 GameplayTags 228 项和 Diagnostics 133 项；另有示例宿主构建与 golden baseline）；Ability 4 项、AI Inference 11 项通过。日志位于 `local/Logs/framework-leaf-contracts/` 的本次门禁与消费测试目录。
- 注释检查：AI Runtime 和本次修改的 `ContinuousTagRequirements` 无报告项；GameplayTags 其他文件仍有 64 项存量注释问题。仓库结构检查仍报告既有 3 条 RVO2 计划断链与 4 条散落日志提醒，未改动这些文件。
- 未运行 Unity Editor／Player、候选制品安装或真实帧同步／状态同步链；本批没有修改同步路径或共享世界契约。七包仍为 `candidate`。

## P1 实施记录：MOBA 诊断解码与客户端双连接释放

- `protocol.moba` 的诊断入口统一复用本包生成的 MemoryPack 注册表，保留显式的帧输入提交及指标解码；现存注册优先、不改生成物和线格式。`network.sdk` 保持模型和玩法无关，不新增项目协议依赖。
- `network.client` 保持一站式与分步装配。交付前取消或自动挂接失败时回收所持连接；分步挂接失败保留房间连接。释放始终按战斗、房间顺序尝试；单个异常原样传播，操作和清理同时失败时保留两项异常。
- `.NET` Release 定向门禁：SDK 150 项、客户端 12 项通过。解码用例用仓内类型本地编码；生命周期用例使用内存网关和故障替身。证据位于 `local/Logs/framework-client-lifecycle/20261124-135857/`。
- 完整 `network-sdk` 门禁前五步通过，第六步 `Network room tests` 73 项通过、3 项失败：房间夹具沿用状态头或用请求序号充当响应操作码，与当前 SDK 的裸负载和响应操作码匹配规则不一致。本批未改房间实现或测试；后续步骤因门禁中止未运行。
- 未修改共享世界契约、帧同步／状态同步业务执行或服务端停机逻辑；没有执行 Unity Editor／Player 和实际战斗链路。

## P1 实施记录：房间应答夹具与门禁复跑

- 房间夹具默认返回裸负载，内存网关回送原请求的操作码与序号；创建房间同时覆盖默认策略和显式启用旧状态头，登录拒绝用例验证业务失败原因与连接释放。仅修改测试，不修改 SDK、房间运行时或线格式。
- `.NET` Release：房间专项 77 项通过。完整 `network-sdk` 前六步通过，第七步 `Network battle engine tests` 首个发送用例未发送输入，且测试进程持续等待，随后手动中止。静态核对显示该战斗夹具没有触发连接认证完成，未修改战斗实现或测试。
- 未被完整运行执行的后五步单独复跑均通过：InMemory、LiteNet、WebSocket 各 1 项，战斗配置和客户端各 12 项。完整门禁尚未通过。本地报告位于 `local/Logs/framework-room-fixtures/20261124-141409/`。

## P1 实施记录：战斗输入认证夹具与完整门禁

- 战斗连接替身由 `Connect()` 触发连接事件，应答保留操作码／序号并使用 SDK 默认裸负载。新增订阅应答前不发送输入、认证等待期间取消不发送输入两项回归。异步用例给提交等待传入 5 秒预算，并以 6 秒测试期限兜底；未改运行时、协议或门禁配置。
- `.NET` Release：战斗专项 20 项通过，完整 `network-sdk` 12 步共 313 项通过。本地证据位于 `local/Logs/framework-battle-fixtures/20261124-142636/`，不代表 Unity 或真实战斗同步链路验收。

## 稳定与扩展的推进顺序

先固定包依赖、公开契约与运行状态的所有权，再验证包外扩展，最后按已确认职责拆分实现。当前已有接口与分包，不宜仅为统一形式增加一层抽象或批量拆类；扩展能力应有真实消费路径和失败用例。

| 顺序 | 工作重点 | 完成判定 |
| --- | --- | --- |
| 1. 依赖与行为基线 | 对齐清单、程序集与 .NET 镜像，去掉证实未使用的反向依赖；把已有生命周期与异常契约放入现行门禁。 | 定向依赖检查及受影响测试通过，分别记录 Unity 独立消费与运行时证据。 |
| 2. 包外扩展契约 | 从独立程序集使用公开 API，验证创建／注册、每次执行的状态隔离、失败与释放，以及需要时的捕获／恢复。先以 Pipeline 为试点，其他模块按实际消费者推进。 | 扩展方无需改框架源码或访问内部成员，覆盖正常执行和非法状态拒绝；未覆盖作者化或生产装配时明确标注。 |
| 3. 所有权与实现拆分 | 沿 Host、DI、注册表与同步适配的真实调用路径，明确默认实例、宿主实例、释放责任和重建责任；大类只按独立职责拆分。 | 保持公开 API 与行为基线；共享世界若发生实质修改，分别验证帧同步和状态同步，机制决策另按准入规则确认。 |

## 稳定化实施记录：依赖声明与 Pipeline 包外扩展

- `ability`、`combat.projectile`、`world.snapshot`、`game.battle.runtime` 和 MOBA View 清单补齐现有程序集已经使用的直接依赖。Ability 同时核对随包 Editor，所以声明 `base.editor`；这不等于作者化依赖已经可选。MOBA View 移除无法解析的旧 `AbilityKit.HFSM` 引用，保留现有 `.Core` 与 `.Unity` 引用。没有变更玩法实现、配置或生成物。
- World DI 源码不使用 `AbilityKit.Attributes`，因此同时移除 Unity asmdef 和 .NET 镜像中的多余引用，避免世界容器因历史引用引入战斗属性领域。未改变 DI API、服务注册或状态所有权。
- 扩展现有[依赖审计](../../../tools/audit_unity_package_dependencies.ps1)：`-IncludeEditor` 检查随包编辑器，`-StrictInternalReferences` 拒绝未解析的 AbilityKit／GUID 引用；测试和可选样例不作为生产依赖。旧入口默认仍为运行时模式。五个框架包的严格检查接入现有 `core-stability`，不新增平行门禁。审计按 asmdef 检查直接依赖，不分析实际源码使用、递归安装或外部程序集完整归属；World DI 的 `Microsoft.CSharp` 保留外部归属告警。
- [Pipeline 包外阶段契约测试](../../../src/AbilityKit.Pipeline.Tests/PipelineExternalPhaseContractTests.cs)只使用公开 API，验证工厂创建的独立状态、重叠运行与暂停／取消隔离、值捕获、`AttachRun` 恢复后继续执行，以及非法自定义载荷在全树写入前拒绝。恢复不重放阶段进入动作。包内 README 和[扩展指南](../../../Unity/Packages/com.abilitykit.pipeline/Document/PipelineRuntimeDesign.md#扩展指南)补充工厂与自定义状态接入要求；未实现工厂的旧阶段仍共享对象，自定义写回失败仍无事务补偿，没有改运行时语义。
- 本次 `.NET` Release：Pipeline **37/37**（新增 5 项）、World DI **62/62**；依赖审计隔离自测 **8/8**。`core-stability` 的新定向检查及原有 16 包直接依赖检查均通过，配置校验 **203/203**。这些是单步门禁及工具自测，未执行完整 `core-stability` 或 `runtime-contracts`。证据位于本机 `local/Logs/framework-stability/20261124-201324/`；最终 Pipeline、World DI、新依赖检查与原依赖检查分别见 `20261124-203446-core-stability`、`20261124-204255-runtime-contracts`、`20261124-204255-core-stability`、`20261124-203512-core-stability` 子目录，自测见 `audit-selftest-verified.log`，配置检查见 `gate-config-report.json`。
- 本批是直接依赖与扩展协议的本地 E3 证据。未运行 Unity Editor／Player、独立 UPM 安装或两条真实同步链；未改变共享世界执行契约。下一批沿所有权和真实装配路径继续，不把本批通过表述为全框架稳定或发布验收完成。
- 仓库结构严格检查仍因 `plans/ShooterSveltoRvo2AdaptationPlan.md` 的三条既有 RVO2 目标断链而失败，另有四处历史日志提醒；本批未新增链接或编码问题，不移动仍未确认消费者的旧日志。Pipeline 随包文档检查无新增问题，完整输出见本机同目录的 `repo-structure.log`。

## 核心设计优化实施：生命周期、配置扩展与职责提取

用户确认按推荐顺序优化，并要求功能不变。本批先固定成功、失败、排序及恢复合同，再按 Pipeline → ConfigDatabase → 预测／投射物 → BehaviorTree／HFSM → ConnectionManager 推进。只开放配置表已有原位替换能力，其他新增协作类均为内部实现；未引入新的玩法、轨迹、恢复模式或协议。

| 范围 | 本批实现与保持的边界 |
| --- | --- |
| Pipeline 生命周期 | Run 在启动／附着初始化回调之后固定实际登记的注册表，后续切换只影响新登记的运行。阶段创建／重置失败、启动回调失败及开始追踪失败均归还阶段列表；已开始登记的失败启动撤销登记，保留原异常。正常完成、失败、丢弃的事件顺序及业务上下文所有权保留。省略注册表仍使用全局实例，不静默改变既有全局观察行为。 |
| ConfigDatabase | [表构建器](../../../Unity/Packages/com.abilitykit.ability/Runtime/Ability/Config/Database/ConfigTableMaterializer.cs)集中工厂及反射回退，[增量提交对象](../../../Unity/Packages/com.abilitykit.ability/Runtime/Ability/Config/Database/PreparedIncrementalTableUpdate.cs)负责目标资格与提交。公开 [IConfigTableContentReplacer](../../../Unity/Packages/com.abilitykit.ability/Runtime/Ability/Config/Database/IConfigTableContentReplacer.cs)，外部工厂表可保持原表身份进行增量替换。保留先转换全部变更、再预检全部目标、最后依序提交及发布事件的顺序；自定义替换必须无副作用且对合法源不抛异常，未承诺任意插件提交异常的跨表回滚。 |
| 客户端预测 | [PredictionWindowCalculator](../../../Unity/Packages/com.abilitykit.host.extension/Runtime/FrameSync/PredictionWindowCalculator.cs)独立计算积压、EWMA、延迟、硬上限及理想帧限制。保留浮点运算顺序、取整、窗口与停滞计数，驱动器继续拥有输入消费、世界推进及恢复调度。 |
| 投射物 | [ProjectileCollisionQueries](../../../Unity/Packages/com.abilitykit.combat.projectile/Runtime/Projectile/Runtime/ProjectileCollisionQueries.cs)隔离碰撞端口、固定分段轨迹扫描、忽略碰撞重试和帧内时间估计。保持分段数、重试数、定点 epsilon、float 查询边界及命中返回顺序；世界仍拥有弹体、事件及恢复。 |
| BehaviorTree | [TreeDebugProjection](../../../Unity/Packages/com.abilitykit.behaviortree/Runtime/Execution/TreeDebugProjection.cs)接管静态描述、动态调试全量／差量投影；移除已被 TreeTopology 替代且无调用的私有 Flatten／Visit／FindDefinition 旧链。运行栈、条件重评估、快照预检及恢复仍由原运行时负责。 |
| HFSM | [StateMachineCompiler](../../../Unity/Packages/com.abilitykit.hfsm.core/Runtime/Core/Runtime/StateMachineCompiler.cs)接管定义深拷贝、绑定解析、迁移索引和排序，内部图及并行行为每次编译独立创建。沿用 RuntimeBindings 扩展入口，定义哈希、动作次序、并行退出与快照格式不变。 |
| 网络连接 | [KickPushDecoder](../../../Unity/Packages/com.abilitykit.network.runtime/Runtime/Network/Runtime/KickPushDecoder.cs)封装现有可选踢出载荷解码。保留精简解析器、未知转义及损坏字段的兼容行为；ConnectionManager 仍负责先发 Kicked 再关闭及普通推送透传。只使用本地自造载荷测试。 |

Ability 的 `.NET` 镜像补充直接引用 `AbilityKit.Attributes`：其现有源码直接消费 AttributeContext／AttributeEffect，移除 World DI 的无关引用后不应再依赖传递引用。这是编译依赖闭包修复，不改变 Unity 已有的属性包依赖或领域行为。

本批受影响的状态账本：

| 状态／依赖 | 权威归属与恢复合同 |
| --- | --- |
| 预测帧、确认帧、输入历史、回滚游标与 EWMA | 仍归驱动器原 WorldContext；计算器没有持久状态，不持有输入、世界或回滚服务。 |
| 弹体位置、定时、命中资格、名单／分配器及快照 | 仍归 ProjectileWorld／Projectile 与原 rollback provider，字段、版本、名单顺序及恢复入口未改变。查询对象只借用原碰撞世界，无新增快照字段。 |
| 行为树运行栈、节点状态／随机流及 HFSM 活动机器／行为快照 | 仍由各自运行时独占；调试描述为派生投影。编译后的 HFSM 图仍是每个运行时独立构建，不共享状态或改变恢复次序。 |
| Pipeline 上下文与阶段列表 | 业务上下文继续由既有成功／失败清理路径或调用方负责；阶段列表由框架租借／回收。新增登记归属引用属于运行依赖，不进入进度快照。 |

本地 `.NET` Release 验证（E3，均无失败／跳过）：

| Suite | 结果 |
| --- | --- |
| Pipeline runtime | 44/44；新增切换注册表、启动回调选择归属，以及 factory／Reset／start／trace 失败资源回收用例。新增回归在修复前复现六项失败，修复后全过。 |
| Ability | 9/9；外部程序集自定义表及反射回退的原位替换、转换失败不提交、缺失替换能力的预检拒绝。 |
| Host Extension | 47/47；新增窗口 EWMA、输入延迟、最大窗口关闭及理想帧压缩的真实宿主推进断言。 |
| Combat Projectile | 97/97；现有确定性轨迹、碰撞、弹跳、命中身份、区域及恢复合同。 |
| BehaviorTree | 171/171；运行、节点库、观察与恢复等现有 suite。 |
| HFSM Core | 56/56；确定性时钟、定义隔离、绑定、层级、并行、生命周期及恢复合同。 |
| Network Runtime | 284/284；新增切片解码、缺字段／损坏字段／转义兼容、通知关闭次序及禁用处理透传。 |
| World StateSync / World FrameSync | 分别 128/128、47/47，独立执行原有门禁步骤。 |
| MOBA 定向集成 | 787/787，筛选 ConfigTableManifest／Projectile／FrameSync／StateSync；其中包含正式世界与 ClientPredictionDriverModule、正式回滚注册表的在途 Cast 重演、两次预测纠偏，以及 Jinx 投射物本地状态同步表现接纳／拒绝。FrameSync 文件名筛选本身没有命中，实际帧同步证据来自 StateSync 命名空间下的上述正式驱动重演用例，不能按目录名判同步模式。 |

回归使用现有 core-stability 单步入口及现有 Ability／MOBA 测试工程，未新建测试工程或门禁。定向框架运行时及 Editor 依赖审计通过。日志和 TRX 均在本机 `local/Logs/core-design-refactor/20261124/`；MOBA 结果见 `moba/moba.trx`，Ability 见 `ability/ability.trx`，其余见对应 gate-summary.json。没有执行完整 core-stability、Unity Editor／Player、线上联机或发布消费验收，不提升为 E4／E5。

**剩余边界**：ConfigDatabase 的多输入加载入口、预测驱动的长 OnPreTick、ProjectileWorld 的长 Tick 仍有继续拆解空间；本批先分离可独立验证的职责，没有为降低行数强拆运行状态。FrameTime 具体绑定、全局配置事件总线、轨迹扩展／插件状态恢复等契约未改，后续应逐项以明确消费者及回归再设计。结构严格检查仍只有既有三条 RVO2 断链和四项历史日志提醒，本批不代替其归属治理。

## 下一批拆分实施：客户端预测状态与推进阶段

2026-11-25 按推荐顺序实施首项预测驱动拆分。此处更新上一批“剩余边界”中的长 `OnPreTick` 与 `FrameTime` 具体绑定；ConfigDatabase、ProjectileWorld 及其他候选仍留待后续批次。本批不改变玩法机制、同步模式选择、协议、快照载荷或恢复范围。

| 对象 | 当前归属与扩展边界 |
| --- | --- |
| [ClientPredictionDriverModule](../../../Unity/Packages/com.abilitykit.host.extension/Runtime/FrameSync/ClientPredictionDriverModule.cs) | 保留 Host 生命周期、世界登记、公开查询／控制、对账入口及帧后捕获与提交。主文件从 1737 行拆至 972 行；不以总行数代替职责判定。 |
| [逐世界上下文](../../../Unity/Packages/com.abilitykit.host.extension/Runtime/FrameSync/ClientPredictionWorldContext.cs)与[驱动状态](../../../Unity/Packages/com.abilitykit.host.extension/Runtime/FrameSync/ClientPredictionDriverState.cs) | 分开运行引用、帧进度、输入历史、重演状态、哈希对账及窗口诊断；模块诊断保持原有跨世界汇总与重置口径。内部协作类不成为包外扩展 API。 |
| [推进控制器](../../../Unity/Packages/com.abilitykit.host.extension/Runtime/FrameSync/ClientPredictionTickCoordinator.cs) | 准备门禁之后仍按 remote → timeline → local → ideal 解析来源，再按结算时间线 → 权威确认 → 重演 → 本地预测推进。每世界每次 Host Tick 至多提交一次；本地队列、输入执行及结算时间线分别由内部协作类负责，无每帧控制器对象或委托链分配。 |
| [IFrameTimeAligner](../../../Unity/Packages/com.abilitykit.world.framesync/Runtime/FrameSync/IFrameTimeAligner.cs) | 新增可选能力，默认 FrameTime 实现已有 AlignTo；预测驱动只使用登记时同一 IFrameTime 对象提供的能力。只读或缺失时钟仍沿用不对齐路径，不创建第二个时钟。定点乘法及步长锁定算法不变，基线对齐仍传零备用步长。 |

受影响状态账本：

| 状态／运行依赖 | 权威归属与保持的恢复合同 |
| --- | --- |
| 世界与输入端、时钟、回滚协调器、哈希函数及 reconciler 引用 | 仍由当前世界和宿主装配；上下文只借用。世界实例登记、销毁和同 ID 重建隔离不变，引用不写入模拟快照。 |
| 确认／预测游标、已执行与权威输入历史、回滚范围及等待游标 | 仍归逐世界预测驱动；只改变字段组织。恢复帧末 N 后从 N+1 正式重演，不增加业务 Provider、历史事件补发或恢复入口。 |
| 待提交结算帧及更新句柄、原始预测后缀 | 保持记录身份校验，世界完成和快照捕获成功后才消费、发布；失败仍停在原有门禁。原始 payload 克隆与后缀连续性检查保留。 |
| EWMA、窗口、理想帧与模块诊断 | 派生调度及诊断数据，保留浮点运算、计数和重置顺序；不把它们作为权威世界状态或另一同步模式的恢复基线。 |

本地 `.NET` Release 验证共 **518 项通过，0 失败、0 跳过**（E3）：

| 范围 | 本次结果与覆盖 |
| --- | --- |
| Host Extension | 62/62；拆分前基线 47/47，新增 15 个用例覆盖默认／自定义／只读／缺失时钟、重定基线、相同输入确认不重复模拟、差异恢复后逐帧重演、门禁早于来源解析、本地输入重定向／覆盖／过期丢弃，以及两种结算路径的成功／世界失败／捕获失败与后缀重演。 |
| World FrameSync | 47/47；独立运行既有 core-stability 单步，覆盖现有定点时钟和恢复原语。 |
| World StateSync | 128/128；独立运行既有 core-stability 单步，未用帧同步结果代替状态同步验证。 |
| MOBA 定向集成 | 281/281；筛选 MobaSkillLifecycleRollbackPreparationTests、MobaLocalDualModeContractTests、PublishedHeroPredictionReplayTests、GragasQPredictionReplayTests。包含正式世界／ClientPredictionDriverModule／回滚注册表的在途 Cast、持续进度及两次预测纠偏；双模式用例分别走输入帧执行和状态投影导入。它们不是线上或 Unity 实机验收。 |

日志及 TRX 在本机 `local/Logs/prediction-driver-split/20261125/`：最终 Host 结果见 `final-host/`，两条同步原语分别见 `framesync/`、`statesync/`，MOBA 见 `moba/moba.trx`；搬移方法体和阶段分支的字段映射后词法核对见 `source-equivalence.json`。八个新增 Unity 脚本均有独立 meta GUID。包内文档已同步可选时钟契约。

未执行完整 core-stability、Unity Editor／Player、外部联机或发布制品消费，不提升为 E4／E5。结构严格检查仍报告原有三条 RVO2 计划断链和四项历史日志提醒，编码问题为零；本批不删除这些存量。下一项仍建议先拆 ProjectileWorld 的逐弹体推进职责，再评审配置加载流水线，保持同样的行为基线与回归要求。

## 下一批拆分实施：ProjectileWorld 单弹推进与碰撞响应

2026-11-25 按推荐顺序拆分投射物世界的逐弹推进。帧级 `Tick` 继续拥有活动列表遍历、swap-back 移除及对象池回收；逐弹处理负责返回／跟踪目标、速度与距离预算、生命周期及到达事件；碰撞处理负责分段扫描、命中过滤、身份去重、命中策略与反弹状态。新增协作类型和方法均为私有实现，不增加包外扩展 API、每帧委托或堆对象。

本批只移动和分离既有处理职责。定点运算表达式及顺序、碰撞查询实现、命中与退出事件顺序、swap-back 遍历、池化所有权、快照字段与恢复契约均保持不变；未新增玩法特例、Provider 或恢复状态。

| 对象 | 当前职责边界 |
| --- | --- |
| [`ProjectileWorld.Tick`](../../../Unity/Packages/com.abilitykit.combat.projectile/Runtime/Projectile/Runtime/ProjectileWorld.cs) | 仅遍历活动弹体；逐弹处理要求移除时仍在原遍历位置交换删除并回收。`float` 入口仍转换为 `Fixed64` 后调用原重载。 |
| [逐弹推进](../../../Unity/Packages/com.abilitykit.combat.projectile/Runtime/Projectile/Runtime/ProjectileWorld.TickProcessing.cs) | 处理目标丢失、返回与跟踪、定点位移、生命周期、周期事件和最大距离响应，并交由碰撞处理推进当前位置。 |
| [碰撞与命中处理](../../../Unity/Packages/com.abilitykit.combat.projectile/Runtime/Projectile/Runtime/ProjectileWorld.Collision.cs) | 使用栈上值类型传递本帧扫描状态；沿用现有碰撞查询，负责穿透上限、忽略碰撞体、命中冷却、身份过滤、退出判定及反弹目标更新。 |

本地 `.NET` Release 投射物测试为 **97/97 通过**；Release 包构建为 0 错误、8 个 NuGet 解析／目标框架兼容警告。另运行现有 `core-stability` 投射物单步时，Debug 构建在执行测试前被当前工作区 Diagnostics 包的编译错误阻断（`EditorProfiler` 的 `_scopes`／`_recordScope` 和 `fromProbe` 错误），这些文件不属于本批改动。故该门禁单步记录为失败，不把 Release 测试结果冒充为它已通过。

测试日志和 TRX 保存在本机 `local/Logs/projectile-world-split/20261125/`。结构严格检查发现 0 项暂存本地文件问题、0 个编码问题；同时保留 3 条既有 RVO2 计划链接断链及 4 项历史日志产物提醒。本批未执行 Unity Editor／Player、线上联机或发布制品消费验证，不提升为 E4／E5。下一项建议再评审 `ConfigDatabase` 多输入加载入口的职责边界，并保持输入优先级、替换时机及失败语义不变。

## 基础执行包实施：Dataflow 与 Compute

- `dataflow` 是同步处理链，`combat.damage` 通过派生管线累计领域结果。移除 Runtime 未使用的 Core 程序集和 UPM 依赖，启用 `noEngineReferences`。处理器名称读取失败时保留原异常、当前索引及此前的完整输出，不把上一阶段的名称错记为失败来源。处理器顺序快照、异形输入处理和 Context 所有权不变。
- `compute` 是应用显式装配的批处理后端链，不负责 Dataflow 调度。将可用性探测异常和执行异常统一记为 `Faulted`，保留配置顺序、观察通知与显式回退。验证器仍由调用方提供，失败不恢复调用方的输出缓冲。此次不改 `compute.unityjobs` 的作业图或原生缓冲契约。
- 新用例在修复前分别复现了旧阶段名称串入和探测异常逃逸。修复后 `core-stability` 的 Dataflow 单步 **21/21**，Compute **7/7**，伤害管线 **5/5**；三包 UPM 依赖审计及 `instance2` Unity 2022.3.62f1 Editor 批处理编译通过。未运行 Unity Jobs 作业、Player、帧同步或状态同步端到端链，也未验证独立制品安装。

## 配置库全量加载拆分

[ConfigLoadInputs](../../../Unity/Packages/com.abilitykit.ability/Runtime/Ability/Config/Database/ConfigLoadInputs.cs) 负责路径和候选来源选择；[ConfigDatabase.FullReload](../../../Unity/Packages/com.abilitykit.ability/Runtime/Ability/Config/Database/ConfigDatabase.FullReload.cs) 保留各入口的校验和异常边界，共用表暂存与提交。表转换仍由 `ConfigTableMaterializer` 承担，增量原位替换不变。

| 入口 | 保留的选择和失败行为 |
| --- | --- |
| `IConfigSource` | 完整路径字节 → 完整路径文本 → 原始表路径文本；反序列化抛异常可继续下一个候选。 |
| 文本／字节字典 | 文本优先完整路径的非空值；字节在完整键存在时不再查原始键，即使该值为空。选定输入后，解析失败不重试。 |
| 混合字典 | 先选字节；没有非空字节才选文本。字节解析失败不回退到文本。 |
| 配置组 | 当前组未找到时才按顺序查后续组；即使后续组供数，也由声明组解码，非空字节先于文本。重复声明按构建顺序覆盖。 |
| DTO 数组 | 按 DTO 类型键查找，并检查数组元素类型；非严格模式接受缺失数组，不接受类型不符。 |

`IConfigSource`、混合字典和 DTO 数组的非严格模式以空表替换缺失表，不合并旧表。全部表准备成功后才替换条目表和 DTO 表、递增一次版本，再通知订阅者；提交前失败保留旧表及版本。异常传播和失败通知仍依入口区分，不新增全局总线隔离，也不承诺表工厂副作用或订阅者异常的回滚。

2026-11-25 本地验证：拆分前配置库契约 **17/17**；拆分后 Ability **21/21**、MOBA 配置专项 **21/21**（表清单及混合配置加载），`instance2` Unity 2022.3.62f1 Editor 批处理编译退出码 0。未运行 Unity 场景、独立 Player 或两条同步链端到端验证；未改变配置、玩法或同步契约。

## HotReload 运行时与编辑器边界

`AbilityKit.HotReload` 保持程序集名与热替换、清理、重入契约，仅直接引用 World.DI、World.Entitas 和 Entitas DLL，并禁止引擎引用。配置日志监听保留脚本 GUID 和 SubsystemRegistration 注册时机，移至仅 Editor 编译的 `AbilityKit.HotReload.Editor`。因此 Ability 和 Core 仍是完整 UPM 包的依赖，但不再是该 Runtime 程序集的直接引用。未改动 MOBA 宿主的程序集选择、配置重载总线或世界执行链。

本地 `core-stability` HotReload 单步 **13/13**。使用 Unity 2022.3.62f1 编译器、已有 World 程序集与仓库 Entitas DLL 单独编译 Runtime，无引擎引用；隔离 Unity 工程以实际监听器、事件总线和日志源代码验证三次 Play Mode 进入（一次启用、两次禁用 Domain Reload），每次成功和失败通知各记录一次。`instance2` 正在使用，未重编完整工程；未验证独立 UPM 安装、DLL 装载场景或 Player。依赖审计通过 asmdef 检查，工具不识别 Entitas DLL 的所有者，已对照 `com.abilitykit.thirdparty.entitas` 中的实际文件和版本声明。

文档类型：工程审计与分批路线 | 事实基线：初始盘点 2026-11-24，各实施批次见正文 | 证据等级：全量分组为 E0；实施记录有本地契约证据 E3，不等同发布签核。

*文档版本：v2.1 | 最后更新：2026-11-25*
