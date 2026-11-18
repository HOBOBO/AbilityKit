# Moba Runtime 阶段性上线化排查

> 范围：`Unity/Packages/com.abilitykit.demo.moba.runtime`
>
> 目标：从 demo 代码向正式上线项目演进前，按构建逻辑层、进入战斗、接受输入、执行逻辑、快照输出的完整流程，梳理当前不规范点、铺量风险和后续优化方向。

## 0. 本轮 P0 推进状态

本轮已按“先阻断不确定状态”的原则完成第一批 P0 落地，重点不做大规模重构，而是先把会导致正式战斗漏快照、错帧执行、半初始化开局和核心占位逻辑误放行的问题收紧。

已完成：

1. 快照批量收集语义修复：新增 moba runtime 内部批量接口，由 Router 一次遍历 emitter，IO 端不再通过重复调用单快照接口来模拟批量读取。
2. 输入帧协议收敛：Driver 默认把外部命令提交到下一模拟帧，并保留显式 target frame 重载；输入协调器开始拒绝负帧和过期帧。
3. 开局初始化失败处理收敛：WorldInitData 增加 opcode 校验，开局归一化异常不再静默吞掉，缺 ActorEntityInitPipeline 时阻断开局。
4. 核心 placeholder 阻断或显式标记：资源扣除接入真实 ResourceContainer；未接真实服务的 Buff/HP predicate 改为告警并返回 false；伤害减免和护盾 pass-through 增加运行时告警。

本轮继续推进的正式化改造：

1. 协议入口增加 `MobaProtocolValidation`，把 create-world、enter-game 的必填字段、帧率、输入延迟、玩家列表、本地玩家归属、出生位标记等校验集中到协议层。
2. 开局校验拆成 envelope 校验和 resolved 校验：进入配置归一化前只校验协议外壳和可路由字段，归一化后再校验技能、属性模板、普攻技能等完整 loadout 字段，避免误拒绝可由配置补齐的请求。
3. `MobaCreateWorldInitCodec.TryDeserialize` 输出失败原因，`WorldInitStage` 在写入 start spec 前阻断非法 init payload。
4. `MobaEnterGameFlowService` 新增 `TryApplyGameStartSpec` 和 `MobaGameStartResult`，将开局失败从单个 bool 升级为结构化失败码，覆盖协议非法、重复开局、缺 ActorContext、缺 ActorEntityInitPipeline、构建失败、快照发布失败等情况。
5. `StartGameStage` 和 legacy `MobaSpawnService` 改用结构化开局结果；legacy fallback 不再使用固定 `default` 玩家作为本地玩家，而是绑定到首个 spawn 的 playerId。
6. `EnterMobaGameRes` 源码字段顺序调整为与 `MemoryPackOrder` 一致，未改变已有 wire order。
7. 进入战斗参数组装边界迁到 `AbilityKit.Host.Extensions.Moba`：host/test/server 侧负责把房间、匹配或测试 spawn 数据组装为 start plan 和 `WorldInitData`，runtime 只消费 init data 并执行战斗世界开局。
8. `MobaBattleStartPlanBuilder` 增加 host spawn 到 start plan / `WorldInitData` 的标准入口，runtime session host 和 legacy spawn fallback 都转为复用 host.extension 的统一组装逻辑。

验证结果：

- 已执行 `dotnet build Unity/AbilityKit.Protocol.Moba.csproj --no-restore`，结果为 0 errors。
- 已执行 `dotnet build Unity/AbilityKit.Host.Extensions.Moba.csproj --no-restore`，结果为 0 errors。
- 已执行 `dotnet build Unity/AbilityKit.Demo.Moba.Runtime.csproj --no-restore`，结果为 0 errors。
- 当前仍有 protocol、host extension moba、runtime 工程 warnings，主要是 Unity 生成工程里的引用版本冲突、生成代码 nullable 批注和 World DI 字段注入导致的未赋值警告，未发现本轮改动引入的编译错误。

## 1. 当前主链路概览

### 1.1 构建逻辑层

外部 Session 通过 `MobaSessionCoordinatorHost` 创建逻辑世界宿主：

- `CreateLogicWorldHost` 内部创建 `WorldTypeRegistry`、注册 `MobaWorldBlueprintsRegistration`，再创建 `WorldManager` 和 `HostRuntime`。
- `ConfigureLogicWorldOptions` 将 `WorldId`、`WorldType`、`WorldInitData` 等写入 `WorldCreateOptions`。
- World Blueprint 通过 `MobaLogicWorldBlueprintBase` 组织 Profile、Feature 和模块装配。
- `MobaWorldBootstrapModule` 作为主要世界模块，接入 Bootstrap Flow、Entitas 系统安装和自动系统扫描。
- `MobaServicesAutoModule` 通过命名空间扫描注册 Application、Systems、Infrastructure 服务。

整体方向已经从 demo 的集中式启动逻辑，逐步迁移成 Blueprint + Module + Flow + Service 扫描结构，这是正式化的正确方向。但当前仍有一些生命周期、扫描边界、静态状态和 fallback 行为需要收敛。

### 1.2 进入战斗

进入战斗链路当前为：

1. host/test/server 侧在 `AbilityKit.Host.Extensions.Moba` 中把房间、匹配或测试 spawn 数据组装为 `MobaBattleStartPlan`。
2. `MobaBattleStartPlan` 统一转换为带 `MobaWorldBootstrapModule.InitOpCode` 的 `WorldInitData`，再注册到 `WorldCreateOptions.ServiceBuilder`。
3. `WorldInitStage` 读取并反序列化 init payload，生成 `MobaGameStartSpec`。
4. `StartGameStage` 从 `MobaGameStartSpecService` 取出 spec，并调用 `MobaEnterGameFlowService.TryApplyGameStartSpec`。
5. `MobaEnterGameFlowService` 校验开局请求，调用 `ActorSpawnPipeline` 构造 actor，并通过 `ActorEntityInitPipeline` 初始化属性、资源、技能。
6. 开局后发布 EnterGame 和 ActorSpawn 快照，并设置 `MobaGamePhaseService` 为 InGame。

这条链路已经具备正式战斗开局的骨架。本轮已完成基础协议校验、结构化开局失败结果，以及进入战斗参数组装职责迁移。`MobaSessionCoordinatorHost` 现在只消费显式 `MobaPlayerLoadout`，并通过 host.extension 的 `MobaBattleStartPlanBuilder` 生成 `WorldInitData`；旧的默认 loadout/start spec 填充和 runtime 侧 spawn 转换包装已经移除。后续还需要继续收敛 opCode/payload 版本化、gateway wire schema 和跨端错误码映射。runtime 内部应保持为“消费 start spec 并执行战斗世界开局”，不再承载房间、匹配、测试环境如何拼装入场参数的业务决策。

### 1.3 接受输入

输入链路当前为：

1. 外部通过 `MobaBattleDriverHost.SubmitCommands` 提交 `PlayerInputCommand`。
2. `MobaBattleDriverHost` 使用当前 `_currentFrame` 将输入提交给 `IMobaBattleInputPort`。
3. `MobaBattleIOPort` 转交给 `IMobaInputCoordinator`。
4. `MobaInputCoordinator` 直接分发给按 opcode 注册的 `IMobaInputCommandHandler`。
5. 当前主要处理器包括移动输入 `MobaMoveInputCommandHandler` 和技能输入 `MobaSkillInputCommandHandler`。

输入层已从 Driver 和 ECS 系统中抽出独立端口与协调器，命令必须通过显式 contract/handler 进入正式分发链路。正式化前仍需要继续明确帧语义、过期/未来输入策略和高频日志策略。

### 1.4 执行逻辑

执行链路当前为：

1. `MobaBattleDriverHost.Step` 递增 `_currentFrame`，累计 `_logicTimeSeconds`，然后调用 `HostRuntime.Tick(deltaTime)`。
2. `MobaWorldBootstrapModule.Install` 通过 `AutoSystemInstaller` 扫描并安装系统。
3. 系统通过 `WorldSystem`、`WorldSystemPhase` 和 `MobaSystemOrder` 控制执行阶段和顺序。
4. 技能、效果、Buff、持续行为、投射物、召唤物、实体清理等都挂在同一套世界 Tick 中推进。

执行层已有统一顺序表，但系统安装依赖扫描规则，且部分系统内仍存在吞异常、placeholder、静态缓存和模板残留。

### 1.5 快照输出

快照链路当前为：

1. `MobaBattleDriverHost.TryGetSnapshot` / `CollectSnapshots` 调用 `IMobaBattleOutputPort`。
2. `MobaBattleIOPort` 转给 `IWorldStateSnapshotProvider`。
3. `MobaSnapshotRouter` 聚合多个 `IMobaSnapshotEmitter`。
4. 各 emitter 输出 EnterGame、ActorSpawn、ActorDespawn、ProjectileEvent、AreaEvent、DamageEvent、StateHash、ActorTransform 等快照。

当前快照类型已经比较完整，但 provider 的单次读取语义和多 emitter 批量收集方式存在不稳定风险，需要优先收敛。

## 2. 高优先级风险

### P0-1 快照批量收集语义不稳定

相关代码：

- `MobaBattleIOPort.CollectSnapshots` 重复调用 `_snapshots.TryGetSnapshot(frame, out snapshot)` 收集多个快照。
- `MobaSnapshotRouter.TryGetSnapshot` 每次只返回第一个能产出快照的 emitter。
- 多个 snapshot service 使用 `_lastFrame`、`_sent`、清空 buffer 等方式控制同帧输出。

问题：

当前结构把“是否消费快照”“同帧是否只能读取一次”“一个 frame 是否允许多个快照类型”混在 `TryGetSnapshot` 中。`CollectSnapshots` 试图通过 while 循环重复读取同一 frame，但 Router 每次从第一个 emitter 开始询问，若 emitter 内部用 `_lastFrame` 阻止重复读取，可能出现只读到第一个快照、读不到后续 emitter、或者不同 emitter 顺序依赖的问题。

铺量风险：

- 客户端可能漏收关键事件，如伤害、投射物、出生、状态哈希。
- 回放和状态同步难以复现，因为快照读取本身带消费副作用。
- 多个消费者读取同一 frame 时结果可能不一致。

建议：

- 将 `IWorldStateSnapshotProvider` 扩展出明确的批量接口，如 `CollectSnapshots(FrameIndex frame, IList<WorldStateSnapshot> snapshots, int maxSnapshots)`。
- 由 `MobaSnapshotRouter` 一次遍历所有 emitter，而不是让外层重复调用 `TryGetSnapshot`。
- 明确 snapshot emitter 的协议：读取是否消费、同帧是否可重复读取、同类型是否允许多条。
- 对事件类快照和状态类快照拆开语义：事件可消费，状态应可重复读取或按版本读取。

### P0-2 输入帧和 Tick 帧可能存在 off-by-one 语义错位

相关代码：

- `MobaBattleDriverHost.SubmitCommands` 使用当前 `_currentFrame` 提交输入。
- `MobaBattleDriverHost.Step` 先 `_currentFrame + 1`，再执行 `HostRuntime.Tick(deltaTime)`。
- Tick 后按 `_currentFrame` 拉取 transform snapshot。

问题：

输入提交帧与实际执行帧之间没有明确协议。如果外部在 Step 前提交输入，则输入标记为旧 frame，但 Step 执行的是新 frame；如果外部在 Step 后提交输入，则输入可能进入刚执行完的 frame。当前代码没有看到延迟帧、目标帧、乱序策略或帧门控。

铺量风险：

- 锁步、回放、状态校验时会出现输入归属帧不一致。
- 线上问题难以复现，尤其是网络输入和本地模拟输入混合时。
- 快照 frame 与输入 frame 对不上，排查成本高。

建议：

- 明确 Driver 协议：输入提交的是 target frame、current frame 还是 next frame。
- `SubmitCommands` 建议接受外部 frame，或统一提交到 `_currentFrame + inputDelay`。
- 在 `MobaInputCoordinator.CanSubmit` 增加帧合法性校验、过期输入丢弃、未来输入缓存策略。
- 在日志和快照中保留 input frame、simulation frame、snapshot frame 三者关系。

### P0-3 开局初始化允许静默降级（已部分落地）

相关代码：

- `MobaEnterGameFlowService` 中 `_config`、`_generator` 为 optional。
- `_generator == null` 时 actor 仍会创建，只跳过 `InitializeFromLoadout`。
- `WorldInitStage` 反序列化失败或缺 `WorldInitData` 后只 log 并 return。

问题：

正式战斗中，配置库、实体初始化器、开局协议都是必需条件。当前部分依赖缺失或协议错误时，只记录日志然后继续启动，可能产生“战斗已经进入，但实体属性/技能/资源不完整”的半初始化状态。

铺量风险：

- 线上房间可能进入不可恢复的异常状态。
- 玩家表现为无法移动、无法放技能、属性为默认值，但服务端没有明确失败码。
- QA 和线上排查只能从后续症状反推启动问题。

建议：

- 区分 Demo/Lobby/正式 Battle world 的启动策略。正式 Battle world 缺必需依赖应 fail-fast。
- `WorldInitStage` 校验 `init.OpCode == MobaWorldBootstrapModule.InitOpCode`。（已落地）
- `WorldInitStage` 对 init payload 执行协议 envelope 校验，阻断非法 create-world 数据进入 start spec。（已落地）
- `MobaEnterGameFlowService.TryApplyGameStartSpec` 返回结构化结果，而不是只返回 bool。（已落地）
- runtime 侧默认 loadout/start spec 归一化器已删除，开局数据必须由 host.extension 或外部正式协议显式提供。

### P0-4 玩法核心仍存在 placeholder 逻辑

相关代码：

- `CombatPredicates` 中目标、Buff、HP 等条件存在 TODO/临时值。
- `ConsumeResourcePlanActionModule` 资源扣除尚未实现。
- `DamagePipelineService` 中 mitigate、shield 等阶段仍是 placeholder。

问题：

触发器条件、资源扣除、伤害结算属于战斗正确性的核心路径。如果这些位置继续以占位逻辑运行，后续技能越多，错误会以配置问题、技能问题、同步问题等形式扩散。

铺量风险：

- 技能配置看似生效，但条件判断与实际战斗状态不一致。
- 资源未扣除导致技能无限释放，或者客户端表现与逻辑服不一致。
- 伤害链路无法承载护盾、减伤、免伤、反伤等正式玩法。

建议：

- 把 predicate、resource、damage mitigation 标记为正式战斗上线阻断项。
- 在配置加载或技能预热阶段扫描禁用 placeholder action/predicate。
- 为核心 predicate 和 damage pipeline 增加最小回归测试。

## 3. 中优先级风险

### P1-1 服务和系统注册过度依赖命名空间扫描

相关代码：

- `MobaServicesAutoModule` 使用 namespace prefix 注册服务。
- `MobaWorldBootstrapModule` 使用 `AutoSystemInstaller` 扫描 `AbilityKit.Demo.Moba` 和 Projectile 命名空间。
- `MobaLogicWorldFeatures` 声明了 Feature 位，但仍需确认 Feature 是否完整控制模块安装。

问题：

命名空间扫描降低了样板代码，但正式项目增长后，新服务或系统如果放错 namespace，可能静默不注册；旧系统如果仍在扫描范围内，也可能被误安装。

铺量风险：

- 功能在编辑器能跑，打包或拆 asmdef 后注册失败。
- 多 WorldType/Profile 无法清晰控制启用模块。
- Legacy 系统难以逐步下线。

建议：

- 为关键服务增加启动期自检清单。
- 将 Feature 位与模块安装绑定，避免“声明启用”和“实际扫描”脱节。
- 对系统扫描输出排序列表，用于 CI 或启动日志核对。

### P1-2 高频路径日志过多

相关代码：

- `MobaBattleIOPort.Submit` 每批输入输出 `Log.Info`。
- `MobaMoveInputCommandHandler` 每条移动输入输出 `Log.Info`。
- `MobaBattleDriverHost`、运动系统、Tick 系统存在高频日志。

问题：

输入、移动、Tick、快照都是高频路径。当前 Info 日志适合 demo 调试，但正式铺量会造成日志量、GC、IO 和观测噪声问题。

铺量风险：

- 压测数据失真。
- 日志平台成本和噪声上升。
- 真正异常被高频普通日志淹没。

建议：

- 建立战斗日志级别策略：Error/Warning 默认开启，Info 采样或按 battleId/playerId 开关，Trace 仅本地或定向诊断。
- 高频输入日志改为采样统计，如每 N 帧汇总数量、opcode 分布、最大延迟。
- 关键丢输入路径保留可限频诊断。

### P1-3 多处 catch 吞异常

相关代码：

- 技能生命周期系统仍存在部分空 catch 或异常后退化为默认帧的路径。
- `MobaSkillCastCancelRequestSystem` 多处空 catch。
- `MobaSkillCastInstanceSyncSystem` 获取 frame 失败时回退到 0。
- `MobaSkillCastDestroyCleanupSystem` 空 catch。
- `SkillPipelineRunner` 多处读取/清理上下文时空 catch。

问题：

吞异常会把结构错误变成后续状态异常。对于技能生命周期，这会直接影响战斗正确性。

铺量风险：

- 技能实例泄漏、取消失败、销毁失败不易定位。
- 回放/同步异常缺少第一现场。
- 线上只表现为偶发卡死或状态不一致。

建议：

- 空 catch 至少改为限频日志，包含 skillId、actorId、castId、frame。
- 对可预期缺字段使用 `TryGet` 风格，不用异常做流程控制。
- 对不可恢复错误上报战斗健康状态。

### P1-4 静态状态可能跨 World/房间污染

相关代码：

- `ActorEntityInitPipeline` 中 `LoggedMissingCharacterIds`、`LoggedMissingAttributeTemplateIds`、`LoggedMissingConfig` 为 static。
- `MobaBootstrapStageInitializer` 使用 static `_initialized`。
- `DemoWireSerializerBootstrap` 使用静态安装状态。
- `PassiveSkillTriggerEventRollbackLog` 已于 2026-11-17 清理：无业务生产/消费方，不保留该事件日志或其临时集合。
- `MobaContinuousModifierQueryService` 使用 static `Records` 字典。
- `TriggeringConstants`、`TriggeringIdUtil` 使用静态缓存。

问题：

静态缓存对单 demo world 方便，但正式多房间、多 World、多测试并行时，需要明确哪些静态是只读全局缓存，哪些是运行时状态。运行时状态 static 容易产生串房间和测试污染。

铺量风险：

- 某房间错误日志被另一房间的静态去重屏蔽。
- 临时集合在并发/嵌套调用下互相覆盖。
- 热重载或 domain reload 后状态不可预期。

建议：

- 只保留不可变配置/反射缓存为 static。
- 临时集合改为局部变量、对象池或 World scoped service。
- 日志去重应按 WorldId/BattleId 维度，而不是进程全局。

### P1-5 输入校验和失败诊断不足

相关代码：

- `LogicWorldInputCoordinatorBase.CanSubmit` 默认恒 true。
- `MobaSkillInputCommandHandler` 多处 return 无诊断。
- `MobaMoveInputCommandHandler` 未显式校验 payload 长度和反序列化异常。
- `MobaInputCoordinator` 解析不到 `SkillExecutor` 后技能输入可能静默丢弃。

问题：

输入层缺少系统性的校验、失败码、限频诊断和反序列化保护。

铺量风险：

- 非法包、过期包、乱序包、空 payload 引发难定位问题。
- 技能输入丢失时没有足够上下文。
- 外部网络层和逻辑层责任边界不清。

建议：

- 输入协调器增加统一校验：frame、player、opcode、payload、phase、actor binding。
- handler 只处理业务语义，公共失败统计放到 coordinator。
- 技能执行器缺失应在启动健康检查阶段阻断，而不是运行时静默跳过。

## 4. 低优先级但应清理的问题

### P2-1 Demo/Legacy/Template 命名残留

相关代码：

- `DemoWireSerializerBootstrap` 仍保留 Demo 命名。
- `MobaSpawnService` 明确标注为 legacy fallback。
- `TemplateStage` 仍在 runtime 包中。
- `IGameEntityFactory`、`GameEvents`、`GameSnapshotTypes`、`MobaSystemOrder` 等仍有“模板”说明。
- `MobaConfigGroups.LegacyJson` 仍是配置分组之一。
- `CDLEnum.LegacyAimPos` 保留旧入口。

问题：

这些命名和注释本身不一定影响运行，但会让后续开发误判哪些路径是正式路径，哪些是兼容路径。

建议：

- 将 legacy fallback 集中到单独 namespace 或 asmdef，并加 Deprecated 标记。
- 移除未启用模板 Stage，或只保留在 Samples/Docs。
- Demo 命名替换为 Runtime/Protocol/Serializer 等正式命名。

### P2-2 代码注释乱码和重复 using

相关代码：

- `ActorEntityInitPipeline` 文件头注释存在乱码。
- `MobaSkillInputCommandHandler` 注释存在乱码。
- `ActorSpawnPipeline` 存在重复 using。

建议：

- 上线前统一清理乱码注释，避免影响维护体验和文档生成。
- 通过格式化或 analyzer 清理重复 using。

## 5. 建议优化路线

### 第一阶段：阻断不确定性

目标是避免正式战斗进入半初始化、漏快照、错帧执行的状态。

优先动作：

1. 重构快照输出接口，让 Router 原生支持批量收集。
2. 明确输入帧协议，修正 Driver 提交帧与 Tick 帧关系。
3. 将正式 Battle world 的必需依赖从 optional 改为启动期校验。
4. 移除或阻断 predicate/resource/damage pipeline 中的 placeholder。
5. 将开局反序列化、归一化、实体初始化错误转成结构化失败。

### 第二阶段：收敛运行时可观测性

目标是让线上问题有第一现场，同时控制日志成本。

优先动作：

1. 高频 Info 日志降级为采样、Trace 或诊断开关。
2. 空 catch 全部改为可限频诊断或显式 Try 流程。
3. 输入丢弃、技能失败、快照缺失、系统安装列表增加统一统计。
4. 战斗健康状态增加关键错误码，如 init_failed、input_frame_invalid、snapshot_emitter_failed。

### 第三阶段：正式化结构边界

目标是让后续铺量时模块边界清楚，新增功能不靠隐式扫描和约定猜测。

优先动作：

1. 将 `MobaLogicWorldFeatures` 与模块安装真正绑定。
2. 为 WorldType/Profile 建立必需服务和系统清单。
3. Legacy fallback 独立隔离，并设置迁移截止点。
4. 清理 Demo/Template/Legacy 命名、乱码注释和旧协议入口。
5. 将静态运行时状态迁移到 World scoped service 或显式上下文。
6. 继续拆分 host.extension moba 的 shared/server/client 边界：shared 只保留协议组装和房间状态，server/client 侧再依赖 runtime 服务。

## 6. 建议验收清单

### 构建逻辑层

- [ ] 每个正式 WorldType 有明确 Profile、Feature、Module 清单。
- [ ] 启动日志或测试能输出实际安装的服务和系统列表。
- [ ] 必需服务缺失时启动失败，而不是运行时空引用或静默降级。
- [ ] Legacy fallback 不会在正式 Battle world 默认启用。

### 进入战斗

- [x] `WorldInitData` 校验 opcode。
- [x] host.extension shared 负责组装 start plan 和 `WorldInitData`，runtime 正式链路不再直接手工拼 `MobaCreateWorldInitPayload`。
- [x] host spawn 到 `MobaBattleStartPlan` / `WorldInitData` 的标准入口已集中到 host.extension，legacy spawn fallback 也复用同一套组装逻辑。
- [ ] `WorldInitData` 校验 payload version、battleId、player 列表、random seed。
- [ ] 配置缺失、英雄缺失、属性模板缺失、技能 loadout 缺失能返回明确失败。
- [ ] Actor 创建、注册、PlayerActorMap、技能初始化、初始快照在同一事务语义下完成。
- [x] 缺 ActorEntityInitPipeline 时开局失败不会进入 InGame phase。

### 接受输入

- [x] Driver 默认输入 frame 语义已收敛为 next frame，并提供显式 target frame 入口。
- [x] 负帧和过期输入已有基础拒绝策略。
- [ ] 未来输入、非法 player、非法 opcode、非法 payload 有统一处理。
- [ ] 输入 frame 语义有测试覆盖。
- [ ] 技能输入依赖缺失会启动期失败。
- [ ] 高频输入日志默认关闭或采样。

### 执行逻辑

- [ ] 系统顺序有文档和测试保护，关键阶段不依赖扫描偶然顺序。
- [ ] 技能 cast 生命周期不吞异常。
- [x] Resource 扣除不再使用 placeholder。
- [x] 未接真实服务的 Buff/HP Predicate 不再误放行。
- [x] Damage mitigate/shield pass-through 已显式运行时告警。
- [ ] Buff、Projectile 等核心链路继续排查 placeholder。
- [ ] 静态运行时状态已清理或证明为只读安全缓存。

### 快照输出

- [x] Router 支持一次性收集同 frame 多个快照。
- [ ] 每个 emitter 的读取/消费语义明确。
- [ ] 事件快照和状态快照分离处理。
- [ ] 快照缺失、序号错乱、重复输出有诊断。

## 7. 总结

当前 `com.abilitykit.demo.moba.runtime` 已经不再是简单 demo：World 蓝图、Bootstrap Flow、World DI、输入端口、快照路由、技能/效果/投射物/触发器链路都已经形成了正式战斗 Runtime 的雏形。

真正需要优先处理的不是“代码是否够多”，而是“正式运行时是否允许不确定状态继续前进”。目前最影响上线化的风险集中在三类：快照读取语义不稳定、输入帧协议不明确、开局与核心玩法存在静默降级或 placeholder。先把这三类问题收敛，后续再清理 Legacy、日志、静态状态和模板残留，整体结构就能比较稳地从 demo runtime 过渡到可铺量的战斗 runtime。
