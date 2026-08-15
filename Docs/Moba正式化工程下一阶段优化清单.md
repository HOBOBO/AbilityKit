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
- 监听注册、ownerKey、trace context、ongoing plan component 多处维护，容易出现泄漏、重复注册或清理顺序不一致。

**建议落地**

1. 将 ongoing trigger plan 更新改成 diff apply：复用组件内列表，按 ownerKey / triggerId 增删，不在常规更新时重建整表。
2. 把 ownerKey 计算、监听注册、trace context 创建/结束封装到单一生命周期协调器。
3. 对被动技能、Buff 持有的触发计划统一接入同一套 owner-bound subscription contract。
4. 增加“重复注册、缺失释放、ownerKey 泄漏”的 runtime validator。

**验收标准**

- 被动技能刷新不会在无变化时替换 ongoing plan list。
- actor 销毁、Buff 结束、被动技能移除后，对应订阅与 trace context 全部释放并可测试验证。

**实施结果与保留边界（2026-08-15）**

- `MobaPassiveSkillLifecycleService` 已集中维护被动 listener、owner binding、ongoing plan、continuous runtime、action 与 passive root trace 的生命周期；actor 注销和被动移除通过同一 ownerKey 清理链路收口。
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

- `MobaSkillCastRuntimeService` 已存在 runtime aggregate、retain/release、children、trace context 等概念。
- `SkillExecutor.StartPreparedCast` 在 runner 启动失败时会 `ForceTerminate` runtime handle。
- Buff/projectile/summon 侧已有 retain/release 记录，但清理日志和异常处理仍分散。

**不足**

- 技能输入、pipeline runner、runtime aggregate、trace、临时实体的失败码与生命周期事件还没有完全统一。
- 等待 child 的 runtime、异常清理、trace end 失败等路径主要依赖日志与 warning。

**建议落地**

1. 定义 `MobaSkillCastLifecycleEvent`：Prepared、Started、Rejected、PipelineEnded、WaitingChildren、ForceTerminated、Finalized。
2. 让 `SkillExecutor` / `SkillPipelineRunner` / `MobaSkillCastRuntimeService` 共享同一失败码与生命周期事件。
3. 对 Buff/projectile/summon 的 retain-release 加 validator：检测悬挂 retain、重复 release、child 未完成导致 runtime 长期未 finalize。
4. 把 View/ET 的技能运行快照读取改为 buffer 填充，避免为了展示 running/ended snapshot 产生数组。

**验收标准**

- 任意技能施放实例都能从 runtime id 追踪到根 trace、children、结束原因和最终状态。
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
