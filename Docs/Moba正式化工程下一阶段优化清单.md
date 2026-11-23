# Moba 正式化工程下一阶段优化清单

## 背景

本清单基于现有 Moba 设计/审计文档与当前代码抽样整理，目标是把下一阶段工作从“能跑的示例链路”继续推进到可维护、可观测、低 GC、边界清晰的正式工程链路。

已参考的既有文档包括：

- `Docs/Moba工程设计与GC热点审计.md`
- `Docs/MobaBattleFormalIntegrationGuide.md`
- `Docs/MobaBattleLogicLayerRectification.md`
- `Docs/MobaSkillCastRuntime聚合生命周期设计.md`

当前代码相较旧文档已有若干收口：`MobaInputCoordinator` 已切到 `MobaInputCommandContractRegistry` / `MobaInputCommandHandlerRegistry`；`MobaSnapshotRouter.CollectSnapshots` 已对空参数和非法 `maxSnapshots` 抛异常；`MobaEnterGameFlowService` 已对缺失玩法服务、非法玩法 ID、非法 actor 绑定做失败返回。因此本清单不重复推进这些已闭环事项，而聚焦仍然存在或需要进一步正式化的点。

## P0：主链路硬边界与热路径 GC 收口

### P0-1 输入/启动/快照链路的字符串诊断去热路径化

**现状**

- `MobaBattleIOPort.Submit` 在输入提交路径中构造 trace 字符串，例如提交帧、数量、首个玩家与 opCode。
- `SkillExecutor.TryHandleInput` / `TryStartCastFromInput` / `Step` 仍通过字符串 `failReason` 与插值字符串表达正常控制流或诊断结果。
- `MobaRuntimeValidationReport.FormatEntry` / `FormatSummary` 是字符串模型；虽然已有 `MobaRuntimeValidationMode` 与采样间隔，但报告构造和写日志仍偏字符串化。
- `MobaEnterGameFlowService` 的启动日志与失败日志仍使用插值字符串。启动不是逐帧热点，但属于正式链路边界，应统一接入结构化诊断。

**风险**

- 输入、技能与校验可能在压测、运行时采样或异常风暴下产生不必要 GC。
- 字符串 `failReason` 作为主返回通道会让业务判断、埋点、日志文案耦合，后续国际化/协议化/自动化测试都不稳定。

**建议落地**

1. 为输入、技能施放、校验报告引入枚举/结构化失败码，字符串只在日志输出层按需格式化。
2. 为 `MobaRuntimeLog` / `IMobaBattleDiagnosticsService` 增加 `IsEnabled(module, purpose)` 或等价门控，调用方先判断再构造字符串。
3. 把 `SkillExecutor` 的 `failReason` 补一层 `MobaSkillCastFailure` / `SkillInputHandleResult` 结构化通道，旧 bool/string API 可保留为兼容包装。
4. 将 `MobaBattleIOPort.Submit` 的 trace 改为受门控的结构化参数或低频采样。

**验收标准**

- 输入提交、技能输入、技能 Step、运行时校验在默认 runtime 模式下不构造非必要插值字符串。
- 单测覆盖：关闭日志/诊断时不会调用 message factory；失败码可稳定断言。

### P0-2 数组返回 API 与快照/查询边界的分配治理

**现状**

- `MobaBattleStateQueryService.GetAllEntityStates` 使用池化 `List<LogicWorldEntityState>` 收集，但最终仍 `ToArray()` 返回。
- `GetDiagnosticEntityStates` 基于数组再次分配诊断数组。
- `MobaSnapshotBuffer.DrainToArray` / `PeekArray`、部分 rollback provider、actor spawn snapshot 发布路径仍使用 `ToArray()`。
- `MobaEnterGameFlowService.PublishEnterGameSnapshots` 对 `spawnEntries.ToArray()` 序列化。
- ET 侧已在部分输入缓冲上改成 copy-to-buffer 模式，可作为后续统一方向参考。

**风险**

- 快照、状态查询、诊断读取是外部同步与 View 接入的常见边界，数组返回会把内部复用成果抵消在边界层。
- 未来接 Orleans/ET/Unity View 多端时，状态读取频率升高会放大分配。

**建议落地**

1. 增加 `FillAllEntityStates(List<LogicWorldEntityState> buffer)` 与 `FillDiagnosticEntityStates(List<MobaDiagnosticEntityState> buffer)`，保留数组 API 作为兼容包装。
2. 为 `MobaSnapshotBuffer` 增加 `DrainTo(IList<T> destination)` / `PeekTo(IList<T> destination)`，减少热路径 `ToArray()`。
3. 启动期 spawn payload 如果必须序列化数组，改为单次明确边界；若后续频繁复用，应提供 `IReadOnlyList` 序列化重载。
4. 对 ET driver、View snapshot dispatcher、runtime output port 统一采用“调用方传入缓冲”的约定。

**验收标准**

- 状态查询与快照收集主路径可不分配数组。
- 旧数组 API 保持可用，但文档标注为兼容/低频接口。

### P0-3 被动技能/触发计划动态更新的集合分配与生命周期收口

**现状**

- `MobaPassiveSkillTriggerRegisterSystem` 已引入部分 `ObjectPool<HashSet<long>>` / `ObjectPool<Dictionary<int,long>>` / `ObjectPool<List<long>>`。
- 但 `UpdateOngoingTriggerPlansFromPassive` 附近仍有 `new List<OngoingTriggerPlanEntry>` 用于组件替换，触发监听与 ongoing trigger plan 的生命周期仍分散在注册系统、订阅服务与 trace 生命周期之间。
- 旧审计文档提到的被动触发计划重建分配已有改善，但未完全工程化为“可复用差量同步”。

**风险**

- 被动技能、Buff、装备、光环等会引起频繁增删监听，若仍以重建列表为主，人数/单位数量上升后会成为 GC 热点。
- 监听注册、ownerKey、正式 execution context、ongoing plan component 多处维护，容易出现泄漏、重复注册或清理顺序不一致；Trace 只应是可选投影。

**建议落地**

1. 将 ongoing trigger plan 更新改成 diff apply：复用组件内列表，按 ownerKey / triggerId 增删，不在常规更新时重建整表。
2. 把 ownerKey 计算、监听注册与领域 Execution Context ownership 明确分层：订阅协调器只管理 owner 路由，Context 创建者负责创建/结束，Trace Adapter 只观察。
3. 对被动技能、Buff 持有的触发计划统一接入同一套 owner-bound subscription contract。
4. 增加“重复注册、缺失释放、ownerKey 泄漏”的 runtime validator。

**验收标准**

- 被动技能刷新不会在无变化时替换 ongoing plan list。
- actor 销毁、Buff 结束、被动技能移除后，对应订阅与正式 execution context 全部释放并可测试验证；安装 Trace Adapter 时投影同步收尾。

**实施结果与保留边界（2026-08-15）**

- `MobaPassiveSkillLifecycleService` 已集中维护被动 listener、owner binding、ongoing plan、continuous runtime、action 与 passive root Execution Context 的生命周期；actor 注销和被动移除通过显式 ownership 清理链路收口，Trace Adapter 可选观察。
- ongoing plan 已改为按 ownerKey 原地差量同步。配置无变化时保持 plan list、entry、trigger array 与 revision 的对象和值稳定；单项移除时复用原列表及未变化 entry/array，只有新增 owner 或 trigger 配置实际变化时才创建对应对象。
- listener desired id、临时 context owner set、actor owner-key snapshot 已接入集合池或 actor 生命周期内复用；`MobaOngoingTriggerPlansReconcileSystem` 使用字段级聚合列表，避免逐帧创建 reconcile 缓冲。
- trigger gateway 的订阅 apply/stop 仍由 `MobaTriggerPlanReconcileService` 与 `MobaTriggerPlanSubscriptionService` 负责。lifecycle service 只维护计划数据与 revision，不新增对 gateway 的强耦合；公开 `OngoingTriggerPlanEntry.TriggerIds` 数组契约保持不变。
- 定向 lifecycle 测试覆盖稳定重复同步、单项移除 identity、actor 注销后的 listener/plan/owner binding/trace 清理，结果为 7/7 通过。
- runtime core 使用 `BuildProjectReferences=false` 复用既有依赖产物完成两次编译，最终结果为 0 error。默认完整项目引用构建仍受工作树中既有 `AbilityKit.Core` Public API analyzer 配置与未跟踪 API 基线文件影响，本批未修改或回滚这些无关状态。

## P1：正式宿主集成与能力契约补齐

### P1-1 BattleRuntimePort / IO / StartSpec 的外部契约稳定化

**现状**

- `MobaBattleMainFlowHealthValidator` 已要求 `IMobaBattleRuntimePort`、输入端口、输出端口、启动 spec、快照健康等关键服务存在。
- `MobaBattleIOPort.Submit` 已把内部 `LogicWorldInputSubmitFailureCode` 映射成 host 扩展层 `MobaInputSubmitFailureCode`。
- `MobaBattleRuntimeStatus` 已能描述 GameStart/Input/SnapshotOutput/StateReadModel 等 capability。

**不足**

- 当前契约仍偏 runtime 内部验证，尚未形成面向 Orleans/ET/Unity View 的“能力矩阵 + 行为语义”文档与测试集。
- 输入提交失败、部分处理、无命令处理等边界需要端到端样例证明。

**建议落地**

1. 建立 Moba Runtime Port acceptance tests：覆盖启动前状态、启动成功、重复启动、非法输入帧、部分处理、快照输出契约。
2. 将 `MobaBattleRuntimeStatus` 与 `MobaBattleMainFlowHealthValidator` 的语义同步到集成文档，明确 host 应如何降级或拒绝接入。
3. 为 ET / Orleans 接入提供最小 contract fixture，避免各宿主重复拼启动参数和解释失败码。

**验收标准**

- 任一宿主只依赖 host extension contract 即可判断 runtime 是否能启动、能收输入、能产出快照。
- 对外错误码稳定，不依赖日志文本。

### P1-2 技能施放聚合生命周期继续正式化

**现状**

- `MobaSkillCastRuntimeService` 已存在 runtime aggregate、retain/release、children、root execution context 等概念；Trace 不再是业务依赖。
- `SkillExecutor.StartPreparedCast` 在 runner 启动失败时会 `ForceTerminate` runtime handle。
- Buff/projectile/summon 侧已有 retain/release 记录，但清理日志和异常处理仍分散。
- `MobaSkillRuntimeLifecycleHookService` 已按事件开始时的稳定快照派发；回调期间的注册/注销延迟到下一事件，异常隔离并在派发完成后重抛首个异常，避免运行时生命周期观察者改变当前事件的覆盖范围。
- 两条 child retain 入口现已汇总到同一 `MobaSkillRuntimeRetainHandle` 登记路径；legacy `runtimeId + child` 释放会先消费匹配 capability，并可通过 `ValidateRetentionState` 检查 orphaned/untracked child。

**不足**

- 技能输入、pipeline runner、runtime aggregate、trace、临时实体的失败码与生命周期事件还没有完全统一。
- 等待 child 的 runtime、异常清理、trace end 失败等路径主要依赖日志与 warning。

**建议落地**

1. 定义 `MobaSkillCastLifecycleEvent`：Prepared、Started、Rejected、PipelineEnded、WaitingChildren、ForceTerminated、Finalized。
2. 让 `SkillExecutor` / `SkillPipelineRunner` / `MobaSkillCastRuntimeService` 共享同一失败码与生命周期事件。
3. 对 Buff/projectile/summon 的 retain-release 加 validator：检测悬挂 retain、重复 release、child 未完成导致 runtime 长期未 finalize。
4. 把 View/ET 的技能运行快照读取改为 buffer 填充，避免为了展示 running/ended snapshot 产生数组。

**验收标准**

- 任意技能施放实例都能从 runtime id 追踪到根 Execution Context、children、结束原因和最终状态；Trace 启用时可查询同 ID 投影。
- 单测覆盖 pipeline 启动失败、child 等待、Buff/Projectile release 后 finalize。

### P1-3 View/ET 表现层快照消费正式化

**现状**

- View 侧已经存在 remote-driven runtime、confirmed authority world installer、net adapter、timeline operation 等模块。
- ET driver 已部分引入 reusable buffers，输入缓冲已从返回列表改成 copy-to-buffer。

**不足**

- 表现层与逻辑层之间仍有多个数组/DTO 边界，部分转换方法保留 `ToArray()`。
- 本地模拟、远端确认、插值表现、HUD 数据读取之间需要统一“谁拥有缓冲、谁负责清理、谁可缓存”的规则。

**建议落地**

1. 梳理 `RemoteDrivenWorldTickDriver`、`RemoteDrivenWorldRuntimeFactory`、`BattleSessionFeature.NetAdapter`、ET driver 的 snapshot 消费 API。
2. 将运行时快照、事件快照、表现 cue 统一成 copy-to-buffer 或 pooled batch contract。
3. 为 View 层定义 snapshot consumption lifecycle：接收、缓存、插值、释放。
4. 对 ET smoke 增加 GC/分配观察项，至少记录固定帧数内输入与快照路径的分配趋势。

**验收标准**

- View/ET 主循环消费快照时不需要每帧创建新数组。
- 表现层缓存所有权清晰，不持有已释放池对象。

**本批落地（BattleClientSnapshotStream）**

- 网关线程只负责入队，表现线程在 `Drain`/`Present` 中消费；actor 字典始终由 stream 私有持有。
- late attach 与 full-snapshot recovery 使用独立的 baseline actor 数组；投影不会拿到内部字典或可复用缓冲。
- 排序后的 actor ID 顺序按拓扑变化缓存，避免每次 attach 都重新创建临时 ID 列表；正常 `Present`/`Drain` 不触发该排序分配。
- `DetachProjection`、session stop 与 stream reset 都会调用 projection reset，之后不再向已解绑投影投递快照。

下一小批继续处理事件快照与表现 cue 的 copy-to-buffer 边界，并增加 ET smoke 的固定帧分配观测；暂不跨包引入通用对象池。

**本轮稳定性审查补充**

- `PresentationCueReconciliationController` 的容量淘汰已改为只淘汰 inactive history；active cue 的预测、确认、修正、拒绝历史不会因容量压力丢失。
- receipt 队列改为携带 entry identity，避免 key 删除后复用时被旧 receipt 误删。
- 该边界直接影响金克斯等高频技能表现：预测特效保持 active 期间，后续 authority correction/rejection 仍必须命中同一 request key。

审查中确认暂不需要调整 Pipeline/Trigger 的职责边界；事件快照和表现 cue 在跨线程/跨 generation 时的批次生命周期继续按恢复窗口验证。可靠事件恢复窗口现在明确：重复进入等待权威基线时清空上一窗口的 pending 批次，并在保留批次前复制 ingress 事件列表，避免旧序列或复用解码缓冲污染新基线后的回放；同时以批次数和事件数双重限制恢复内存，单次溢出在一个恢复窗口内只报告一次，成功采用基线后重新计数。runtime 额外暴露 `RecoveryWindowId` 与累计 `PendingOverflowCount`，用于将重同步请求、批次丢弃和恢复结果按窗口关联，观测字段不参与玩法决策。后续 ET smoke 仍需补充对批次丢弃、重放、回收的可观测性。

**本轮追加的生命周期修复**

- `BattleSessionLifecycleController` 改用全量 best-effort teardown：某一步失败不会阻止后续订阅、快照缓冲和网络绑定清理。
- 失败结果仍保留失败步骤集合，下一次 detach 只重试未成功步骤；成功步骤不会重复执行。
- 这项修复属于基础生命周期稳定性，和 Pipeline/Trigger 的职责边界无关，但能避免一个表现层清理异常把整场战斗资源留在半释放状态。

## P2：工程化治理、文档与长期演进

### P2-1 Runtime validation 报告模型结构化

**现状**

- 已有 `MobaRuntimeValidationMode`、`RuntimeSampled`、`MaxLoggedEntries`、`RuntimeSampleInterval`。
- 报告条目仍以 string source/path/message/businessId 为主。

**建议落地**

1. 为 validation entry 增加稳定 code、category、severity、business numeric id，可选延迟 message factory。
2. 对 bootstrap strict、editor full、runtime sampled、manual only 制定默认配置表。
3. 将 validation report 可导出为机器可读 DTO，供 CI/编辑器/ET smoke 判断。

**验收标准**

- 自动化测试断言 validation code，而不是中文/英文日志文本。
- runtime sampled 模式不会在未命中采样时构造完整报告文本。

### P2-2 配置加载与表驱动 pipeline 的缓存边界

**现状**

- Luban/JsonNet 配置加载、MO 转换、表驱动 skill pipeline library 中存在大量 List/Array 转换，这是启动期合理开销，但边界尚未明确。
- `TableDrivenMobaSkillPipelineLibrary` 有 skill cache，但 phase definition 与 timeline event 数组的复用策略可以继续明确。

**建议落地**

1. 区分“启动构建期允许分配”和“战斗运行期禁止分配”的配置对象生命周期。
2. 给 table-driven pipeline 增加预热入口，启动阶段完成配置解析、phase 构建和校验。
3. 将运行期按 skillId 查询 pipeline 的路径设为只读缓存，不再触发 List/Array 重建。

**验收标准**

- 战斗开始后首次施放技能不会触发配置解析与 pipeline 构建分配。
- 缺失配置在 bootstrap validation 阶段暴露，不进入运行时软失败。

### P2-3 日志/诊断规范与压测基线

**现状**

- 多数系统已有 once warning、diagnostics service、warning limit 等机制，但调用点使用方式不统一。
- `IMobaBattleDiagnosticsService.Warning(key, Func<string>, maxCount)` 已支持延迟构造 warning 文案，适合热路径诊断。
- `MobaRuntimeLog` 已补齐 `Func<string>` message factory 重载与 `IsEnabled(level, context)`，可在日志禁用时避免字符串构造。
- 旧 ET hot path 日志已做过一轮收敛，Moba runtime 仍需要统一规范。

**建议落地**

1. 建立 Moba 日志规范：禁止热路径无门控插值；异常路径必须带 code；重复 warning 必须有 suppression。
2. 热路径、逐帧路径、批量校验路径只能使用 `MobaRuntimeLog`/`IMobaBattleDiagnosticsService` 的门控或 message factory API，不直接写插值字符串。
3. warning 必须有稳定 key，并通过 `WarningOnce`、`maxCount` 或等价 suppression 控制重复输出。
4. validation/bootstrap 类日志必须保留 stable code/category，自动化测试只断言 code/category，不断言日志文案。
5. 加入轻量压测/烟测脚本：固定玩家数、固定技能输入、固定 Buff/Projectile 生成量，输出帧耗时和 GC 指标。
6. 将 P0/P1 的关键路径纳入 `git diff --check` + targeted test + smoke 的最小验证组合。

**验收标准**

- 新增热路径日志必须通过 code review checklist：是否有 `IsEnabled` 或 message factory、是否有 stable key/code、是否有 suppression。
- 单测覆盖关闭日志/诊断时不会调用 message factory，重复 warning 不会重复构造文案。
- smoke 输出中能看到输入、技能、快照、validation 的分配趋势。

## 已确认不作为下一阶段 P0 重复推进的事项

- 输入命令热修 fallback：当前 `MobaInputCoordinator` 已通过 `MobaInputCommandContractRegistry` 和 handler registry 统一 dispatch，旧文档中的热修回退描述需降级为历史项。
- 快照收集非法参数静默返回：当前 `MobaSnapshotRouter` 和 `MobaBattleIOPort` 已对 null snapshots 与非法 `maxSnapshots` 抛异常。
- 启动期非法 actor 绑定跳过：当前 `MobaEnterGameFlowService.BindPlayerActors` 对非法 actor id 返回失败。
- 缺失 gameplay service 默认继续：当前 `StartGameplay` 对缺失 `_gameplay` 返回 `MissingGameplayService`。

## 推荐推进顺序

1. P0-1：先做日志/失败码/诊断门控，因为它影响输入、技能、校验三条主线。
2. P0-2：再做 buffer-fill / array API 兼容改造，优先状态查询、快照缓冲、ET/View 消费边界。
3. P0-3：随后做被动触发计划 diff apply 和生命周期协调器，降低 Buff/被动/装备扩展风险。
4. P1-1 与 P1-2：补齐 host contract acceptance tests 与技能 runtime lifecycle tests。
5. P1-3 与 P2：最后沉淀表现层快照生命周期、validation DTO、压测基线和日志规范。

### 本批继续落地：恢复期间错误事件流早期丢弃

- 可靠事件 runtime 在等待权威基线时先校验 batch 的 `StreamId`。与当前战斗不匹配的旧战斗推送不会进入 pending 队列，也不会触发新的 resync；它只增加 `PendingInvalidBatchCount` 并以 `BatchRejected/InvalidStream` 进入诊断回调。
- `TimelineId` 不在该阶段提前拒绝：断线恢复可能通过新的 epoch 建立基线，匹配时间线的旧批次也必须保留用于重放。基线建立后仍由 `ReplayOnlyMatchingTimeline` 控制回放范围。
- SDK 和 MOBA 适配层同步透传计数，业务层只用于诊断和压测，不参与技能、Pipeline 或 Trigger 决策。

本批继续收口可靠事件恢复窗口与 MOBA 恢复协调器的边界：`RecoveryWindowId` 现在参与 `ReliableEventResyncRequired` 的窗口级关联。同一窗口内重复的可靠事件失效只保留首个全量状态请求，后续重复信号计入抑制诊断；新 generation 会清空关联状态，恢复动作调用 `RequireAuthoritativeBaseline` 后产生新窗口，跨窗口失败仍可再次重试。该抑制只影响可靠事件 resync，不改变连接丢失、快照导入失败等其他恢复信号，也不参与 Pipeline、Trigger 或技能玩法决策。

**下一批落地（2026-10-31）**

- `ILogicWorldDriverStateBufferBridge` 为 coordinator 增加可选的调用方缓冲状态桥；`MobaBattleDriverHost` 通过复用逻辑状态缓冲并逐项转换，提供 `FillCoordinatorEntityStates`。
- coordinator 原有 `GetAllEntityStates()` 数组接口继续保留为兼容入口；高频 ET/View 状态同步可探测并使用 buffer bridge，避免 runtime 已填充缓冲后宿主又转数组的重复边界分配。
- 状态转换只复制数据值，不把 runtime 实体或内部集合暴露给 coordinator；调用方仍拥有输出缓冲的清理和生命周期。
### 下一批落地（2026-10-31，coordinator bridge 收口）

- 新增 `LogicWorldDriverStateBufferExtensions.FillAllEntityStates` 统一入口：优先探测 `ILogicWorldDriverStateBufferBridge`，旧宿主自动回退 `GetAllEntityStates()`，调用方不再重复实现类型探测与兼容复制。
- Shooter 与 Moba driver host 均实现可选 buffer bridge；数组 API 仍保留给低频诊断和旧接入。
- buffer API 的所有权规则固定为“调用方清理、宿主追加、返回追加数量”；宿主不得暴露内部状态集合，旧数组回退只在不支持 bridge 的宿主发生。
- ET 已直接使用 Moba runtime 的 `FillAllEntityStates`，当前没有 coordinator 数组消费点；后续新增消费应优先使用统一扩展入口并增加 allocation smoke 观测。

### P1-3 事件快照 list bridge（已完成）

ET 逻辑驱动器的 `DispatchFrameSnapshot` 原先把 `FrameSnapshotData` 中的每个
`IReadOnlyList<T>` 都先转换成临时数组，再交给 share 层 dispatcher。现在新增可选的
`IFrameSnapshotListDispatcher`：实现方可以在当前同步调用内直接消费列表，旧的数组接口仍然保留。

- `FrameSnapshotDispatcher` 同时实现 list bridge，并提供 `SubscribeList<T>`；列表订阅收到的是生产者传入的同一个列表引用。
- 旧数组订阅继续有效。只有存在对应的数组订阅时，list dispatch 才按需复制一次数组，因此兼容成本由实际旧消费者承担。
- `SnapshotViewAdapter` 已切换到列表订阅，视图 sink 通过 `FrameSnapshotData` 消费列表；列表只保证在本次同步回调期间有效，订阅者需要跨帧保存时自行复制。
- ET 的 `DispatchFrameSnapshot` 对变换、投射物、区域、伤害、生成、销毁和技能状态事件优先探测 list bridge，旧 dispatcher 自动回退数组路径。变换和生成仍维护历史 `Last*Snapshot` 数组缓存，保证 ET 调试/采样代码兼容。
- `DispatchViewSnapshot` 已经直接传递完整 `FrameSnapshotData`，不属于本批数组热点；后续不要为了统一接口重新拆成逐事件数组。

验收重点：同一列表的顺序、数量和内容必须保持不变；list 订阅不能持有生产者列表跨帧；旧数组订阅和旧 `IFrameSnapshotDispatcher` 实现继续可用。
## 阶段性成熟度与英雄铺量准入标准（2026-11-01）

当前 MOBA 英雄技能链路已经达到“机制表达可复制、单英雄可以按固定流程交付”的阶段性稳定水平。对大多数常规英雄，后续工作以新增配置、Timeline、Trigger 计划、HFSM 参数和验收场景为主，不需要重做职责边界。

综合判断：英雄机制生产能力约 **8/10**，基础设施产品化约 **6.5/10**。当前适合受控批量铺量，不适合宣称无限制批量发布。

### 复杂度准入

**A 类**：普通指向伤害、简单 Buff、单次位移、单条投射物、固定区域和短时蓄力，可以直接按既有链路交付。

**B 类**：多段命中、链式投射物、持续区域、召唤物、充能、多来源 Modifier、护盾与伤害混合、反射/弹射。可以铺量，但必须复用扩展点，明确来源、生命周期、取消、销毁、去重和恢复；实体创建不得由英雄组件自行管理。

**C 类**：全图弹道、服务器命中回溯、跨会话召唤/分身、复杂 AOI、高弹量目标排序、并行 HFSM 分支和要求预测/确认/拒绝/重放一致的长链路。先做能力切片，证明固定 tick 可重演、恢复不重复结算、预算降级可观测、Console/dotnet/Unity 使用同一契约。

### 当前链路闭环

| 阶段 | 权威来源 | 最低验收物 |
|---|---|---|
| 状态与参数 | HFSM Authoring、Modifier | 状态图、稳定参数 ID、来源和清理规则 |
| 数值与身份 | Luban Production Excel | 业务 ID、等级、标签和引用可解析 |
| 单次施放 | SkillFlowCO、ActionEditor logic | phase、提交点、取消和结束 trace |
| 战斗效果 | Trigger Authoring | target query、来源、命中去重和效果结果 |
| 表现消费 | presentation timeline、资源模板 | generation、确认/拒绝、打断和恢复 |
| 快照回滚 | snapshot/rollback provider | 状态、技能阶段、Modifier 和必要实体状态一致 |
| 发布门禁 | exporter、Luban、test-gates | `--check-published`、内容契约、dotnet 和 Unity artifact |

链路已经足以支撑批量制作。最容易导致返工的是跳过状态规划、从表现或 Trigger 直接开始，或只验证 Action trace 而不验证权威状态、效果和恢复结果。

### 仍需并行治理的风险

1. delta/keyframe/resync 和 pure-state 客户端消费尚未完全收口，应在同步层解决。
2. AOI slice、快照预算、召唤物和大范围区域的降级策略仍需独立建设，技能不能静默裁剪目标。
3. ActorId 回滚、Projectile provider、召唤物重建和跨会话恢复必须形成统一契约。
4. manifest、`test-gates.json`、acceptance coverage 和 Unity 资源登记仍有人工漏项风险。
5. 发布物与 Console/Unity 副本可能漂移，生产修改必须回到权威来源并通过发布检查。
6. 完整构建、runtime smoke、view runtime contracts 和部分 Unity gate 尚未全部自动化；这是工程质量风险，不是技能职责边界风险。

### 英雄批次准入标准

- 完成 HFSM/Modifier/Pipeline/Trigger 职责归类；长期模式不能塞进长 Flow，表现回调不能决定玩法结果。
- 所有引用来自当前权威来源，Unity 与 Console 发布 ID 一致。
- 每个跨帧对象都有来源、生命周期、取消、销毁、去重和回滚/重建规则。
- 每个技能至少有真实 dotnet acceptance 场景；复杂技能还要断言 trace、快照或回滚。
- Unity 验收覆盖逻辑动作、表现动作、特效资源、打断/恢复和 generation。
- 通过匹配的 `moba-content-contracts`、`moba-acceptance-dotnet`、`moba-console-smoke` 和 Unity hero gate。
- 失败可按稳定 code、skill ID、trigger ID、phase、generation 或 recovery window 定位。

### 对金克斯的影响

金克斯可以先按普通批次推进：被动加速使用持续 Modifier；Q 双形态使用模式参数，机枪/火箭每次攻击分别走独立 Flow；W 使用投射物与命中 Trigger；E 使用区域/陷阱实体和生命周期；R 先实现单次发射、目标查询、伤害和表现确认，再单独评估全局弹道、AOI 和延迟补偿。

金克斯不会自动要求大规模重构。需要平台扩展的部分应沉淀为可复用能力：模式参数投影、投射物追踪/弹射、区域生命周期、目标查询排序和全局弹道恢复。只有两个以上英雄同时需要同一未表达语义，或现有契约无法满足回滚/恢复/发布一致性时，才升级为框架重构。

阶段性判断：**英雄机制层已达到可铺量水平；网络与产品化基础设施仍需并行收口；后续以局部扩展和门禁强化为主，不需要大规模重构，但 C 类能力必须独立准入。**

### 下一批落地：护盾变更通知闭环（2026-11-05）

- `MobaShieldService.Changed` 现在在个人护盾成功新增/刷新后发布 `Added` / `Refreshed`，携带单调递增 revision、Actor/instance、变更前后数值和当前帧。通知在状态提交后发出；订阅者异常会被隔离，不影响战斗状态。
- 新增 `MobaShieldChangeEventTests` 覆盖 Added、Refresh 的字段与 revision 顺序，以及表现订阅者抛错时提交仍成立。
- 本批只收口个人盾 Add/Refresh 的事件语义。共享池成员变化、移除/过期/耗尽事件、回滚时的 presentation correction 以及 UI/VFX 消费联调仍未覆盖；按权威 `ShieldState` 快照重建的协议仍是客户端状态来源。

### 本轮继续：护盾生命周期事件扩展（2026-11-05）

- 在原有个人盾 Added/Refreshed 事件上，补齐个人盾显式移除、替换移除、过期与吸收耗尽通知；共享盾成员加入/离开通过 pool identity、pool version 和 actor identity 暴露。共享池被整体删除时，对活跃成员发出 Removed。
- 替换事件在替换结果完成重算后发布，避免同步观察者读到新旧盾之间的半提交状态。订阅回调异常仍被隔离，不回滚玩法状态。
- `MobaShieldChangeEventTests` 覆盖新增/刷新、替换事件的提交可见性、显式移除/过期及共享成员加入/离开。

边界仍需清楚标记：共享池整体过期/耗尽目前的清理事件语义尚未统一验证；回滚恢复仍应以权威快照做 presentation correction，revision 不代表跨回滚持久序号；UI/VFX 实际消费和 generation 对齐尚未联调。下一小批先补共享池生命周期和回滚重建契约，再接表现消费，避免把事件流误当成权威状态同步。

### 本轮继续：共享护盾终止与回滚表现修正（2026-11-05）

- 共享池过期和耗尽现在使用明确的 `Expired` / `Depleted` 终止事件；显式整体移除继续使用 `Removed`。终止前清除成员绑定并移除池，之后对原活跃成员逐一发通知，携带 PoolId、最终版本、旧余额和零余额。吸收耗尽只走池终止事件，避免 `Absorbed` 与终止事件对同一次耗尽重复表达。
- 个人盾 CleanupExpired 先删除并重算容器，再发布 Expired/Depleted；同步回调读取的是已提交状态。
- rollback provider 导入盾容器和共享池后，按 Actor 比较导入前后可见护盾总量；只有变化的 Actor 收到 `RollbackCorrection`。它是纠正提示，不代替快照，也不承诺恢复被移除 Shield instance 的事件级身份。
- 新增测试覆盖共享池过期时的单成员通知、通知时池已删除、回滚导入后 Actor 总量纠正，以及个人过期通知时容器已清理；护盾事件定向测试 8 项通过。

事件流仍是表现层提示。跨帧重连应使用 `MobaShieldStateSnapshotService` 的完整 ShieldState baseline；UI/VFX 消费、回滚后具体实例表现重建和共享池跨 rollback generation 的去重仍需后续接入验证。
