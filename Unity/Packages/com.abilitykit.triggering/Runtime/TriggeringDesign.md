# AbilityKit Triggering 运行时模块设计文档（Runtime）

本文档面向：
- 需要在运行时**注册触发器**、**派发事件**、**加载 TriggerPlan JSON** 的使用者
- 需要扩展“任意条件/任意行为”、接入 deterministic replay / ECS 的开发者

目标：帮助你快速建立对 `com.abilitykit.triggering/Runtime` 的整体心智模型，并能按推荐路径落地使用。

---

## 1. 模块分层与目录结构

Runtime 目录大致按职责拆分：

- `Eventing/`
  - `EventBus`：事件总线（Immediate/Queued），支持 `Flush()`
  - `EventSchemaRegistry`：eventId -> argsType/name 的运行时 schema 注册表
  - `StableStringId`：稳定字符串哈希（string -> int）作为运行时 ID

- `Runtime/`
  - `TriggerRunner`：触发器调度器（订阅事件、按 phase/priority 执行、提供 `ExecCtx`）
  - `ExecCtx`：执行上下文（包含 TriggerContext、Registries、Blackboard、Payload、Policy、Legacy 等）
  - `ExecutionControl`：控制短路（StopPropagation/Cancel）
  - `TriggerContext` / `ITriggerContextSource`：由宿主注入的上下文（目前内置 Frame/Sequence）

- `Plan/`
  - `TriggerPlan<TArgs>`：强类型计划结构（Predicate=Function/Expr/Legacy，Actions=强类型调用 + Legacy）
  - `PlannedTrigger<TArgs>`：将 `TriggerPlan` 解析成可执行触发器
  - `PredicateExprPlan`：布尔表达式（RPN 逆波兰）
  - `RpnIntExprRuntime`：整数 RPN 表达式运行时解析/缓存/求值（用于更复杂的数值来源）
  - `Json/TriggerPlanJsonDatabase`：从 JSON 加载计划并注册到 runner

- `Registry/`
  - `FunctionRegistry`：注册 predicate/function 的委托
  - `ActionRegistry`：注册 action 的委托

- `Blackboard/` / `Payload/`
  - 黑板：`DictionaryBlackboard` / `IBlackboardResolver` 等
  - payload：`PayloadAccessorRegistry` / `IPayloadIntAccessor<TArgs>` 等

- `Example/`
  - 纯 C# 示例（无 Unity 场景依赖），用于快速理解 API 组合方式

---

## 2. 核心数据流（从事件到触发执行）

一次事件触发的核心流程：

1. 业务侧调用 `EventBus.Publish(eventKey, args)`
2. `EventBus` 将事件分发给订阅者（Immediate 模式立刻派发；Queued 模式等待 `Flush()`）
3. `TriggerRunner` 作为订阅者收到事件：
   - 从 `ITriggerContextSource` 获取 `TriggerContext`
   - 构造 `ExecCtx`（把 registries/blackboard/payload/policy/legacy/control 统一注入）
   - 按 `phase -> priority -> registrationOrder` 顺序遍历触发器
4. 对每个触发器：
   - `Evaluate(args, execCtx)` 判定是否满足条件
   - 若满足则 `Execute(args, execCtx)` 执行动作
   - 若 `ExecutionControl.StopPropagation` 或 `ExecutionControl.Cancel` 被置位，则短路退出

---

## 3. 触发器模型：ITrigger 与 TriggerPlan

### 3.1 ITrigger<TArgs>
两步接口：
- `bool Evaluate(in TArgs args, in ExecCtx ctx)`
- `void Execute(in TArgs args, in ExecCtx ctx)`

你既可以直接写 `DelegateTrigger<TArgs>`（手写逻辑），也可以使用计划系统 `TriggerPlan<TArgs>` + `PlannedTrigger<TArgs>`。

### 3.2 TriggerPlan<TArgs>
`TriggerPlan` 是推荐的“可序列化/可 codegen/可回放”的中间结构。

- `Phase / Priority`：调度顺序
- `Predicate` 三种形态：
  - `None`：无条件，永远通过
  - `Function`：任意条件（委托），从上下文取值做复杂判断
  - `Expr`：布尔表达式（RPN），性能高且更易 deterministic replay
  - `Legacy`：旧系统逃生舱（通过 `ILegacyTriggerExecutor` 执行）

- `Actions` 两种形态：
  - 强类型 ActionCallPlan：通过 `ActionRegistry` 查委托并执行
  - LegacyActionPlan：旧系统逃生舱

`PlannedTrigger<TArgs>` 会在第一次执行时 Resolve：
- 将 `FunctionId/ActionId` 解析为真实委托
- 在执行时按 arity 求值参数（来自 payload/blackboard/const）

---

## 4. “条件”如何表达与扩展

### 4.1 Expr（RPN 布尔表达式）
`PredicateExprPlan` 目前内置节点：
- `CompareInt`：对 `IntValueRef` 做 `Eq/Ne/Gt/Ge/Lt/Le`
- `And/Or/Not`
- `Const`

特点：
- 可序列化、可 codegen
- 可做 deterministic replay（输入显式化）
- 高性能（stackalloc + RPN）

### 4.2 Function（任意条件，推荐扩展点）
当你需要：
- 从上下文取复杂数据（ECS/World/服务查询）
- 做非线性的内部逻辑（查表/组合规则/状态机）

使用 `PredicateKind=Function`：
- 在 `FunctionRegistry` 注册 `PlannedTrigger<TArgs>.Predicate0/1/2` 委托
- `TriggerPlan` 中引用 `FunctionId`

常见数据来源：
- A：`ctx.Context`（来自 `ITriggerContextSource`）
- B：`ctx.Blackboards`
- C：自定义服务（推荐通过依赖注入或闭包捕获；如需更结构化可考虑把 services 放入 TriggerContext/ExecCtx）

### 4.3 Legacy（旧系统逃生舱）
当计划系统还没覆盖你们已有的复杂条件/动作节点时：
- `LegacyPredicatePlan` / `LegacyActionPlan` 保存 `type + args`
- 运行时通过 `ILegacyTriggerExecutor` 桥接到旧系统 factory/runner

---

## 5. “行为/动作”如何表达与扩展

### 5.1 强类型 Action
通过 `ActionRegistry` 注册委托：
- `PlannedTrigger<TArgs>.Action0/1/2`

`ActionCallPlan` 支持：
- `arity=0`：无参数
- `arity=1/2`：参数来自 `IntValueRef`（const/payloadField/blackboard）

### 5.2 Legacy Action
同条件的 Legacy：用于旧系统行为节点的兼容。

---

## 6. 数值来源与表达式：IntValueRef 与 RPN Int

### 6.1 IntValueRef
用于 “从哪里拿 int 值”：
- `Const`
- `PayloadField(fieldId)`
- `Blackboard(boardId, keyId)`

### 6.2 RPN Int（RpnIntExprRuntime）
当你需要更复杂的数值来源（例如 `payload.amount + bb:combat:atk`）且希望运行时解析：
- `RpnIntExprPlan`：保存 `lang + text`
- `RpnIntExprRuntime`：运行时解析并缓存节点
- `RpnIntExprEval`：用 `ExecCtx` 解析 token 并求值

建议：
- 为 deterministic replay：固定 `lang` 版本、限制 token 集合、避免非确定输入

---

## 7. 确定性（Deterministic Replay）策略

运行时通过 `ExecPolicy.RequireDeterministic` 控制：
- 当 `RequireDeterministic=true`：
  - `PlannedTrigger` 在 Resolve 时会拒绝注册为非确定性的 function/action（registries 会带 `isDeterministic` 标记）

工程建议：
- 将所有“非确定输入”显式化：写入 payload 或 blackboard
- 在回放时只依赖 event stream + 受控黑板更新

---

## 8. JSON 加载（TriggerPlanJsonDatabase）

`TriggerPlanJsonDatabase` 支持：
- 从 JSON 读取多条 trigger 记录
- `RegisterAll(runner)` 批量注册到 runner

当前 DTO 支持：
- `Predicate.Kind = none/expr/legacy`
- `expr` 里 nodes 对应 `BoolExprNode`
- `Actions` 支持 arity=0/1/2 + `IntValueRef`
- `LegacyPredicate` / `LegacyActions`

注意：
- JSON 加载的计划是 `TriggerPlan<object>`，适用于“无强类型 payload / 纯 runtime 配置”的场景。
- 若你需要强类型 args 校验，建议配合 `EventSchemaRegistry` 或 codegen。

---

## 9. 示例索引（Runtime/Example）

建议按顺序阅读：

- `TriggeringExample.cs`
  - 从零搭建 runner + payload/blackboard + RPN Int 求值

- `TriggerPlanExample.cs`
  - 标准计划示例：复合条件（RPN And/Or/Not + Compare）+ 复合行为（多 action）+ 触发事件

- `ExecutionControlExample.cs`
  - `StopPropagation` / `Cancel` 的短路行为

- `QueuedEventBusExample.cs`
  - Queued 模式下 `Publish` 与 `Flush`

- `TriggerPlanJsonDatabaseExample.cs`
  - JSON 加载 + 注册

- `AnyPredicate_ContextSourceExample.cs`
  - A：从 `ctx.Context`（contextSource）取值做任意条件

- `AnyPredicate_BlackboardExample.cs`
  - B：从黑板取值做任意条件

- `AnyPredicate_CustomServiceExample.cs`
  - C：从自定义服务取值做任意条件（闭包捕获方式演示）

- `PhasePriorityExample.cs`
  - phase/priority 执行顺序

- `EventSchemaRegistryExample.cs`
  - event schema 的注册与查询

---

## 10. 推荐上手路径（最短路径）

1. 先跑通：`TriggeringExample`（理解 runner/bus/blackboard/payload）
2. 再看：`TriggerPlanExample`（理解计划系统 + 复合条件/复合行为）
3. 再看：`AnyPredicate_*`（理解“任意条件”扩展点）
4. 最后接入：JSON/codegen/Legacy executor

---

## 11. 设计约束与未来扩展建议

- 当前 `TriggerContext` 仅包含 `Frame/Sequence`，若你需要“服务容器/世界引用”，可以考虑：
  - 扩展 TriggerContext（增加 `object Services` / `IServiceProvider` / 自定义接口）
  - 或在 `ExecCtx` 增加一个 `Services` 字段（由 TriggerRunner 构造时注入）

- 对 expression 体系（Bool/Int）可持续扩展：
  - 增加更多 ValueRef（float/bool/string/entityId）
  - 增加更多节点（范围、集合包含、标签匹配等）
  - 增加 schema 驱动的 codegen 以确保强类型与性能
