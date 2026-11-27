# AbilityKit Timer (com.abilitykit.timer)

通用定时器和调度框架。运行时不引用 Unity 引擎，直接依赖 `com.abilitykit.core` 与 `com.abilitykit.deterministic`。

## 核心职责

只做一件事：**管理时间流逝和任务调度**。

但不仅限于简单计时——通过 `IScheduler` 接口，可以支持复杂场景：
- 延时执行
- 周期任务
- 持续任务
- 外部控制取消

## 设计原则

- **接口驱动**：`ITimer` 接口让使用方自行注入实现
- **复用队列**：任务是引用类型，创建任务和队列扩容会分配；推进时不为任务列表复制快照
- **框架层纯净**：不依赖任何 Unity 或平台特定 API
- **结构清晰**：按职责拆分为多个文件

## 模块结构

```
com.abilitykit.timer/
├── package.json
├── README.md
└── Runtime/
    └── Core/
        ├── Interfaces/           # 接口定义
        │   ├── ITimer.cs
        │   ├── IScheduler.cs
        │   ├── IScheduledTask.cs
        │   ├── ISimpleTask.cs
        │   └── TaskState.cs
        ├── Timer/                # 计时器实现
        │   └── SystemTimer.cs
        ├── Tasks/               # 任务实现
        │   ├── ScheduledTaskBase.cs
        │   ├── DelayTask.cs
        │   ├── PeriodicTask.cs
        │   └── ContinuousTask.cs
        └── Scheduler/            # 调度器实现
            ├── TaskList.cs
            └── DefaultScheduler.cs
```

## 核心类型

### 接口层 (`AbilityKit.Timer`)

| 类型 | 说明 |
|------|------|
| `ITimer` | 基础计时器接口，由使用方实现 |
| `IScheduler` | 调度器接口 |
| `IScheduledTask` | 调度任务接口 |
| `ISimpleTask` | 简单任务接口 |
| `TaskState` | 任务状态枚举 |

### 实现层

| 类型 | 说明 |
|------|------|
| `SystemTimer` | Stopwatch 实现的高精度计时器 |
| `TimerUtility` | 计时器辅助方法 |
| `DelayTask` | 延时任务 |
| `PeriodicTask` | 周期任务 |
| `ContinuousTask` | 持续任务 |
| `DefaultScheduler` | 默认调度器实现 |

## 使用示例

### 基础用法

```csharp
var scheduler = new DefaultScheduler();

// 延时 2 秒执行
scheduler.ScheduleDelay(() => Console.WriteLine("2秒后"), 2f);

// 每秒执行一次，持续 10 秒
scheduler.SchedulePeriodic(() => Console.WriteLine("Tick"), 1f, 10f);

// 每秒执行一次，最多 5 次
scheduler.SchedulePeriodic(() => Console.WriteLine("Tick"), 1f, maxExecutions: 5);

// 持续执行，外部控制结束
var task = scheduler.ScheduleContinuous(
    onTick: (dt) => DoWork(dt),
    onComplete: () => Console.WriteLine("完成"),
    durationSeconds: 10f  // 可选超时
);

// 随时取消
task.RequestCancel("用户取消");

// 更新（使用自己的 deltaTime）
scheduler.Tick(deltaTime);
```

### 自定义计时器

```csharp
// 实现 ITimer 接口
public struct UnityTimer : ITimer
{
    private float _startTime;

    public float Elapsed => UnityEngine.Time.time - _startTime;

    public void Reset()
    {
        _startTime = UnityEngine.Time.time;
    }
}
```

### 任务状态查询

```csharp
var task = scheduler.ScheduleDelay(() => DoSomething(), 5f);

// 查询状态
if (task.State == TaskState.Completed)
{
    Console.WriteLine("任务已完成");
}

if (task.IsCanceled)
{
    Console.WriteLine($"任务被取消: {task.CancelReason}");
}
```

## 调度与任务生命周期

- `DefaultScheduler` 由单个调用方串行推进，回调在 `Tick()` 的调用线程同步执行。回调可添加、取消或完成任务；新任务从下次 `Tick()` 开始推进。回调不能递归调用同一调度器的 `Tick()`，也不应自行推进已交给调度器的任务。任务队列不保证按到期时刻或注册顺序跨任务派发。
- 取消只做标记，任务在后续被访问时移除。周期任务在回调中取消或显式完成后，不再补触发本次剩余的到期回调；持续任务只在自然到期时执行 `onComplete`。
- 若任务推进或回调抛错，`Tick()` 取消并移除失败任务（保留已有取消原因），原样抛出异常。不回退已推进任务或回调副作用，也不替未访问的任务补记这次时间；它们留待下次推进。
- 周期间隔必须能转换为大于零的定点秒数。定点累计有助于稳定时限比较，但调度器不提供战斗帧时钟、快照或回滚能力。

## 与其他模块的关系

| 模块 | 关系 |
|------|------|
| `com.abilitykit.triggering` | Timer 为 Triggering 提供基础时间能力，使用方注入实现 |
| `com.abilitykit.flow` | Timer 用于 FlowNode 的超时检测 |
| `com.abilitykit.hfsm` | Timer 用于状态转换超时，自行实现 ITimer |
| `com.abilitykit.modifiers` | MagnitudeSource.TimeDecay 依赖时间提供 |

## 命名空间

所有类型都在 `AbilityKit.Timer` 命名空间下。

## 版本历史

| 版本 | 日期 | 说明 |
|------|------|------|
| 1.0 | 2026-04 | 初版，接口驱动，使用方注入实现 |
