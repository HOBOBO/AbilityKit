# Triggering 正式边界策略

`Triggering` 的正式主线只保留 `TriggerRunner<TCtx>`、`TriggerPlan<TArgs>`、`PlannedTrigger<TArgs, TCtx>`、`ExecCtx<TCtx>`、`ActionRegistry`、`FunctionRegistry`、`ActionScheduler`、`RuleScheduler` 与 `Runtime/Validation`。

## 规则

- 新功能只进入正式主线，不再新增旧调度、旧派发或迁移壳。
- 旧的 `Runtime/Dispatcher`、`Runtime/Scheduler`、`Runtime/Executable` 与 `Runtime/Legacy` 仅允许以只读历史记录存在。
- 文档、样例与测试只描述正式能力与边界，不再保留历史兼容说明。

## 约定

- 需要业务连续调度时，优先使用 `RuleScheduler`。
- 需要触发器计划内动作调度时，优先使用 `ActionScheduler`。
- 需要上下文访问时，使用 `ExecCtx<TCtx>` 及其正式服务适配。
