# MOBA Runtime Context 运行时值与快照设计

本文以 2026-09-17 的实现为准，供设计评审使用。描述已实现行为；建议和未实现能力在后文单列。Runtime Context 是 MOBA 对 `AbilityKit.Context` 的业务适配，不是新增一套战斗真值。

## 1. 三个模块如何分工

| 层次 | 所有者与主要类型 | 数据/目的 | 不负责 |
|------|------------------|-----------|--------|
| Trace | `MobaTraceRegistry` / Trace 基础包 | 节点身份、因果树、生命周期、精简元数据 | Buff 的实时数值、完整状态恢复 |
| Context 基础设施 | `ContextRegistry`、`ContextValueResolver`、`SnapshotStorage` | 属性注册、外部实时 provider、快照与读策略 | 业务调度、自动采样、序列化协议 |
| MOBA 执行 Context | 强类型 payload、`MobaCombatExecutionContext` | 本次执行事实、来源归一化、领域输入 | 上游活对象的永久保存 |
| Runtime Context | `MobaRuntimeContextService`、reference/accessor | 独立身份下的 Buff 实时值与最终快照查询 | 逐帧历史库、所有 runtime 的统一注册、权威战斗状态 |

依赖方向：MOBA 领域生命周期调用 Runtime Context 服务；服务组合 Context 基础设施并绑定真实 BuffRuntime。执行载荷传递 reference，条件/行为通过 accessor 读取。Trace 来源 ID 只是该值视图中的关联字段，不用来读取 Runtime Context。

```text
BuffRuntime（领域真值）
  -> 实时 provider -> ContextValueResolver -> 条件/行为或工具读取
  -> 移除时复制最终字段 -> SnapshotStorage -> 销毁后只读查询

强类型执行 payload
  -> Origin / Lineage -> Trace 因果节点
  -> RuntimeContextReference(ID, Version) -> 上述值解析器
  -> 可选入口观察 -> 独立受管 Trace-facts 存储 -> Trace 摘要 / Artifact
```

## 2. 身份与引用语义

| 字段 | 命名空间与含义 |
|------|----------------|
| BuffRuntime.SourceContextId | Trace 来源节点；不是 Runtime Context entity |
| ParentContextId / RootContextId / OwnerContextId | 来源链的父级、根和所有权锚点；不能按数字相等映射到 Context Registry |
| RuntimeContextId | Runtime Context 服务内独立的 Context entity ID |
| RuntimeContextVersion | 引用携带的预期版本；不是帧号或逐次数值修订号 |
| RuntimeContextIdentity | RegistryId、EntityId、Generation；仅本地实例能力，不进入权威载荷/hash |
| SkillRuntimeHandle | 技能 runtime 的 ID、generation 和 root trace 关联；不是 Context entity |
| ActorId / BuffId / TriggerId | 角色、配置、触发器各自的身份，不是上下文 ID |

RuntimeContextReference 携带 ID/Version 和可选 ContextEntityReference，不携带活对象。服务绑定、恢复及正式 Buff 阶段/事件载荷传播本地实例身份；IsValid 要求正 ID，携带身份时还要求正 Version，并校验完整性及 EntityId 一致。部分损坏的身份不能降级成裸 ID。TryGetBuffReference 只读取当前实际绑定的身份，不创建数据。

旧 `new MobaRuntimeContextReference(id,version)` 仍为弱兼容引用，不具备跨服务或 ID 重用保护；结果 IsIdentityChecked=false，不能当作精确实例引用。新引用跨服务或命中同 ID 不同代次时返回 IdentityMismatch。业务恢复校验另要求正 ID、正 Version，本地 RegistryId/Generation 不序列化为恢复身份。

示例：技能 Trace 根为 10，Buff Trace 节点为 12，Buff Runtime Context 为 3。移除 payload 继续保留 SourceContextId=12 和 RuntimeContextId=3：前者导航来源链，后者读取最终值，两条路径互不替代。

## 3. 数据保存在哪里

`MobaRuntimeContextService` 是 scoped 逻辑服务，拥有 Registry、SnapshotStorage、RealtimeProviderRegistry 和 Resolver。目前只注册 `MobaBuffContextProperty` 的专用 provider。

实时 provider 按 ContextId 保存 BuffRuntime 引用及 target actor、绑定时 lifecycle state/frame。读取数值时从 runtime 投影 StackCount、Remaining、IntervalRemaining 等字段；不需要每帧复制到 Registry。绑定时的 state/frame 不是每次读值时自动刷新，不能将它们当成所有数值的最后修改时间。

`MobaBuffRuntimeContextData` 提供以下字段：BuffId、source/target actor、Trace/RootTrace/OwnerTrace ID、层数、剩余与间隔秒数、生命周期状态、帧、技能 runtime handle。ContextId/Version 由对应 property/snapshot/runtime reference 提供。

| 对象 | 保存内容 | 生命周期 |
|------|----------|----------|
| 实时 provider entry | BuffRuntime 活引用与绑定事实 | 正常绑定至移除解绑，仅由逻辑服务维护 |
| `MobaBuffContextProperty` | 读取时生成的值投影，使用 runtime Version | 单次读取视图，不是 Buff 真值 |
| `MobaBuffContextSnapshot` | 结束时复制的字段及 ID/Version | 内存留存，移除 runtime 后仍可读 |
| `MobaPersistentContextSourceSnapshot` | 跨帧来源身份与能力句柄 | 来源传播，不保存 Buff 的全部可变数值 |
| Buff state recovery payload | 明确导出的领域纯状态 | 显式导入重建，不等同于上述查询快照 |

Buff 最终快照接入受管路径：类型 `moba.buff.state`、SchemaVersion=1、Purpose=Observation、Kind=removed。字段在构造时复制，载荷不再实现可变销毁 hook，IsRealtimeAvailable 恒为 false。销毁事实在保存 metadata 中固定，不能通过窗口查询改变。

SnapshotStorage 支持多份受管记录，默认总容量 4096、每实体 16，自动淘汰最早保存且未保护的记录。MOBA 仍只在结束时采集一次最终状态，不因此增加逐帧采集。服务 Dispose 清理全部数据；当前未接入自动帧窗口/TTL 或 Trace retain 关联。工具关闭只清理自身订阅、样本和缓存，不销毁此逻辑服务。

每次 Context 实体创建或显式恢复由注册表分配本地单调 Generation，刷新绑定保持同一代次；Clear、回滚后重新创建也获得新代次。Buff 最终快照使用同一实体 Generation，服务不再维护另一套分配器。该代次用于实时实例引用和受管快照身份，不写入 Buff 权威载荷/hash，不改变 RuntimeContextVersion。旧 ID/Version 弱引用的别名风险仍存在。

工具使用 `SnapshotReader` 的只读接口，先取得记录并保存 ContextSnapshotReference，再通过 `ReadSnapshot<MobaBuffContextSnapshot, TValue>` 精确查询；不会因同 ID 新实例、最新记录变化或实时 provider 存在而改读其他数据。显式恢复会删除旧 ID 的全部快照，旧引用返回 Unavailable，而非继续作为权威事实展示。

## 4. 正常生命周期

### 创建、刷新与周期触发

`BuffContextRegistry.EnsureBuffContext` 维护 Trace/source/origin，并调用 Runtime Context 绑定。首次无有效实体时分配新 ID，Version 设为 1；已有实体必须实际绑定到同一 runtime，否则拒绝占用。刷新/周期更新重新绑定 state/frame，数值仍读领域真值。

BuffEventArgs 和 BuffTriggerContext 复制 RuntimeContextId/Version/Identity，查询时不从 Runtime 活对象重新取身份。延迟载荷在同 ID 恢复后仍指向旧代次，不能自动升级为新绑定；条件/行为不应从 SourceContextId 猜测 runtime ID。不要让工具打开动作触发绑定，也不要为了画曲线在领域数值修改点反向调用编辑器。

### 移除、异常与回池

```text
先提交移除（从活动列表删除）
  -> 停止并清理持续行为
  -> 结束 Trace / 取消 owner 动作
  -> 冻结最终字段、TrySaveManaged 快照
  -> 解绑实时 provider、Destroy Context entity、MarkDestroyed
  -> 保留 runtime ID/Version，发布移除事件 / 表现 / OnRemove 效果
  -> 释放技能保留与生命周期通知
  -> 清零引用与运行时绑定、回池
```

`EndByRuntimeNoClear` 采用 preserveReference=true，移除阶段仍可用原引用读最终快照，但 RealtimeOnly 不应命中。最终 ClearRuntimeBindings / 回池清零；即使结束 hook 抛异常，BuffEndFlow 仍继续清理并最终重抛首个异常。

重复销毁时不覆盖已有最终快照。缺少 Trace 来源 ID 不妨碍独立 Runtime Context 清理。直接 `SnapshotAndDestroyBuffContext` 默认清零引用；只有需要后续移除载荷读取的流程才显式保留。

最终快照是在持续行为清理之后复制的结束状态，不代表每个 OnRemove action 执行后的新状态；移除 action 即使改变活对象字段，也不应修改该冻结快照。

## 5. 读取策略、版本和失败

推荐入口：`payload.GetRuntimeContextValue<TValue, TProperty>(contexts, key, mode)`。先校验 reference 身份，再按模式解析一次实时属性或快照记录；Version>0 时从该对象读取版本并比较，最后从同一对象读取字段。版本缺失、版本不匹配或字段缺失不再转到另一来源借数据。精确实时属性需要实现 IContextValueProvider 提供一致视图；旧 ID-only 的逐键 provider 兼容读取固定同一个 provider 实例，不在每个字段上重新回退。旧快照仅实现 ISnapshotAccessor 时也固定同一对象，但仍无可靠字段命中标志，不具备精确受管快照契约。

返回 Found、Value、Source、Failure、ExpectedVersion、ActualVersion、IsIdentityChecked 和 ResolvedSnapshot。精确实时引用在实体存在时不会借用历史快照补缺失属性/字段；实体结束解绑后，允许回退的模式仅解析同 Generation 的 `moba.buff.state/removed` 记录。此回退是显式最新最终状态查询，不是固定历史引用；结果 ResolvedSnapshot 提供本次选中的精确快照身份，历史工具应保存它并使用 ReadSnapshot，后续不重复走最新查询。

| 模式 | 活动阶段 | 结束解绑后 |
|------|----------|------------|
| RealtimeThenSnapshot | 实时优先 | 最终快照 |
| RealtimeOnly | 实时 | 缺失，不用快照掩盖生命周期问题 |
| SnapshotOnly | 未保存则缺失 | 最终快照 |
| SnapshotThenRealtime | 快照优先，缺失读实时 | 最终快照 |

SnapshotOnly 不是“请求任意历史帧”。`TryGetBuffContext` 是属性投影读取快捷入口，当前最终字段快照不提供完整 property 对象；结束后应按键读值，不假设快捷 API 可还原属性。

Failure 包括 MissingContextService、MissingPayload、MissingRuntimeContext、InvalidRuntimeContext、VersionUnavailable、VersionMismatch、ValueMissing，以及 IdentityMismatch、ContextServiceDisposed、InvalidReadMode、ContextUnavailable、SnapshotUnavailable。精确引用区分实体/快照不可用与字段缺失；旧 ID-only 缺失保留 ValueMissing 兼容行为。新业务建议保留详细失败信息；兼容 bool API 只适合不需要诊断原因的调用点。

当前 Version 语义必须谨慎解释：

- 正常新绑定 Version=1；刷新、层数和剩余时间变化没有统一自动递增规则。
- 恢复保留持久化的正 Version，实时 property 和最终 snapshot 都使用该版本。
- payload Version>0 时做相等校验；Version<=0 的兼容读取跳过版本检查。
- 校验不是按 Version 定位历史值，也不保证旧 payload 读到“触发时数值”。需要触发时事实必须显式保存 stage snapshot。
- Context 游标回滚可能复用 ID，新绑定又为 Version=1；携带 RegistryId/Generation 的引用拒绝旧实例，旧 ID/Version 兼容引用仍不能防止别名。
- 版本和字段从同一选定属性/快照对象读取，实时读取结束后再次校验实例身份；可变自定义属性没有自动深拷贝或跨线程原子一致性保证，宿主应在模拟线程协调。

## 6. 状态恢复与本地回滚

| 路径 | 当前能力 | 明确限制 |
|------|----------|----------|
| Buff 权威恢复 | 预检后清理旧 Buff、按纯状态重建、显式 RestoreEntity、绑定新 runtime | 需要 actor、父技能 runtime 和可选 Continuous 依赖就绪；不是完整 Trace 历史恢复 |
| Context entity 回滚 | 保留仍存在的确认实体，撤销预测 provider/快照/实体并回退游标 | 确认实体被销毁则拒绝，不复活、不恢复完整生命周期 |
| Buff timer 回滚 | 恢复仍存在且成员身份匹配的 Buff 可变字段 | 成员或绑定形态变化拒绝，不重建 Buff |
| Skill Trace 局部回滚 | 恢复捕获根/子节点结束状态，撤销 Trace 分配边界后的预测节点 | 不恢复完整 Context、不恢复被 Purge 节点或所有未捕获节点的状态 |

Buff 恢复的非零 ID 必须唯一、不与无关实体冲突，当前 runtime 的实际 provider 归属也要通过预检。ID=0/Version=0 的旧状态保持无身份，不自动分配以免严格状态比较失败；其他无效组合拒绝。RestoreEntity 只推进分配游标，清除该 ID 旧快照后绑定新 runtime。

Buff 恢复载荷与哈希当前包含 RuntimeContextId/Version，表明它们是本恢复域身份的一部分；技能的本地 Trace 附加载荷不进入技能权威 hash。不能笼统地说“所有调试 ID 都不参与哈希”。

Context 回滚预检在解绑 provider、删除快照和销毁预测实体之前校验游标及确认实体；非法输入不先修改这些映射。确认实体保留本地代次，重放的新实体分配新代次。原载荷版本与权威 hash 不因此变化；预检不是并发业务对象和外部回调的完整事务。

查询快照没有统一深度序列化、完整基线或重建协议，不应直接拿来作为重连状态。跨帧来源 snapshot 中的技能 handle 只是 generation 校验的能力值，使用前还需解析；来源仍可展示不代表技能还能执行。

## 7. 工具与性能边界

逻辑服务负责绑定、快照与清理，即使所有调试窗口关闭也必须正确。允许诊断系统由逻辑开关决定是否采集，但工具消费只能读现成 Registry/映射/快照，不驱动写入、不补业务状态。

数值曲线消费只读诊断属性样本并保存编辑器自身历史，面板查询不负责修改或补采领域数据。曲线受逻辑诊断采样间隔和工具刷新间隔影响，可以遗漏两次采样间的变化，也不能反推出未采样帧的真实值；需要精确逐事件分析时应显式设计诊断采集协议，不在现有 Trace 热路径塞入大数据。

实时字段读取存在属性投影分配、命名值装箱/类型检查；结束时只复制必要字段。当前设计减少每帧镜像写入，但没有零分配或任意规模性能保证，应按真实负载测量。Snapshot 已有独立容量和 retain 管理，尚未与 Trace 的留存窗口统一。全部快照容量被 retain 时采集返回 false，不能阻断 Buff 移除；工具不得申请 retain 来反向控制逻辑保存。

### 7.1 效果执行入口的观察快照

`MobaEffectExecutionService` 在正式 EffectExecution 节点建立、执行上下文推进后，通过可选 `IMobaEffectExecutionSnapshotHook` 提交入口观察。逻辑层诊断处于 Events/Full、启用 Skill 通道且未冻结时才采集；窗口是否打开不参与判断。只复制现有阶段数值和 runtime 引用，不序列化完整 payload，不调用 Runtime Context 的最新值解析器补齐缺失字段。hook/provider 异常与保存失败不阻断效果执行。

外部 `MobaEffectExecutionSnapshotStore` 复用受管快照机制，类型为 `moba.effect.execution-entry`、SchemaVersion=1、Purpose=Observation、Kind=execution-entry，默认最多 4096 份，每个执行节点一份。此存储的 EntityId 明确属于 TraceContextId 命名空间，和 Runtime Context 服务的快照存储独立；不能拿这个 ID 查询 ContextRegistry。

`MobaEffectExecutionEntrySnapshot` 不保存原始 payload 或 BuffRuntime，只保存采集帧、真实效果配置 ID、触发计划 ID、payload 类型名、RuntimeContextId/Version 和阶段的层数/持续/剩余/总时长。阶段是否存在以 provider 的 bool 返回为准，全零阶段仍可存在；未提供不显示成零。Runtime ID/Version 是入口时的引用事实，不代表已冻结其全部实时字段。直接 trigger 的真实 EffectConfigId 可为 0，不把 trigger ID 补成效果配置。

MobaTraceMetadata 仅附加精确快照引用，不把数值写入基础 TraceContextRecord。每节点只采集一次，不在快照淘汰后重新读取 live payload。诊断 Trace 摘要带上纯 `BattleDiagnosticEffectExecutionFacts`，查询版本组合结构和快照修订；本地窗口、Artifact 导出/导入共享这一事实对象。旧 Artifact 缺字段时标记 NotCaptured，不猜测历史值。

容量自动淘汰后，Trace 节点仍可能存在，但摘要明确显示 Evicted。Trace Purge、预测分配撤销和 Clear 同步删除相应外部记录，Dispose 解绑订阅并清数据。Clear 后新代次与 SnapshotId 不别名旧引用；窗口关闭不删除逻辑快照，也不申请 retain 来改变逻辑保存。本批未把所有存活 Trace 根永久 retain 快照，避免因树长期留存耗尽容量。

该快照仅表示效果执行入口事实，不包含每个 action 的前后状态、最终伤害计算结果、全部角色属性或完整恢复基线；条件/预算拒绝且尚未建立正式执行节点时没有这份快照。动作前后事实由下一节的独立类型提供，不扩大入口快照的含义。

### 7.2 动作执行前后事实与实际 HP 提交

`MobaEffectExecutionService` 复用已有 Action 作用域入口、正常退出和作用域中止路径，调用可选 `IMobaActionExecutionSnapshotHook`。结束观察发生在 CurrentAction 身份重置及 Trace.End 之前，按精确 ContextId + ActionIndex + ActionId 配对；嵌套效果各自维护动作身份，不从结束时的“当前动作”猜测入口。观察异常不改变动作执行结果，缺少帧服务时不伪造采集帧。

`MobaActionExecutionSnapshotStore` 使用独立受管存储，类型 `moba.action.execution`、SchemaVersion=2、Purpose=Observation，Kind 为 action-entry/action-end。v1 保存动作资源值与 HP 提交，v2 增加独立伤害管线结果和观察覆盖状态；版本不是恢复协议。默认总容量 4096 份，每节点最多 2 份；待完成观察也有界，每动作 HP 提交和伤害结果分别最多 32 条，超过分别标记截断。metadata 的 ActionSnapshot 只保存最新阶段的精确引用，结束记录包含入口冻结字段，不能因入口被淘汰而重读实时值。入口引用在结束后仍表示入口，不能自动升级成结束记录；结束记录淘汰也不回退入口或最新实时数据。

前后数值只读取来源、目标实体已有 ResourceContainer.Map 中的 HP/Mana Current，缺失 Actor、缺失资源与真实零分别表示，不调用 GetOrCreate 或会初始化资源的 helper，不保存实体、payload 或全部属性列表。实体绑定标识是该诊断存储的本地单调 token，弱引用索引不延长实体寿命，并结合 Entitas creationIndex 识别对象复用；它不是 Runtime Context generation。Actor ID 相同但绑定变更或任一实体缺失时，工具不计算前后差值。

调用状态分别为 EntryOnly、Completed、Failed、Aborted。Completed 仅表示 Triggering 执行过程（含前后 cue）未抛异常，不证明产生了玩法效果；Failed 也可能已有部分真实提交；Aborted 表示作用域清理而未收到正常退出。入口采集关闭不会在结束时补采；结束时冻结/关闭或精确入口记录已淘汰，则不读取实时字段、不补结束记录，现存入口明确缺少结束事实。

实际 HP 提交复用逻辑层已发布的 `health.change.committed` 强类型事件，不在伤害/治疗业务热路径额外调用调试工具。只复制 `MobaHealthChangeResult` 中的类型、来源/目标、数值类型/原因、请求与实际值、HP 前后与上限、即时来源节点 ID。关联从有效即时节点（缺失时仅用已提供的父 ID）沿真实保留树查找最近 Action，校验已有根身份，最多查找 256 层；遇到未采样的最近动作、无节点或根冲突时不归入外层“当前动作”。订阅属于同一世界事件总线，来源中未包含跨世界身份，因此不可接共享跨世界总线并仅凭整数 ID 证明归属。

资源前后差值包含执行期间嵌套行为的影响；提交列表只归属于最近动作，不能把外层差值等同于外层列表之和。多目标动作的提交列表可包含上下文目标之外的目标，但前后资源视图只覆盖上下文来源/目标，不是完整目标集。当前只记录动作作用域期间的实际 HP 提交，不承诺覆盖异步后代、护盾全吸收、拒绝、零效果或未进入提交的分支；零条提交不能推导动作未执行，也不等于没有其他玩法效果。

CommitsComplete 表示已订阅已知同步 EventBus、动作期间观察开关没有变化且没有观察到采集间隙，不表示“整个战斗的结果完整”；CommitsTruncated 另表示数量截断。EventBus 仅增加只读 DispatchMode，不改变事件执行逻辑。排队模式或无法确认派发模式的自定义 IEventBus 保守标记不完整，动作结束后的延迟派发不改写已冻结结果，观察器绝不调用 Flush 推进业务事件。逻辑诊断模式、通道和统一冻结入口维护 CaptureRevision，往返切换后仍能标记覆盖不完整，不把恢复采集后的局部结果当全量。独立直接操作底层 ring store 不属于统一控制协议。读取只提供纯不可变事实 DTO，提交集合复制冻结；本地 Trace 详情与 Artifact 导出/导入共享这些字段，旧 Artifact 缺字段为 NotCaptured。

Purge、预测撤销、Trace.Clear 和 Dispose 清理快照及待完成记录，外部 Trace.End 丢弃未配对的待完成观察，Dispose 解绑事件订阅。Clear 不回退 SnapshotId，增长存储代次。窗口关闭只清工具缓存，不删除逻辑快照、不申请 retain、不决定采集开关。这些观察事实不进入权威状态载荷/哈希，不是可恢复快照或完整数值曲线回放。

### 7.3 未产生 HP 提交的伤害管线结果

HP 提交列表不覆盖护盾吸收、拒绝和零伤害，不能用“无提交”猜测这些结果。动作快照 v2 另复制逻辑诊断收集器已接受的 DamageCalculation 不可变事件，不新增原始攻击/计算对象的序列化或持久引用，不在工具查询时运行规则或补采。收集器只在事件成功入库后通知内部观察者，各观察者异常隔离，不改变事件接受结果。没有待完成观察时直接返回，不遍历因果树。复制后伤害事实随动作快照留存，即使原诊断事件环形缓冲已经淘汰该事件也不丢失已冻结事实。

`DamagePipelineService` 已有 TargetMissing、ShieldCommitRejected、HealthCommitRejected、Completed 终态，本批补充 InvalidRequest（未进入核心的无效目标）、TransactionRejected（事务取消）、ExecutionFailed（异常出口）、PostCommitNotificationFailed（核心结果已返回后的通知异常）。异常仍原样抛出，拒绝仍按原接口返回 null，不修改伤害/护盾提交规则。事务失败说明只保留最多 128 字符，异常只保留类型名；没有采集计算结果的阶段明确 HasCalculation=false，不将零填充值冒充真实数值。

动作伤害结果保存事件序号/帧、来源/目标、来源节点、终态、基础/原始/减免后/护盾计划/HP 计划与实际的 Q32.32 raw 值以及有限详情。分类使用这些冻结事实：Completed 且实际 HP 值为正表示 Applied；HP 计划不为正、减免后伤害为正且护盾覆盖减免后伤害表示 FullyAbsorbed；其他 HP 计划不为正表示 NoHpDamage。不能仅凭原始伤害为零猜测免疫、未命中或具体规则原因。拒绝时显示的是护盾计划，不是已成功吸收值。ExecutionFailed 可能已有部分 HP 提交；提交完成后通知异常另外记录，不把已经完成的 HP 提交抹成失败。

结果沿真实 Trace 树归属最近动作，遇到未采样嵌套动作或根身份冲突不归入外层。采集同时受动作观察的 Skill 开关和诊断 DamageAndHeal 通道控制；缺少同世界同收集器或关闭伤害通道时状态为 NotCaptured。DamageCoverageContinuous 仅说明已开启的伤害通道在该动作期间没有开关间隙或已知事件入库失败，不保证所有伤害入口都会提供终态；DamageResultsTruncated 单独表示数量截断。HP 提交覆盖状态与伤害诊断覆盖状态独立，排队业务事件总线不会被观察器 Flush。

本地 Trace 详情和 Artifact 共享纯 DTO；旧动作 Artifact 缺伤害字段时显式 NotCaptured，旧 DamageCalculation 的 raw 格式/SchemaVersion=1 保持不变，原终态整数值不改变，仅扩展终态枚举。当前覆盖正式 DamagePipeline，不承诺直接 CommitDamage 的拒绝、治疗零恢复、所有规则失败原因、每个公式中间阶段或动作结束后的异步后代；这些需要分别复用业务结果协议后再扩展，不能反向改变战斗执行来方便展示。

## 8. 设计评审重点与未实现项

当前已实现：Buff 按独立身份读取实时/最终值、移除阶段保留引用、显式身份恢复和冲突预检、技能 Trace 局部回滚与预测撤销、只读查询缓存刷新。

需要评审后再确定的方向：

1. Runtime Context 是否只作为可选读适配层，而非所有战斗对象的统一数据袋。
2. 本地 RegistryId/Generation 已保护正式引用；其他仍只传播 ID/Version 的兼容载荷需要按调用边界逐步迁移，不能在读取旧载荷时补成当前代次。
3. 存储已有受管多份记录；是否需要增加施加/执行等业务采集点，仍应按用途与性能明确，不能默认逐帧复制。
4. 完整 Buff/Context 生命周期回滚如何恢复 provider、Continuous、技能保留与 Trace 保留，不能只放宽 missing 校验。
5. 快照和 Trace 历史的容量、TTL、清理时机及回滚窗口保护如何协调。
6. Buff 观察事件当前可能把 RuntimeContextId 放入通用 ContextId，而其他 Trace 导航消费该字段；两类引用尚需显式区分。
7. 权威恢复目前不重建完整 Trace 历史/生命周期保留通知，冷恢复后的溯源完整性不能按正常执行路径保证。
8. 精确实体引用不覆盖 FlowContextScope 的旧 flow ID；TraceRegistryDirectory 的静态列表也未保护并发注册/移除。此批不改变基础 Trace 的线程安全约束，测试与工具需按宿主线程边界使用。

以上为仍需评审的限制或建议，不代表已落地保证；受管快照管理和效果执行入口观察已实现，不能据此推导完整数值回放或恢复能力。

## 9. 实现与验证入口

- [Context 基础设计](../../com.abilitykit.context/Document/Context上下文注册与快照模块开发设计文档.md)
- [Trace 基础设计](../../com.abilitykit.trace/Document/Trace溯源树模块开发设计文档.md)
- [MOBA 整体上下文指南](../Runtime/Docs/MobaCombatContextDesignGuide.md)
- [Runtime Context 服务](../Runtime/Application/Services/Context/Runtime/MobaRuntimeContextService.cs)
- [引用与 accessor](../Runtime/Application/Services/Context/Runtime/MobaRuntimeContextAccess.cs)
- [Buff 值模型与最终快照](../Runtime/Application/Services/Context/Runtime/MobaBuffContextProperty.cs)
- [效果入口载荷](../Runtime/Application/Services/Context/Snapshots/MobaEffectExecutionEntrySnapshot.cs)
- [效果入口快照存储](../Runtime/Application/Services/Diagnostics/MobaEffectExecutionSnapshotStore.cs)
- [动作快照载荷](../Runtime/Application/Services/Context/Snapshots/MobaActionExecutionSnapshot.cs)
- [动作快照存储](../Runtime/Application/Services/Diagnostics/MobaActionExecutionSnapshotStore.cs)
- [Buff 移除流程](../Runtime/Application/Services/Buffs/Lifecycle/BuffEndFlow.cs)
- [Buff 状态恢复](../Runtime/Application/Services/Buffs/MobaBuffStateRecoveryProvider.cs)
- [Context entity 回滚](../Runtime/Application/Rollback/MobaContextEntityRollbackProvider.cs)
- [技能与 Trace 本地回滚](../Runtime/Application/Rollback/MobaSkillRuntimeRollbackProvider.cs)

近期定向测试覆盖恢复身份/版本、冲突拒绝、移除异常清理、冻结值、同帧预测撤销、树查询刷新和诊断标记。[Runtime 身份回归](../../../../src/AbilityKit.Demo.Moba.Tests/Trace/MobaRuntimeContextIdentityTests.cs) 另覆盖跨服务误用、同 ID/Version 恢复、游标重放复用、载荷身份冻结、来源混读拒绝、弱兼容 provider/accessor、非法回滚无副作用和 Dispose 后禁止绑定；不构成完整跨域回滚、冷恢复或大规模性能验证。

文档版本：1.5。最后更新：2026-09-17。
