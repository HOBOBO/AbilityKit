# MOBA 上下文模块指南

## 设计指南

当前端到端战斗上下文设计、生命周期规则、运行时上下文诊断和扩展检查清单见 [整体设计指南](../../../Docs/MobaCombatContextDesignGuide.md)。

设计评审建议从整体指南开始，再阅读 [Runtime Context 值与快照设计](../../../../Document/RuntimeContext运行时值与快照设计文档.md)、[Context 基础包](../../../../../com.abilitykit.context/Document/Context上下文注册与快照模块开发设计文档.md) 和 [Trace 基础包](../../../../../com.abilitykit.trace/Document/Trace溯源树模块开发设计文档.md)。Runtime Context 文档单独列出已实现行为、版本限制、恢复边界和待评审项。

## 用途

`Context` 模块是 MOBA 玩法执行共享的运行时上下文基础设施。它连接强类型触发载荷、执行期上下文聚合、来源快照、起源传播、谱系构建和溯源集成。

该模块不得变成通用业务数据袋。新玩法逻辑应优先采用强类型载荷，仅在集成回退场景下使用键值管线上下文。

Runtime Context 正式引用携带本地 ContextEntityReference（RegistryId/EntityId/Generation），与业务 Version 分离。阶段/事件载荷在生成时复制身份，不在读取时绑定当前代次；实时读取先解析一致属性视图，历史工具保存 ResolvedSnapshot 后使用精确 ReadSnapshot。旧 ID-only 入口仍是弱兼容路径，本地身份不进入权威恢复载荷/hash。

## 主要模型优先级

按以下顺序使用模型：

1. `MobaTriggerInvocationContextBase`
   - 推荐作为新触发载荷的基类。
   - 载荷应通过统一的 `IMobaTriggerExecutionPayload` 契约公开起源、谱系和溯源信息。

2. `MobaCombatExecutionContext`
   - 效果、动作和条件执行期间的规范执行期模型。
   - 执行服务应先把载荷规范化为此模型，再运行动作逻辑。

3. `MobaPersistentContextSourceSnapshot`
   - 跨帧及异步生命周期的规范来源快照。
   - Buff、投射物、召唤物、持续行为和延迟执行流程应保留此快照，而不是保留活动运行时对象。

4. `MobaContextSourceView`
   - 用于查询、调试、保留和传输的视图。
   - 它有意覆盖较广，但不应取代 `MobaCombatExecutionContext` 成为主要执行模型。

5. `AbilityContextKeys` / `AbilityContextExtensions`
   - 管线数据袋兼容层。
   - 这些键不能替代强类型载荷。

## 起源、谱系、溯源和来源语义

- `MobaGameplayOrigin` 回答该玩法操作来自何处。
- `MobaTriggerLineageContext` 回答该操作如何接入溯源谱系链。
- `MobaTriggerTraceContext` 是紧凑的触发器溯源表示。
- `MobaContextSourceView` 是面向查询、快照、保留、调试面板和诊断的已解析来源视图。
- `MobaCombatExecutionContext` 聚合当前可执行载荷、谱系输入、起源、执行快照、技能运行时句柄和帧。

## 新载荷规则

新触发载荷应：

1. 正式触发执行载荷应继承 `MobaTriggerInvocationContextBase`。
2. 使用已有起源或谱系数据实现 `TryGetOrigin`、`TryGetLineageContext` 和 `TryGetTraceContext`。
3. 能公开查询/保留来源信息时实现 `IMobaContextSourceProvider`。
4. 来源需要跨异步或跨帧执行存活时实现 `IMobaPersistentContextSourceProvider`。
5. 不要只添加角色、配置或上下文 ID 等基础字段，而不同时公开正式的起源或谱系提供者。

## 旧基础字段兼容

`MobaGameplayOrigin.FromLegacy` 和构建器中的旧基础字段 API，是为仍只携带角色/配置/上下文基础字段的旧载荷提供的兼容桥。

新代码应优先：

- 传播已有的 `MobaGameplayOrigin`。
- 从 `MobaTriggerLineageContext` 构建。
- 为异步生命周期捕获 `MobaPersistentContextSourceSnapshot`。
- 在执行服务中规范化为 `MobaCombatExecutionContext`。

## 效果入口观察

`MobaEffectExecutionEntrySnapshot` 是执行入口事实，不是 Runtime Context 最新值，也不是状态恢复基线。采集由逻辑层可选 hook 在诊断 Events/Full + Skill 通道且未冻结时触发，编辑器通过 Trace 摘要只读消费。数据存于独立 Trace-facts 快照存储；其中 EntityId 是 TraceContextId，不能混用 RuntimeContextId。详见 [受管快照与效果入口设计](../../../../Document/RuntimeContext运行时值与快照设计文档.md#71-效果执行入口的观察快照)。

`MobaActionExecutionSnapshot` 是独立动作观察类型，保存入口/结束的来源与目标 HP/Mana、调用状态及有界实际 HP 提交列表。只读取已有资源，通过现有提交事件按最近真实动作关联，不把调用成功等同于玩法成功，也不补采缺失或淘汰记录。详见 [动作前后事实设计](../../../../Document/RuntimeContext运行时值与快照设计文档.md#72-动作执行前后事实与实际-hp-提交)。

动作观察 v2 增加独立伤害终态列表，复用已接受诊断事件，覆盖护盾全吸收、零 HP 伤害、目标缺失、提交拒绝和事务/执行异常；不凭“无 HP 提交”推测结果。缺少伤害通道、覆盖间隙和数量截断分别标记，旧 Artifact 缺字段为 NotCaptured。详见 [伤害结果设计](../../../../Document/RuntimeContext运行时值与快照设计文档.md#73-未产生-hp-提交的伤害管线结果)。

## 命名约定

所有权上下文标识应优先命名为 `OwnerContextId`。`OwnerKey` 仅作为面向谱系结构的兼容别名保留。

直接来源执行上下文使用 `SourceContextId`。
传播起源链中的直接父级使用 `ParentContextId`。
因果链的稳定根使用 `RootContextId`。
