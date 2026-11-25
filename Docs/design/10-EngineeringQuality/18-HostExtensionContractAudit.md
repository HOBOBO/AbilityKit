# Host Extension 职责与稳定化盘点

> 2026-11-23 源码盘点。记录仓内调用、现有契约和整改建议，不代表新增同步机制获批，也不作为发版承诺。

## 1. 先确定包边界

`host.extension` 应保留为 **Host 的可选运行时装配层**。`host` 提供宿主生命周期和模块/Feature 接口，`world.di` 管理世界的创建、登记和释放，`world.framesync`、`world.snapshot`、`world.networkfragments` 提供同步原语；本包把这些原语接到宿主帧、世界服务与会话输入输出上。项目拥有同步模式选择、玩法输入解释、房间/重连流程和装配策略。时间服务已明确世界容器所有权，见[时间与世界启动契约](../../../Unity/Packages/com.abilitykit.host.extension/Documentation~/TimeAndWorldStart.md)，不再为其他模块复制这种服务注册实现。

当前不是 Unity 引擎依赖问题：三个运行时 asmdef 都启用 `noEngineReferences`，也没有发现为本次整改拆 Unity 平台基础包的理由。实际问题是 **包内职责、程序集分区和支持范围没有对齐**。先定契约和测试，再决定哪一块需要拆；不为目录对称新增包。

## 2. 问题与优先级

| 优先级 | 源码事实 | 处理建议 |
| --- | --- | --- |
| P0：世界状态残留 | [`WorldManager`](../../../Unity/Packages/com.abilitykit.world.di/Runtime/World/Management/WorldManager.cs#L105) 先摘除再释放；[`HostRuntime`](../../../Unity/Packages/com.abilitykit.host/Runtime/Host/Framework/HostRuntime.cs#L83) 在 `Destroy` 返回后才发销毁回调，直接 `DisposeAll` 也不走此入口。[`WorldAutoStartModule`](../../../Unity/Packages/com.abilitykit.host.extension/Runtime/WorldStart/WorldAutoStartModule.cs#L64) 按 ID 保存成功记录，同 ID 重建时可能错误跳过。预测与回滚上下文还保存世界及服务引用；预测推进和 [`ServerRollbackModule.TryRollbackAndReplay`](../../../Unity/Packages/com.abilitykit.host.extension/Runtime/Rollback/ServerRollbackModule.cs#L79) 不先核对当前登记实例。服务端回滚卸载只解绑回调，不清 `_contexts`。 | 不改 Host 通知和广播，分别验证各模块只消费当前登记的同一实例、卸载后停止服务，以及同 ID 新代不继承旧状态。先做自动启动和服务端回滚，预测记账单独覆盖。 |
| P0：宿主帧与世界帧提交混用 | [`FrameSyncDriverModule`](../../../Unity/Packages/com.abilitykit.host.extension/Runtime/FrameSync/FrameSyncDriverModule.cs#L221) 逐会话检查守卫并广播，只要有会话提交（或没有会话），就发一次不含世界身份的 `PostStep`。回滚捕获随后枚举自己的所有上下文，再查守卫；这不等同于消费每个世界本轮的提交凭据。已有失败帧守卫并非缺失，缺的是跨模块对“本轮完成了哪一个世界的哪一帧”的明确契约。 | 先用双世界回归固定现状：一方失败、另一方继续时分别观察时钟、快照、输入历史、回滚捕获与广播。若需改变帧号/通知/捕获时点或失败传播，先提方案，不顺手改协议。 |
| P1：安装隐式有序 | `ServerFrameTimeModule` 安装时探测帧事件，没有则走宿主兜底；`ServerRollbackModule` 找不到帧事件则抛错。`ClientPredictionDriverModule` 还注册宿主 Tick Gate。`HostRuntimeModuleHost` 正序装、逆序拆；[`HostRuntimeFeatures`](../../../Unity/Packages/com.abilitykit.host/Runtime/Host/Framework/HostRuntimeFeatures.cs#L21) 同类型注册覆盖，卸载按类型移除。它们不是可以任意排列、重复安装或热替换的一组模块。 | 写清依赖、互斥项、创建世界前的安装顺序及逆序关闭；测试正式组合和错误配置。不把本批扩大为 Host Feature 注册机制重写。 |
| P1：预测职责堆叠 | [`ClientPredictionDriverModule`](../../../Unity/Packages/com.abilitykit.host.extension/Runtime/FrameSync/ClientPredictionDriverModule.cs) 超过 1,700 行，既接 Host Hook，又处理输入排队、预测窗口、确认时间线、回滚/对账和诊断。`WorldContext` 内同时存帧游标、待提交数据及服务引用；宿主级统计还与逐世界统计混在同一个外壳。 | 不先拆包或统一 MOBA 与 Shooter 算法。先以现有对账、重定基线和失败帧测试锁住行为，再梳理状态归属，抽内部职责，保留公开 Feature、默认参数和执行顺序。 |
| P1：Unity 与 .NET 支持面漂移 | `Runtime` 有 41 个 C# 源文件；[.NET 工程](../../../src/AbilityKit.Host.Extension/AbilityKit.Host.Extension.csproj) 的 `Compile` 条目匹配 32 个。缺的是两份客户端 CatchUp 文件、四份共享 CatchUp 文件、一份服务端输入历史、观战驱动和远端输入队列。Unity 把同一包分为三个程序集，.NET 则把已选源码编进一个程序集。另一方面，客户端和服务端主驱动仍在 Unity 的基础 asmdef 下。 | 先记录支持矩阵，再决定哪些活跃类型补进 .NET。把编译文件清单、直接依赖和程序集归属作为发布检查；移动类型跨 asmdef 不是普通文件整理。 |
| P2：文档和目录带有早期结构 | 按 `Runtime/Moba` 写的房间与开局文档已随 MOBA 宿主包归档，其余 `Document/` 与 `Runtime/FrameSync` 仍有并列的早期设计文档。`FrameSyncDriverModule.cs` 原先混放的追帧和输入缓冲类型已按职责拆分。 | 先标注旧设计并指向现行入口；同程序集内按职责拆文件和统一中文 XML 注释，保留命名空间、类型名和现有 `.meta`。不因旧文档或目录名直接删类型。 |

上述 P0 风险来自源码路径核对；既有正常路径通过不等于已覆盖异常释放、同 ID 换代或混合世界提交。

## 3. 首发支持候选与退役候选

| 能力 | 仓内采用证据 | 建议 |
| --- | --- | --- |
| 时间、自动启动、帧同步驱动、客户端预测、服务端回滚 | MOBA 的 `MobaHostRuntimeBuilder` 装配服务端驱动和世界时间，并可选开启回滚；客户端 `RemoteDrivenRuntimeModuleFactory` 和 `ConfirmedAuthorityRuntimeModuleFactory` 消费预测驱动。 | 保留；回滚标为可选，明确安装顺序、关闭语义和已验证装配。默认只承诺通过验证的配置，不宣称任意多世界或热替换。 |
| `Server/BattleHost`、帧包会话装配 | Orleans 的 `BattleLogicHostGrain` 用 `BattleTickDriver`、`BattleSnapshotPublisher`；MOBA 的 `BattleSessionNetAdapter` 消费双世界帧包适配。 | 保留活跃原语；双世界装配已迁至 MOBA 客户端，不纳入框架公开面。 |
| 观战、远端输入队列、补帧原语 | `SpectatorSessionRuntime` 用 `SpectatorWorldDriver`；Shooter 的 `ShooterRemoteInputSubmitStrategy` 用 `RemoteClientInputSubmitQueue`，网关还调用 `WorldStartFrameCatchUpCalculator`；MOBA 的 `SessionWorldCatchUpController` 用 `WorldCatchUpDriver`。 | 保留，不因 .NET 镜像缺席或 CatchUp 旧模块没接线而删除。独立验收，不与重连闭环混为一谈。 |
| 旧 `FrameSyncCatchUpClientModule` 及配套消息、策略、历史 | 在 `Unity/Packages`、`Unity/Assets`、`src`、`Server`、`tools` 的包外 C# 源码中未找到这组模块和历史接口的装配；Orleans 有独立 CatchUp 合约，不能凭类型同名判定是该实现。 | 不列入首发承诺。保留期间明确实验/待退役状态；删除前另核对公开 API、包外采用、序列化或反射入口，并保留已接线的补帧原语。 |

## 4. 建议实施顺序与验收

两批已分别修复自动启动/服务端回滚、客户端预测/帧同步会话的实例核对与失效清理；前文的问题描述保留为盘点时的源码证据。双世界诊断已完成，逐世界提交方案待确认。

1. **先收敛世界生命周期。** 给自动启动和服务端回滚补释放异常、直接 `DisposeAll`、同 ID 重建及安装/卸载的定向用例。旧世界不得再被启动、恢复、重演或捕获；新世界按自己的处理器与历史运行。之后给预测上下文和帧同步会话补同类测试，区分已绑定世界与显式登记的无世界会话，不误删后者。保留时间模块既有 `WorldTimeLifecycleTests` 作为防回归基线。
2. **再锁帧契约与分离内部职责。** 固定确认帧、预测帧、重演范围、守卫失败、跨世界隔离的边界测试；状态账本分别标明模拟权威状态、预测/回滚缓存、临时提交数据和运行引用。捕获完整帧末 N，重演从 N+1 开始，不补发历史玩法事件。行为改变按 [MOBA 机制治理](../../MobaMechanismGovernance.md) 另行确认。
3. **最后拉齐发版面。** 维护 Unity 和 .NET 支持矩阵，优先覆盖已接线的观战/输入队列等能力；按矩阵验收实际程序集消费者，分批处理旧 CatchUp 和旧文档。先运行 `AbilityKit.Host.Extension.Tests` 与 Shooter 定向回归；共享执行链改动再补 MOBA 正式装配与回滚定向，以及 `Unity-Instance2` 上的 EditMode 消费验证。现有两项 Unity 回归不是全包支持证明。

## 5. 双世界提交诊断（2026-11-23）

[诊断用例](../../../src/AbilityKit.Host.Extension.Tests/FrameCompletionGuardTests.cs) 使用真实 `WorldManager`，按帧同步驱动 → 世界时钟 → 服务端回滚的相对顺序装配；两个探针世界从帧 1 正常推进，帧 2 注入故障。前两项交换失败世界的登记顺序，采样世界输入、帧包、时钟、回滚捕获及宿主 `PostStep`。这是框架级可复现诊断，不等同于生产场景复现。

| 条件 | 当前实测 |
| --- | --- |
| 两个世界都暴露帧守卫，其中一个在模拟中抛错并标记失败（两种顺序） | 失败世界的时钟、快照读取、广播和回滚捕获停在帧 1，无法恢复帧 2；健康世界及宿主 `PostStep` 推进至帧 3。失败世界的帧 2 输入已经发出 `InputsFlushed`，该回调在模拟前触发，不能视为提交结果。 |
| 失败世界没有暴露帧守卫 | `WorldManager` 隔离异常，健康世界继续，但失败世界也推进时钟、读取快照、广播并捕获了帧 2。生产 MOBA 有自己的 `IWorldFrameGuard`；本项复现的是通用装配缺少守卫时的风险。 |
| 模拟完成，但启用回滚的世界在该帧捕获失败 | 失败捕获未落库，帧 2 无法恢复；该帧仍已广播，时钟也已推进。健康世界正常提交。此例不同于模拟失败，是否阻断发布需要明确策略。 |

根因：`WorldManager.Tick` 隔离异常但不向 Host 返回逐世界结果；`FrameSyncDriverModule` 在守卫缺席时没有独立成功证据，先广播再发宿主级 `PostStep`。时钟与回滚各自枚举世界并检查守卫；回滚捕获结果不会反馈给已经执行的发布。现状诊断用例通过仅代表复现，不表示后两种行为是应保持的契约。Host Extension 40/40 通过，含新增 4 项诊断；本批无运行时代码或 Unity 装配变更，未重跑 Unity。

### 待确认：逐世界提交与失败策略

1. **区分宿主步进与世界结果。** 建议由实际推进世界的入口报告本轮的世界实例与帧结果；绑定世界只有在本轮 Tick 成功、实例仍有效且现有守卫允许提交时才可发布。未执行或执行失败都不算成功；无法提供结果的自定义管理器不纳入首发的严格多世界支持，不能用全局 `PostStep` 或 `CanCommit` 单独推定成功。保留 `PostStep` 的宿主语义、显式无世界会话及网络帧包格式。
2. **把必需回滚捕获与发布前置条件对齐。** 建议仅在开启回滚、且命中捕获间隔的帧上要求捕获成功后才发布该世界的帧；快照仍捕获正确帧末时钟及既有 Provider 状态。捕获失败时该世界不发布、不继续驱动，等待明确恢复或重建；不伪造快照，也不假装回退了已产生的模拟副作用。关闭回滚和未到捕获间隔的帧不新增捕获依赖，健康世界不被拖停。
3. **实施门槛。** 确认后再设计结果的传递点和错误报告，核对 MOBA 实际装配、宿主级订阅者、帧包/快照顺序及捕获时钟。验收覆盖无守卫抛错、未执行但守卫可提交、捕获失败、关闭回滚、非捕获帧、无世界会话及健康世界连续性。两条机制可分开确认；本批不修改生产代码、通知、帧号、协议或恢复语义。

## 6. 战斗流程与协议分层复核（2026-11-23）

落地文档与诊断入口：[Host Extension 战斗流程与协议边界](../07-NetworkSynchronization/17-HostExtensionBattleProtocolBoundaries.md)。本批固定首发职责，不修改运行时或线协议。

本节审查的是接入者如何从上行命令得到权威战斗结果，而不是重复世界实例清理。当前没有理由把英雄、技能、房间规则或具体 wire schema 移入 `host.extension`。主要问题是部分通用入口把特定装配的假设隐含在名字和默认行为中。

### 实际链路与权威边界

- **通用宿主链**：[帧驱动](../../../Unity/Packages/com.abilitykit.host.extension/Runtime/FrameSync/FrameSyncDriverModule.cs#L142) 将输入入队、在宿主帧前提交给世界输入端口，帧尾将输入批次和可选快照包装为 `FramePacket`。这是帧输送能力，不能把该输入列表当成逐命令执行回执。
- **MOBA Orleans 结算链**：[战斗宿主](../../../Server/Orleans/src/AbilityKit.Orleans.Grains/Battle/BattleLogicHostGrain.cs#L654) 核对世界、运行代际和帧，返回执行、拒绝、重复等逐命令结果及领域决策。[帧同步 Grain](../../../Server/Orleans/src/AbilityKit.Orleans.Grains/FrameSync/BattleFrameSyncGrain.cs#L598) 先把结果提交到结算历史，再通知结算帧观察者；客户端[按装配提供结算帧源](../../../Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Client/Session/Features/FrameSync/RemoteDrivenRuntimeModuleFactory.cs)。这不是通用 `FramePacket` 的另一种序列化，也不能用通用输入批次替代结算记录。
- **Shooter 状态同步接入**：使用[远端输入提交策略](../../../Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Unity/PlayMode/ShooterRemoteInputSubmitStrategy.cs#L35) 和项目自己的同步控制器，不需要复用 MOBA 的双世界帧输入分发。房间加载、权威开局和重连协商分别归网关/房间及项目会话；包内 `WorldAutoStartModule` 只是调用世界提供的启动处理器，并不代表全员就绪或服务端允许运行。

### 不合理之处与建议

| 优先级 | 问题与源码证据 | 推荐边界 |
| --- | --- | --- |
| P0：接收与结算混用 | `FrameSyncDriverModule.SubmitInput` 返回真只表示绑定会话接受排队；`clientId` 在该方法内只用于拒绝日志，没有核验它与 `input.Player` 的归属。输入汇集后会被放进下行帧包，而不是按实际运行结果筛选。Orleans 结算链另有明确的逐命令结果。这里是直接接入通用入口的契约风险，不是说正式网关没有校验。 | 区分传输接收、权威排帧、玩法执行结果。会话适配负责把可信身份与命令绑定，领域执行者决定能否移动、施法并产生结果；不要为通用输入队列补一套技能校验。文档和示例不得把入队成功当成技能已执行。 |
| P0：适配器隐藏双世界策略 | [`FramePacketNetAdapter`](../../../Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Client/Session/FramePacketNetAdapter.cs#L67) 同时负责缓冲初始化、输入双写和快照路由：任一世界存在就进入双缓冲逻辑，两套默认缓冲均允许缺帧填空；读包时没有在该入口核对包世界身份，输入处理异常被记录后，外层仍可继续喂快照。当前 MOBA 会话确有调用，不能视为无用；这也不是已实测的串场缺陷。 | 已原样迁入 MOBA 会话。它是特定帧包接入配方，不是所有模式的默认协议。先核对上游世界/代际过滤，以及谁有权把空帧认定为权威无输入。明确输入与快照能否独立接收及如何报告部分成功；不能通过换一种默认补帧模式直接修所有组合。 |
| P1：快照与输入的复用层混杂 | [`FrameSyncDriverModule`](../../../Unity/Packages/com.abilitykit.host.extension/Runtime/FrameSync/FrameSyncDriverModule.cs#L268) 在批量快照模式把同一帧拆成多包，首包带输入，后续包仅带快照；无快照时也发输入帧。MOBA 接入另有聚合后再适配的路径。单个包里的空数组因此不能独立充当「已确认无命令」的通用证明。 | 为帧输入、快照与权威结算分别记录生产者、帧键、去重及消费条件；允许既有传输合包，不必为分层改 wire 格式。先测同帧多包、重排/重复、缺失与真实空帧。 |
| P1：重连旧入口给出过强承诺 | 未找到 [`FrameSyncCatchUpClientModule`](../../../Unity/Packages/com.abilitykit.host.extension/Runtime/Client/FrameSync/CatchUp/FrameSyncCatchUpClientModule.cs#L39) 的仓内正式装配。`DecideCatchUp` 将无需补帧和需要快照都投影成 `null`，`TryCatchUp` 把它当成功；`ApplyCatchUpPayload` 用输入数组长度计算目标与预算，未将数组内容提交给世界，且硬编码 30 Hz。 | 不将旧入口纳入首发重连契约，也不把它接入正式恢复。保留活跃补帧原语；真正恢复需要校验战局/代际、可信基线和连续后缀，无法恢复时显式回到全量基线。退役旧入口与替换协议分开评审。 |
| P1：输入背压有丢弃语义 | [`RemoteClientInputSubmitQueue`](../../../Unity/Packages/com.abilitykit.host.extension/Runtime/Client/StateSync/RemoteClientInputSubmitQueue.cs#L64) 保留有界在途请求与一个最新待发项；等待项可被替换。这是已被 Shooter 使用的策略，不等于所有战斗命令都能用该队列。 | 首发说明哪些调用允许覆盖。持续状态可能只关心最新值；必须逐条处理的一次性操作需要另外核对去重、回执与重发，不把它们无差别迁入 latest-wins 队列。 |

**首发建议：** `host.extension` 承诺宿主步进、输入调度/缓冲、可选预测与回滚的装配原语，不承诺一套适用所有玩法的端到端协议。先给「通用帧包装配」「MOBA 结算帧」「Shooter 状态同步」各画一条权威链和消息语义表，标注入口信任、帧键、结果及恢复来源；再补错战局/代际、同帧多包和真空帧对照等契约诊断。有正式消费者的路径继续保留，旧补帧协议先排除在发布承诺之外。任何协议、补帧或玩法结算语义变动须按治理规则单独确认；本节只做源码复核。

## 7. 框架原语与项目装配的归属（2026-11-24）

房间、开局、网络请求绑定等已在 [`com.abilitykit.demo.moba.host`](../../../Unity/Packages/com.abilitykit.demo.moba.host/package.json)。早期房间与开局说明随该包[归档](../../../Unity/Packages/com.abilitykit.demo.moba.host/Documentation~/Historical/MobaExtensionDesign.md)。首批迁出 MOBA 客户端的双世界帧包装配，并分离帧同步原语源码；不修改协议或推进策略。

| 对象 | 当前证据与首发建议 |
| --- | --- |
| `FrameSyncDriverModule`、`ServerRollbackModule`、时间与世界启动钩子 | 保留。它们操作宿主、世界端口、时钟和快照，不决定房间或玩法语义。`RemoteClientInputSubmitQueue`、`ClientPredictionReconciliationCoordinator` 也可作为原语，但调用者自行选择是否允许覆盖输入和如何对账；Shooter 已使用。 |
| `FramePacketNetAdapter` | 已迁至 `demo.moba.view.runtime` 客户端会话层并设为内部类型，与 `BattleSessionNetAdapter` 共用会话上下文；旧框架适配接口不再导出。通用 `FramePacket`、缓冲和快照路由留在各自框架包；双写、缺帧、异常及路由行为不变。 |
| `WorldCatchUpDriver`、`FrameSyncInputHubFactory`、`FrameJitterBufferHub<TFrame>` | 已从 `FrameSyncDriverModule.cs` 按追帧、输入缓冲职责拆文件；命名空间和公开签名不变，Unity 与 .NET 均包含相同三种类型。 |
| `SpectatorWorldDriver` | 暂不随第一批搬。仓内生产调用只见 MOBA 客户端，但类型同时提供原始输入驱动和通用结算端口驱动。先核对两种入口的消费与生命周期，再决定哪些保留为驱动原语、哪些随 MOBA 会话绑定；不因当前只有一个使用方就把两条路径一起下放。 |

不新增中间包，也不让框架反向依赖示例。遇到第二个独立消费者，再根据实际世界拓扑和缺帧语义决定是否提炼通用装配。
