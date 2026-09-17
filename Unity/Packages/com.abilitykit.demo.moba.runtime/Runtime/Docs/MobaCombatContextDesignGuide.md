# MOBA 战斗上下文整体设计指南

本文记录 `com.abilitykit.demo.moba.runtime` 当前战斗上下文设计，用于后续扩展 Buff、AOE、Projectile、Summon、Continuous、延迟执行和触发器条件/行为时保持一致。

本文为当前设计评审入口，以 2026-09-16 的实现为准。先阅读下面的职责与身份对照，再按需要阅读 [Context 基础设施](../../../com.abilitykit.context/Document/Context上下文注册与快照模块开发设计文档.md)、[Trace 因果树](../../../com.abilitykit.trace/Document/Trace溯源树模块开发设计文档.md) 和 [Runtime Context 值与快照](../../Document/RuntimeContext运行时值与快照设计文档.md)。阶段性文档仅作为演进背景，不能替代当前契约。

## 职责与身份对照

| 模型 | 当前职责 | 身份/数据边界 |
|------|----------|---------------|
| Trace | 精简因果结构、来源节点与结束状态 | Trace ContextId/RootId/ParentId；元数据不等于运行时真值 |
| AbilityKit.Context | entity/property 注册、实时 provider 读取和快照存储 | Context entity ID；不调度、不自动采样、不自动重建领域对象 |
| MOBA 执行 Context | 当前触发/条件/行为的规范输入 | 强类型 payload + origin/lineage + 可选 runtime reference |
| Runtime Context | 目前主要为 Buff 提供可选实时/最终值适配 | 独立 RuntimeContextId/Version；不是 Trace ID 或 SkillRuntimeHandle |

Trace ID 与 Runtime Context ID 分属不同注册表，即使数字相同也不能互读。来源快照保留因果身份，值快照保留复制数值，恢复载荷负责显式重建领域状态，三者不是同一种 snapshot。

逻辑生命周期拥有 Trace 创建/结束、runtime 绑定及最终快照；工具只读现有映射/诊断样本，可间隔查询，关闭时清理自身订阅与缓存。诊断采集允许使用逻辑开关，工具开关不能反向驱动业务状态。

## 设计目标

战斗上下文要解决三个问题：

1. 当前这次触发在执行什么。
2. 这次触发从哪里来，和上游链路是什么关系。
3. 跨帧对象的实时/快照数据如何被条件和行为稳定读取。

这三类问题不能混成一个大对象。当前设计把它们拆成三条线：执行上下文、溯源上下文、运行时上下文。

## 三类上下文边界

### 执行上下文

执行上下文描述当前这次执行需要的事实。核心类型是 `MobaCombatExecutionContext`。

它承载：

- 原始 payload。
- source actor、target actor。
- trigger id、config id、frame。
- execution snapshot。
- skill runtime handle。
- 已归一化的 origin、lineage、source view。

它是条件和行为执行时的主模型。触发入口可以来自 Buff、Projectile、Summon、AOE、持续行为或旧 pipeline 数据，但进入效果执行服务后应尽量归一化成 `MobaCombatExecutionContext`。

维护规则：

- 不要把业务字段持续追加到 `MobaCombatExecutionContext`。
- 新领域需要额外数据时，优先放到领域 payload 或领域 action input。
- 只有执行期通用事实才进入 `MobaCombatExecutionContext`。

### 溯源上下文

溯源上下文描述这次执行从哪里来、父子链路如何关联、如何用于追踪和诊断。

核心概念：

- `MobaGameplayOrigin`：回答来源是什么。
- `MobaTriggerLineageContext`：回答当前执行如何接入 trace lineage。
- `MobaTriggerTraceContext`：轻量触发 trace 表示。
- `MobaContextSourceView`：用于查询、保留、调试和诊断的 source view。
- `MobaPersistentContextSourceSnapshot`：跨帧、异步生命周期保留的 source snapshot。

维护规则：

- trace/source/origin 是溯源，不等于 runtime context。
- Buff、Projectile、Summon、AOE 这类对象即使没有实时可变状态，也仍然可以有 origin/source/trace。
- 跨帧对象不能长期持有上游 live runtime 对象，应保留 `MobaPersistentContextSourceSnapshot` 或稳定 id。

### 运行时上下文

运行时上下文描述一个跨帧对象当前可读的实时/快照值。核心入口是 `MobaRuntimeContextReference` 和 `IMobaRuntimeContextPayload`。

运行时上下文只适合这类场景：对象有跨帧生命周期，并且条件或行为需要通过 context id 读取它的实时或销毁后快照数据。

当前 Buff 已接入运行时上下文：

- Buff 创建或刷新时，绑定 live provider。
- Buff 周期触发时，payload 暴露 context id 和 version。
- 条件和行为通过 runtime context accessor 读取实时值。
- Buff 移除时，保存最终 snapshot，销毁 live provider 绑定。

维护规则：

- 不是所有跨帧对象都必须注册 runtime context。
- 只需要溯源的对象使用 origin/source/trace 即可。
- 只有需要被条件/行为按 id 读取实时/快照数据的对象，才实现 `IMobaRuntimeContextPayload`。

## 触发到条件到行为流程

整体流程：

```text
Trigger payload
  -> MobaEffectExecutionService.ExecuteTrigger / ExecuteRulePlan
  -> Create MobaCombatExecutionContext
  -> Create MobaTriggerConditionContext
  -> MobaTriggerConditionRegistry.Evaluate
  -> MobaTriggerPlanExecutor
  -> MobaPlanActionInputResolver
  -> MobaPlanActionInput / MobaEffectActionInput / MobaProjectileActionInput / MobaSummonActionInput
  -> Action module
```

关键点：

- payload 是入口，负责暴露它天然拥有的信息。
- `MobaEffectExecutionService` 是归一化边界。
- 条件读取 `MobaTriggerConditionContext`。
- 行为读取 action input。
- action input 不重新创造上下文，只包装 `MobaCombatExecutionContext` 和服务解析结果。

## 代码流程图

本节用 `mermaid` 记录当前代码结构中的主要上下文传递路径。维护时如果代码入口变化，应同步更新本节。

### 总体执行时序

```mermaid
sequenceDiagram
    autonumber
    participant Payload as Trigger payload
    participant EffectSvc as MobaEffectExecutionService
    participant ExecCtx as MobaCombatExecutionContext
    participant CondCtx as MobaTriggerConditionContext
    participant CondReg as MobaTriggerConditionRegistry
    participant PlanExec as MobaTriggerPlanExecutor
    participant InputResolver as MobaPlanActionInputResolver
    participant ActionInput as Action input
    participant Action as Action module

    Payload->>EffectSvc: ExecuteTrigger / ExecuteRulePlan
    EffectSvc->>ExecCtx: CreateCombatExecutionContext(payload, triggerId, configId)
    ExecCtx-->>EffectSvc: normalized execution facts + origin + lineage + runtime payload
    EffectSvc->>CondCtx: CreateConditionContext(execCtx)
    CondCtx-->>CondReg: strongly typed condition input
    CondReg->>CondReg: Evaluate(triggerId, conditionContext)
    alt condition pass
        EffectSvc->>PlanExec: Execute(plan, execCtx)
        PlanExec->>InputResolver: Resolve(triggerArgs, worldCtx)
        InputResolver->>ActionInput: Create core/effect/projectile/summon input
        ActionInput-->>Action: execution facts + source/target + runtime context accessor
        Action->>Action: execute domain logic
    else condition fail
        EffectSvc->>EffectSvc: end trace as predicate miss
    end
```

### 三类上下文数据流

```mermaid
flowchart LR
    Payload[Trigger payload]

    Payload --> Execution[MobaCombatExecutionContext]
    Payload --> Source[MobaGameplayOrigin / Lineage / Trace / SourceView]
    Payload --> RuntimeRef[MobaRuntimeContextReference]

    Execution --> CondCtx[MobaTriggerConditionContext]
    Execution --> PlanInput[MobaPlanActionInput]
    Execution --> DomainInput[Effect / Projectile / Summon input]

    Source --> TraceScope[MobaEffectTraceScopeSnapshot]
    Source --> Persistent[MobaPersistentContextSourceSnapshot]
    Persistent --> CrossFrame[Cross-frame object retention]

    RuntimeRef --> RuntimeService[MobaRuntimeContextService]
    RuntimeService --> Realtime[Realtime provider]
    RuntimeService --> Snapshot[SnapshotStorage]
    Realtime --> RuntimeValue[Runtime value]
    Snapshot --> RuntimeValue

    CondCtx --> RuntimeValue
    PlanInput --> RuntimeValue
    DomainInput --> RuntimeValue
```

### 条件读取 runtime context

```mermaid
sequenceDiagram
    autonumber
    participant Condition as IMobaTriggerCondition
    participant CondCtx as MobaTriggerConditionContext
    participant Access as MobaRuntimeContextAccessExtensions
    participant RuntimeSvc as MobaRuntimeContextService
    participant Resolver as ContextValueResolver
    participant Provider as Realtime provider
    participant Snapshots as SnapshotStorage

    Condition->>CondCtx: GetRuntimeContextValue<TValue,TProperty>(contexts, key)
    CondCtx->>Access: payload.GetRuntimeContextValue(contexts, key)
    Access->>Access: TryResolveRuntimeContext(payload)
    alt no runtime context
        Access-->>Condition: Failure MissingRuntimeContext
    else runtime context resolved
        Access->>Resolver: TryGetValue<long,TProperty>(contextId, Version)
        Resolver->>Provider: try realtime version
        Provider-->>Resolver: version found / missing
        alt version mismatch
            Resolver-->>Access: actualVersion
            Access-->>Condition: Failure VersionMismatch
        else version valid
            Access->>Resolver: GetValue<TValue,TProperty>(contextId, key)
            Resolver->>Provider: realtime read
            alt realtime missing and snapshot allowed
                Resolver->>Snapshots: snapshot fallback
            end
            Resolver-->>Access: ContextValueResult<TValue>
            Access-->>Condition: Success or ValueMissing
        end
    end
```

### 行为读取 runtime context

```mermaid
sequenceDiagram
    autonumber
    participant Module as Plan action module
    participant Input as MobaPlanActionInput / domain input
    participant ExecCtx as MobaCombatExecutionContext
    participant Payload as Original payload
    participant Access as MobaRuntimeContextAccessExtensions
    participant RuntimeSvc as MobaRuntimeContextService

    Module->>Input: GetRuntimeContextValue<TValue,TProperty>(contexts, key)
    Input->>ExecCtx: GetRuntimeContextValue(contexts, key)
    ExecCtx->>Access: GetRuntimeContextValue(Payload, contexts, key)
    Access->>Payload: IMobaRuntimeContextPayload.TryGetRuntimeContext
    alt payload exposes context id/version
        Access->>RuntimeSvc: Resolver read with version check
        RuntimeSvc-->>Module: diagnostic result
    else payload has no runtime context
        Access-->>Module: MissingRuntimeContext
    end
```

### Buff 生命周期与上下文同步

```mermaid
flowchart TD
    Apply[BuffApplyFlow.Apply / refresh] --> Registry[BuffContextRegistry.EnsureBuffContext]
    Registry --> RuntimeSvc[MobaRuntimeContextService.EnsureBuffContext]
    RuntimeSvc --> Bind[MobaBuffRealtimeContextProvider.Bind]
    Bind --> Runtime[BuffRuntime.RuntimeContextId + RuntimeContextVersion]

    Runtime --> Tick[BuffContinuousIntervalHandler.OnInterval]
    Tick --> StageExec[BuffStageEffectExecutor.Execute]
    StageExec --> Payload[BuffTriggerContext]
    Payload --> RuntimePayload[IMobaRuntimeContextPayload.TryGetRuntimeContext]
    Payload --> EffectSvc[MobaEffectExecutionService.ExecuteTrigger]
    EffectSvc --> Condition[Condition read]
    EffectSvc --> Action[Action read]
    RuntimePayload --> Condition
    RuntimePayload --> Action

    Runtime --> Remove[BuffEndFlow.EndRuntime]
    Remove --> End[BuffContextRegistry.EndByRuntimeNoClear]
    End --> Destroy[MobaRuntimeContextService.SnapshotAndDestroyBuffContext]
    Destroy --> Save[SnapshotStorage.TrySaveManaged final snapshot]
    Save --> Unbind[MobaBuffRealtimeContextProvider.Unbind + entity Destroy]
    Unbind --> Notify[Remove events / effects with preserved ID and Version]
    Notify --> Clear[ClearRuntimeBindings + pool release]
```

### Buff 周期触发到效果行为

```mermaid
sequenceDiagram
    autonumber
    participant Manager as MobaContinuousManager
    participant Handler as BuffContinuousIntervalHandler
    participant StageExec as BuffStageEffectExecutor
    participant Payload as BuffTriggerContext
    participant EffectSvc as MobaEffectExecutionService
    participant CondReg as MobaTriggerConditionRegistry
    participant PlanExec as MobaTriggerPlanExecutor

    Manager->>Handler: OnInterval(continuous, config, executionContext)
    Handler->>StageExec: Execute(triggerIds, buffId, source, target, contextId, stage, runtime)
    StageExec->>Payload: CreatePayload(..., runtime, persistentSource)
    Payload-->>Payload: exposes origin + lineage + trace + runtime context
    StageExec->>EffectSvc: ExecuteTrigger(request)
    EffectSvc->>CondReg: Evaluate(triggerId, conditionContext)
    alt condition pass
        EffectSvc->>PlanExec: Execute plan actions
    else condition fail
        EffectSvc->>EffectSvc: close trace without action execution
    end
```

### Projectile 行为接入

```mermaid
flowchart LR
    Trigger[Effect trigger] --> EffectSvc[MobaEffectExecutionService]
    EffectSvc --> PlanExec[MobaTriggerPlanExecutor]
    PlanExec --> Resolver[MobaPlanActionInputResolver.ResolveProjectile]
    Resolver --> ProjectileInput[MobaProjectileActionInput]
    ProjectileInput --> Source[CreateSourceContext / Build origin]
    ProjectileInput --> Aim[Resolve aim and actor data]
    ProjectileInput --> RuntimeRead[Optional runtime context read]
    Source --> ProjectileRuntime[Projectile runtime or launcher module]
    Aim --> ProjectileRuntime
    RuntimeRead --> ProjectileRuntime

    Note1[Rule: projectile should keep source snapshot for async lifetime]
    Source --> Note1
    Note2[Rule: only add runtime context if projectile owns readable mutable state]
    RuntimeRead --> Note2
```

### Summon 行为接入

```mermaid
flowchart LR
    Trigger[Effect trigger] --> EffectSvc[MobaEffectExecutionService]
    EffectSvc --> PlanExec[MobaTriggerPlanExecutor]
    PlanExec --> Resolver[MobaPlanActionInputResolver.ResolveSummon]
    Resolver --> SummonInput[MobaSummonActionInput]
    SummonInput --> Spawn[TryResolveSpawnPosition]
    SummonInput --> Origin[BuildOrigin / CreateSourceContext]
    SummonInput --> RuntimeRead[Optional runtime context read]
    Spawn --> SummonRuntime[Summon creation]
    Origin --> SummonRuntime
    RuntimeRead --> SummonRuntime

    SummonRuntime --> ChildPayload[Future summon trigger payload]
    ChildPayload --> ChildOrigin[Propagate parent/root/source origin]
    ChildPayload --> ChildRuntime[Expose runtime context only if summon has live readable state]
```

### AOE 与 Continuous 接入判断

```mermaid
flowchart TD
    NewObject[New AOE / Continuous object] --> NeedSource{Needs provenance?}
    NeedSource -->|yes| Source[Expose origin / lineage / trace]
    NeedSource -->|no| Minimal[Use simple typed payload]

    Source --> CrossFrame{Lives across frames?}
    CrossFrame -->|yes| Persist[Store MobaPersistentContextSourceSnapshot]
    CrossFrame -->|no| ExecOnly[Execution context only]

    Persist --> NeedRuntime{Conditions/actions need live mutable values by id?}
    ExecOnly --> NeedRuntime
    NeedRuntime -->|no| NoRuntime[Do not register runtime context]
    NeedRuntime -->|yes| RuntimeData[Define runtime data/property/snapshot]
    RuntimeData --> Provider[Register realtime provider]
    Provider --> PayloadRuntime[Payload implements IMobaRuntimeContextPayload]
    PayloadRuntime --> EndLifecycle[On end: snapshot + unbind]
```

### 上下文归一化决策树

```mermaid
flowchart TD
    Payload[Incoming payload] --> CombatProvider{Implements IMobaCombatExecutionContextProvider?}
    CombatProvider -->|yes| UseCombat[Use provided MobaCombatExecutionContext]
    CombatProvider -->|no| BuildCombat[CreateCombatExecutionContext]

    BuildCombat --> SourceProvider{Can resolve source/origin/lineage?}
    SourceProvider -->|yes| MergeSource[Merge origin + lineage + source view]
    SourceProvider -->|no| Legacy[Fallback to legacy actor/config/context primitives]

    MergeSource --> RuntimeProvider{Implements IMobaRuntimeContextPayload?}
    Legacy --> RuntimeProvider
    RuntimeProvider -->|yes| KeepRuntime[Keep payload as runtime context provider]
    RuntimeProvider -->|no| NoRuntime[Runtime read returns MissingRuntimeContext]

    KeepRuntime --> ConditionContext[Create MobaTriggerConditionContext]
    NoRuntime --> ConditionContext
    ConditionContext --> PlanInput[Create action input]
```

## Runtime Context 读取流程

条件和行为统一通过 runtime context accessor 读取值：

```text
Condition or Action Input
  -> GetRuntimeContextValue<TValue, TProperty>
  -> payload.TryResolveRuntimeContext
  -> MobaRuntimeContextService.Resolver
  -> ContextValueResolver
  -> Realtime provider first, snapshot fallback
  -> diagnostic result
```

读取结果使用 `MobaRuntimeContextValueResult<TValue>` 表达，不只返回 bool。

失败原因包括：

- `MissingContextService`：没有传入 `MobaRuntimeContextService`。
- `MissingPayload`：payload 为空。
- `MissingRuntimeContext`：payload 没有运行时上下文。
- `InvalidRuntimeContext`：context id 无效。
- `VersionUnavailable`：需要校验版本，但上下文内没有版本值。
- `VersionMismatch`：payload 携带的版本与当前上下文版本不一致。
- `ValueMissing`：上下文存在，但目标 key 不存在。

兼容接口 `TryGetRuntimeContextValue` 仍然保留，适合不关心失败原因的旧调用点。新条件和新行为建议优先用诊断结果，方便定位配置错误、生命周期错误和过期引用。

## 版本校验

`MobaRuntimeContextReference` 包含 `ContextId` 和 `Version`。

当 `Version > 0` 时，读取值前会先从运行时上下文读取 `MobaRuntimeContextKeys.Version`：

- 读不到版本，返回 `VersionUnavailable`。
- 版本不一致，返回 `VersionMismatch`。
- 版本一致，继续读取目标 key。

该校验可以拒绝版本不同的引用，但当前并不保证彻底防止同 ID 新对象别名：Context 游标回滚可复用 ID，新绑定 Version 又为 1。正常刷新与数值变化没有统一自动递增版本规则，因此 Version 不是逐帧修订号，也不能用来查某个历史版本。Version<=0 的兼容引用跳过检查，stage snapshot 才保存触发时事实。当前校验放在 MOBA runtime 层，没有修改底层 `AbilityKit.Context` 包协议。

## 网络同步与数据恢复边界

上下文读取模型不是网络同步的权威状态模型。网络同步游戏需要把写入、同步输出、诊断读取和状态恢复拆开维护，否则很容易把调试视图或 runtime context 误用成可回放、可重连、可校验的战斗状态。

四类模型的职责如下：

- 写入模型：由输入命令、生命周期服务和领域系统修改真实运行时对象，例如 Actor、Buff、Projectile、Random、SkillRuntime。
- 同步快照模型：由 snapshot emitter 输出给表现层、网关或录像系统，描述某一帧需要广播的状态或事件。
- 诊断读取模型：由 runtime port 或调试服务导出当前可观察状态，用于工具、面板、日志和低频检查。
- 恢复模型：由显式 state export/import/hash 契约导出纯状态，并能在重连、校验失败、回滚或权威覆盖时还原。

维护规则：

- `ContextRegistry` 负责实体/属性；`ContextValueResolver` 和 `MobaRuntimeContextService` 组合实时 provider、snapshot fallback 和条件/行为读取，不作为 MOBA 的权威网络状态仓库。
- `SnapshotStorage` 保存的是上下文快照，不等价于完整 battle state baseline。
- `MobaCombatExecutionContext`、`MobaTriggerConditionContext` 和 action input 是执行期读模型，不能承担状态恢复职责。
- `WorldStateSnapshot` 面向同步输出，可以包含事件快照和状态快照；是否可用于恢复必须由恢复契约显式声明。
- `GetLogicWorldEntityStates` 这类接口只能作为诊断或低频 read model，不能作为正式同步源。
- 新增会影响逻辑结果的跨帧状态时，除 runtime context 外，还要评估是否需要加入 state export/import/hash 覆盖。

首批恢复域按风险和已有结构落地：

1. Actor transform：已有 transform snapshot 和 rollback provider，先保证位置/朝向/缩放可导出、导入和参与 hash。
2. Random：已有确定性随机和 rollback payload，必须参与 hash，避免随机消耗漂移无法定位。
3. Buff：先导出 BuffRuntime 的纯字段状态，导入时重建运行时列表并重新绑定 runtime context，不走普通 Apply 流程以避免表现事件和触发副作用。

技能 runtime 已有独立恢复及本地回滚 provider；其他领域的恢复完整性需按具体 provider 审计，不能因存在同步快照就认定可恢复。只用于表现的 cue 不进入逻辑恢复状态。Buff 恢复当前显式保留 RuntimeContextId/Version，预检身份冲突后重建，旧 ID=0/Version=0 保持无身份；这两项身份参与 Buff 状态比较与 hash。

### 当前回滚保证范围

Context entity 回滚只撤销预测实体/provider/快照并回退游标，确认实体被销毁则拒绝。Buff timer 回滚只恢复仍存在且身份与形态匹配的成员。两者都不具备完整 Buff/Context 生命周期重建。

技能本地回滚另存精简 Trace 生命周期及分配边界，恢复捕获根/已登记子节点状态，并撤销同一世界中边界之后的预测分支。Trace ID 不回退，已确认根外部引用不改，当前树 Revision 刷新；历史事件保留并在诊断开启时添加预测撤销标记。该本地附加载荷不进入技能权威状态或 hash，也不复活 Purge 节点，不保证所有未捕获历史节点状态恢复。

```mermaid
flowchart LR
    Input[Input command] --> Write[Domain write model]
    Write --> Runtime[Live runtime objects]
    Runtime --> Snapshot[Sync snapshot emitters]
    Runtime --> Diagnostic[Diagnostic read model]
    Runtime --> Restore[State export/import/hash]

    Snapshot --> Network[Network / replay output]
    Diagnostic --> Tools[Tools / logs / debug panels]
    Restore --> Recovery[Rollback / reconnect / authority override]

    Context[ContextRegistry + RuntimeContextService] --> Read[Condition / action reads]
    Runtime --> Context

    Rule1[Rule: context reads are not authoritative state storage]
    Context --> Rule1
    Rule2[Rule: formal restore uses explicit state contracts]
    Restore --> Rule2
```

## Buff 生命周期

Buff 是当前 runtime context 的标准接入样例。

创建或刷新：

```text
BuffLifecycle / BuffContextRegistry
  -> MobaRuntimeContextService.EnsureBuffContext
  -> MobaBuffRealtimeContextProvider.Bind
  -> BuffRuntime.RuntimeContextId / RuntimeContextVersion
```

周期触发：

```text
BuffContinuousIntervalHandler
  -> BuffStageEffectExecutor
  -> BuffTriggerContext
  -> IMobaRuntimeContextPayload.TryGetRuntimeContext
  -> condition/action runtime value read
```

移除：

```text
BuffEndFlow.EndRuntime（先提交活动列表移除）
  -> 停止并清理持续行为
  -> BuffContextRegistry.EndByRuntimeNoClear
  -> SnapshotAndDestroyBuffContext(preserveReference: true)
  -> TrySaveManaged final snapshot with destruction metadata / Unbind provider / Destroy entity
  -> Remove events / OnRemove effects（原 ID/Version，读取最终快照）
  -> 生命周期通知 / 清零绑定与引用 / 回池
```

维护规则：

- Buff 移除时必须同步 snapshot 和 provider unbind。
- 移除后仍需要查询最终状态时，从 snapshot 读取。
- 新增 Buff 字段如果要开放给条件/行为读取，应先加入 `MobaBuffRuntimeContextData.TryGetValue` 和 key 常量。
- Buff 每次结束采集一份受管最终快照，不代表逐帧历史或 OnRemove 执行后的结果；重复销毁不覆盖已冻结值。存储已支持多份历史，工具应保存精确引用并通过 SnapshotReader 只读查询。
- 受管记录区分 TypeId/SchemaVersion、业务 Version 和本地 Generation；SnapshotId 不随回滚或 Clear 回退。Generation 不进入 Buff 权威状态，旧 runtime ID/Version 引用的别名限制仍需单独处理。
- `TryGetBuffContext` 活动阶段读取 property 投影；最终字段快照应通过 accessor 按键读取。
- 无 Trace source ID 也必须清理独立 Runtime Context，结束 hook 异常仍继续清理。

## AOE / Projectile / Summon / Continuous 接入规则

新增跨帧对象时先判断它需要哪类上下文。

只需要知道来源和链路：

- 提供 origin/source/trace。
- 跨帧保留 `MobaPersistentContextSourceSnapshot`。
- 不注册 runtime context。

需要实时可变状态被条件/行为读取：

- 分配稳定 context id。
- 实现对应 runtime context data/property/snapshot。
- 注册 realtime provider。
- payload 实现 `IMobaRuntimeContextPayload`。
- 生命周期结束时 snapshot + unbind。

需要领域行为参数：

- 增加领域 action input 或 resolver。
- 不把领域字段塞进通用 `MobaCombatExecutionContext`。

## 条件开发规范

条件应优先读取 `MobaTriggerConditionContext` 暴露的强类型信息。

当条件需要读取跨帧对象运行时值时：

- 使用 `GetRuntimeContextValue<TValue, TProperty>` 获取诊断结果。
- 对 `VersionMismatch` 和 `MissingRuntimeContext` 给出明确失败 key。
- 不直接依赖 BuffRuntime，除非该条件明确只服务 Buff 并且不要求跨对象复用。

建议模式：

```text
context.GetRuntimeContextValue<TValue, TProperty>(runtimeContexts, key)
  -> result.Found ? pass : fail with result.Failure
```

## 行为开发规范

行为模块应通过 `MobaPlanActionInputResolver` 取得输入。

通用行为使用 `MobaPlanActionInput`。效果行为使用 `MobaEffectActionInput`。投射物行为使用 `MobaProjectileActionInput`。召唤行为使用 `MobaSummonActionInput`。

维护规则：

- action input 是执行入口，不是长期状态容器。
- 行为需要 runtime context 值时，从 action input 调用 runtime accessor。
- 行为需要新领域能力时，扩展领域 action input，而不是扩大通用上下文。

## 不应做的事

- 不要把 trace 当成 runtime context。
- 不要把 runtime context 当成任意业务 data bag。
- 不要为了统一而让所有对象都实现 Buff 风格上下文。
- 不要在跨帧对象中长期保存 live runtime 引用作为溯源依据。
- 不要绕过 `MobaEffectExecutionService` 直接拼装零散条件/行为输入。
- 不要新增 key/value 读取替代已有强类型 payload。

## 维护检查清单

新增一种触发 payload 时检查：

- 是否能暴露 origin、lineage、trace。
- 是否需要 `MobaPersistentContextSourceSnapshot`。
- 是否真的需要 runtime context。
- 是否能被 `MobaEffectExecutionService` 归一化成 `MobaCombatExecutionContext`。

新增一种跨帧对象时检查：

- 生命周期创建、刷新、移除是否都有明确入口。
- 移除时是否 snapshot + unbind。
- 是否存在 context id/version 过期读取风险。
- 条件/行为是否通过 accessor 读取，而不是直接拿 live 对象。

新增一个条件或行为时检查：

- 是否优先使用强类型输入。
- 是否正确处理 runtime context diagnostic failure。
- 是否避免依赖 Buff 专属类型，除非它就是 Buff 专属逻辑。

## 现有文档索引

- [Runtime Context 值与快照设计](../../Document/RuntimeContext运行时值与快照设计文档.md)：具体生命周期、版本语义、恢复限制和待评审问题。
- [Context 基础包设计](../../../com.abilitykit.context/Document/Context上下文注册与快照模块开发设计文档.md)：注册/读取/快照基础职责。
- [Trace 基础包设计](../../../com.abilitykit.trace/Document/Trace溯源树模块开发设计文档.md)：因果树、保留、回滚和预测撤销。

- `Runtime/Application/Services/Context/README.md`：Context 模块规则和主模型优先级。
- `Runtime/Application/Services/Triggering/PlanActions/README.md`：Plan Action 输入和模块边界。
- `Runtime/Docs/GoldenSkillFlowGuide.md`：技能和效果执行主流程。
- `Runtime/Docs/RuntimeArchitectureGuide.md`：runtime 包整体结构。
- `Docs/Moba触发器事件上下文与溯源设计.md`：早期触发器上下文和溯源设计。
- `Docs/Moba效果触发上下文溯源与技能释放运行时阶段性设计.md`：效果触发上下文阶段设计。
- `Docs/Moba战斗上下文阶段性代码总结.md`：战斗上下文阶段性代码总结。

当这些文档与当前代码不一致时，以本文和当前代码为准；阶段文档主要作为背景和演进记录。
