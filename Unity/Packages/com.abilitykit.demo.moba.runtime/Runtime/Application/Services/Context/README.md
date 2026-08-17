# MOBA 上下文模块指南

## 设计指南

当前端到端战斗上下文设计、生命周期规则、运行时上下文诊断和扩展检查清单见 `Runtime/Docs/MobaCombatContextDesignGuide.md`。

## 用途

`Context` 模块是 MOBA 玩法执行共享的运行时上下文基础设施。它连接强类型触发载荷、执行期上下文聚合、来源快照、起源传播、谱系构建和溯源集成。

该模块不得变成通用业务数据袋。新玩法逻辑应优先采用强类型载荷，仅在集成回退场景下使用键值管线上下文。

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

## 命名约定

所有权上下文标识应优先命名为 `OwnerContextId`。`OwnerKey` 仅作为面向谱系结构的兼容别名保留。

直接来源执行上下文使用 `SourceContextId`。
传播起源链中的直接父级使用 `ParentContextId`。
因果链的稳定根使用 `RootContextId`。
