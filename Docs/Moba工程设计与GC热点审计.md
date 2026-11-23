# Moba 工程设计与 GC 热点审计

## 背景

本轮审计目标不是继续做零散修补，而是重新从当前实现出发，排查 MOBA runtime/view 工程中“不够完善”或“设计边界不合理”的点。重点关注：

- 校验、日志、诊断是否会在高频路径中提前构造字符串。
- 每帧/每事件路径是否存在 `new List<T>`、`ToArray()`、`HashSet<T>`、字典重建、装箱等分配。
- 兼容层、调试层、验证层是否缺少明确开关、生命周期约束和频率边界。
- 表现层是否把临时字符串 key、日志和 DTO 转换放到了帧同步或快照分发路径。

## 总体结论

当前工程的问题不是单个 `string` 或单个 `ToArray()`，而是多个横切设计边界还没有收敛：

1. **校验/诊断/日志 API 默认使用字符串**，调用方经常在进入 API 前就完成字符串插值，导致即使最终不输出日志，也已经产生 GC。
2. **运行时系统执行辅助层没有提供延迟格式化能力**，`Require`、`Warn`、`RecordDuration` 的调用点普遍传入已构造好的 `detail/context/message`。
3. **事件总线为了兼容 typed/object 两套订阅，会在热事件上重复发布并装箱**，区域、伤害等路径都存在这个模式。
4. **被动技能持续触发计划的同步方式偏“重建式”**，每次刷新用 `HashSet`、`Dictionary`、`List`、数组复制来求差和更新，适合功能验证，但不适合频繁变更或大量单位。
5. **快照/回滚/输入路径仍以数组复制作为外部契约**，在固定帧或网络帧上存在稳定分配。
6. **表现层快照处理仍有字符串 key 和日志分配**，虽然不直接影响权威逻辑，但会影响客户端帧稳定性。
7. **验证系统虽当前主要在 bootstrap 运行，但缺少显式运行模式/开关限制**，后续如果被放入热重载、编辑器轮询或运行时巡检，会放大字符串和集合分配问题。

## P0：高频运行时 GC 与帧稳定性问题

### 1. 诊断耗时上下文提前拼接

相关位置：

- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Diagnostics/MobaBattleDiagnosticsService.cs:20`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Systems/MobaWorldSystemExecution.cs:117`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Skills/Pipeline/MobaSkillPipelineStepSystem.cs:86`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Systems/Effects/MobaEffectsStepSystem.cs:96`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Combat/Damage/DamagePipelineService.cs:106`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Buffs/MobaBuffService.cs:217`

现状：

- `RecordDuration(metricName, startTimestamp, warnThresholdMs, string context)` 要求调用方传入 `string context`。
- 多个系统直接传入 `$"candidates={entities.Length} stepped={stepped}"`、`$"pending={pendingAtStart} executed={executed} remaining={_pending.Count}"` 等。
- 即使最终耗时没有超过 warn 阈值，字符串参数也已经在调用前构造。

风险：

- 技能、效果、伤害、Buff drain 都是帧内或事件高频路径。
- 当诊断服务启用时，每帧会形成稳定小对象分配。
- 这种分配很隐蔽，因为表面上 `RecordDuration` 内部只有超阈值才日志，但上下文已在外部创建。

建议：

- 新增无分配或低分配 API：
  - `RecordDuration(metric, start, threshold)`：热路径默认不带字符串。
  - `RecordDuration(metric, start, threshold, MobaDiagnosticContext context)`：结构化字段。
  - `RecordDuration(metric, start, threshold, Func<string> contextFactory)`：仅慢路径触发时构造字符串，注意闭包也可能分配，需谨慎用于非极热路径。
- 优先把技能、效果、Buff、伤害的上下文改成：
  - 常规采样用 `Sample/Gauge/Counter` 记录数值。
  - 慢路径日志才拼接字符串。
- `MobaWorldSystemExecution.RecordDuration` 应作为统一入口，先补能力再迁移调用点。

### 2. 被动技能触发计划重建式更新

相关位置：

- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Skills/Passive/Systems/MobaPassiveSkillTriggerRegisterSystem.cs:80`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Skills/Passive/Systems/MobaPassiveSkillTriggerRegisterSystem.cs:88`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Skills/Passive/Systems/MobaPassiveSkillTriggerRegisterSystem.cs:116`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Skills/Passive/Systems/MobaPassiveSkillTriggerRegisterSystem.cs:135`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Skills/Passive/Systems/MobaPassiveSkillTriggerRegisterSystem.cs:139`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Skills/Passive/Systems/MobaPassiveSkillTriggerRegisterSystem.cs:154`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Skills/Passive/Systems/MobaPassiveSkillTriggerRegisterSystem.cs:194`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Skills/Passive/Systems/MobaPassiveSkillTriggerRegisterSystem.cs:201`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Skills/Passive/Systems/MobaPassiveSkillTriggerRegisterSystem.cs:233`

现状：

- 每次 `UpdateOngoingTriggerPlansFromPassive` 会创建：
  - `new HashSet<long>()`
  - `new Dictionary<int, long>()`
  - `new List<long>()`
  - `new List<long> { ownerKey }`
  - `new int[triggerIds.Count]`
  - 新的 `List<OngoingTriggerPlanEntry>`
  - 新的 `OngoingTriggerPlanEntry` 对象
  - `_ownerKeysByActor[actorId] = new HashSet<long>(desired)`
- 当前系统是 Reactive，但只要 `SkillLoadout` 替换频繁、角色多、被动多，GC 会线性放大。

风险：

- 被动技能系统属于战斗核心链路，后续玩法复杂后容易成为尖刺来源。
- 数据结构是“为不可变替换”设计的，但没有与触发频率绑定，也没有复用缓冲区。

建议：

- 第一阶段：引入系统级复用缓冲，避免每次创建临时 `HashSet/Dictionary/List`。
- 第二阶段：把 `new List<long> { ownerKey }` 改成单 key 删除 API。
- 第三阶段：让 `OngoingTriggerPlans` 组件支持就地更新或池化列表，减少整表替换。
- 第四阶段：把 `TriggerIds` 数组从运行时复制改为配置侧预编译只读数组，组件只引用稳定数组。

### 3. 事件总线 typed/object 双发布与装箱

相关位置：

- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Systems/Area/MobaAreaSyncSystem.cs:95`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Systems/Area/MobaAreaSyncSystem.cs:121`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Systems/Area/MobaAreaSyncSystem.cs:122`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Combat/Damage/DamagePipelineService.cs:152`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Combat/Damage/DamagePipelineService.cs:163`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Combat/Damage/DamagePipelineService.cs:169`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Combat/Damage/DamagePipelineService.cs:175`

现状：

- 区域事件构造 `AreaEventArgs` 后先发布 typed payload，再 `object boxed = payload` 发布 object payload。
- 伤害事件对 `AttackInfo`、`AttackCalcInfo`、`DamageResult` 也有 typed/object 双发布。
- 如果 payload 是 struct 或含 struct 字段，object fallback 可能装箱；即使 payload 是 class，也会多一次事件派发和订阅匹配成本。

风险：

- 区域 enter/exit、伤害 pipeline 都是战斗高频事件。
- object fallback 属于兼容层设计，但现在没有显式开关，也没有判断是否存在 object 订阅者。

建议：

- 为事件总线增加 `HasSubscribers(EventKey<object>)` 或订阅计数查询，只有存在 object 订阅才 fallback 发布。
- 对 gameplay 触发路径统一迁移到 typed event，object event 只保留调试/兼容模式。
- 增加 `MobaEventPublishOptions`：`TypedOnly`、`TypedAndObjectCompat`、`DebugMirror`。
- P0 先处理区域和伤害两条高频路径。

### 4. 热路径日志 API 缺少调用前 gating

相关位置：

- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Diagnostics/MobaRuntimeLog.cs:81`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Diagnostics/MobaRuntimeLog.cs:139`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Systems/Motion/MobaMotionTickSystem.cs:66`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Systems/Motion/MobaMotionLocomotionInputSystem.cs:92`
- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Client/SnapshotRouting/FrameSnapshotDispatcher.cs:110`

现状：

- `MobaRuntimeLog.Write` 内部会判断 `ShouldLog`，但调用方已经构造 `message`。
- 多个运行时和 view 路径直接使用 `$"..."` 传入 `Log.Info/Warning/Error`。
- 运动系统虽然有采样次数限制，但采样命中时仍会产生大字符串。

风险：

- 如果日志级别关闭，仍可能因为调用方插值造成分配。
- 视图层快照分发日志如果未关闭，会随着网络帧稳定分配。

建议：

- 为 `MobaRuntimeLog` 暴露 `IsEnabled(level, purpose)`，让调用方先判断再拼字符串。
- 增加延迟格式化重载：`Info(context, Func<string>)`，但热路径要避免闭包捕获。
- 对运动、快照分发等路径默认移到 `Trace/Investigation` 且要求开关启用。
- 对 view 层提供统一 `BattleViewLog`，默认 release/normal mode 不做字符串日志。

## P1：帧同步、快照、输入、表现层分配问题

### 1. 快照/回滚导出使用 List + ToArray

相关位置：

- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Rollback/MobaActorTransformRollbackProvider.cs:24`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Rollback/MobaActorTransformRollbackProvider.cs:26`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Rollback/MobaActorTransformRollbackProvider.cs:37`

现状：

- Transform rollback 每次导出创建新 `List<Entry>`，再 `entries.ToArray()`。
- 2026-11-17 已移除无业务生产/消费方的 Passive trigger 事件回滚日志，不再为历史事件导出数组；该项结束，不能作为优化事件快照分配的依据。
- `BinaryObjectCodec.Encode` 接口以数组 payload 为核心，推动上游复制。

风险：

- 如果 rollback snapshot 是固定帧周期导出，会形成稳定 GC。
- 未来 actor 数量增加后，数组复制成本会扩大。

建议：

- 为 rollback provider 增加实例级复用 `List<Entry>` 缓冲。
- 为 codec 增加 `IReadOnlyList<T>` 或 writer callback 支持，减少强制 `ToArray()`。
- 对 rollback 导出做频率策略：仅关键帧或状态变化后导出。

### 2. 本地输入队列按帧 ToArray

相关位置：

- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Shared/Domain/BattleLocalInputQueue.cs:41`
- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Shared/Domain/BattleLocalInputQueue.cs:43`

现状：

- 每次 `Flush()` 都把 `_buffer` 转为数组入队。
- 空输入使用 `Array.Empty<T>()` 没问题，有输入时每帧分配数组。

风险：

- 本地输入是帧同步核心路径；移动/技能输入频繁时会稳定分配。
- 外部契约 `ILocalInputSource<LocalPlayerInputEvent[]>` 固化了数组语义。

建议：

- P1 先复用固定容量 frame input buffer 或引入 pooled array。
- 中期将契约改为 `IReadOnlyList<LocalPlayerInputEvent>` 或自定义 struct span-like reader。
- 如果网络协议最终需要数组，在序列化边界再集中复制。

### 3. 表现层 PresentationCue 使用字符串 request key

相关位置：

- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Presentation/ViewEvents/BattlePresentationCueViewEventHandler.cs:18`
- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Presentation/ViewEvents/BattlePresentationCueViewEventHandler.cs:55`
- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Presentation/ViewEvents/BattlePresentationCueViewEventHandler.cs:177`
- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Presentation/ViewEvents/BattlePresentationCueViewEventHandler.cs:184`
- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Presentation/ViewEvents/BattlePresentationCueViewEventHandler.cs:189`
- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Presentation/ViewEvents/BattlePresentationCueViewEventHandler.cs:192`

现状：

- `_activeByRequestKey` 是 `Dictionary<string, IEntityId>`。
- 当数据没有 `InstanceKey/RequestKey` 时，会拼接 `cue:...` 字符串作为 dedupe key。

风险：

- 表现 cue 可以随技能、Buff、伤害频繁出现。
- 字符串 key 会造成分配，也使 key 语义散落在字符串格式里。

建议：

- 引入 `PresentationCueRequestKey` struct：包含 `TriggerId/TriggerEventId/ActionIndex/Order/SourceActorId/TargetActorId`。
- 字符串外部 key 保留为兼容输入，但内部字典使用 struct key。
- 对 `InstanceKey/RequestKey` 做稳定 id 映射，避免每帧字符串字典查找。

### 4. view/net DTO 状态把枚举 ToString 放到运行时路径

相关位置：

- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Client/Session/Features/Sim/RemoteDrivenInputRuntime.cs:40`
- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Client/Session/BattleSessionNetAdapter.cs:60`

现状：

- `MissingMode = _buffer.MissingMode.ToString()` 这类转换把 enum 转字符串用于状态 DTO。

风险：

- 如果状态 DTO 每帧刷新，会造成无意义字符串分配。

建议：

- DTO 内保留 enum/int，UI 展示层最终渲染时再格式化。
- 对调试面板增加刷新频率限制。

## P2：Bootstrap/验证/配置设计边界问题

### 1. Runtime validation 全字符串报告模型

相关位置：

- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Validation/MobaRuntimeValidation.cs:18`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Validation/MobaRuntimeValidation.cs:44`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Validation/MobaRuntimeValidation.cs:88`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Validation/MobaRuntimeValidation.cs:93`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Validation/MobaRuntimeValidation.cs:99`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Systems/Bootstrap/Flow/Stages/PlanTriggeringStage.cs:96`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Systems/Bootstrap/Flow/Stages/PlanTriggeringStage.cs:100`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Systems/Bootstrap/Flow/Stages/PlanTriggeringStage.cs:105`

现状：

- `MobaRuntimeValidationEntry` 由 `Source/Path/Message/BusinessId` 字符串组成。
- `FormatSummary()`、`FormatEntry()`、`FormatAllEntries()` 都构造字符串。
- 配置校验器大量拼接 `path`、`businessId.ToString()`、错误描述。
- 当前入口在 `PlanTriggeringStage` bootstrap 调用，暂时不是每帧问题。

风险：

- 当前安全性依赖“只在 bootstrap 调用”的隐式约定。
- 如果后续接入运行时热重载、编辑器巡检、GM 命令、在线诊断，会产生大量字符串和列表分配。
- 全字符串模型不利于国际化、错误码聚合、批量压缩和工具侧过滤。

建议：

- 增加 `MobaRuntimeValidationMode`：`Disabled`、`BootstrapStrict`、`EditorFull`、`RuntimeSampled`、`ManualOnly`。
- 在 `IMobaRuntimeValidationRunner` 层加入运行频率限制和显式开关，不允许系统直接无条件调用。
- 把 entry 改为结构化：`Code`、`SourceId`、`PathId`、`EntityKind`、`BusinessIntId`、`Severity`。
- 字符串 message/path 只在导出、日志、编辑器展示时生成。
- 对 config validation 预编译常用路径或使用 error code + 参数，不在循环中拼 path。

### 2. Validator contract 每次 bootstrap 反射创建

相关位置：

- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Validation/MobaRuntimeValidation.cs:191`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Validation/MobaRuntimeValidation.cs:216`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Validation/MobaRuntimeValidation.cs:222`

现状：

- `CreateDefault()` 每次构造 required validator 列表。
- `RegisterInto()` 用 `Activator.CreateInstance` 创建 validator。

风险：

- 属于 bootstrap 低频，不是 P0。
- 但体现出 validator 系统还没有“注册表/工厂缓存/静态契约”的边界。

建议：

- 将默认契约静态缓存。
- 用泛型注册函数或预编译工厂替换 `Activator.CreateInstance`。
- Registry 防重复注册，避免多次 bootstrap/测试场景重复 validator。

### 3. Bootstrap reflection 与日志偏调试化

相关位置：

- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Systems/Bootstrap/Flow/MobaBootstrapStageInitializer.cs`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Systems/Bootstrap/Flow/Stages/TriggerPlansStage.cs`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Systems/Bootstrap/Flow/Stages/WorldInitStage.cs`

现状：

- bootstrap 阶段有反射扫描、`GetCustomAttribute`、`ToArray`、路径日志等。
- 这些通常可接受，但当前没有统一分层：哪些是 editor/dev 才开，哪些是 server runtime 必须开。

建议：

- 明确 bootstrap profile：`DevelopmentVerbose`、`ServerProduction`、`ClientProduction`。
- Server production 默认只输出关键错误和摘要指标。
- 反射扫描结果缓存到静态表或构建期生成。

## 横切设计问题

### 1. 缺少“频率边界”这一层抽象

很多 API 从功能上是对的，但没有表达“可以在哪里调用”：

- validation：理论上应 bootstrap/editor/manual，不应 per-frame。
- diagnostics context：应只在慢路径格式化，不应每帧拼字符串。
- logging：应先判断级别，再构造消息。
- object event fallback：应只在兼容订阅存在时启用。
- snapshot/export：应按固定策略导出，不应由数组契约迫使每帧复制。

建议增加统一标记或约定：

- `HotPath`：禁止字符串插值、LINQ、`ToArray()`、临时集合、反射。
- `FramePath`：允许数值采样和复用容器，不允许日志字符串常态化。
- `EventPath`：允许必要 payload，但避免 object mirror 和装箱。
- `BootstrapPath`：允许字符串和反射，但需要开关和摘要输出。
- `EditorOnly`：允许完整报告和富文本字符串。

### 2. 日志、诊断、异常三套观测能力还没有统一成本模型

当前已有：

- `MobaRuntimeLog`
- `MobaBattleDiagnosticsService`
- `MobaBattleExceptionPolicyService`
- `MobaWorldSystemExecution`

但成本模型不一致：

- 日志内部有级别开关，但调用方先构造字符串。
- 诊断有 warn 阈值，但调用方先构造 context。
- 异常 policy 可统一处理，但 detail 仍多为字符串。

建议：

- 以 `MobaWorldSystemExecution` 为 runtime 系统统一入口。
- 提供结构化 detail：`actorId/skillId/runtimeId/frame/count/duration`。
- 只有最终写日志时格式化字符串。
- 文档约束：热路径禁止直接使用 `AbilityKit.Core.Logging.Log`。

### 3. 兼容层没有成本开关

典型例子：

- typed event + object event 双发布。
- diagnostic state fallback。
- view 字符串 key fallback。
- validation string report fallback。

建议：

- 每个兼容层都应有显式 `CompatMode` 或 `DebugMode`。
- 默认 production 使用强类型/结构化/零或低分配路径。
- 工具和调试模式再启用字符串、object、完整报告。

## 建议落地批次

### Batch 1：P0 热路径观测零分配化

目标：先消除最隐蔽、最容易扩散的字符串 GC。

改造项：

1. 给 `MobaRuntimeLog` 增加公开 `IsEnabled(level, purpose)`。
2. 给 `MobaBattleDiagnosticsService` 增加无 context 的热路径推荐 API，并让慢路径才格式化 context。
3. 给 `MobaWorldSystemExecution.RecordDuration` 增加结构化/延迟 context 重载。
4. 修改技能、效果、Buff、伤害调用点，不再每次传 `$"..."`。
5. 将运动和快照分发日志改为 trace/investigation 且调用前 gating。

验收标准：

- 技能/效果/Buff/伤害常规帧执行时，不因诊断 context 产生字符串分配。
- 日志关闭时，调用点不构造消息字符串。

### Batch 2：P0 被动触发计划与事件兼容层降分配

目标：处理真实玩法扩展后最可能变成尖刺的系统。

改造项：

1. `MobaPassiveSkillTriggerRegisterSystem` 引入复用缓冲。
2. 增加单 ownerKey 删除 API，移除 `new List<long> { ownerKey }`。
3. 缓存 passive skill trigger id 数组，避免每次复制。
4. `AreaSync`、`DamagePipeline` object fallback 改成可开关或订阅存在才发布。

验收标准：

- 被动技能 loadout 刷新不再创建多组临时集合。
- 无 object 订阅时，区域/伤害事件不再 object mirror 发布。

### Batch 3：P1 帧同步和表现层数组/字符串契约优化

目标：减少客户端帧和网络帧稳定分配。

改造项：

1. `BattleLocalInputQueue` 从数组队列改为 pooled frame input 或 readonly list 契约。
2. rollback provider 复用导出列表，并评估 codec 是否能接受 list writer。
3. PresentationCue 内部 key 改为 struct。
4. View snapshot dispatch 日志默认关闭，并迁移到统一 view log gate。

验收标准：

- 有输入帧不再固定 `ToArray()` 分配，或分配集中到协议序列化边界。
- PresentationCue 无外部 key 时不再拼接字符串 key。

### Batch 4：P2 验证系统结构化与开关化

目标：解决用户指出的“校验都用字符串、没有开关限制会频繁 GC”的根因。

改造项：

1. 增加 `MobaRuntimeValidationOptions` 和 `MobaRuntimeValidationMode`。
2. `PlanTriggeringStage` 只在 bootstrap strict/editor 模式执行 validation。
3. Validation entry 增加 error code、结构化参数，字符串 message 延迟生成。
4. Config validator path 从字符串拼接迁移到结构化 path token。
5. validator contract 静态缓存，去掉常规 bootstrap 反射创建。

验收标准：

- validation 不能被无开关地放到 per-frame path。
- runtime sampled/manual 模式有明确频率限制。
- 默认 production 不生成完整字符串报告，除非启动失败或手动导出。

## 当前推荐优先级

1. **先做 Batch 1**：收益最大，侵入相对低，能立刻建立热路径观测成本规范。
2. **再做 Batch 2**：被动触发和事件兼容层属于核心战斗扩展风险，需要尽早收口。
3. **随后 Batch 4**：validation 是用户明确指出的问题，当前虽然 bootstrap 运行，但必须补开关和结构化边界，避免后续误用。
4. **最后 Batch 3**：表现层/输入/rollback 需要契约调整，改造面更大，适合在 runtime 观测成本稳定后推进。

## 备注

本审计没有把所有字符串都视为问题。判断标准是：

- 是否处于每帧、每事件、每输入、每快照路径。
- 是否在日志级别/诊断阈值判断前已经分配。
- 是否由公共 API 设计诱导调用方提前分配。
- 是否缺少显式开关导致未来可能被误放入高频路径。

因此，bootstrap 阶段的字符串和反射不是当前最高优先级；但 validation 系统需要优先补“运行模式和频率边界”，否则它很容易从 bootstrap 工具演变成 runtime GC 源。
