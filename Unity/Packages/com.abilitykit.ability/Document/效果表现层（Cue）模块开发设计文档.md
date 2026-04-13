# Ability-Kit 效果表现层（Cue）模块开发设计文档

> **阅读对象**：希望了解 Cue 系统如何设计的开发者
>
> **文档目标**：让你理解"什么是 Cue"、"Cue 如何与效果生命周期关联"、"如何实现自定义 Cue"

---

## 一、设计理念：为什么需要 Cue 系统？

### 1.1 逻辑与表现分离的必要性

在游戏开发中，效果（Effect）通常包含两方面的内容：

1. **逻辑层**：属性修改、状态变化、伤害计算等核心游戏机制
2. **表现层**：视觉特效、音效、粒子、UI 反馈等与玩家感知相关的内容

```
❌ 传统做法的问题：

┌─────────────────────────────────────────────────────────────────┐
│  方式一：逻辑和表现耦合在同一个效果中                           │
│                                                                 │
│  public class BuffEffect {                                      │
│      public void Apply() {                                     │
│          // 逻辑：增加攻击力                                    │
│          attribute.Modify(Attribute.Atk, +10);                 │
│                                                                 │
│          // 表现：播放特效                                      │
│          PlayVFX("Buff_Attack_Up.vfx");                        │
│          PlaySFX("Buff_Apply.wav");                             │
│      }                                                          │
│  }                                                              │
│                                                                 │
│  问题：逻辑和表现强耦合，测试困难                                │
│  问题：在帧同步游戏中，表现层代码难以独立调试                    │
│  问题：复用性差，换一套表现需要修改逻辑代码                      │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│  方式二：通过事件回调通知外部                                    │
│                                                                 │
│  public class BuffEffect {                                     │
│      public event Action OnApply;                              │
│      public void Apply() {                                     │
│          attribute.Modify(Attribute.Atk, +10);                 │
│          OnApply?.Invoke();                                    │
│      }                                                          │
│  }                                                              │
│                                                                 │
│  问题：需要为每种效果定义事件，接口分散                          │
│  问题：生命周期管理复杂（何时创建/销毁表现）                     │
│  问题：难以保证逻辑和表现的同步性                                │
└─────────────────────────────────────────────────────────────────┘
```

### 1.2 Cue 系统的设计思路

```
✅ Cue 系统的核心思想：

┌─────────────────────────────────────────────────────────────────┐
│                                                                 │
│   逻辑层（Effect）           表现层（Cue）                        │
│   ┌─────────────┐           ┌─────────────┐                   │
│   │  Gameplay   │──────────►│  IGameplay  │                   │
│   │   Effect    │  生命周期   │  EffectCue  │                   │
│   │   Spec      │  回调通知   │             │                   │
│   └─────────────┘           └─────────────┘                   │
│           │                         │                          │
│           │                         │                          │
│           ▼                         ▼                          │
│   ┌─────────────┐           ┌─────────────┐                   │
│   │  Attribute  │           │  VFX/SFX/UI │                   │
│   │  Modification│           │             │                   │
│   └─────────────┘           └─────────────┘                   │
│                                                                 │
│   核心原则：                                                     │
│   - Cue 不知道任何逻辑细节                                      │
│   - Effect 不知道表现如何实现                                   │
│   - 两者仅通过生命周期回调接口通信                              │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 1.3 参考 Unreal GAS 的 Cue 设计

Unreal Engine 的 Gameplay Ability System (GAS) 中，GameplayEffect 配有 GameplayCue 机制：

| GAS 概念 | Ability-Kit 对应 |
|----------|-----------------|
| `UGameplayEffect` | `GameplayEffectSpec` |
| `FGameplayCueParameters` | `EffectExecutionContext` + `EffectInstance` |
| `GameplayCue_Notify` | `IGameplayEffectCue` |
| `OnActive` | `OnActive` |
| `WhileActive` | `WhileActive` |
| `Removed` | `OnRemove` |

---

## 二、核心接口设计

### 2.1 IGameplayEffectCue 接口

```csharp
namespace AbilityKit.Ability.Share.Effect
{
    /// <summary>
    /// 游戏效果表现层接口
    /// 用于在效果生命周期中触发视觉、音效等表现
    /// </summary>
    public interface IGameplayEffectCue
    {
        /// <summary>
        /// 效果激活时调用
        /// 对于即时效果：立即调用然后 OnRemove
        /// 对于持续效果：在效果开始时调用一次
        /// </summary>
        /// <param name="context">执行上下文，包含来源、目标、服务等信息</param>
        /// <param name="instance">效果实例，包含运行时状态</param>
        void OnActive(in EffectExecutionContext context, EffectInstance instance);

        /// <summary>
        /// 效果激活期间每帧调用
        /// 适用于需要持续表现的效果（如持续施法特效）
        /// </summary>
        /// <param name="context">执行上下文</param>
        /// <param name="instance">效果实例</param>
        void WhileActive(in EffectExecutionContext context, EffectInstance instance);

        /// <summary>
        /// 效果移除时调用
        /// 用于清理表现资源（如停止粒子、隐藏图标）
        /// </summary>
        /// <param name="context">执行上下文</param>
        /// <param name="instance">效果实例</param>
        void OnRemove(in EffectExecutionContext context, EffectInstance instance);
    }
}
```

### 2.2 Cue 生命周期流程

```
┌─────────────────────────────────────────────────────────────────┐
│                    Cue 生命周期流程图                            │
└─────────────────────────────────────────────────────────────────┘

                          ┌─────────────┐
                          │  效果应用   │
                          │  Apply()    │
                          └──────┬──────┘
                                 │
                                 ▼
                    ┌────────────────────────┐
                    │  1. 检查应用条件        │
                    │  2. 创建 EffectInstance │
                    │  3. 执行 EffectComponent.OnApply │
                    │  4. 调用 Cue.OnActive() │◄────── 表现层开始
                    │  5. 添加到活跃列表       │
                    └───────────┬────────────┘
                                │
            ┌───────────────────┼───────────────────┐
            │                   │                   │
            ▼                   ▼                   ▼
    ┌───────────────┐   ┌───────────────┐   ┌───────────────┐
    │  即时效果      │   │  持续效果      │   │  无限效果      │
    │  Instant      │   │  Duration     │   │  Infinite     │
    └───────┬───────┘   └───────┬───────┘   └───────┬───────┘
            │                   │                   │
            │                   ▼                   │
            │       ┌───────────────────┐           │
            │       │   Step() 每帧调用  │           │
            │       │                   │           │
            │       │  ┌───────────────┐ │           │
            │       │  │执行周期回调    │ │           │
            │       │  │Component.OnTick│ │           │
            │       │  └───────────────┘ │           │
            │       │                   │           │
            │       │  ┌───────────────┐ │           │
            │       │  │ Cue.WhileActive│ │◄─────────┤
            │       │  │ 每帧调用       │ │
            │       │  └───────────────┘ │           │
            │       └─────────┬─────────┘           │
            │                 │                     │
            │                 ▼                     │
            │       ┌───────────────────┐           │
            │       │  持续时间耗尽？    │           │
            │       └─────────┬─────────┘           │
            │                 │                     │
            └────────┬────────┴─────────────────────┘
                     │
                     ▼
         ┌─────────────────────────┐
         │  1. 调用 Cue.OnRemove()  │◄────── 表现层结束
         │  2. 执行Component.OnRemove│
         │  3. 清理 GrantedTags      │
         │  4. 关闭 EffectSource     │
         └─────────────────────────┘
```

### 2.3 空 Cue 实现

```csharp
namespace AbilityKit.Ability.Share.Effect
{
    /// <summary>
    /// 空 Cue 实现，用于不需要表现层的场景
    /// </summary>
    public sealed class NullGameplayEffectCue : IGameplayEffectCue
    {
        public static readonly NullGameplayEffectCue Instance = new NullGameplayEffectCue();

        private NullGameplayEffectCue() { }

        public void OnActive(in EffectExecutionContext context, EffectInstance instance) { }
        public void WhileActive(in EffectExecutionContext context, EffectInstance instance) { }
        public void OnRemove(in EffectExecutionContext context, EffectInstance instance) { }
    }
}
```

---

## 三、核心数据结构

### 3.1 GameplayEffectSpec - 效果规格

```csharp
public sealed class GameplayEffectSpec
{
    public GameplayEffectSpec(
        EffectDurationPolicy durationPolicy,      // 持续策略
        float durationSeconds,                     // 持续时间
        float periodSeconds,                       // 周期时间
        GameplayTagRequirements applicationRequirements, // 应用条件
        GameplayTagContainer grantedTags,          // 授予的标签
        IReadOnlyList<IEffectComponent> components, // 逻辑组件
        bool executePeriodicOnApply = false,
        IGameplayEffectCue cue = null)             // ★ Cue 表现层
    {
        // ... 赋值逻辑
        Cue = cue;  // ★ 可选，默认为 null
    }

    public IGameplayEffectCue Cue { get; }
    // ... 其他属性
}
```

### 3.2 EffectInstance - 效果实例

```csharp
public sealed class EffectInstance
{
    internal EffectInstance(int id, GameplayEffectSpec spec)
    {
        Id = id;                          // 唯一实例ID
        Spec = spec;                      // 关联的规格
        ElapsedSeconds = 0f;              // 已流逝时间
        RemainingSeconds = spec.DurationSeconds; // 剩余时间
        NextTickInSeconds = spec.PeriodSeconds;  // 下次Tick
        StackCount = 1;                   // 叠加层数
        State = new Dictionary<object, object>(); // 运行时状态
    }

    public int Id { get; }                // 实例唯一标识
    public GameplayEffectSpec Spec { get; } // 效果规格（包含Cue）

    public float ElapsedSeconds { get; internal set; }      // 已流逝秒数
    public float RemainingSeconds { get; internal set; }    // 剩余秒数
    public float NextTickInSeconds { get; internal set; }    // 距下次Tick

    public int StackCount { get; internal set; }             // 叠加层数

    public Dictionary<object, object> State { get; }         // ★ Cue 可用的状态存储
}
```

### 3.3 EffectExecutionContext - 执行上下文

```csharp
public readonly struct EffectExecutionContext
{
    public EffectExecutionContext(
        IServiceProvider services,       // 服务容器
        IFrameTime time,                 // 帧时间
        object source,                   // 来源对象
        object target,                   // 目标对象
        IUnitFacade targetUnit,          // 目标单位门面
        IEventBus eventBus,              // 事件总线
        long sourceContextId = 0)        // 源上下文ID
    {
        // ... 赋值
    }

    public IServiceProvider Services { get; }    // ★ Cue 可获取服务
    public IFrameTime Time { get; }              // ★ Cue 可获取帧时间
    public long SourceContextId { get; }          // ★ Cue 可获取上下文链

    public object Source { get; }                // 来源
    public object Target { get; }                 // 目标
    public IUnitFacade TargetUnit { get; }        // 目标单位

    public GameplayTagContainer TargetTags { get; }      // 目标标签
    public AttributeContext TargetAttributes { get; }    // 目标属性
    public IEventBus EventBus { get; }           // 事件发布
}
```

---

## 四、Cue 调用时机

### 4.1 EffectContainer - Cue 调用管理

`EffectContainer` 是 Cue 生命周期的管理者，在以下时机调用 Cue：

```csharp
public sealed class EffectContainer
{
    public EffectInstance Apply(GameplayEffectSpec spec, in EffectExecutionContext context)
    {
        // ... 条件检查、实例创建 ...

        // 【时机1】效果激活时
        (spec.Cue ?? NullGameplayEffectCue.Instance).OnActive(in context, inst);

        _active.Add(inst);

        if (spec.DurationPolicy == EffectDurationPolicy.Instant)
        {
            Remove(inst.Id, in context);  // 即时效果立即移除
            return inst;
        }

        // ...
        return inst;
    }

    public void Step(in EffectExecutionContext context)
    {
        var dt = context.Time.DeltaTime;

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var inst = _active[i];
            var spec = inst.Spec;

            inst.ElapsedSeconds += dt;

            // 【时机2】每帧调用（持续效果期间）
            (spec.Cue ?? NullGameplayEffectCue.Instance).WhileActive(in context, inst);

            // 周期Tick处理
            if (spec.PeriodSeconds > 0f)
            {
                inst.NextTickInSeconds -= dt;
                while (inst.NextTickInSeconds <= 0f)
                {
                    TickInstance(inst, in context);
                    inst.NextTickInSeconds += spec.PeriodSeconds;
                }
            }

            // 持续时间耗尽
            if (spec.DurationPolicy == EffectDurationPolicy.Duration
                && inst.RemainingSeconds <= 0f)
            {
                RemoveAt(i, in context);
            }
        }
    }

    private void RemoveAt(int index, in EffectExecutionContext context)
    {
        var inst = _active[index];
        _active.RemoveAt(index);

        // 【时机3】效果移除时
        (inst.Spec.Cue ?? NullGameplayEffectCue.Instance).OnRemove(in context, inst);

        // 清理 EffectComponent
        var components = inst.Spec.Components;
        for (int i = 0; i < components.Count; i++)
        {
            components[i]?.OnRemove(in context, inst);
        }

        // ... 清理标签、关闭源上下文 ...
    }
}
```

### 4.2 调用时序图

```
┌─────────────────────────────────────────────────────────────────┐
│              即时效果（Instant）调用时序                         │
└─────────────────────────────────────────────────────────────────┘

时间线:  Apply()      OnActive()   OnRemove()
        ──────┬─────────────┬─────────────►

EffectContainer:
        │     │             │
        │     │             │
        ├─────┤             │
        │ Apply()           │
        │   - 创建实例      │
        │   - OnApply回调   │
        │             ┌─────┤
        │             │ OnActive() ◄─── Cue 开始表现
        │             │
        │             │ Remove() ◄──── 立即移除
        │             │   - OnRemove() ◄─── Cue 结束表现
        │             │
        └─────────────┴───────────────── 生命周期结束


┌─────────────────────────────────────────────────────────────────┐
│              持续效果（Duration）调用时序                        │
└─────────────────────────────────────────────────────────────────┘

时间线:  Apply()      OnActive()   WhileActive()...   时间到   OnRemove()
        ──────┬─────────────┬─────────────┬─────┬─────────────┬──────────►
        │     │             │             │     │             │
        │     │             │             │     │             │
        ├─────┤             │             │     │             │
        │ Apply()           │             │     │             │
        │   - 创建实例      │             │     │             │
        │   - 设置剩余时间  │             │     │             │
        │             ┌─────┤             │     │             │
        │             │ OnActive() ◄─────┼─────┤             │
        │             │   表现开始       │     │             │
        │             │             ┌───┴─────┤             │
        │             │             │ WhileActive() ◄────────┼────── 每帧
        │             │             │   持续表现              │       调用
        │             │             │                         │
        │             │             │             ┌───────────┤
        │             │             │             │ 时间耗尽   │
        │             │             │             │           │
        │             │             │             │  Remove()  │
        │             │             │             │  - OnRemove() ◄── Cue
        │             │             │             │    清理表现   结束
        └─────────────┴─────────────┴─────────────┴───────────┴────────
```

---

## 五、Cue 与逻辑层的关系

### 5.1 职责边界

```
┌─────────────────────────────────────────────────────────────────┐
│                      Cue 与逻辑层的边界                          │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────┬─────────────────────────────────┐
│           逻辑层（Effect）       │         表现层（Cue）           │
├─────────────────────────────────┼─────────────────────────────────┤
│                                 │                                 │
│  ● 属性修改                     │  ● 视觉特效（VFX）              │
│  ● 状态授予                     │  ● 音效（SFX）                  │
│  ● 伤害计算                     │  ● 粒子效果                     │
│  ● 周期触发                     │  ● UI 状态图标                  │
│  ● 条件检查                     │  ● 屏幕震动                     │
│  ● 标签管理                     │  ● 动作通知                     │
│                                 │                                 │
│  【不允许】                      │  【不允许】                      │
│  ● 播放任何音效                 │  ● 修改属性值                   │
│  ● 创建任何 VFX                │  ● 改变游戏逻辑                 │
│  ● 引用任何 Unity 对象         │  ● 访问数据库                   │
│                                 │  ● 执行帧同步相关逻辑           │
│                                 │                                 │
└─────────────────────────────────┴─────────────────────────────────┘
```

### 5.2 数据流向

```
┌─────────────────────────────────────────────────────────────────┐
│                      Cue 数据获取途径                            │
└─────────────────────────────────────────────────────────────────┘

                    EffectExecutionContext
                           │
           ┌───────────────┼───────────────┐
           │               │               │
           ▼               ▼               ▼
    ┌──────────┐    ┌──────────┐    ┌──────────┐
    │ Services │    │   Time   │    │   Tags   │
    │          │    │          │    │          │
    │ 可获取：  │    │ 可获取：  │    │ 可获取：  │
    │ - VFX服务 │    │ - DeltaTime│  │ - 目标标签│
    │ - SFX服务 │    │ - Frame  │    │ - 来源标签│
    │ - UI服务  │    │          │    │          │
    └──────────┘    └──────────┘    └──────────┘
           │               │               │
           └───────────────┼───────────────┘
                           │
                           ▼
                    EffectInstance
                           │
           ┌───────────────┼───────────────┐
           │               │               │
           ▼               ▼               ▼
    ┌──────────┐    ┌──────────┐    ┌──────────┐
    │    Id    │    │  Spec    │    │  State   │
    │          │    │          │    │          │
    │ 唯一标识  │    │ 效果规格  │    │ 运行时   │
    │ 用于关联  │    │ Cue配置  │    │ 状态存储 │
    │ 表现实例  │    │          │    │          │
    └──────────┘    └──────────┘    └──────────┘
                           │
           ┌───────────────┼───────────────┐
           │               │               │
           ▼               ▼               ▼
    ┌──────────┐    ┌──────────┐    ┌──────────┐
    │Duration  │    │ Elapsed  │    │ Stack    │
    │Policy     │    │Seconds   │    │Count     │
    │          │    │          │    │          │
    │ 即时/持续 │    │ 已流逝   │    │ 叠加层数 │
    │/无限     │    │ 时间     │    │          │
    └──────────┘    └──────────┘    └──────────┘
```

### 5.3 Cue 的状态存储

Cue 可以通过 `EffectInstance.State` 字典存储运行时状态，实现跨生命周期通信：

```csharp
// Cue 示例：存储表现层需要的信息
public class DamageCue : IGameplayEffectCue
{
    public void OnActive(in EffectExecutionContext context, EffectInstance instance)
    {
        // 存储伤害值，供后续使用
        instance.SetState("DamageValue", 100f);
        instance.SetState("ImpactPoint", context.Target);

        // 通过服务播放特效
        var vfxService = context.Services.GetService<IVFXService>();
        vfxService.Play("Hit_Effect", context.Target);
    }

    public void WhileActive(in EffectExecutionContext context, EffectInstance instance)
    {
        // 持续效果期间可以获取之前的存储值
        if (instance.TryGetState<float>("DamageValue", out var damage))
        {
            // 每帧更新表现
        }
    }

    public void OnRemove(in EffectExecutionContext context, EffectInstance instance)
    {
        // 清理表现
        var vfxService = context.Services.GetService<IVFXService>();
        vfxService.Stop("Hit_Effect", context.Target);
    }
}
```

---

## 六、EffectSource 上下文关联

### 6.1 为什么需要 EffectSource

在帧同步游戏中，Effect 需要与 EffectSource 关联，以支持回放和录制功能：

```
┌─────────────────────────────────────────────────────────────────┐
│                  Effect 与 EffectSource 的关联                  │
└─────────────────────────────────────────────────────────────────┘

EffectContainer.Apply() 过程中：

1. 获取 EffectSourceRegistry 服务
2. 创建子 EffectSource
   ┌─────────────────────────────────────────────────────┐
   │ EffectSourceRegistry.CreateChild()                 │
   │ {                                                   │
   │     parentContextId: sourceContextId (来自技能),    │
   │     kind: Effect,                                   │
   │     configId: instance.Id,                         │
   │     sourceActorId: 来源ActorId,                     │
   │     targetActorId: 目标ActorId,                    │
   │     frame: 当前帧号                                 │
   │ }                                                   │
   └─────────────────────────────────────────────────────┘
3. 将 SourceContextId 存入 EffectInstance.State
4. Cue 可以通过 context.SourceContextId 访问上下文链
```

### 6.2 Cue 与 EffectSource 的交互

```csharp
public void EffectContainer.RemoveAt(int index, in EffectExecutionContext context)
{
    // 从 State 获取 SourceContextId
    if (inst.TryGetState<long>(EffectSourceKeys.SourceContextId, out var scidL))
        endContextId = scidL;

    // 调用 Cue.OnRemove
    (inst.Spec.Cue ?? NullGameplayEffectCue.Instance).OnRemove(in context, inst);

    // 结束 EffectSource
    if (endContextId != 0)
    {
        var r = context.Services.GetService(typeof(EffectSourceRegistry))
                  as EffectSourceRegistry;
        var reason = EffectSourceEndReason.Completed;
        if (inst.RemainingSeconds <= 0f)
            reason = EffectSourceEndReason.Expired;
        r.End(endContextId, frame, reason);
    }
}
```

---

## 七、事件发布机制

### 7.1 Cue 触发的事件

EffectContainer 在关键时机发布事件，Cue 可以订阅这些事件：

```csharp
public static class EffectTriggering
{
    public static class Events
    {
        public const string Apply = "effect.apply";    // 效果应用
        public const string Tick = "effect.tick";      // 周期触发
        public const string Remove = "effect.remove";  // 效果移除
    }

    public static class Args
    {
        public const string Source = "source";
        public const string Target = "target";
        public const string Spec = "effect.spec";
        public const string Instance = "effect.instance";
        public const string InstanceId = "effect.instanceId";
        public const string StackCount = "effect.stackCount";
        public const string ElapsedSeconds = "effect.elapsedSeconds";
        public const string RemainingSeconds = "effect.remainingSeconds";
    }
}
```

### 7.2 事件发布示例

```csharp
private static void PublishDefaultEvent(
    IEventBus bus,
    string eventId,
    in EffectExecutionContext context,
    EffectInstance instance)
{
    var args = PooledTriggerArgs.Rent();
    args[EffectTriggering.Args.Source] = context.Source;
    args[EffectTriggering.Args.Target] = context.Target;
    args[EffectTriggering.Args.Spec] = instance?.Spec;
    args[EffectTriggering.Args.Instance] = instance;
    args[EffectTriggering.Args.InstanceId] = instance?.Id ?? 0;
    args[EffectTriggering.Args.StackCount] = instance?.StackCount ?? 0;
    args[EffectTriggering.Args.ElapsedSeconds] = instance?.ElapsedSeconds ?? 0f;
    args[EffectTriggering.Args.RemainingSeconds] = instance?.RemainingSeconds ?? 0f;

    // 添加源上下文信息
    if (context.SourceContextId != 0)
    {
        args[EffectSourceKeys.SourceContextId] = context.SourceContextId;
        EffectOriginArgsHelper.FillFromServices(args, context.SourceContextId, context.Services);
    }

    bus.Publish(new TriggerEvent(eventId, instance, args));
}
```

---

## 八、使用示例

### 8.1 简单伤害 Cue

```csharp
/// <summary>
/// 伤害效果的表现层实现
/// </summary>
public class DamageEffectCue : IGameplayEffectCue
{
    private readonly string _vfxPath;
    private readonly string _sfxPath;

    public DamageEffectCue(string vfxPath, string sfxPath)
    {
        _vfxPath = vfxPath;
        _sfxPath = sfxPath;
    }

    public void OnActive(in EffectExecutionContext context, EffectInstance instance)
    {
        // 播放命中特效
        var vfxService = context.Services.GetService<IVFXService>();
        if (vfxService != null)
        {
            vfxService.Play(_vfxPath, context.Target);
        }

        // 播放音效
        var sfxService = context.Services.GetService<ISFXService>();
        if (sfxService != null)
        {
            sfxService.PlayOneShot(_sfxPath, context.Target);
        }

        // 屏幕震动
        var cameraService = context.Services.GetService<ICameraService>();
        cameraService?.Shake(0.1f, 0.2f);
    }

    public void WhileActive(in EffectExecutionContext context, EffectInstance instance)
    {
        // 即时效果不需要持续表现
    }

    public void OnRemove(in EffectExecutionContext context, EffectInstance instance)
    {
        // 清理（如果特效需要手动清理）
    }
}
```

### 8.2 持续增益 Cue

```csharp
/// <summary>
/// 持续增益效果的表现层实现
/// </summary>
public class BuffCue : IGameplayEffectCue
{
    private readonly string _buffIcon;
    private readonly string _loopVfx;
    private Entity _vfxEntity;

    public BuffCue(string buffIcon, string loopVfx)
    {
        _buffIcon = buffIcon;
        _loopVfx = loopVfx;
    }

    public void OnActive(in EffectExecutionContext context, EffectInstance instance)
    {
        // 显示Buff图标
        var uiService = context.Services.GetService<IUIService>();
        uiService?.ShowBuffIcon(_buffIcon, context.Target);

        // 创建循环特效
        var vfxService = context.Services.GetService<IVFXService>();
        if (vfxService != null)
        {
            _vfxEntity = vfxService.Create(_loopVfx, context.Target);
        }

        // 更新Buff持续时间显示
        UpdateDurationDisplay(instance);
    }

    public void WhileActive(in EffectExecutionContext context, EffectInstance instance)
    {
        // 每帧更新持续时间显示
        UpdateDurationDisplay(instance);

        // 可以根据剩余时间调整表现强度
        if (instance.RemainingSeconds < 1f)
        {
            // 快结束时闪烁效果
        }
    }

    public void OnRemove(in EffectExecutionContext context, EffectInstance instance)
    {
        // 隐藏Buff图标
        var uiService = context.Services.GetService<IUIService>();
        uiService?.HideBuffIcon(_buffIcon, context.Target);

        // 销毁循环特效
        _vfxEntity?.Destroy();
    }

    private void UpdateDurationDisplay(EffectInstance instance)
    {
        var uiService = context.Services.GetService<IUIService>();
        uiService?.UpdateBuffDuration(_buffIcon, instance.RemainingSeconds);
    }
}
```

### 8.3 创建带 Cue 的效果规格

```csharp
// 创建伤害效果规格
var damageSpec = new GameplayEffectSpec(
    durationPolicy: EffectDurationPolicy.Instant,  // 即时效果
    durationSeconds: 0f,
    periodSeconds: 0f,
    applicationRequirements: new GameplayTagRequirements(),
    grantedTags: null,
    components: new IEffectComponent[]
    {
        new AttributeEffectComponent(
            Attribute.Atk,
            AttributeModifierType.Add,
            new NumberValue(100))
    },
    executePeriodicOnApply: false,
    cue: new DamageEffectCue("Effects/Hit_VFX", "Sounds/Impact")); // ★ 添加 Cue

// 创建Buff效果规格
var buffSpec = new GameplayEffectSpec(
    durationPolicy: EffectDurationPolicy.Duration,  // 持续效果
    durationSeconds: 5f,
    periodSeconds: 0f,
    applicationRequirements: new GameplayTagRequirements(),
    grantedTags: new GameplayTagContainer(BuffTags.AttackUp),
    components: new IEffectComponent[]
    {
        new AttributeEffectComponent(
            Attribute.Atk,
            AttributeModifierType.PercentAdd,
            new NumberValue(0.5f))
    },
    executePeriodicOnApply: false,
    cue: new BuffCue("Icons/AttackUp", "Effects/Buff_Glow")); // ★ 添加 Cue
```

---

## 九、设计总结

### 9.1 核心优势

| 特性 | 说明 |
|------|------|
| **职责分离** | Cue 专注于表现，Effect 专注于逻辑 |
| **可测试性** | 逻辑层可以单独测试，不依赖 Unity 对象 |
| **可复用性** | 同一逻辑效果可以搭配不同 Cue 实现不同表现 |
| **可替换性** | 可以随时替换 Cue 实现，不影响逻辑层 |
| **帧同步友好** | Cue 不会影响帧同步逻辑，仅处理表现 |

### 9.2 扩展点

1. **自定义 Cue 基类**：可以创建抽象基类封装通用逻辑
2. **Cue 配置化**：通过配置文件定义 Cue 类型和参数
3. **Cue 优先级**：支持多个 Cue 叠加时的优先级管理
4. **Cue 事件**：Cue 内部可以发布自定义事件

### 9.3 注意事项

- Cue 不应持有任何游戏逻辑状态
- Cue 不应修改 `EffectInstance.Spec` 中的数据
- Cue 的 `WhileActive` 应避免执行重量级操作
- 使用 `NullGameplayEffectCue` 当不需要表现层时

---

## 十、文件清单

| 文件路径 | 说明 |
|----------|------|
| `Runtime/Ability/Effect/IGameplayEffectCue.cs` | Cue 接口定义 |
| `Runtime/Ability/Effect/NullGameplayEffectCue.cs` | 空 Cue 实现 |
| `Runtime/Ability/Effect/GameplayEffectSpec.cs` | 效果规格（含 Cue 字段） |
| `Runtime/Ability/Effect/EffectInstance.cs` | 效果实例（含 State） |
| `Runtime/Ability/Effect/EffectExecutionContext.cs` | 执行上下文 |
| `Runtime/Ability/Effect/EffectContainer.cs` | Cue 生命周期管理 |
| `Runtime/Ability/Effect/EffectDurationPolicy.cs` | 持续策略枚举 |
| `Runtime/Ability/Effect/EffectTriggering.cs` | 事件定义 |
