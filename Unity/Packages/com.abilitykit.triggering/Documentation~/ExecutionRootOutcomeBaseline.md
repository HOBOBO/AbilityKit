# 执行根结果：现状与首发决策

## P0：行为基线，不是新语义

以下以 `PlannedTrigger` 已通过执行资格检查、带正值 `InterruptPriority`，执行根不自行写入中断或拒绝控制为前提。`TriggerRunner` 派发时提供共享的 `ExecutionControl`。

| 执行根结果 | Runner 观察到的结果 | 消耗 `Once`／开始 `Cooldown` | 后续低优先级规则 |
| --- | --- | --- | --- |
| `Success` | `OnActionExecuted`，计入本次派发执行数 | 是 | 被自动优先级中断跳过 |
| `Skipped` | 同正常完成，Runner 不读取结果 | 是 | 被自动优先级中断跳过 |
| `Failed` | 同正常完成，不发 `OnActionFailed` | 是 | 被自动优先级中断跳过 |
| 抛出异常 | `OnActionFailed`，不计入本次派发执行数；直接调用 `PlannedTrigger.Execute` 则向上抛出 | 是 | 不应用计划的自动优先级中断，继续派发 |

`TriggerPlanExecutionResult.ExecutedCount` 不是 Runner 的派发计数，也不驱动 `PlannedTrigger` 的资格计数。资格按尝试更新；若未通过资格检查，执行根不被调用，Runner 仍只能观察到 `Execute` 正常返回。资格状态属于该触发器实例；当前测试用显式 `ExecPolicy.TotalTimeMs` 验证冷却到期边界，不代表 Runner 提供推进时钟或恢复资格状态的能力。

证据：`.NET` 的 `ExecutionRootOutcomeBaselineTests` 覆盖四种结果的派发通知、执行数、低优先级后续规则与资格消耗；不是 MOBA 集成测试。

## MOBA 入口差异（源码核对）

- 全局执行根由 `PlanTriggeringStage` 注册为 `PlannedTrigger`；所有者绑定由 `MobaTriggerPlanSubscriptionService` 注册为同类触发器。两者经过通用派发边界，不应由直接调用的返回值推导其派发结果。
- `MobaTriggerPlanExecutorRuntime` 的直接执行根路径读取 `result.IsSuccess && !control.IsActionRejected`；这是宿主调用的返回约定，不等同于 Runner 的 `OnActionExecuted`。

## 首发前待确认的行为选择

| 决策 | 需要明确的边界 |
| --- | --- |
| 是否区分尝试与实际执行 | `Success(0)`、`Skipped`、`Failed` 和异常分别是否消耗 `Once`、`Repeat` 与 `Cooldown`；不能通过改一个计数器同时决定所有资格规则。 |
| 哪些结果应用自动中断 | 区分计划配置的自动优先级中断与动作主动写入的中断；明确何时继续当前事件内的后续规则。 |
| 如何向调用者暴露结果 | 区分跳过、业务失败、异常和部分动作已发生；分别确认派发统计、通知/Cue 与 MOBA 直接执行入口，不把失败视作隐式回滚。 |

这些选择会改变共享执行语义。本批不实施其中任何一项；选定方案后再补全局订阅、所有者订阅和直接执行的宿主验收。
