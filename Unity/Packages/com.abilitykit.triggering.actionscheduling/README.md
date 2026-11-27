# com.abilitykit.triggering.actionscheduling

Triggering 的可选扁平动作调度扩展。即时规则不需要安装本包；自有调度的项目可以直接实现 `IFlatActionScheduler`，无需引用内置实现。

## 接入

1. 安装本包，在使用方的 asmdef 中显式引用 `AbilityKit.Triggering.ActionScheduling`（同时按需引用 `AbilityKit.Triggering`、`AbilityKit.Core`）。.NET 使用方直接引用 `AbilityKit.Triggering.ActionScheduling.csproj`。
2. 创建 `ActionSchedulerManager`（命名空间 `AbilityKit.Triggering.Runtime.ActionScheduler`），将其作为 `flatActionScheduler` 注入 `TriggerRunner<TCtx>`。安装本包不会自动注册服务或推进时间。
3. 由宿主调用 `Update(deltaMs, dispatcherContext)` 推进。释放规则注册句柄只停止订阅；待执行动作由宿主用 `RemoveScheduler(triggerId)` 或 `Clear()` 清理。可选 `ActionSchedulerPools` 由宿主拥有，先清理调度任务，再释放池。

## 首发 API

| 公开类型 | 支持的宿主入口 |
| --- | --- |
| `ActionSchedulerManager` | 构造时可传池；通过 `IFlatActionScheduler` 注入；用 `Update` 串行推进，`RemoveScheduler` / `Clear` 清理。`SchedulerCount` 是当前分组数，`TotalActionCount` 是当前持有实例数，包括尚未回收的终态实例，并非正在执行的数量。 |
| `ActionSchedulerPools` | 按默认配置构造，可给诊断作用域命名；`Prewarm` 预热、`Dispose` 释放。`GetDebugSnapshots` 仅在 Unity Editor 可用，用于诊断，不承诺固定的池明细。 |

不要从动作回调重入同一管理器的推进或清理。宿主需要暂停时停止调用 `Update`；这只停止推进，不阻止事件派发或新计划登记。`RemoveScheduler` 和 `Clear` 不解除事件订阅，结束规则应先退订，再清理待执行动作。分组、动作实例、执行器和池配置是内部实现，不是扩展点；需要自定义调度行为时应实现 `IFlatActionScheduler`。

这是原内置扁平动作调度的独立装配，不接管执行树、事件派发、帧时钟或恢复策略。周期、持续、重试和 `Queued` 是本实现行为，不是其他调度器的互换承诺；本包不提供调度快照或预测回滚契约。

验收使用仓库 `core-stability` 门禁中的 `Triggering action scheduling tests`，以及 Unity EditMode 的 `AbilityKit.Triggering.ActionScheduling.Tests`。
