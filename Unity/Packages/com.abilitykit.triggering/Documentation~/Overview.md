# Triggering — 事件触发规则引擎

## 一、定位

Triggering 回答的问题是：**当事件发生时，哪些游戏规则应被评估，满足什么条件，以及按什么顺序执行哪些动作？** 延迟与周期是可选的规则能力，不是触发或即时执行的前提。

它是 AbilityKit 的事件触发与规则执行包，正式主线围绕 `TriggerRunner<TCtx>`、`TriggerPlan<TArgs>`、`PlannedTrigger<TArgs, TCtx>`、`ExecCtx<TCtx>`、`ActionRegistry`、`FunctionRegistry` 和验证体系展开。包内不假设 Buff、子弹、AOE 等业务概念；这些概念应作为项目层动作、条件、上下文或调度驱动接入。

Triggering 与 Pipeline 的关系：Pipeline 负责流程编排，Triggering 负责流程中事件响应与规则执行。两者互补，不互相替代。

## 二、正式主线

1. **只用触发**：只需类型化通知时用 `EventBus`；需要条件和稳定顺序时，用 `TriggerRunner` 注册手写 `ITrigger`。无需 JSON、时钟或调度推进。
2. **即时数据规则**：用 `TriggerPlan`、注册表及验证表达「事件 → 条件 → 动作」。配置可由代码构造，不强制加载文件；`Cooldown` 等带时间的触发资格另需宿主提供时间语义，但不是未来任务。
3. **时间扩展**：只有声明延迟、周期或持续任务时，才选用对应调度入口。宿主决定何时推进和清理任务。

```text
TriggerRunner<TCtx>
├── Register / RegisterPlan       → 注册手写触发器或数据化计划
├── Dispatch / EventBus.Publish   → 发布事件并触发规则
└── Phase → Priority → Order      → 稳定排序与执行顺序

TriggerPlan<TArgs>
├── PredicateExprPlan             → 数据化条件表达式
└── ActionCallPlan[]              → 数据化动作调用

ExecCtx<TCtx>
├── Context                       → 项目上下文
├── FunctionRegistry              → 条件函数扩展点
├── ActionRegistry                → 动作扩展点
├── BlackboardResolver            → 黑板数据解析
├── NumericDomains                → 数值域解析
└── ExecutionControl              → StopPropagation / Cancel
```

## 三、扩展方式

- 条件扩展：通过 `FunctionRegistry` 注册确定性或非确定性函数，再由 `PredicateExprPlan` 引用。
- 动作扩展：通过 `ActionRegistry` 注册强类型动作，再由 `ActionCallPlan` 引用。
- 上下文扩展：通过自定义 `TCtx`、`ITriggerContextSource<TCtx>` 或 `ITriggerDispatcherContext` 提供项目服务。
- 调度扩展：按任务语义选择下表的专项入口，不能互相替代。
- 校验扩展：通过 `ITriggerValidator<TCtx>` 或独立验证器在运行前发现配置错误。

## 四、调度边界

即时接入不创建动作管理器或调用 `Tick` / `Update`。使用非即时扁平动作时，宿主显式注入并推进自己持有的管理器。缺少管理器会明确失败，不会转成即时执行；见[正式入口与错误边界](FormalApiBoundary.md#专项宿主入口)。

| 层级 | 定位 | 推荐状态 |
| --- | --- | --- |
| `Runtime.ActionScheduler` | 扁平 `TriggerPlan` 的延迟、周期、连续动作 | 可选专项 |
| `ITriggerExecutionScheduler` | 执行树的时间节点向宿主登记子树，可复用包内 `TriggerExecutionScheduler` | 可选专项 |
| `Runtime.RuleScheduler` | 独立的业务时间规则，例如“延后/每隔/保持期间” | 可选专项 |
| `Runtime.Schedule` | 早期通用调度与适配层 | 兼容层，不新增依赖 |
| `Runtime.Scheduler` | 已删除的旧入口 | 仅作历史定位，不是现存调度层 |

`RuleScheduler` 的 `WhileActive` 表示“调度条目未完成、未取消、未中断时持续按间隔执行”，不代表 Core Continuous 的 `ContinuousState.Active`，也不接管 Buff 生命周期。Buff 等持续行为仍应由 Core `IContinuousManager` 管理，Triggering 只负责规则触发与调度驱动适配。

## 五、最小接入示例

从 Package Manager 导入 **Triggering Mainline**，运行 `FormalTriggeringMainlineExample.RunOnce()`。它只验证即时规则，不加载 JSON、不装配调度、不推进时间。独立业务定时规则另见 **Triggering Rule Scheduling**，排队事件的 `Flush` 另见 **Triggering Queued Events**（事件刷新不是时间调度）。详见[样例盘点](SampleInventory.md)。

## 六、稳定性约束

- 数据化计划进入运行前应执行验证，至少覆盖引用、动作参数、调度语义和 UGC 限制。
- `ExecPolicy.RequireDeterministic` 下，计划不得依赖随机数、系统时间、网络请求等非确定性能力。
- Runtime 包不应新增 Buff、Projectile、AOE 等项目词汇；这些应下沉到 Demo、Samples 或业务包。
- 遗留目录保留是为了兼容现有 GUID 和旧项目引用，不代表正式主线入口。
