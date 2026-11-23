# Triggering 产品化验收指南

## 验收目标

| 维度 | 要求 | 验收方式 |
| --- | --- | --- |
| 即时主线 | 事件、条件、计划与动作可独立使用；消费方不加载 JSON、不装配调度、不调用 `Tick` / `Update` | 单独运行主线消费者，验证条件拒绝、同步顺序和退订 |
| 确定性 | `ExecPolicy.RequireDeterministic` 下不得依赖随机数、系统时间、网络请求或未登记的非确定性函数/动作 | 同一计划、同一事件序列、同一上下文快照重复执行，动作顺序和结果一致 |
| 计划动作 | 立即动作在派发中调用；延迟、周期等调度动作由 `ActionScheduler` 推进 | 分别验证立即与调度路径，不把立即执行视为旧入口 |
| 可选业务调度 | 独立规则级时间意图可用 `RuleScheduler`，不进入即时样例 | 单独运行定时规则样例，验证到期前不执行、到期仅执行一次 |
| 对象池 | 运行时热点对象应通过 `TriggeringRuntimePools` 或显式 fallback 管理 | 检查 `TriggeringRuntimePoolingHotspotScanTests` 是否覆盖新增热点 |
| 阶段收口 | 包说明、接入路径、测试命令与验收标准必须能从文档索引找到 | 检查 `StageClosureGuide.md`、`Documentation~/README.md` 与本指南是否同步 |

## 记录要求

- 验收报告只记录正式入口、正式边界和正式样例。
- 兼容入口与下线证据统一记入[正式边界清单](FormalApiBoundary.md)，不写入新接入示例。
- 新增能力必须先落到正式主线，再补充对应验证。
- 正式样例以 [`Samples~/FormalMainline/FormalTriggeringMainlineExample.cs`](../Samples~/FormalMainline/FormalTriggeringMainlineExample.cs) 作为最小可复制入口，通过 Package Manager 可选导入。
- 定时规则样例单独导入，见[样例盘点](SampleInventory.md)。即时样例不装配调度；这是接入和运行行为边界，不是运行时程序集裁剪承诺。
- 运行时诊断以 `TriggeringDiagnosticCollector` 和编辑器诊断菜单作为验收入口。

## 固定验证流程

修改影响 Triggering 行为、样例或验收文档时，从仓库根目录运行：

```powershell
dotnet test src/AbilityKit.Triggering.Tests/AbilityKit.Triggering.Tests.csproj
dotnet run --project tools/triggering-consumer/AbilityKit.Triggering.Consumer.csproj
```

第二条命令通过仓库源码构建独立 .NET 消费者，只编入即时样例，不使用发布包或发布探针，也不替代 Unity 导入 / Player 验证。

`FlatActionSchedulingAssemblyTests` 固定验收类型化与动态注册前拒绝、直接执行无部分副作用及不消耗触发资格、事件派发失败隔离、宿主显式推进同一实例，以及执行根不受未使用扁平列表影响。

构建通过后，在 Unity Test Runner 中执行 `com.abilitykit.triggering` 的 Editor tests，重点确认以下扫描测试：

- `RuntimeCompatibilityLegacyEntryScanTests`：外部包不得重新引用历史触发器入口。
- `TriggeringProductAcceptanceChecklistTests`：文档、样例、菜单验收入口保持同步。
- `TriggeringRuntimePoolingHotspotScanTests`：已池化运行时热点不得重新散落直接实例化。

## 常用检查

1. 搜索新增代码是否仍然引用旧调度或旧派发入口。
2. 检查示例是否仅使用正式计划、上下文与调度类型。
3. 检查验收测试是否覆盖计划执行、动作调度与业务调度。
4. 检查对象池接入是否只覆盖短生命周期运行时热点。
5. 检查产品说明是否仅描述正式能力，并指向 `StageClosureGuide.md`。
