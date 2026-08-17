# Triggering 正式 API 边界

`Triggering` 的正式 API 只围绕以下主线组织：

- `TriggerRunner<TCtx>`
- `TriggerPlan<TArgs>`
- `PlannedTrigger<TArgs, TCtx>`
- `ExecCtx<TCtx>`
- `ActionRegistry`
- `FunctionRegistry`
- `ActionScheduler`
- `RuleScheduler`
- `Runtime/Validation`

## 职责

- `TriggerRunner<TCtx>` 负责驱动正式触发执行。
- `TriggerPlan<TArgs>` 与 `PlannedTrigger<TArgs, TCtx>` 负责计划与执行组织。
- `ActionScheduler` 负责计划内动作调度。
- `RuleScheduler` 负责业务级持续调度。
- `ExecCtx<TCtx>` 负责上下文读取与正式服务访问。

## 边界

- 触发器计划内动作不要直接依赖旧派发或旧调度概念。
- 持续效果与业务节奏使用 `RuleScheduler`，不要在正式文档中回到旧 `Runtime/Scheduler` 术语。
- 上下文服务通过正式的注册与适配访问，不再依赖历史兼容入口。

## 示例准则

1. 新样例直接使用正式入口。
2. 新文档只描述主线职责和可复用边界。
3. 需要业务节奏时优先选择 `RuleScheduler`。
4. 需要计划动作时优先选择 `ActionScheduler`。
