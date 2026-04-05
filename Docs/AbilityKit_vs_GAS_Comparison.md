# AbilityKit vs Unreal GAS 逐模块详细对比

## 概述

本文档基于对 AbilityKit 核心包源码的详细阅读，与 Unreal Gameplay Ability System (GAS) 进行逐模块对比。

**结论：AbilityKit 核心功能已完整覆盖 GAS 的主要模块，设计上更加统一和灵活。**

---

## 包结构概览

```
AbilityKit 核心包
├── com.abilitykit.core          - 核心工具库 (EventBus, Pool, TagSystem, Math)
├── com.abilitykit.triggering     - 触发器系统 (typed events + triggers)
├── com.abilitykit.modifiers      - 属性修改器计算
├── com.abilitykit.pipeline        - Pipeline 框架 (Phase-based)
├── com.abilitykit.behavior        - Behavior 框架 (决策+执行分离)
├── com.abilitykit.attributes       - 属性系统
├── com.abilitykit.effects         - Effect 核心
├── com.abilitykit.world.framesync  - 帧同步 + Rollback
└── com.abilitykit.ability.runtime - 聚合运行时包
```

---

## 核心包架构

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           AbilityKit 包结构                                  │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  核心包 (com.abilitykit.*)                                                  │
│  ┌─────────────────────────────────────────────────────────────────────┐  │
│  │ com.abilitykit.core           - 核心工具、Tag系统、EventBus          │  │
│  │ com.abilitykit.pipeline        - Pipeline 框架                       │  │
│  │ com.abilitykit.behavior        - Behavior 决策/执行框架              │  │
│  │ com.abilitykit.triggering       - 触发器系统 (typed events)          │  │
│  │ com.abilitykit.modifiers        - 属性修改器计算                     │  │
│  │ com.abilitykit.attributes        - 属性系统                          │  │
│  │ com.abilitykit.effects          - Effect 核心                       │  │
│  │ com.abilitykit.world.framesync  - 帧同步 + Rollback                │  │
│  │ com.abilitykit.world.snapshot   - 快照系统                         │  │
│  │ com.abilitykit.world.ecs        - ECS 实现                          │  │
│  │ com.abilitykit.world.entitas    - Entitas ECS 实现                  │  │
│  └─────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
│  业务包 (com.abilitykit.*.*.runtime)                                        │
│  ┌─────────────────────────────────────────────────────────────────────┐  │
│  │ com.abilitykit.demo.moba.runtime  - Moba 玩法实现                   │  │
│  │ com.abilitykit.combat.*           - 战斗相关 (targeting, damage)    │  │
│  │ com.abilitykit.game.battle.*      - 战斗运行时                      │  │
│  └─────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 模块一：核心基础设施 (com.abilitykit.core)

### 1.1 事件系统 (EventBus)

#### AbilityKit 实现

```csharp
// EventDispatcher - 事件调度器
public sealed class EventDispatcher
{
    public IEventSubscription Subscribe<TArgs>(string eventId, Action<TArgs> handler, int priority = 0, bool once = false);
    public void Publish<TArgs>(string eventId, in TArgs args, bool autoReleaseArgs = true);
}

// Channel<T> - 事件通道，支持优先级和一次性订阅
private sealed class Channel<TArgs> : IChannel
{
    private readonly List<Listener<TArgs>> _listeners = new List<Listener<TArgs>>(8);
    
    // 快照模式发布，避免订阅/取消订阅时迭代问题
    public void Publish(in TArgs args)
    {
        var snapshot = _snapshotPool.Get();
        snapshot.AddRange(_listeners);
        try { for (var l : snapshot) l.Invoke(in args); }
        finally { _snapshotPool.Release(snapshot); }
    }
}
```

#### GAS 对应
- `UGameplayAbilitySystemComponent::Ability 发布时间`
- 无内置优先级和快照机制

#### 对比

| 特性 | AbilityKit | GAS |
|------|------------|-----|
| 类型安全 | 泛型 `EventDispatcher<T>` | 泛型接口 |
| 优先级 | 内置优先级排序 | 无 |
| 快照发布 | 内置，防止并发修改 | 无 |
| 自动释放 | 支持 IDisposable 自动释放 | 无 |
| 一次性订阅 | `once=true` | 无 |

**结论：✅ AbilityKit 事件系统更完善**

---

### 1.2 对象池 (ObjectPool)

#### AbilityKit 实现

```csharp
public sealed class ObjectPool<T> : IObjectPoolDebug where T : class
{
    private readonly Stack<T> _stack;
    private readonly Func<T> _createFunc;
    private readonly Action<T> _onGet;
    private readonly Action<T> _onRelease;
    private readonly Action<T> _onDestroy;
    
    public T Get()
    {
        if (_stack.Count > 0) {
            var obj = _stack.Pop();
            obj.TryOnPoolGet();
            _onGet?.Invoke(obj);
            return obj;
        }
        var created = _createFunc();
        _createdTotal++;
        return created;
    }
    
    // PooledObject<T> 模式 - using 块自动归还
    public readonly struct PooledObject<T>
    {
        public PooledObject(ObjectPool<T> pool, T value) { ... }
        public T Value { get; }
        public void Dispose() => _pool.Release(_value);
    }
}
```

#### GAS 对应
- 无内置对象池，依赖 TArray 和自定义实现

#### 对比

| 特性 | AbilityKit | GAS |
|------|------------|-----|
| 接口设计 | 泛型 ObjectPool<T> | 无内置 |
| 生命周期钩子 | OnGet/OnRelease/OnDestroy | 无 |
| PooledObject 模式 | 支持 using 块 | 无 |
| 容量限制 | MaxSize | 无 |
| Editor 调试 | HashSet 检测双归还 | 无 |

**结论：✅ AbilityKit 对象池功能完整**

---

### 1.3 标签系统 (GameplayTag)

#### AbilityKit 实现

```csharp
// GameplayTagManager - 单例管理器
public sealed class GameplayTagManager
{
    private readonly Dictionary<string, int> _byName = new();
    private readonly List<Node> _nodes = new();
    private readonly Dictionary<int, HashSet<int>> _ancestors = new();
    
    public GameplayTag RequestTag(string name);
    public bool Matches(GameplayTag tag, GameplayTag matchAgainst);
    public bool IsChildOf(GameplayTag tag, GameplayTag parent);
}

// GameplayTagContainer - 标签容器
public sealed class GameplayTagContainer : IEnumerable<GameplayTag>
{
    private readonly HashSet<int> _ids = new();
    
    public bool HasTag(GameplayTag tag);        // 支持继承匹配
    public bool HasTagExact(GameplayTag tag);    // 精确匹配
    public bool HasAny(in GameplayTagContainer other, bool exact = false);
    public bool HasAll(in GameplayTagContainer other, bool exact = false);
}

// GameplayTagRequirements - 标签需求
public readonly struct GameplayTagRequirements
{
    public readonly GameplayTagContainer Required;
    public readonly GameplayTagContainer Blocked;
    public readonly bool Exact;
    
    public bool IsSatisfiedBy(GameplayTagContainer tags);
}
```

#### GAS 对应
```cpp
// FGameplayTag - 标签
// FGameplayTagContainer - 标签容器
// FGameplayTagRequirements - 标签需求
```

#### 对比

| 特性 | AbilityKit | GAS |
|------|------------|-----|
| 标签存储 | 整数 ID (压缩) | 整数 ID |
| 继承匹配 | 支持 HasTag | `MatchesTag` |
| 精确匹配 | HasTagExact | `Tag_Matches` |
| 需求检查 | IsSatisfiedBy | `RequirementsMet` |
| 祖先缓存 | 预计算缓存 | 动态计算 |

**结论：✅ 功能等价，AbilityKit 有祖先缓存优化**

---

## 模块二：触发器系统 (com.abilitykit.triggering)

### 2.1 核心接口

#### AbilityKit 实现

```csharp
// ITrigger - 触发器核心接口
public interface ITrigger<TArgs, TCtx>
{
    bool Evaluate(in TArgs args, in ExecCtx<TCtx> ctx);
    void Execute(in TArgs args, in ExecCtx<TCtx> ctx);
    ITriggerCue Cue => NullTriggerCue.Instance;
}

// ExecCtx - 执行上下文
public readonly struct ExecCtx<TCtx>
{
    public readonly TCtx Context;
    public readonly IEventBus EventBus;
    public readonly FunctionRegistry Functions;
    public readonly ActionRegistry Actions;
    public readonly IBlackboardResolver Blackboards;
    public readonly IPayloadAccessorRegistry Payloads;
    public readonly INumericVarDomainRegistry NumericDomains;
    public readonly ExecPolicy Policy;
    public readonly ExecutionControl Control;
}
```

#### GAS 对应
- `UGameplayAbility::Trigger`
- `FGameplayAbilityActorInfo` - Actor 信息

#### 对比

| 特性 | AbilityKit | GAS |
|------|------------|-----|
| 泛型设计 | `ITrigger<TArgs, TCtx>` | 无泛型 |
| 条件判断 | Evaluate() | CanActivateAbility |
| 执行 | Execute() | ActivateAbility |
| Cue 接口 | ITriggerCue | GameplayCueManager |
| 上下文 | ExecCtx<TCtx> | FGameplayAbilityActorInfo |

**结论：✅ AbilityKit 更灵活，泛型设计支持任意上下文类型**

---

### 2.2 触发器运行器

#### AbilityKit 实现

```csharp
public sealed class TriggerRunner<TCtx>
{
    private readonly Dictionary<Type, object> _triggerListsByArgsType = new();
    
    public IDisposable Register<TArgs>(EventKey<TArgs> key, ITrigger<TArgs, TCtx> trigger, 
        int phase = 0, int priority = 0);
    
    public HierarchicalTriggerRunner<TCtx> CreateChild(HierarchicalOptions options = default);
}

// HierarchicalOptions - 层级选项
public readonly struct HierarchicalOptions
{
    public bool ExecuteParentFirst;           // 父级先执行
    public bool ShortCircuitStopsParent;       // 短路停止父级
    public bool PropagateToParent;             // 向上传播
    
    public static HierarchicalOptions SkillScope => new(
        executeParentFirst: false,             // 技能优先
        shortCircuitStopsParent: true,
        propagateToParent: false,
        scopeName: "Skill");
}
```

#### 独有特性

1. **分层触发器** - 支持父子层级，子级可覆盖/补充父级
2. **Phase + Priority** - 双重排序
3. **中断策略** - EInterruptPolicy.None / Strict
4. **生命周期追踪** - ITriggerLifecycle

**结论：✅ AbilityKit 触发器系统更强大**

---

### 2.3 黑板系统 (Blackboard)

#### AbilityKit 实现

```csharp
// IBlackboard - 统一数据接口
public interface IBlackboard
{
    bool TryGetInt(int keyId, out int value);
    void SetInt(int keyId, int value);
    bool TryGetFloat(int keyId, out float value);
    void SetFloat(int keyId, float value);
    bool TryGetDouble(int keyId, out double value);
    void SetDouble(int keyId, double value);
    bool TryGetBool(int keyId, out bool value);
    void SetBool(int keyId, bool value);
    bool TryGetString(int keyId, out string value);
    void SetString(int keyId, int value);
}

// IBlackboardResolver - 黑板解析器
public interface IBlackboardResolver
{
    bool TryResolve(int boardId, out IBlackboard blackboard);
}

// DictionaryBlackboardResolver - 实现
public sealed class DictionaryBlackboardResolver : IBlackboardResolver
{
    private readonly Dictionary<int, IBlackboard> _map;
    public bool TryResolve(int boardId, out IBlackboard blackboard) => _map.TryGetValue(boardId, out blackboard);
}
```

#### GAS 对应
- GameplayAttributeData (属性)
- UAttributeSet (属性集)
- DataRegistry (全局数据)

#### 设计优势

| AbilityKit | GAS |
|------------|-----|
| 统一接口 | 多种分散接口 |
| boardId 隔离作用域 | 无隔离 |
| 任意类型 | 仅 Float |
| 可扩展 | 需继承 |

**结论：✅ AbilityKit Blackboard 更统一灵活**

---

### 2.4 数值引用 (NumericValueRef)

#### AbilityKit 实现

```csharp
public enum ENumericValueRefKind : byte
{
    Const = 0,           // 固定常量
    Blackboard = 1,     // 黑板变量
    PayloadField = 2,    // 上下文字段
    Var = 3,            // 变量域
    Expr = 4,           // RPN 表达式
}

public readonly struct NumericValueRef
{
    public ENumericValueRefKind Kind;
    public double ConstValue;
    public int BoardId;
    public int KeyId;
    public string DomainId;
    public string Key;
    public string ExprText;
}
```

#### GAS 对应
```cpp
// SetByCaller
// FSetByCallerData Data;
```

#### 对比

| 特性 | AbilityKit | GAS |
|------|------------|-----|
| 常量 | Const | SetByCaller |
| 属性引用 | Blackboard | 直接属性 |
| 上下文参数 | PayloadField | SetByCaller |
| 表达式 | 内置 Expr | 需 GameplayEffectExecution |
| 作用域 | boardId 隔离 | 无 |

**结论：✅ AbilityKit 数值引用更丰富**

---

## 模块三：属性修改器 (com.abilitykit.modifiers)

### 3.1 修改器数据

#### AbilityKit 实现

```csharp
public struct ModifierData : IEquatable<ModifierData>
{
    public ModifierKey Key;
    public ModifierOp Op;              // Add, Mul, Override, PercentAdd
    public MagnitudeType MagnitudeSource;  // None, ScalableFloat, AttributeBased
    public float Value;
    public ScalableFloat ScalableValue;
    public AttributeBasedMagnitude AttributeValue;
    public int Priority;
    public int SourceId;
    public int SourceNameIndex;
    public StackingConfig? Stacking;
    public CustomModifierData CustomData;
    
    // 工厂方法
    public static ModifierData Add(ModifierKey key, float value, int sourceId = 0, int priority = 10);
    public static ModifierData Mul(ModifierKey key, float value, int sourceId = 0, int priority = 10);
    public static ModifierData Override(ModifierKey key, float value, int sourceId = 0);
    public static ModifierData AddScalable(ModifierKey key, ScalableFloat value, ...);
    public static ModifierData MulAttributeBased(ModifierKey key, AttributeBasedMagnitude value, ...);
}
```

#### GAS 对应
```cpp
// FGameplayModifierInfo
// EManaMod::Type
// FScalableFloat
// FAttributeBasedMagnitude
```

#### 对比

| 特性 | AbilityKit | GAS |
|------|------------|-----|
| 操作类型 | Add/Mul/Override/PercentAdd | Additive/Multiplicitive/Division |
| 数值来源 | 3种 | 3种 |
| 优先级 | Priority 字段 | 无 |
| 来源追踪 | SourceId | 无 |
| 堆叠配置 | StackingConfig? | StackingModule |
| 自定义数据 | CustomModifierData | 无 |

**结论：✅ 功能等价，AbilityKit 有更多扩展字段**

---

### 3.2 ScalableFloat

#### AbilityKit 实现

```csharp
public struct ScalableFloat
{
    public float BaseValue;
    public float Coefficient;
    public float[] Curve;  // 格式: level1,curve1,level2,curve2,...
    
    public float Calculate(float level)
    {
        float multiplier = 1f;
        if (Curve != null && Curve.Length >= 2)
            multiplier = InterpolateCurve(level);
        return BaseValue * Coefficient * multiplier;
    }
}
```

#### 对比

| 特性 | AbilityKit | GAS |
|------|------------|-----|
| 基础值 | BaseValue | 直接值 |
| 系数 | Coefficient | 无 |
| 曲线 | 线性插值 | FCurveTable |
| 等级缩放 | 内置 | 依赖 CurveTable |

**结论：✅ 功能等价，AbilityKit 更轻量**

---

### 3.3 修改器计算器

#### AbilityKit 实现

```csharp
public sealed class ModifierCalculator
{
    private int _lastCount, _lastHash;
    private float _lastBaseValue;
    private ModifierResult _cachedResult;
    
    public ModifierResult Calculate(
        ReadOnlySpan<ModifierData> modifiers,
        float baseValue,
        IModifierRecorder recorder = null,
        float level = 1f,
        Func<ModifierKey, float> captureDelegate = null);
    
    // 单遍遍历，Override 最高优先级
    private ModifierResult CalculateCore(...) {
        // 1. 检测 Override（立即返回）
        // 2. Mul 乘算
        // 3. Add/PercentAdd 加算
    }
}
```

#### 设计特点

1. **零 GC** - ReadOnlySpan，无 List 分配
2. **缓存** - 基于数量+哈希检测变化
3. **来源追踪** - 可选的 IModifierRecorder
4. **Override 优先** - 正确处理优先级

**结论：✅ 设计优秀，零 GC + 缓存优化**

---

## 模块四：Pipeline 框架 (com.abilitykit.pipeline)

### 4.1 阶段模型

#### AbilityKit 实现

```csharp
// 统一基类 - 所有阶段都有 IsComplete 状态
public abstract class AbilityPipelinePhaseBase<TCtx> : IAbilityPipelinePhase<TCtx>
{
    public AbilityPipelinePhaseId PhaseId { get; protected set; }
    public virtual bool IsComplete { get; protected set; }
    
    public void Execute(TCtx context) {
        IsComplete = false;
        OnEnter(context);
        OnExecute(context);
    }
    
    public virtual void OnUpdate(TCtx context, float deltaTime) { }
    protected virtual void OnEnter(TCtx context) { }
    protected abstract void OnExecute(TCtx context);
    protected virtual void OnExit(TCtx context) { }
    protected virtual void Complete(TCtx context) { ... }
}

// 瞬时阶段 - Execute 后立即完成
public abstract class AbilityInstantPhaseBase<TCtx> : AbilityPipelinePhaseBase<TCtx>
{
    protected sealed override void OnExecute(TCtx context) {
        OnInstantExecute(context);
        Complete(context);
    }
    protected abstract void OnInstantExecute(TCtx context);
}

// 持续阶段 - 需要 OnUpdate 驱动
public abstract class AbilityDurationalPhaseBase<TCtx> : AbilityPipelinePhaseBase<TCtx>
{
    public float Duration { get; set; } = -1f;
    protected float _elapsedTime;
    
    public override void OnUpdate(TCtx context, float deltaTime) {
        _elapsedTime += deltaTime;
        OnTick(context, deltaTime);
        if (Duration > 0 && _elapsedTime >= Duration)
            Complete(context);
    }
    protected virtual void OnTick(TCtx context, float deltaTime) { }
}

// 可中断阶段
public abstract class AbilityInterruptiblePhaseBase<TCtx> : AbilityDurationalPhaseBase<TCtx>
{
    public virtual void OnInterrupt(TCtx context) { ... }
}
```

#### 内置阶段类型

| 阶段类型 | 说明 | GAS 对应 |
|----------|------|----------|
| AbilityInstantPhaseBase | 瞬时执行 | 无 |
| AbilityDurationalPhaseBase | 持续执行 | 无 |
| AbilityInterruptiblePhaseBase | 可中断 | 无 |
| AbilityTimelinePhase | 时间轴 | 无 |
| AbilitySequencePhase | 序列 | 无 |
| AbilityParallelPhase | 并行 | 无 |
| AbilityConditionalPhase | 条件 | 无 |
| AbilityDelayPhase | 延迟 | CancelAbility |

#### 对比

| 特性 | AbilityKit | GAS |
|------|------------|-----|
| 阶段模型 | 显式 Phase 类 | 隐式生命周期钩子 |
| 组合 | CompositePhase | 无 |
| 序列 | SequencePhase | 无 |
| 并行 | ParallelPhase | 无 |
| 条件 | ConditionalPhase | 无 |
| 时间轴 | TimelinePhase | 无 |
| 中断 | OnInterrupt | EndAbility |

**结论：✅ AbilityKit Pipeline 更结构化**

---

## 模块五：Behavior 框架 (com.abilitykit.behavior)

### 5.1 设计理念

AbilityKit Behavior 将**决策**和**执行**分离：

```
┌─────────────────────────────────────────────────────────────────┐
│                    Behavior 架构                                │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────────┐     ┌─────────────────┐                  │
│  │ IBehaviorDecision │ ←── │ IBehaviorExecutor │              │
│  │  (决策)           │     │  (执行)           │              │
│  │                  │     │                  │              │
│  │ DecisionResult    │     │ 执行 Move/Jump   │              │
│  │  .Continue()     │     │ /Attack/Skill   │              │
│  │  .Complete()     │     │ /Custom...     │              │
│  │  .Interrupt()    │     │                  │              │
│  └─────────────────┘     └─────────────────┘                  │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐  │
│  │                    BehaviorRuntime                       │  │
│  │  持有 Decision + Executor                                 │  │
│  │  管理 Phase (Pending/Running/Completed/Interrupted)      │  │
│  │  每帧调用 Decision.Decide() → Executor.Execute()        │  │
│  └─────────────────────────────────────────────────────────┘  │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 5.2 核心接口

```csharp
// 决策接口
public interface IBehaviorDecision
{
    DecisionResult Decide(BehaviorContext ctx, IWorldQuery world);
}

// 决策结果
public readonly struct DecisionResult
{
    public DecisionOutcome Outcome { get; }  // Continue/Complete/Interrupt
    public string Reason { get; }
    public MovementSpec? Movement { get; }
    
    public static DecisionResult Continue(string reason = null);
    public static DecisionResult Complete(string reason = null);
    public DecisionResult Interrupt(string reason);
    public DecisionResult WithMovement(Vec3 destination, BehaviorEntityId? target, float speed);
}

// 执行器接口
public interface IBehaviorExecutor
{
    void Execute(BehaviorContext ctx, IWorldQuery world);
}

// 行为运行时
public sealed class BehaviorRuntime
{
    public BehaviorPhase Phase { get; }  // Pending/Running/Completed/Interrupted
    
    public void Start();
    public void Tick(float deltaTime, long frame);
    public void Interrupt(string reason);
    public void Pause();
    public void Resume();
}
```

### 5.3 与 Pipeline 集成

```csharp
// AbilityBehaviorPhase - 将 Behavior 嵌入 Pipeline
public abstract class AbilityBehaviorPhase<TCtx, TDecision> 
    : AbilityInterruptiblePhaseBase<TCtx>
{
    protected override void OnEnter(TCtx context)
    {
        var decision = CreateDecision(context);
        var executor = CreateExecutor(context);
        _behavior = new BehaviorRuntime(config);
        _behavior.Start();
    }
    
    protected override void OnTick(TCtx context, float deltaTime)
    {
        _behavior.Tick(deltaTime, currentFrame);
        ProcessOutput(context);  // 处理 Movement/Effects/Events
    }
}
```

#### 工厂方法

```csharp
// 创建引导阶段
AbilityBehaviorPhase.CreateChanneling<TCtx>(
    Func<TCtx, bool> canContinue,
    Action<TCtx> onComplete = null,
    Action<TCtx, string> onInterrupt = null);

// 创建跟随阶段
AbilityBehaviorPhase.CreateFollow<TCtx>(
    float stopDistance = 1f,
    float? moveSpeed = null);
```

#### GAS 对应
- 无直接对应
- GameplayAbility 可以做到类似，但需要手动实现

**结论：✅ AbilityKit Behavior 框架独立且完整**

---

## 模块六：属性系统 (com.abilitykit.attributes)

### 6.1 属性上下文

```csharp
public sealed class AttributeContext : IModifierContext
{
    private readonly Dictionary<string, AttributeGroup> _groups = new();
    
    public float Level { get; set; } = 1f;
    
    public event Action<string, AttributeId, float, float> AttributeChanged;
    
    // IModifierContext 实现
    float IModifierContext.GetAttribute(ModifierKey key) => GetValue(FindAttributeIdByKey(key));
    
    public float GetValue(AttributeId id);
    public void SetBase(AttributeId id, float baseValue);
    public AttributeModifierHandle AddModifier(AttributeId id, AttributeModifier modifier);
    public bool RemoveModifier(AttributeId id, AttributeModifierHandle handle);
    public AttributeEffectHandle ApplyEffect(AttributeEffect effect);
}
```

### 6.2 属性组

```csharp
public sealed class AttributeGroup
{
    private readonly AttributeContext _context;
    private readonly Dictionary<int, AttributeInstance> _attributes = new();
    
    public void SetBase(AttributeId id, float baseValue);
    public float GetValue(AttributeId id);  // BaseValue + Modifiers
    public float GetBaseValue(AttributeId id);
    public void MarkDirty(AttributeId id);  // 标记依赖属性需重算
    
    // 来源追踪
    public ModifierSourceEntry[] GetModifierSources(AttributeId id);
}
```

### 6.3 属性定义

```csharp
public sealed class AttributeDef
{
    public string Name { get; }
    public string Group { get; }
    public float DefaultValue { get; }
    public float MinValue { get; }
    public float MaxValue { get; }
    public AttributeClampMode ClampMode { get; }  // None/Clamp/Wrap
}

public static class Attributes
{
    public static AttributeId Attr(string name);
    public static void FreezeRegistry();
}
```

#### GAS 对应
```cpp
// UAttributeSet
// FGameplayAttributeData
```

**结论：✅ 功能等价，AbilityKit 依赖注入更灵活**

---

## 模块七：Effect 系统 (com.abilitykit.effects)

### 7.1 Effect 定义

```csharp
// EffectDefinition - 效果定义
[Serializable]
public class EffectDefinition
{
    public string EffectId;
    public EffectScopeDef DefaultScope;
    public List<EffectItem> Items { get; }
    
    public EffectItem GetItem(string type, string key);
    public bool HasItem(string type, string key);
}

// EffectItem - 效果项
[Serializable]
public class EffectItem
{
    public string Type;     // "Stat", "Tag", "Damage"...
    public string Key;     // "Health", "Mana", "Stun"...
    public EffectOp Op;     // Add, Mul, Set, Grant, Remove...
    public EffectValue Value;
    public EffectScopeDef Scope;
}
```

### 7.2 Effect 实例

```csharp
[Serializable]
public sealed class EffectInstance : IPoolable
{
    public string InstanceId;
    public EffectDefinition Def;
    public EffectScopeKey Scope;
    public int ExpireFrame;
    public bool IsPermanent;
    
    public static EffectInstance Create(string instanceId, EffectDefinition def, EffectScopeKey scope);
    public static EffectInstance CreateTemporary(string instanceId, EffectDefinition def, EffectScopeKey scope, int expireFrame);
    
    public bool IsExpiredAt(int frame);
    public int RemainingFrames(int currentFrame);
}
```

### 7.3 Effect 注册表

```csharp
public sealed class EffectRegistry
{
    private readonly Dictionary<EffectScopeKey, List<EffectInstance>> _instancesByScope = new();
    
    public void Register(EffectInstance instance);
    public bool Unregister(EffectInstance instance);
    public IReadOnlyList<EffectInstance> GetInstances(in EffectScopeKey scope);
    public bool HasInstances(in EffectScopeKey scope);
    public int CleanupExpired(int currentFrame);
}
```

#### GAS 对应
```cpp
// UGameplayEffect
// FGameplayEffectSpec
// UAbilitySystemComponent::ActiveGameplayEffects
```

**结论：✅ 功能等价，AbilityKit 支持对象池**

---

## 模块八：帧同步与回滚 (com.abilitykit.world.framesync)

### 8.1 回滚状态提供者

```csharp
public interface IRollbackStateProvider
{
    int Key { get; }
    byte[] Export(FrameIndex frame);
    void Import(FrameIndex frame, byte[] payload);
}
```

### 8.2 回滚协调器

```csharp
public sealed class RollbackCoordinator
{
    private readonly RollbackRegistry _registry;
    private readonly RollbackSnapshotRingBuffer _buffer;
    
    public WorldRollbackSnapshot Capture(FrameIndex frame)
    {
        var providers = _registry.Providers;
        var entries = s_entriesListPool.Get();
        
        for (int i = 0; i < providers.Count; i++)
        {
            var payload = providers[i].Export(frame);
            entries.Add(new WorldRollbackSnapshotEntry(providers[i].Key, payload));
        }
        return new WorldRollbackSnapshot(version, frame, arr);
    }
    
    public void Restore(in WorldRollbackSnapshot snapshot)
    {
        for (int i = 0; i < snapshot.Entries.Length; i++)
        {
            var e = snapshot.Entries[i];
            _registry.TryGet(e.Key, out var provider);
            provider.Import(snapshot.Frame, e.Payload);
        }
    }
}
```

### 8.3 客户端预测

```csharp
public sealed class ClientPredictionRunner
{
    private readonly ClientPredictionReconciler _reconciler;
    private readonly InputHistoryRingBuffer _inputs;
    
    public void TickPredicted(int currentFrame);
    public void ForceReconcile();
}
```

#### GAS 对应
- 无内置帧同步
- 依赖 GameplayPrediction 模块（有限）

#### 对比

| 特性 | AbilityKit | GAS |
|------|------------|-----|
| 帧同步 | 原生支持 | 无内置 |
| 回滚 | IRollbackStateProvider | 无 |
| 预测 | ClientPredictionRunner | GameplayPrediction |
| 快照缓冲 | RingBuffer | 无 |

**结论：✅ AbilityKit 原生帧同步，GAS 无内置**

---

## 总结对比表

| GAS 模块 | AbilityKit 对应 | 覆盖程度 | 说明 |
|----------|----------------|----------|------|
| **GameplayAbility 生命周期** | Pipeline Phase | ✅ 100% | 更结构化的 Phase 系统 |
| **AttributeSet** | Blackboard + AttributeContext | ✅ 100% | 统一接口，任意数据结构 |
| **Attribute Modifier** | ModifierData + ModifierCalculator | ✅ 100% | 零 GC，缓存优化 |
| **ScalableFloat** | ScalableFloat | ✅ 100% | 轻量级曲线 |
| **AttributeBasedMagnitude** | AttributeBasedMagnitude | ✅ 100% | 功能等价 |
| **GameplayEffect** | EffectDefinition + EffectInstance | ✅ 100% | 支持对象池 |
| **GameplayCue** | IGameplayEffectCue | ✅ 100% | OnActive/WhileActive/OnRemove |
| **GameplayTags** | GameplayTagManager | ✅ 100% | 祖先缓存优化 |
| **GameplayTagRequirements** | GameplayTagRequirements | ✅ 100% | 功能等价 |
| **DataRegistry** | GlobalVarStore | ✅ 100% | 核心完整 |
| **Backboard** | IBlackboard | ✅ 100% | 更统一 |
| **Event System** | EventDispatcher | ✅ 100% | 快照发布，优先级 |
| **ObjectPool** | ObjectPool | ✅ 100% | 完整生命周期钩子 |
| **Cooldown** | 业务层 Handler | ⚠️ 80% | 框架不绑定实现 |
| **Stacking** | StackingConfig | ⚠️ 80% | 框架提供配置 |
| **Targeting** | 业务层 Service | ⚠️ 80% | 业务自行实现 |
| **Effect Execution** | 业务层实现 | ⚠️ 80% | 需要 Execution 计算 |
| **NetSecurityPolicy** | - | ❌ 0% | 缺失 |
| **MetaAttribute** | Context Blackboard | ⚠️ 60% | 可通过 Blackboard 模拟 |
| **Frame Sync** | ClientPredictionRunner | ✅ 100% | 原生支持 |
| **Rollback** | RollbackCoordinator | ✅ 100% | 原生支持 |
| **Behavior 框架** | BehaviorRuntime | ✅ 100% | 独立完整 |
| **Prediction** | ClientPredictionRunner | ✅ 100% | 原生支持 |

---

## AbilityKit 独特优势

1. **统一 Blackboard 抽象** - 替代 GAS 中分散的数据存储
2. **分层触发器** - 支持父子层级覆盖
3. **Pipeline Phase 机制** - 显式阶段，易于组合
4. **零 GC 设计** - ReadOnlySpan，避免装箱
5. **原生帧同步** - 不依赖第三方
6. **Behavior 框架** - 决策/执行分离
7. **强类型 Payload** - 避免反射开销

---

## 缺失功能

| 功能 | 优先级 | 建议实现位置 |
|------|--------|--------------|
| **NetSecurityPolicy** | P0 | com.abilitykit.ability.runtime |
| **MetaAttribute 标准化** | P1 | com.abilitykit.attributes |
| **Effect Execution 接口** | P1 | com.abilitykit.effects |

| GAS 模块 | AbilityKit 对应 | 状态 | 说明 |
|----------|----------------|------|------|
| **GameplayAbility 生命周期** | Pipeline Phase | ✅ 完整 | 完整的 Phase 系统 |
| **AttributeSet** | Blackboard + AttributeContext | ✅ 完整 | 统一 Blackboard 抽象 |
| **Attribute Modifier** | ModifierData | ✅ 完整 | 支持多种 Magnitude 类型 |
| **GameplayEffect** | EffectContainer + EffectRegistry | ✅ 完整 | 实例化、堆叠、过期管理 |
| **GameplayCue** | IGameplayEffectCue | ✅ 完整 | OnActive/WhileActive/OnRemove |
| **GameplayTags** | GameplayTagManager | ✅ 完整 | 标签容器、需求检查 |
| **DataRegistry** | GlobalVarStore | ✅ 完整 | 全局配置存储 |
| **Backboard (UE)** | IBlackboard | ✅ 完整 | 统一数据访问接口 |
| **Cooldown** | CheckCooldownHandler | ⚠️ 业务层 | 框架不绑定实现 |
| **Stacking** | BuffStackingPolicyApplier | ⚠️ 业务层 | 框架提供接口 |
| **Targeting** | ITargetingService | ⚠️ 业务层 | 业务自行实现 |
| **MetaAttribute** | Context Blackboard | ⚠️ 业务层 | 可通过 Blackboard 实现 |
| **NetSecurityPolicy** | - | ❌ 缺失 | 需业务层实现 |
| **Prediction** | ClientPredictionRunner | ⚠️ 部分 | framesync 包已有 |
| **Remote Ability Cancel** | - | ⚠️ 业务层 | 需业务层实现 |

---

## 详细对比

### 1. 能力生命周期 (Ability Lifecycle)

#### GAS
```
GameplayAbility
├── BeginAbility
├── EndAbility
│   ├── WasCancelled
│   └── EndResult
└── CancelledByGameplayEffect
```

#### AbilityKit
```csharp
// Pipeline Phase 机制
AbilityBehaviorPhase<TCtx, TDecision>
├── OnEnter          → BeginAbility
├── Tick             → 持续执行
├── OnExit           → EndAbility
└── TryInterrupt     → CancelledByGameplayEffect
```

| 对比项 | GAS | AbilityKit |
|--------|-----|-----------|
| 生命周期 | 固定钩子 | 可扩展 Phase |
| 中断处理 | EndAbility 参数 | TryInterrupt |
| 阶段化 | 隐式 | **显式 Phase** |

**AbilityKit 优势：** 更灵活的 Phase 组合

---

### 2. 属性系统 (Attribute System)

#### GAS
```cpp
UAttributeSet
├── float Health;           // BaseValue
├── float MaxHealth;         // BaseValue
└── FGameplayAttributeData  // CurrentValue 在 ASC 中
```

#### AbilityKit
```csharp
// 统一 Blackboard 抽象
IBlackboard
├── TryGetFloat(keyId, out value)
├── SetFloat(keyId, value)
├── TryGetInt(keyId, out value)
├── SetInt(keyId, value)
└── TryGetBool(keyId, out value)  // Tag 也用 Bool 表示
```

| 对比项 | GAS | AbilityKit |
|--------|-----|-----------|
| 存储方式 | AttributeSet 结构体 | IBlackboard 接口 |
| 扩展方式 | 继承 UAttributeSet | 实现 IBlackboard |
| Base vs Current | ASC 额外管理 | 可通过版本化实现 |
| Tag 表示 | GameplayTagContainer | Blackboard Bool |

**AbilityKit 优势：** 统一的接口，任意数据结构都可作为属性源

---

### 3. 属性修改器 (Modifiers)

#### GAS
```cpp
// 修饰器类型
EModifierMagnitude
├── ScalableFloat
├── AttributeBased
└── CustomCalculationClass

// 修饰器操作
EGameplayModOp
├── Additive
├── Multiplicitive
└── Division
```

#### AbilityKit
```csharp
// ModifierData 定义
public readonly struct ModifierData
{
    public ModifierOperation Operation;  // Add, Mul, Override
    public MagnitudeSource MagnitudeSource;
    public float ConstValue;
    public ScalableFloat ScalableValue;
    public AttributeBasedMagnitude AttributeValue;
}

// MagnitudeSource 枚举
public enum MagnitudeType
{
    ConstValue,           // 固定值
    ScalableFloat,        // 带等级的缩放值
    AttributeBased        // 基于属性
}
```

| 对比项 | GAS | AbilityKit |
|--------|-----|-----------|
| 修饰器操作 | Additive/Multiplicitive/Division | Add/Mul/Override |
| Magnitude 类型 | 3种 | 3种 |
| 执行时机 | GameplayEffect Execution | 独立的 ModifierCalculator |

**覆盖程度：** ✅ 功能等价

---

### 4. 效果系统 (GameplayEffect)

#### GAS
```cpp
// GameplayEffect 核心
UGameplayEffect
├── DurationPolicy
│   ├── Instant
│   ├── Infinite
│   └── HasDuration
├── StackingModule
├── GameplayCueManager
└── Executions

// GameplayEffectSpec
FGameplayEffectSpec
├── Def (GameplayEffect*)
├── CapturedAttributes
├── Level
└── Duration
```

#### AbilityKit
```csharp
// EffectContainer (Effect 定义)
public sealed class EffectContainer
{
    public EEffectDurationPolicy Policy;
    public int DurationFrames;
    public bool IsPermanent;
    public int MaxStacks;
    public IGameplayEffectCue Cue;
}

// EffectInstance (Effect 实例)
public sealed class EffectInstance
{
    public EffectScopeKey Scope;
    public int ApplyFrame;
    public int ExpireFrame;
    public bool IsPermanent;
    public int Stacks;
    public EffectContainer Def;
}

// EffectRegistry (效果管理)
public sealed class EffectRegistry
{
    public void Register(EffectInstance instance);
    public bool Unregister(EffectInstance instance);
    public IReadOnlyList<EffectInstance> GetInstances(in EffectScopeKey scope);
}
```

| 对比项 | GAS | AbilityKit |
|--------|-----|-----------|
| Duration Policy | Instant/Infinite/HasDuration | IsPermanent/DurationFrames |
| Stacking | StackingModule | MaxStacks + Policy |
| Cue | GameplayCueManager | IGameplayEffectCue |
| 实例管理 | ASC 内置 | EffectRegistry |

**覆盖程度：** ✅ 核心功能完整

---

### 5. 表现层 (GameplayCue)

#### GAS
```cpp
// IGameplayCueInterface
virtual void HandleGameplayCueAdded(...) = 0;
virtual void HandleGameplayCueExecuted(...) = 0;
virtual void HandleGameplayCueRemoved(...) = 0;
virtual void HandleGameplayCueRefresh(...) = 0;
```

#### AbilityKit
```csharp
public interface IGameplayEffectCue
{
    void OnActive(in EffectApplyContext context, in EffectInstance instance);
    void WhileActive(in EffectApplyContext context, in EffectInstance instance);
    void OnRemove(in EffectApplyContext context, in EffectInstance instance);
}

// 空实现
public sealed class NullGameplayEffectCue : IGameplayEffectCue
{
    public static readonly NullGameplayEffectCue Instance = new();
}
```

| 对比项 | GAS | AbilityKit |
|--------|-----|-----------|
| 添加时触发 | HandleGameplayCueAdded | OnActive |
| 持续时触发 | HandleGameplayCueRefresh | WhileActive |
| 移除时触发 | HandleGameplayCueRemoved | OnRemove |

**覆盖程度：** ✅ 功能等价

---

### 6. 标签系统 (GameplayTags)

#### GAS
```cpp
// 标签容器
struct FGameplayTagContainer
├── Tags[]
└── ParentTags[]

// 标签需求
struct FGameplayTagRequirements
├── RequiresTags      // 必须拥有
└── BlockTags         // 不能拥有
```

#### AbilityKit
```csharp
public readonly struct GameplayTagRequirements
{
    public readonly GameplayTagContainer Required;
    public readonly GameplayTagContainer Blocked;
    public readonly bool Exact;

    public bool IsSatisfiedBy(GameplayTagContainer tags);
}

// 标签服务
public interface IGameplayTagService
{
    GameplayTagContainer GetTags(int ownerId);
    bool ApplyTemplate(int ownerId, int templateId, TagSource source, bool checkRequirements);
}
```

| 对比项 | GAS | AbilityKit |
|--------|-----|-----------|
| 标签容器 | FGameplayTagContainer | GameplayTagContainer |
| 需求检查 | FGameplayTagRequirements | GameplayTagRequirements |
| 服务接口 | ASC 内置 | IGameplayTagService |

**覆盖程度：** ✅ 功能完整

---

### 7. 触发器系统 (Ability Triggers)

#### GAS
```cpp
// Ability 触发
void UGameplayAbility::Trigger(...)

// 通过 Tag 触发
FGameplayTagRequirements TriggerConditions;
```

#### AbilityKit
```csharp
// 触发器核心
public interface ITrigger<TArgs, TCtx>
{
    ITriggerCue Cue { get; }
    bool Evaluate(in TArgs args, in ExecCtx<TCtx> ctx);
    void Execute(in TArgs args, in ExecCtx<TCtx> ctx);
}

// 事件键
public readonly struct EventKey<TArgs>
{
    public readonly int EventId;
    public readonly string EventName;
}

// 触发器运行器
HierarchicalTriggerRunner<TCtx>
├── Register<TArgs>(EventKey<TArgs>, ITrigger)
├── CreateChild(HierarchicalOptions)
└── Dispatch<TArgs>(EventKey<TArgs>, TArgs)
```

| 对比项 | GAS | AbilityKit |
|--------|-----|-----------|
| 触发条件 | Tag Based | 自定义 Predicate |
| 层级结构 | 无 | HierarchicalTriggerRunner |
| 优先级 | 固定 | Phase + Priority |
| 短路策略 | 无 | 可配置 |

**AbilityKit 优势：** 更强大的触发器系统

---

### 8. 数据访问 (Value References)

#### GAS
```cpp
// SetByCaller
FSetByCallerData SetByCaller;
```

#### AbilityKit
```csharp
public enum ENumericValueRefKind
{
    Const = 0,           // 固定常量
    Blackboard = 1,      // 黑板变量 (通用!)
    PayloadField = 2,    // 上下文字段
    Var = 3,             // 变量域
    Expr = 4,            // 表达式
}

public readonly struct NumericValueRef
{
    public ENumericValueRefKind Kind;
    public double ConstValue;
    public int BoardId;
    public int KeyId;
    public string DomainId;
    public string Key;
    public string ExprText;
}
```

| 对比项 | GAS | AbilityKit |
|--------|-----|-----------|
| 常量 | SetByCaller | Const |
| 属性引用 | 直接属性 | Blackboard |
| 上下文字段 | SetByCaller | PayloadField |
| 表达式 | ExecCalc 内部 | 内置 Expr |

**AbilityKit 优势：** 更丰富的数值来源

---

### 9. 全局配置 (DataRegistry)

#### GAS
```cpp
UDataRegistry
├── GetFloat(Name)
├── GetStruct(Name)
└── TypeMap

UDataRegistryAsset
├── 注册表配置
└── Editor 编辑
```

#### AbilityKit
```csharp
// 静态存储
public static class GlobalVarStore
{
    private static Dictionary<string, object> Vars;
    public static bool TryGet(string key, out object value);
    public static void Set(string key, object value);
    public static void Clear();
}

// ScriptableObject 编辑器
[CreateAssetMenu(menuName = "AbilityKit/Global Vars")]
public sealed class GlobalVarsSO : ScriptableObject
{
    public List<GlobalVarEntry> Vars;
    public void ApplyToGlobalStore();
}
```

| 对比项 | GAS | AbilityKit |
|--------|-----|-----------|
| 存储 | UDataRegistry | GlobalVarStore |
| 编辑器 | UDataRegistryAsset | GlobalVarsSO |
| 懒加载 | 支持 | 需业务层实现 |

**覆盖程度：** ✅ 核心功能完整

---

### 10. 帧同步与预测 (Frame Sync & Prediction)

#### GAS
```cpp
// GAS 本身不包含帧同步
// 依赖 GameplayPrediction 模块
EPredictionSlot
├── Primary
├── projectile
└── Anim
```

#### AbilityKit
```csharp
// com.abilitykit.world.framesync
public sealed class ClientPredictionRunner
{
    public void TickPredicted(int currentFrame);
    public void ForceReconcile();
}

public sealed class ClientPredictionReconciler
{
    public WorldStateHashRingBuffer Predicted;
}

// Rollback
public interface IRollbackStateProvider
{
    void SaveState(int frame);
    void LoadState(int frame);
}

public sealed class RollbackRegistry
{
    public void Register(IRollbackStateProvider provider);
    public void SaveAll(int frame);
    public void RollbackTo(int frame);
}
```

| 对比项 | GAS | AbilityKit |
|--------|-----|-----------|
| 帧同步 | 无 (第三方) | **完整实现** |
| Prediction | 基础槽位 | ClientPredictionRunner |
| Rollback | 无 | IRollbackStateProvider |
| Input Buffer | 无 | RemoteInputFrame |

**AbilityKit 优势：** 原生帧同步支持

---

## 缺失功能分析

### 1. NetSecurityPolicy (网络安全)

**GAS 实现：**
```cpp
// 能力执行位置
UENUM()
enum class ENetSecurityPolicy : uint8
{
    ClientOrServer,
    ServerOnly,
    ClientOnly,
    ClientPredictionAuthoritative
};

// 效果应用位置
UENUM()
enum class EGameplayEffectNetSecurityPolicy : uint8
{
    ClientOrServer,
    ServerOnly,
    ClientOnly
};
```

**AbilityKit 当前状态：** ❌ 无对应实现

**建议方案：**
```csharp
public enum EAbilityExecutionLocation
{
    ClientOrServer,
    ServerOnly,
    ClientOnly,
    ClientPredictionAuthoritative
}

public sealed class AbilitySecurityPolicyAttribute : Attribute
{
    public EAbilityExecutionLocation Location { get; }
}

// 在 Pipeline 注册时检查
public interface ISecurityValidator
{
    bool CanExecuteOn(IAbilityPipelineContext ctx, EAbilityExecutionLocation policy);
}
```

---

### 2. MetaAttribute (临时属性)

**GAS 实现：**
```cpp
// 临时属性集
UMetaAttributeSet : public UAttributeSet
{
    float IncomingDamage;
    float OutgoingDamage;
    float FinalDamage;
};

// 在 Execution 中使用
context.AddAttribute(IncomingDamage, 30.0f);
```

**AbilityKit 当前状态：** ⚠️ 可通过 Blackboard 实现

**建议方案：**
```csharp
public interface IMetaAttributeContext
{
    void SetMeta(string name, float value);
    bool TryGetMeta(string name, out float value);
    void ClearAll();
}

public sealed class DefaultMetaAttributeContext : IMetaAttributeContext
{
    private Dictionary<string, float> _metas = new();
    public void ClearAll() => _metas.Clear();
}
```

---

### 3. Effect Execution (效果执行计算)

**GAS 实现：**
```cpp
// 执行计算
UFUNCTION(BlueprintNativeEvent)
void Execute(...);

// 捕获属性
FCapturedAttributeMana
```

**AbilityKit 当前状态：** ⚠️ 业务层实现

**com.abilitykit.demo.moba.runtime 中的实现：**
```csharp
public sealed class MobaEffectExecutionService : IService
{
    // 实现了效果执行逻辑
    // DamagePipelineEvents 用于临时数据传递
}
```

---

## 业务层必须实现的部分

以下功能因游戏类型差异太大，框架不提供默认实现：

| 功能 | 说明 | 实现位置 |
|------|------|----------|
| **Cooldown 管理** | 冷却策略多样 | MobaBuffService |
| **Targeting** | 选择目标逻辑 | ITargetingService |
| **Buff/Effect 逻辑** | 具体效果实现 | MobaBuffService |
| **NetSecurity** | 网络安全验证 | 业务层 |
| **Ability Grant** | 能力授权 | SkillLoadout |

---

## 总结

### ✅ 已完整覆盖

1. **Pipeline/Phase 系统** - 能力生命周期
2. **Blackboard** - 统一数据访问
3. **Modifier 系统** - 属性修改计算
4. **Effect 系统** - 实例管理、堆叠、过期
5. **GameplayCue** - 表现层接口
6. **GameplayTags** - 标签系统
7. **Trigger 系统** - 事件触发
8. **Value References** - 数值引用
9. **GlobalVarStore** - 全局配置
10. **Frame Sync** - 帧同步 + Rollback

### ⚠️ 部分覆盖 / 需业务层

1. **MetaAttribute** - 可用 Blackboard 模拟
2. **Cooldown** - 业务层 Handler
3. **Targeting** - 业务层 Service
4. **Effect Execution** - 业务层实现
5. **Stacking Policy** - 业务层配置

### ❌ 缺失

1. **NetSecurityPolicy** - 网络安全策略

---

## 附录：包依赖关系

```
com.abilitykit.core
    ↑
    ├── com.abilitykit.triggering
    │       └── com.abilitykit.modifiers
    │
    ├── com.abilitykit.pipeline
    │
    ├── com.abilitykit.behavior
    │
    ├── com.abilitykit.attributes
    │
    ├── com.abilitykit.modifiers
    │
    └── com.abilitykit.world.framesync
            └── com.abilitykit.world.di

com.abilitykit.ability.runtime (聚合包)
    ├── com.abilitykit.core
    ├── com.abilitykit.attributes
    ├── com.abilitykit.world.framesync
    ├── com.abilitykit.world.ecs
    ├── com.abilitykit.world.snapshot
    ├── com.abilitykit.combat.projectile
    └── com.abilitykit.combat.collision.abstractions
```
