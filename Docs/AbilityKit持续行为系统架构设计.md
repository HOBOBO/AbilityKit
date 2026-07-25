# AbilityKit 持续行为系统架构设计

## 1. 设计目标

### 1.1 核心问题
游戏中的"持续行为"（Continuous）是一个广泛存在的概念：
- Buff/DEBUFF
- 引导技能
- AI 行为（巡逻、追击）
- 移动/冲刺
- DOT（持续伤害）

这些对象都有共同特征：
- 有生命周期（激活、暂停、恢复、结束）
- 可能被中断
- 有时长限制

### 1.2 设计原则

| 原则 | 说明 |
|------|------|
| **最小化核心** | Core 包只定义抽象，不包含具体业务逻辑 |
| **接口组合优于继承** | 扩展点通过可选接口实现 |
| **职责分离** | 生命周期管理 vs 业务执行 分离 |
| **业务层定制** | 具体逻辑由业务层实现 |

---

## 2. 核心接口设计

### 2.1 IContinuous - 持续体外壳

```
Runtime/Core/Continuous/IContinuous.cs
```

统一所有具有"持续时间、可被中断"的对象的外壳。

```csharp
public interface IContinuous
{
    IContinuousConfig Config { get; }
    ContinuousState State { get; }
    bool IsActive { get; }
    bool IsTerminated { get; }
    bool IsPaused { get; }
    float ElapsedSeconds { get; }

    void Activate();
    void Pause();
    void Resume();
    void Abort(string reason);

    event Action<IContinuous, ContinuousEndReason> OnEnded;
}
```

### 2.2 IContinuousConfig - 配置接口

```
Runtime/Core/Continuous/IContinuousConfig.cs
```

采用**接口组合模式**，核心接口最小化，扩展点通过可选接口实现。

#### 核心接口（必须实现）

```csharp
public interface IContinuousConfig
{
    string Id { get; }           // 唯一标识
    long OwnerId { get; }        // 所属实体ID
    bool CanBeInterrupted { get; } // 是否可被中断
}
```

#### 扩展接口（按需实现）

| 接口 | 说明 | 使用场景 |
|------|------|----------|
| `ITagConfig` | 标签匹配、暂停/阻止规则 | BUFF 标签系统 |
| `IMutexConfig` | 互斥组管理 | 同一类型 BUFF 互斥 |
| `IDurationConfig` | 定时过期 | 有时长的 BUFF/技能 |
| `IHierarchyConfig` | 嵌套层级 | 父子 BUFF 级联 |
| `IStackConfig` | 堆叠层数 | 可叠加 BUFF |

#### 扩展接口示例

```csharp
// 互斥配置扩展
public interface IMutexConfig
{
    string MutexGroup { get; }   // 互斥组名称
    int Priority { get; }        // 优先级
}

// 时长配置扩展
public interface IDurationConfig
{
    float? DurationSeconds { get; }  // null 表示无限期
}

// 标签配置扩展
public interface ITagConfig
{
    ITagContainer Tags { get; }
    ITagContainer PauseByTags { get; }
    ITagContainer BlockByTags { get; }
}
```

### 2.3 IContinuousManager - 管理器接口

```
Runtime/Core/Continuous/IContinuousManager.cs
```

管理器接口由**业务层实现**，core 包不提供默认实现。

```csharp
public interface IContinuousManager
{
    bool Register(IContinuous continuous);
    void Unregister(IContinuous continuous, ContinuousEndReason reason);
    bool TryActivate(IContinuous continuous);

    IReadOnlyList<IContinuous> GetOwnerContinuous(long ownerId);
    IReadOnlyList<IContinuous> GetOwnerActiveContinuous(long ownerId);

    void InterruptAll(long ownerId, string reason);
    void PauseAll(long ownerId);
    void ResumeAll(long ownerId);

    int ActiveCount { get; }
    int TotalCount { get; }
}
```

---

## 3. 标签与修改器如何挂到持续行为

`IContinuous` 不应直接拥有一套固定的标签容器或属性计算器。它只提供可预测的生命周期节点；标签服务、Modifier 投影器和项目规则通过管理器或 lifecycle binder 接到这些节点上。这样 Buff、引导、运动过程和周期触发器都可以复用同一种接法，又不会被迫依赖某个战斗状态模型。

| 协作能力 | 更合适的职责归属 | 与持续行为的连接时机 | 不应由持续体自己决定的事 |
|------|------|------|------|
| 有效标签聚合 | Owner 的标签查询服务 | 激活前校验；标签变化后重算活动过程 | 标签来自基础状态、装备还是其他过程，以及标签匹配规则 |
| 暂停、恢复或中断 | 项目标签策略 | 满足或失去持续条件时 | 某个控制标签是暂停引导还是直接打断 |
| 属性/参数 Modifier | 来源感知的 Modifier projector | Activate/Resume 投影；Pause/End 撤销 | 多来源数值如何取最大、相加、覆盖或按优先级计算 |
| 标签/Modifier 清理 | 生命周期 binder 或 Owner 资源索引 | End/Unregister | 移除某个来源后其他来源是否仍继续生效 |

这层分工有两个约束。第一，标签变化不应只靠每个持续体在 Tick 中自行轮询；标签服务应能重算指定 Owner 下受影响的活动过程，再决定暂停、恢复或结束。第二，Modifier 必须带来源标识。一个过程结束时只能撤销自己投影的那部分值，不能误删装备、光环或其他状态的结果。

因此，持续行为提供的是“什么时候可以接入或撤销”的稳定时机，而不是替项目规定“接入什么规则”。项目可以为实时 MOBA 接上标签门禁、控制中断和属性投影；回合制项目也可以只在回合切换时检查状态，完全不需要每帧标签重算或复杂 Modifier 聚合。

---

## 4. 状态机设计

### 4.1 ContinuousState

```
Runtime/Core/Continuous/ContinuousState.cs
```

```
  ┌─────────────┐
  │ Inactive    │ ← 创建后初始状态
  └──────┬──────┘
         │ Activate()
         ▼
  ┌─────────────┐
  │ Active      │ ← 正常运行
  └──────┬──────┘
         │
    ┌────┴────┬─────────────┐
    │         │             │
    │ Pause() │ Expire()    │ Abort()
    │         │             │
    ▼         ▼             ▼
┌────────┐ ┌────────┐ ┌────────┐
│Paused  │ │ Expired │ │Aborted │
└────────┘ └────────┘ └────────┘
    │                        │
    │ Resume()               │
    ▼                        ▼
┌────────┐              ┌────────┐
│ Active │              │Aborted │ (终态)
└────────┘              └────────┘
```

### 4.2 ContinuousEndReason

```
Runtime/Core/Continuous/ContinuousEndReason.cs
```

| 枚举值 | 说明 |
|--------|------|
| `Completed` | 正常完成（到期） |
| `Interrupted` | 被中断（Abort） |
| `Replaced` | 被替换（互斥） |
| `OwnerDead` | 所属实体死亡 |
| `CleanedUp` | 被清理 |

---

## 5. 包定位差异

### 5.1 Behavior 包 vs Triggering 包

虽然两个包都实现了 `IContinuous`，但定位不同：

| 维度 | Behavior 包 | Triggering 包 |
|------|------------|---------------|
| **核心问题** | "我应该做什么？" | "我应该如何执行？" |
| **设计模式** | Decision + Executor | TriggerPlan + Executor |
| **典型场景** | 巡逻、追击、逃跑、闪避 | 技能释放、BUFF叠加、DOT |
| **配置方式** | 代码定义 BehaviorTree | 数据配置（JSON） |
| **决策频率** | 每帧/每 N 帧决策 | 事件触发 |
| **执行模型** | Decision → Executor（主动轮询） | TriggerPlan 解析（事件驱动） |

### 5.2 为什么保持独立？

1. **问题域不同**：AI 决策 vs 技能执行
2. **开发团队可能不同**：AI 开发者 vs 技能策划
3. **可独立演进**：各自优化不影响对方
4. **灵活性**：不同项目可能只需要其中一个

### 5.3 统一外壳 IContinuous

`IContinuous` 作为**统一外壳**，让两种不同领域的系统可以被同一个 `IContinuousManager` 管理：

```
IContinuousManager（业务层实现）
├── 管理 BehaviorRuntime（AI 行为）
│   └── 内部：Decision.Decide() → Executor.Execute()
│
└── 管理 ProcessUnit（技能/BUFF）
    └── 内部：TriggerPlan 解析 → ContinuousExecutor 执行
```

---

## 6. 业务层接入指南

### 6.1 步骤一：实现 IContinuousManager

业务层根据游戏需求实现管理器：

```csharp
public class MobaContinuousManager : IContinuousManager
{
    private readonly Dictionary<long, List<IContinuous>> _ownerContinuous = new();

    public bool Register(IContinuous continuous)
    {
        // 实现注册逻辑
        // 可以包含互斥检查、标签检查等
    }

    public void Unregister(IContinuous continuous, ContinuousEndReason reason)
    {
        // 实现注销逻辑
    }

    // ... 其他方法
}
```

### 6.2 步骤二：定义业务配置

```csharp
public class BuffConfig : IContinuousConfig,
    ITagConfig, IMutexConfig, IDurationConfig
{
    public string Id { get; set; }
    public long OwnerId { get; set; }
    public bool CanBeInterrupted { get; set; }

    // ITagConfig
    public HashSet<string> Tags { get; set; }
    public HashSet<string> PauseByTags { get; set; }
    public HashSet<string> BlockByTags { get; set; }

    // IMutexConfig
    public string MutexGroup { get; set; }
    public int Priority { get; set; }

    // IDurationConfig
    public float? DurationSeconds { get; set; }
}
```

### 6.3 步骤三：使用

```csharp
var manager = new MobaContinuousManager();
var config = new BuffConfig
{
    Id = "speed_boost_001",
    OwnerId = playerId,
    MutexGroup = "movement_buff",
    Priority = 1,
    DurationSeconds = 5f,
    Tags = new HashSet<string> { "buff", "speed" }
};

var behavior = new MyBehavior(config);
if (manager.TryActivate(behavior))
{
    // 成功激活
}
```

---

## 7. 文件结构

```
Unity/Packages/com.abilitykit.core/
└── Runtime/Core/Continuous/
    ├── ContinuousState.cs        # 状态枚举
    ├── ContinuousEndReason.cs   # 结束原因枚举
    ├── IContinuous.cs           # 持续体接口（核心）
    ├── IContinuousConfig.cs     # 配置接口 + 扩展接口
    └── IContinuousManager.cs    # 管理器接口

Unity/Packages/com.abilitykit.behavior/
└── Runtime/Runtime/
    └── BehaviorRuntime.cs       # 实现 IContinuous

Unity/Packages/com.abilitykit.triggering/
└── Runtime/Continuous/
    ├── ProcessUnit.cs           # 继承 IContinuous
    └── ContinuousExecutorAdapter.cs  # 适配器
```

---

## 8. 设计决策记录

| 日期 | 决策 | 原因 |
|------|------|------|
| 2026-05-13 | 配置采用接口组合模式 | 避免 core 包写死业务逻辑 |
| 2026-05-13 | ContinuousManager 定义为接口 | 业务层需要自定义互斥、标签等逻辑 |
| 2026-05-13 | Behavior 和 Triggering 保持独立 | 定位不同，可独立演进 |
