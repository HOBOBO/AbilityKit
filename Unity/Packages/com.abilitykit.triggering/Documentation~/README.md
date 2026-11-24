# Triggering 文档索引

包内指南统一放在 `Documentation~/`，不作为 Unity 资产导入。

- [`Overview.md`](Overview.md)：包定位、正式主线和扩展方式。
- [`StageClosureGuide.md`](StageClosureGuide.md)：接入路径、对象池建议与后续测试流程。
- [`FormalApiBoundary.md`](FormalApiBoundary.md)：正式、兼容与待删除入口的权威清单。
- [`SourceLayout.md`](SourceLayout.md)：当前源码落点、运行器内部职责与后续目录整理顺序。
- [`ExecutionRootOutcomeBaseline.md`](ExecutionRootOutcomeBaseline.md)：执行根结果、派发与资格计数的行为基线及待确认项。
- [`PredictionRollbackBoundary.md`](PredictionRollbackBoundary.md)：预测回滚状态账本、延迟任务上下文与试点验收。
- [`LegacyMigrationPolicy.md`](LegacyMigrationPolicy.md)：兼容与下线流程。
- [`ProductAcceptanceGuide.md`](ProductAcceptanceGuide.md)：产品化验收基线。
- [`Triggering-Commercial-Remediation-Checklist.md`](Triggering-Commercial-Remediation-Checklist.md)：商业化整改清单。
- [`Samples~/FormalMainline/FormalTriggeringMainlineExample.cs`](../Samples~/FormalMainline/FormalTriggeringMainlineExample.cs)：不装配时间调度的即时接入样板。
- [`Samples~/RuleScheduling/RuleSchedulingExample.cs`](../Samples~/RuleScheduling/RuleSchedulingExample.cs)：单独导入的业务定时规则样例。
- [`SampleInventory.md`](SampleInventory.md)：可选导入样例与历史源码处置记录。

## 编辑器诊断

- `AbilityKit/Triggering/Diagnostics/Write Runtime Report` 导出运行时诊断报告。
- `AbilityKit/Triggering/Diagnostics/Write Product Acceptance Report` 导出产品化验收摘要。
