# Triggering 阶段性收口指南

## 当前状态

Triggering 当前可以作为 AbilityKit 中“事件发生后，按规则评估并执行动作”的阶段性可用包使用。它已经具备正式主线、基础产品化验收、对象池试点接入和回归扫描，适合进入小规模业务接入、样板工程验证和后续增量测试阶段。

当前结论：

- **先用即时主线。** 只要通知时用 `EventBus`；需要条件和顺序时注册手写 `ITrigger`，需要规则数据化时再用计划、注册表及验证。即时主线不要求宿主装配调度或调用推进 API。
- **调度按需接入。** 扁平计划内动作和执行树时间节点各有专项入口；宿主负责推进与生命周期，业务定时任务可在到期时发布即时事件。
- **池化属于扁平动作调度扩展。** `com.abilitykit.triggering.actionscheduling` 的 `ActionInstance` 和三个执行器由调度器专用池管理；即时规则和执行树不使用这个池。
- **验收扫描已建立。** 已有测试覆盖正式入口、legacy 防回流、产品化清单和运行时池化热点边界。

## 推荐接入路径

1. **先接入正式样板。** 通过 Package Manager 导入 **Triggering Mainline**，参考 [`Samples~/FormalMainline/FormalTriggeringMainlineExample.cs`](../Samples~/FormalMainline/FormalTriggeringMainlineExample.cs)，用 `TriggerRunner<TCtx>` 注册数据化计划。
2. **再注册项目动作。** 通过 `ActionRegistry` 暴露项目层动作，不在 Triggering Runtime 中新增 Buff、Projectile、AOE 等业务词汇。
3. **按需接入条件函数。** 通过 `FunctionRegistry` 注册条件函数并声明其确定性。宿主按[确定性边界](FormalApiBoundary.md#确定性边界)选择检查策略，并提供可重演的依赖。
4. **需要未来任务时才装配调度。** 扁平延迟/周期动作、执行树时间节点和独立业务规则按[专项宿主入口](FormalApiBoundary.md#专项宿主入口)分别接入。`EActionExecutionPolicy.Queued` 不提供动作队列，不建议新计划使用。
5. **按需为内置扁平动作调度启用对象池。** 战斗、关卡、世界或玩法域创建 `ActionSchedulerPools`，传给内置管理器，并在结束时先清理调度任务，再释放池。

## 对象池接入建议

对象池不是为了隐藏所有 `new`，而是让高频、短生命周期对象的生命周期可控。当前优先池化以下对象：

| 对象 | 用途 | 生命周期建议 |
| --- | --- | --- |
| `ActionInstance` | 计划动作调度实例 | 随 `ActionScheduler` 注册/完成/移除租还 |
| `DefaultActionExecutor` | 默认动作执行包装 | 随拥有它的 `ActionInstance` 归还 |
| `QueuedActionExecutor` | 兼容策略的单实例防重入包装，不提供排队与排序 | 递归释放内部 executor |
| `RetryActionExecutor` | 重试策略包装 | 递归释放内部 executor |

推荐生命周期：

```csharp
using var pools = new ActionSchedulerPools("battle-001");
pools.Prewarm();

var schedulerManager = new ActionSchedulerManager(pools);

// 战斗/关卡结束时：先清理管理器，后离开池的 using 作用域。
schedulerManager.Clear();
```

注意事项：

- 每个池实例拥有独立作用域，名称只用于诊断，不接受外部共享 `PoolScope`。若多个管理器借用同一个池实例，释放池之前须先清理这些管理器。
- 被外部长期持有、跨帧引用或由业务层拥有的对象不要强行池化。
- 使用 `GetDebugSnapshots()` 可在 Unity Editor 下观察池数量和活跃状态。
- 不传 `ActionSchedulerPools` 时，内置实现仍可按原有无池路径运行。

## 后续测试流程

每次修改 Triggering Runtime、Plans、Scheduling（包括其池化）、Validation、Samples 或文档验收内容后，至少执行以下流程：

1. **.NET 构建与测试**：执行[验收指南](ProductAcceptanceGuide.md#固定验证流程)中的仓库命令。

2. **热点与入口扫描**

   通过编辑器测试覆盖以下扫描：

   - `RuntimeCompatibilityLegacyEntryScanTests`：防止外部包重新引用历史入口。
   - `TriggeringProductAcceptanceChecklistTests`：防止验收文档、样例和菜单入口脱节。
   - `TriggeringRuntimePoolingHotspotScanTests`：防止已池化热点重新散落直接实例化。

3. **Unity Editor 回归**

   在 Unity Test Runner 中执行 `com.abilitykit.triggering` 的 Editor tests，确认构建外的编辑器菜单、包路径解析和扫描测试正常。

## 阶段完成标准

一个 Triggering 变更可以视为阶段性完成，需要同时满足：

- 新增能力从正式主线进入，不引入新的 legacy/compatibility 入口。
- 文档索引、验收指南和样例能说明该能力如何使用。
- 对运行时热点对象，明确是否池化；不池化时说明生命周期原因。
- `AbilityKit.Triggering.csproj` 与 `AbilityKit.Triggering.Tests.csproj` 均能构建通过。
- 对涉及正式边界、目录迁移、对象池或调度行为的改动，补充对应扫描或回归测试。

## 后续演进建议

- 继续把业务样板集中到 Samples 或 Demo，不污染 Runtime 包。
- 对宿主定时与即时规则、扁平动作调度的适用场景补更完整样例。
- 后续若继续池化更多对象，先补热点扫描，再改生命周期，不直接为了减少 `new` 扩大池化范围。
- 在正式发布前，用 Unity Test Runner 跑完整 Editor tests，并保留构建日志作为版本验收记录。
