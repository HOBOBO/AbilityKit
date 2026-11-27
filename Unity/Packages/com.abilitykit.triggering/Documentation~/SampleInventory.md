# Triggering 样例盘点

包提供两份独立的可选导入样例，均位于 `Samples~/`，不编入包的运行时程序集：

- **Triggering Mainline**：`FormalTriggeringMainlineExample.RunOnce()` 演示即时计划校验、条件、同一事件的稳定顺序和退订；`RunNestedDispatchOnce()` 单独演示动作同步发布另一事件、外层恢复以及内外层退订。两个入口都不加载 JSON、不装配调度、不推进时间。保留原主线样例脚本 GUID。
- **Triggering Queued Events**：调用 `QueuedEventBusExample.RunOnce()`，验证 `Publish` 只入队，`Flush` 按阶段升序、同阶段优先级降序派发。迁移保留原队列样例脚本 GUID。

原 **Triggering Rule Scheduling** 已随旧独立业务调度退役。跨包用法由 `com.abilitykit.samples` 的 `SchedulerExample` 展示：宿主用秒推进定时器，到期发布即时事件。该样例不是 Triggering 的导入项。

## 原 `Samples/` 的处置

原目录的 23 份 C# 文件未纳入任何 asmdef。盘点未发现包外消费者或 Unity 资产 GUID 引用；`com.abilitykit.samples` 中的同名类型属于独立样例包，不在本次范围。主线和队列样例迁入 `Samples~/`；未迁移的旧源码及元数据保留在 Git 历史（例如提交 `bcc9cea4a` 的原 `Samples/`），不再作为包内容交付。具体处置如下：

| 原文件 | 处置依据 |
| --- | --- |
| `FormalTriggeringMainlineExample` | 迁入主线样例，之后将定时规则拆为独立样例，不保留重复实现。 |
| `QueuedEventBusExample`、`PhasePriorityExample` | 合并为队列样例，改用 `RegisterPlan`，纠正旧排序说明中的优先级方向。 |
| `README_Example` | 仅有空命名空间，不建立替代占位。 |
| `TriggeringExample`、`TriggerPlanExample`、`TriggerPlanDslExample`、`TCtxFlexibilityExample` | 正式接入以主线样例为准；不继续交付并行的早期接入模板。 |
| `AnyPredicate_BlackboardExample`、`AnyPredicate_ContextSourceExample`、`AnyPredicate_CustomServiceExample`、`NumericExpression_BlackboardDomainExample`、`PayloadStructExample`、`EventSchemaRegistryExample` | 属于专题或旧上下文示意，不当成可直接导入的接入模板。独立样例需先补接入验证。 |
| `HierarchicalTriggerRunnerExample` | 旧层级派发示例已归档；对应的试验性运行器经消费方核对后已退出首发源码。 |
| `ExecutionControlExample`、`TriggerCueExample`、`TriggerLifecycleExample` | 旧扩展示意归档；执行控制、Cue 与生命周期契约不随样例移除。 |
| `TriggerPlanJsonDatabaseExample` | 归档内联配置样例；加载与校验边界以源码目录说明及加载基线为准。 |
| `ScheduleManagerSample`、`SchedulerRegistryExample`、`Scheduler/SchedulerExample`、`Scheduler/BuffScheduleData` | 授时、规则调度与 Buff 生命周期不混讲；宿主定时与即时规则的组合由独立样例包演示。 |

新增样例须显式登记在 `package.json`，放在独立的 `Samples~/` 子目录，验证能单独导入和运行。不要恢复旧 `Samples/` 或将历史示例当成 API 下线的依据。
