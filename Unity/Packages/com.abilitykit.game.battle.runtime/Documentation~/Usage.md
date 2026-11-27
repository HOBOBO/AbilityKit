# Game Battle Runtime 使用指南

> 文档类型：使用指南
> 适用范围：`com.abilitykit.game.battle.runtime` 0.1.0，2026-11-25 工作区源码及 .NET 镜像
> 核校状态：部分核校；本文的接口、请求、排序、状态和报告已作 E0 核对，局部 E3 见第 7 节
> 事实基线：2026-11-25；不包含具体 gameplay、在线传输与 Unity 生命周期验收

## 1. 目标与前置条件

本包为宿主与战斗模块提供共同语言。它不提供具体 transport、战斗调度器、同步算法或世界恢复器。直接依赖见 [package.json](../package.json)，同时满足传递依赖条件；本包的 Unity 2021.3 声明不等于完整依赖图的支持下限。命名空间为 `AbilityKit.Game.Battle`，请求为 `AbilityKit.Game.Battle.Requests`。

| 能力 | 源码入口 | 使用者职责 |
| --- | --- | --- |
| 逻辑收发接口 | [IBattleLogicTransport](../Runtime/Battle/IBattleLogicTransport.cs) | 实现连接、响应处理、订阅和释放 |
| 创建/加入/离开/输入请求 | [请求目录](../Runtime/Battle/Requests/) | 校验业务输入和引用对象寿命 |
| 能力与状态 | [BattleRuntimeStatus](../Runtime/Battle/BattleRuntimeStatus.cs) | 维护真实状态并按要求决定能否启动/推进 |
| 阶段依赖图 | [BattleStageGraph](../Runtime/Battle/BattleStageGraph.cs) | 执行阶段、提交完成集合、处理失败 |
| 系统贡献排序 | [BattleSystemContribution](../Runtime/Battle/BattleSystemContribution.cs) | 创建、调度、释放系统及补偿部分失败 |
| 校验与健康 | [BattleValidationAndHealth](../Runtime/Battle/BattleValidationAndHealth.cs) | 决定启动门禁、采样频率和失败隔离 |

## 2. 完整示例：阶段图与就绪条件

完整 Console 示例，只依赖已装配本包的工程。预期输出 `battle composition passed`；示例仅查询计划，没有创建世界或建立网络。

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Game.Battle;

var graph = BattleStageGraph.Create(new[]
{
    new BattleStageDefinition("battle", 0, new[] { "load" }),
    new BattleStageDefinition("load", 100)
});
var completed = new HashSet<string>(StringComparer.Ordinal);
if (graph.GetAvailableStages(completed).Single().Id != "load")
    throw new InvalidOperationException("Prerequisite order failed");

// 实际宿主在资源加载与校验成功之后才添加完成记录。
completed.Add("load");
if (graph.GetAvailableStages(completed).Single().Id != "battle")
    throw new InvalidOperationException("Next stage failed");

var status = new BattleRuntimeStatus(
    BattleRuntimeCapability.Input | BattleRuntimeCapability.Simulation |
    BattleRuntimeCapability.SnapshotOutput, BattleRuntimeState.Ready);
if (!status.Meets(BattleReadinessRequirement.BattleLoop) ||
    status.Meets(BattleReadinessRequirement.GameStart))
    throw new InvalidOperationException("Readiness failed");
Console.WriteLine("battle composition passed");
```

先决依赖优先于 `Order`；不能通过把 battle 的 Order 调小绕开 load。完成集合由宿主持有，查询本身不会运行阶段或更新状态。

## 3. 传输接口、请求与所有权

`IBattleLogicTransport` 提供 `FramePushed` 事件、Connect/Disconnect 及四类 void Send 方法。接口没有 Tick、Dispose、Task、回执、鉴权或重连完成保证；具体实现的生命周期和额外接口须查该实现。订阅事件后，调用者负责解除；不能假定 Disconnect 等价于销毁对象或离开服务端世界。

| 请求 | 当前语义与限制 |
| --- | --- |
| `CreateWorldRequest` | 构造器拒绝 null options，但保存原 Options 与 Payload 引用，不复制它们；`default` struct 会绕过构造校验 |
| `JoinWorldRequest`、`LeaveWorldRequest` | 携带世界/玩家身份、可选 opcode 与 payload，不提供完整业务合法性校验或服务端接受证明 |
| `SubmitInputRequest` | 持有 WorldId 和 PlayerInputCommand，不选择帧/状态同步模式，也不决定输入的权威路由 |

readonly struct 只限制外层赋值，不深冻结嵌套对象，不证明零分配或跨线程安全。发送前明确 payload 是同步消费、复制还是借用；不可把抽象接口的 void 返回当作服务端成功应用。

## 4. 阶段与系统贡献

`BattleStageGraph.Create` 校验 null 输入/null 元素、重复 ID、空依赖、缺失依赖与循环。ID 使用 ordinal、区分大小写，不自动 trim。拓扑顺序先满足依赖，可选节点再按 Order、最后按原始输入位置稳定排序；同 Order 的原始顺序属于结果的一部分。

`GetAvailableStages(completedSet)` 只返回尚未完成且依赖满足的节点，不更改 completedSet，不执行任务，不是运行时状态机。集合比较器由调用者提供，建议与定义一致使用 ordinal。定义保留传入的依赖列表引用，不保证深冻结；创建后不修改原列表，也不在查询中并发改完成集合。

系统贡献 planner 同样按依赖、Order、原始位置排序，支持 `RunsAfter`/`RunsBefore` 并去重依赖。它不拥有运行时系统，不执行 Tick。`CreateSystems(context)` 顺序调用 factory；重复调用会重新创建，factory 返回 null 不会被自动拒绝。中途抛异常时不补偿、不释放先前创建的系统，调用者要设计分阶段所有权或在工厂外记录已创建资源并清理。贡献及其依赖引用也不深冻结。

## 5. 就绪、校验与健康

`BattleRuntimeStatus.Meets` 只检查所需能力位齐备以及当前状态属于允许掩码。`MissingDependencies`/`Detail` 是说明文本，不参与判断；`Has(None)` 为 true。`BattleLoop` 要求 Input、Simulation、SnapshotOutput 以及 Ready/Running；`GameStart` 要求 GameStart 以及 Created/Ready。这是内置要求的数据定义，不会自动拒绝启动或驱动战斗。

`BattleValidationRegistry` 按 validator 的 Order 稳定执行，Name ordinal 唯一。所有 validator 写入同一 report；发现 BlocksStartup 不提前停止。validator 异常直接传播，后续 validator 不再执行。`IsValid` 仅表示 ErrorCount 为 0，与 BlocksStartup 独立：Warning 可配置阻止启动，Error 也可能不阻止。宿主应分别检查所需条件，而不是仅看其中一个 bool。

`BattleHealthReporter.Collect` 顺序采集，跳过 null provider，拒绝空/重复 Name，provider 异常传播，不是后台采样或异常隔离器。合并优先级为 Unhealthy、Unknown、Degraded、Healthy。空集合为 Healthy，只能解释为没有较差条目，不能作为实际服务已经采样健康的证据。Metrics 保存字典引用，长期保存需上层冻结或复制。

这些 registry/report 不接管 validator/provider 的生命周期，也不提供并发保证。宿主负责何时采样、如何报告失败，以及在部分创建后如何退出。

## 6. 常见问题与迁移

- 阶段列表顺序看似忽略 Order：先查依赖边；Order 只在已满足先决条件的节点之间比较。
- 计划已生成但战斗未运行：补充阶段执行器、完成提交及唯一模拟时钟；planner 只给计划。
- `IsValid=true` 仍不能启动：检查 BlocksStartup 和 runtime readiness，它们是不同条件。
- 创建系统失败后资源泄漏：factory 的部分成果仍归调用者，没有隐式事务或自动释放。
- 从旧传输设计迁移：改用当前命名空间与实际实现；旧 `game.battle.transport.runtime`、旧 StateSyncAdapter 关系不作为现行依赖。历史正文见[旧设计](../Document/BattleTransportDesign.md)，不据此新增实现分支。

帧同步和状态同步都可消费本包的组合能力；同步会话选择权威输入、时钟与恢复路线，共享玩法执行不按同步模式复制。状态、快照或报告对象的存在不等于完整恢复成功。

## 7. 验证范围与关联文档

本轮对照上述 Runtime/Battle 源码和 `BattleCompositionContractTests`，运行现有 .NET 测试工程 6/6 及本文完整示例 1/1 通过，实际命令与日志见仓库 D3 支撑批次记录。测试覆盖状态要求、贡献排序及非法依赖、阶段图、校验和健康聚合，不覆盖所有并发、重入或 factory 失败组合。

没有据此运行真实 transport、完整网络 gate、Unity Editor/Player、Mono/IL2CPP、隔离安装、性能、CI 或玩法双同步链恢复；证据为 E0 与局部 E3，不新增 E4/E5。

返回[文档索引](README.md)；环境见[包入口](../README.md)。
