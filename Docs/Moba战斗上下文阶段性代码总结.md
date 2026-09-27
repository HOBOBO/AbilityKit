# MOBA 战斗上下文阶段性代码总结

本文档用于阶段性汇总当前 `com.abilitykit.demo.moba.runtime` 战斗上下文重构后的代码状态，并客观判断当前设计方向是否适合继续推进到正式版本。

## 1. 当前阶段结论

当前设计方向整体是正确的，适合继续作为正式版战斗逻辑层的主线推进。

这轮重构真正解决的问题不是单点 bug，而是把原来分散在 effect service、payload、plan action、condition、damage、buff、projectile 中的上下文修补逻辑收回到少数明确边界：

- `MobaCombatExecutionContext` 作为只读 execution fact。
- `MobaCombatExecutionContextFactory` 负责上下文构建和 snapshot 合并。
- `MobaEffectExecutionService` 负责 effect 执行编排、预算、condition 和正式 Context session 生命周期；Trace Adapter 可选观察。
- `MobaTriggerPlanExecutor` 负责 trigger plan 查询、运行时依赖检查、evaluate/execute。
- `MobaPlanActionInputResolver` 负责 action 侧输入视图。
- `MobaActionOriginBuilder` 负责 action origin 生成。

这条方向比继续扩展旧 provider/data bag/dictionary 兼容层更健康，也更接近大型项目需要的可演进结构。

当前仍不是最终形态，但已经从“每个模块各自猜上下文”进入了“上下文生成、输入读取、溯源构建各有边界”的阶段。

## 2. 已完成的关键结构变化

### 2.1 上下文从兼容容器收敛为执行事实

`MobaCombatExecutionContext` 当前已经不再持有或代理 `IMobaTriggerDataContext`。它的主要职责是聚合已经推导出的执行事实：payload、lineage、origin、execution snapshot、skill runtime handle、frame。

这是一个重要方向，因为大型项目里如果统一上下文本身继续兼容旧 data bag，它会变成新的万能共享容器。那样短期写 action 很方便，长期会让上下文语义失控。

当前保留的推导属性，例如 `SourceActorId`、`TargetActorId`、`ParentContextId`、`RootContextId`、`OwnerContextId`，仍然有一定聚合逻辑，但这些逻辑已经围绕 typed lineage/origin/snapshot，而不是任意 dictionary。

这轮进一步把 `StackCount`、`ElapsedSeconds`、`RemainingSeconds`、`DurationSeconds` 从基础执行事实中拆出。它们属于 buff、continuous interval、channel 等阶段/领域快照，不再作为 `MobaCombatExecutionContext` 或 `MobaTriggerExecutionSnapshot` 的基础字段暴露；需要读取触发时状态应走 `MobaTriggerStageSnapshot`、`MobaTriggerConditionContext` 或 typed payload/provider。Buff 这类触发后仍可能存活的领域对象，如果 action 需要读取当前运行中状态，则走 `IBuffLiveViewProvider`/`BuffLiveViewResolver` 暴露的 `BuffRuntimeView`，不把实时层数和实时剩余时间回流到基础执行上下文。

阶段判断：方向正确。后续可以继续减少 `MobaCombatExecutionContext` 内部推导，使它更接近“已归一化事实快照”。

### 2.2 构建逻辑移入 factory

`MobaCombatExecutionContextFactory` 已经承担 payload、lineage、snapshot、origin、runtime handle 的合并规则。

这使 `MobaCombatExecutionContext` 不再是“自己构造自己、自己修复自己”的对象，降低了中心对象继续变重的风险。

阶段判断：方向正确。后续若新增 payload 来源，应优先扩展 resolver/factory 的边界规则，而不是把特殊判断写进 action。

### 2.3 effect execution session 明确生命周期边界

`MobaEffectExecutionService` 当前通过 `_executionContexts` 与 execution scope 维护执行栈，并通过内部 `MobaEffectExecutionSession` 保证正式 Context 成对结束；可选 Trace Adapter 只观察 Context 生命周期：

- 进入执行时 push context。
- 创建 effect execution scope。
- 创建 action child trace。
- plan 执行完成后按结果关闭 trace。
- 异常或提前退出时仍能 pop context、关闭 trace、释放 budget。

这比把这些生命周期散落在 `Execute` 和 `ExecuteTriggerId` 中更可靠。尤其是后续 Buff、Projectile、Area、Summon、Unit State 等嵌套触发变多时，session 边界是必要的。

阶段判断：方向正确。后续可以考虑把 `MobaEffectExecutionSession` 提升成独立文件，但当前内部类也可以接受。

### 2.4 trigger plan 执行职责已独立

`MobaTriggerPlanExecutor` 已经从 `MobaEffectExecutionService` 中拆出，负责：

- 按 triggerId 查 plan。
- 检查 eventBus/functions/actions 是否可用。
- 创建 `ExecCtx<IWorldResolver>`。
- 执行 `PlannedTrigger.Evaluate` 与 `PlannedTrigger.Execute`。
- action 注册缺失时尝试 repair 后重试一次。

这让 effect service 回到“战斗 effect 执行编排”角色，不再同时负责 trigger runtime 的具体运行细节。

阶段判断：方向正确。后续如果 plan executor 继续增长，可以再拆 `ExecCtx` factory 和 action repair policy。

### 2.5 action 输入读取开始统一

当前常规 action 已经迁到 `MobaPlanActionInputResolver`，该 resolver 只定位为 core action input，负责 caster、target、aim、execution context、execution scope 这类基础执行事实。motion action 额外通过 `MobaMovementActionInputResolver` 收敛移动输入：

- damage
- buff
- projectile
- presentation
- summon
- resource

这意味着 action module 不再到处直接调用 `PlanContextValueResolver` 读取 caster、target、aim。旧 resolver 目前已退到 `MobaPlanActionInputResolver` 内部 fallback；Dash、Blink、Pull 的 actor、aim direction、target direction、pull direction 由 movement 输入视图统一解析。

这轮已经进一步固化 action 输入边界：配置参数放 args/schema，领域派生数据放专用 input/resolver，payload 专属事实通过 typed provider 进入对应 resolver，runtime service 从 world resolver 解析。后续不应把 damage、buff、projectile、summon、area、presentation 等领域字段继续追加到 `MobaPlanActionInput`。Buff action 若要读取触发时层数/时间，使用 stage snapshot；若要读取仍激活 Buff 的实时层数/时间，使用 `EffectContextPlanValueResolver` 中显式命名的 live buff 方法。

本轮又把 action execution context 解析拆成了严格 `TryResolve` 和过渡 `Resolve`：正式路径只接受 `MobaEffectExecutionService` 当前 session context 或 payload 显式提供的 `IMobaCombatExecutionContextProvider`；fallback create 仍保留，但会记录 warning，用来暴露绕过正式执行链路的入口。

阶段判断：方向正确。`PlanContextValueResolver` 已降级为 internal fallback，action input 字段扩展边界已文档化；action execution context fallback 已开始显式化，下一轮可以根据 warning 和搜索结果继续把 fallback 缩小为 debug/assert 路径。

### 2.6 action origin 生成已收敛

`MobaActionOriginBuilder` 统一处理 action origin：

- 优先继承 execution context 中已有 origin。
- 没有 origin 时从 source/target/fallback kind/config 创建 legacy origin。
- 有当前 effect execution scope 时，把 immediate context 指向当前 effect execution。
- 保留 root/owner/skill runtime handle。

这对 Damage、Buff、Projectile 这类会产生后续链路的 action 很关键。否则每个 action 自己构造 origin，root、owner、parent、runtime handle 很容易分叉。

阶段判断：方向正确。后续 Area、Summon、Unit State 如果也要产生延迟触发，应明确是否使用 `MobaActionOriginBuilder` 或继承已有 origin。

### 2.7 Buff interval 已正式接入 continuous

Buff 周期触发不再由 `MobaPeriodicEffectService` 这套临时 service/component/system 调度，也不再由 `MobaBuffTickSystem` 直接推进。当前正式路径是：Buff 应用阶段创建 `BuffContinuousRuntime` 并注册到 `IContinuousManager`；`MobaContinuousTickSystem` 每帧驱动 `MobaContinuousManager`；manager 统一 tick active continuous，并只从 `IMobaContinuousPeriodicConfig`、`IMobaContinuousIntervalState` 等 continuous 抽象读取 interval 与 interval triggerIds；到达 interval 时交给所有匹配的 `IMobaContinuousIntervalHandler`，Buff 领域由 `BuffContinuousIntervalHandler` 承接，再通过 `BuffStageEffectExecutor` 构造 `BuffTriggerContext` 后调用 `MobaEffectExecutionService.ExecuteTriggerId`。

这个拆分把持续行为推进收敛回 continuous 模块：`IContinuousManager` 管注册、激活、暂停、恢复、中断，`MobaContinuousManager` 在业务层统一处理 duration/interval tick，但不直接认识 Buff、Area、Channel 等领域类型。领域 runtime 状态同步由 continuous 自己实现 `IMobaContinuousRuntimeStateSync`，领域触发由 handler 实现。Buff interval 的触发 payload 继续暴露 stage snapshot 与 live view，因此 action 能区分“触发当帧状态”和“当前仍激活的实时状态”；interval 阶段正式 `MobaExecutionKind` 使用 `BuffTick`，避免和 apply/remove 语义混淆，Trace Adapter 按需投影。

阶段判断：方向正确。旧 `MobaPeriodicEffectService`、独立 periodic runtime/component/tick system 与 Buff binder 已删除；后续如果 Area/Summon/Channel 也需要周期行为，应实现 continuous 抽象与 interval handler 接入 manager，而不是重新引入独立 periodic service 或领域系统自驱动周期逻辑。

## 3. 当前设计解决了什么问题

### 3.1 减少上下文来源混乱

原先的主要问题是同一批信息可能来自：

- skill cast context
- effect context wrapper
- projectile hit args
- buff stage payload
- origin context
- lineage context
- execution context（Trace 仅可选投影）
- runtime handle
- dictionary data bag
- plan action 自己 fallback

现在这些来源并没有完全消失，但它们逐步被收束到了 payload provider、lineage resolver、execution snapshot builder、stage snapshot resolver、factory、action input resolver 这些边界。

这对大型项目非常重要，因为大型项目的扩展点一定会变多。上下文来源可以多，但解析路径不能无限多。

### 3.2 让 action 更接近领域行为

迁移后的 action 更像“消费输入并调用领域 service”，而不是“解析 payload、修补 actor、推导 origin、管理 trace、再执行业务”。

这能降低新增 action 的认知成本，也让 action module 更容易被测试和审查。

### 3.3 trace/root/owner 语义更稳定

当前 root、parent、owner 的语义已经逐步固定：

- parent 表示当前行为挂接的直接 trace/context。
- root 表示一次技能释放或触发链路的根。
- owner 表示生命周期归属，主要服务于 runtime/child retain/release。

这对 Buff、Projectile、Area、Summon 等延迟对象非常关键。只要它们创建时保存 root/owner/origin/runtime handle，后续触发就能回到同一条链路。

### 3.4 effect 执行失败路径更可控

session 使异常路径也能关闭 trace、pop context、释放 budget。这个点在复杂触发系统里比表面看起来更重要，因为任何一次异常遗留 execution context 或 budget 深度，都可能污染后续战斗逻辑。

## 4. 当前仍然存在的问题

### 4.1 `MobaCombatExecutionContext` 仍有一定推导职责

虽然它已经不再代理 data bag，但仍然通过 lineage/origin/snapshot 做优先级推导。

这在当前阶段可以接受，因为项目仍处在上下文归一化阶段。但长期看，理想状态是：

- factory/resolver 完成推导；
- context 尽量只暴露最终值；
- context 不再承担“哪个来源优先”的判断。

风险等级：中。

建议：后续不要继续往 `MobaCombatExecutionContext` 添加新的推导属性。新字段应先判断属于 action input、lineage、execution snapshot、stage snapshot 还是 origin；阶段状态不要回流到基础执行快照。

### 4.2 action execution context resolver fallback 已显式化

`MobaPlanActionExecutionContextResolver` 当前拆成两条路径：

1. `TryResolve` 是正式路径，只从 `MobaEffectExecutionService.TryGetCurrentExecutionContext` 或 triggerArgs 上的显式 combat execution context provider 读取。
2. `Resolve` 是过渡包装，先走 `TryResolve`，失败后才通过 lineage/snapshot/factory 创建 fallback context，并记录 warning。

这比原先静默创建 context 更接近正式版，因为 action 是否处在完整 session/trace/budget 环境中已经可以被观测。风险仍在于旧入口可能继续依赖 fallback 执行。

风险等级：中。

建议：继续观察 warning 和搜索结果；等所有入口稳定后，把 fallback 创建能力缩小为 debug/assert 路径，或让必须严格执行的 action 使用 `TryResolve` 并在失败时显式跳过。

### 4.3 motion action 输入统一已完成

Dash、Blink、Pull 已改为通过 `MobaMovementActionInputResolver` 读取移动输入。这个拆分不是单纯包装，而是因为 motion action 的输入语义更复杂：

- aim direction 与 aim position 的坐标语义；
- caster/target 的 transform 读取；
- 位移方向、距离、落点、碰撞/寻路约束；
- target fallback 和 self fallback；
- 是否需要专门 movement source context。

风险等级：中。

当前做法是新增 movement action input resolver，由它复用通用 `MobaPlanActionInputResolver`，并把方向、位置、目标推导放在移动领域边界内。这样新 motion action 可以消费 typed movement input，而不再回到旧 payload 取值工具。

### 4.4 `PlanContextValueResolver` 已降级为内部 fallback

当前它已经退到 resolver 内部 fallback，类型可见性已改为 internal，并标注 action module 应消费 typed action input。

风险等级：低。

建议：后续继续保持搜索约束，确保新增 action 不再直接调用旧 resolver；如果 fallback 继续减少，可以再考虑重命名为 legacy resolver。

### 4.5 execution session 仍藏在 effect service 内部

当前 `MobaEffectExecutionSession` 是内部 private sealed class。它已经承担了比较明确的生命周期职责。

风险等级：低到中。

建议：如果后续 session 继续增加责任，例如 more trace policy、budget policy、nested execution diagnostics，可以独立成文件，避免 effect service 再次变大。

### 4.6 trace action child 当前按 plan action 预创建

`CreateActionChildNodes` 会为 plan 中 action 创建 child trace，但当前 action 执行和 action child node 的精确关联还比较粗。

风险等级：低到中。

建议：后续如果需要精确定位某个 action 的实际执行、跳过、失败原因，需要让 plan executor/action runtime 与 trace child id 建立更直接的关联。

## 5. 当前设计方向是否适合大型项目

结论：适合，但必须继续保持“边界收紧”的方向。

适合的原因：

- 执行上下文、输入解析、origin 生成、plan 执行、effect session 已经分层。
- 新扩展点可以通过 typed payload/provider 接入，不必让每个 action 识别具体 payload。
- trace/root/owner/runtime 的语义开始稳定，有利于 Buff/Projectile/Area/Summon 等衍生对象统一生命周期。
- Buff interval 已接入 continuous 生命周期和正式 trigger context，不再依赖临时 periodic service。
- action module 正在变薄，领域 service 才是业务承载点。

不适合的风险来自：

- 如果继续允许 action 直接读旧 resolver 或 dictionary，设计会回退。
- 如果所有新字段都塞进 `MobaCombatExecutionContext` 或 `MobaTriggerExecutionSnapshot`，它们会再次变成重型 facade。
- 如果 stack、elapsed、remaining、duration 等阶段事实不走 `MobaTriggerStageSnapshot`，条件/action 会重新和具体 payload 耦合。
- 如果实时 Buff 状态不走 `IBuffLiveViewProvider`/`BuffLiveViewResolver`，action 会再次把 live runtime 引用和触发快照混用。
- 如果后续触发入口绕过 `MobaEffectExecutionService.ExecuteTriggerId`，session/trace/budget 会出现断链。
- 如果 origin/root/owner 的规则在不同 action 里分散实现，溯源会再次分叉。

因此，当前方向不是“已经完成大型项目架构”，而是“已经进入正确轨道，可以继续往正式架构收敛”。

## 6. 阶段性架构评分

| 维度 | 当前评分 | 判断 |
| --- | --- | --- |
| 上下文归一化 | 8/10 | 主链路清晰，stage facts 已从基础 execution snapshot 拆出，Buff live view 已独立为领域读模型，但 context 仍有推导职责 |
| action 输入边界 | 8/10 | 常规 action 与 motion action 已迁移，旧 resolver 已降级为内部 fallback，Buff 快照/live 读取已显式区分 |
| trace/root/owner 语义 | 7.5/10 | 语义已形成，action child 精确关联仍可加强 |
| effect 执行生命周期 | 8/10 | session/budget/trace 收口明显 |
| 扩展接入规范 | 7.5/10 | 文档已补充，后续需要靠代码约束固化 |
| 大型项目可维护性 | 7.5/10 | 方向正确，下一步重点是压缩 execution context fallback |

综合判断：当前约等于从原型/过渡架构进入“可正式化架构”的中段。不是最终版，但已经具备继续规模化的基础。

## 7. 下一阶段建议顺序

### 7.1 已完成：motion action 输入统一

已处理 Dash、Blink、Pull：

- 新增 `MobaMovementActionInputResolver` 与 `MobaMovementActionInput`。
- 统一 caster/target/aim direction/target direction/pull direction/actor registry 读取。
- motion action 不再直接调用 `PlanContextValueResolver`。

### 7.2 已完成：收紧 `PlanContextValueResolver`

motion 迁移完成后已处理：

- 将 `PlanContextValueResolver` 降级为 internal fallback。
- 在注释上标明 action module 不应直接调用。
- 搜索确认直接调用只存在于 resolver 内部。

### 7.3 已完成第一步：压缩 action resolver fallback

`MobaPlanActionExecutionContextResolver` 已新增严格 `TryResolve`，并把 fallback create 移入带 warning 的 `Resolve` 过渡包装：

- 当前 session context 是正常路径。
- payload combat execution context provider 是显式外部触发路径。
- fallback create 仍保留为兼容兜底，但不再静默发生。

### 7.4 第四优先级：延迟对象接入规范代码化

对 Area、Summon、Periodic 等对象统一检查：

- 是否 retain/release skill runtime。
- 是否保存 root/owner/origin。
- 后续触发是否统一走 `ExecuteTriggerId`。
- payload 是否暴露 typed provider。

### 7.5 第五优先级：trace action child 精细化

后续如果要做调试器或战斗回放，应加强：

- action child id 与具体 action 调用绑定。
- action skipped/condition failed/exception reason 记录。
- action 执行耗时或帧号记录。

## 8. 当前阶段的约束原则

后续开发应遵守以下约束：

- 新触发入口必须走 `MobaEffectExecutionService.ExecuteTriggerId` 或 pipeline `Execute`。
- 新 payload 优先实现 typed provider，不让 action 识别具体 payload。
- 新 action 必须优先调用 `MobaPlanActionInputResolver.Resolve`，motion action 使用 `MobaMovementActionInputResolver.Resolve`；需要严格上下文时使用 `TryResolve` 并显式处理失败。
- Buff 触发 action 必须明确读取的是触发快照还是 live view，不能用实时运行状态替代触发时状态。
- 需要溯源的 action 必须使用或继承 `MobaActionOriginBuilder` 的规则。
- 不新增 action module 对 `PlanContextValueResolver` 的直接调用。
- 不把临时字段随意塞进 `MobaCombatExecutionContext`。
- 不重新引入统一 data bag 作为 action 间共享状态。
- condition 可以读取 payload data context，action 不应把 data context 当主要输入来源。

## 9. 阶段结论

当前重构路线值得继续。

最关键的收益是：上下文不再只是“能拿到数据”的工具，而开始成为战斗执行链路的正式结构。effect session、plan executor、action input resolver、origin builder 这些拆分让系统从能跑逐步走向可维护、可扩展、可诊断。

下一阶段不要急着扩新玩法功能，应该继续根据 fallback warning 和搜索结果把剩余兼容路径收紧。收紧后，新增 Buff/Projectile/Area/Summon/Unit State 类触发事件时，架构会更稳，后续扩展也更不容易把上下文重新打散。
