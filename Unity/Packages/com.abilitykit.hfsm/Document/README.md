# HFSM — 分层有限状态机

## 设计理念

HFSM 回答的问题是："实体的状态是什么，以及如何在不同状态间切换？"

基于 UnityHFSM（Inspiaaa），HFSM 提供 StateMachine 容器和 State 基类。每个 State 有 `OnEnter`/`OnLogic`/`OnExit` 生命周期，Transition 支持条件谓词和时间延迟。ITriggerable<TEvent> 接口让状态可以订阅事件并触发转换，实现事件驱动的状态切换。核心原语统一收敛到 `AbilityKit.HFSM` 命名空间（子命名空间如 `AbilityKit.HFSM.Extension`、`AbilityKit.HFSM.Definition`、`AbilityKit.HFSM.Runtime` 承载扩展、数据驱动定义与确定性运行时）。

**与 Flow 的关系**：`HfsmFlowRunner` 将 HFSM 作为 Flow 节点嵌入，使状态机可以被 Flow 的组合器（Sequence、Parallel、Timeout）管理。

## 核心抽象

```
StateMachine<TOwner, TStateId, TEvent>
├── RequestStateChange(to)       → 请求状态切换
├── Trigger(event)              → 触发事件
└── OnLogic(dt)                → 每帧逻辑

StateBase
├── OnEnter()                  → 进入状态
├── OnLogic(dt)                → 状态逻辑
├── OnExit(request)            → 退出状态
└── needsExitTime               → 协调退出标志

TransitionBase
├── ShouldTransition()          → 条件判断
├── BeforeTransition()          → 转换前钩子
└── AfterTransition()          → 转换后钩子

ITriggerable<TEvent>           → 事件驱动转换
IActionable<TEvent>            → 自定义动作
```

## 内置转换类型

| 类型 | 说明 |
|------|------|
| `Transition` | 条件谓词转换 |
| `TransitionAfter` | 时间延迟转换 |
| `ReverseTransition` | 双向转换 |

## 内置 Action 类型

IAction 返回 `BehaviorStatus { Running, Success, Failure }`，支持行为树风格的逻辑组合：

| 类型 | 说明 |
|------|------|
| `SequenceAction` | 顺序执行，任一失败则整体失败 |
| `SelectorAction` | 选择执行，首个成功则整体成功 |
| `WaitAction` | 等待指定时间 |
| `RepeatAction` | 重复执行 |

## 快速示例

```csharp
// 创建状态机
var fsm = new StateMachine<string, string, object>(ownerId: "NPC_001");

// 添加状态
fsm.AddState("Idle", new State
{
    OnLogic = dt => Console.WriteLine("Idle..."),
    needsExitTime = false
});
fsm.AddState("Chase", new State
{
    OnEnter = () => Console.WriteLine("Start chasing"),
    OnLogic = dt => MoveToward(target)
});
fsm.AddState("Attack", new State
{
    OnEnter = () => Console.WriteLine("Attacking"),
    OnExit = request => Console.WriteLine("Stop attacking")
});

// 添加转换
fsm.AddTransition("Idle", "Chase", new Transition(t => distance < chaseRange));
fsm.AddTransition("Chase", "Attack", new Transition(t => distance < attackRange));
fsm.AddTransition("Attack", "Idle", new TransitionAfter(3.0f));  // 3s 后返回 Idle

// 事件驱动转换
fsm.AddTriggerableTransition("Hit", "Attack", "Hurt");
```

## Decorator 模式

`DecoratedState` 包装器为状态提供 AOP 钩子，用于日志、统计、横切逻辑：

```csharp
var loggedState = new DecoratedState(baseState,
    beforeEnter: () => Log($"Entering {stateName}"),
    afterEnter: () => Metrics.RecordStateEntry(stateName)
);
```
