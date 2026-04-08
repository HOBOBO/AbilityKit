# AbilityKit.Modifiers 设计文档

## 文档版本历史

| 版本 | 日期 | 更新内容 |
|------|------|----------|
| 2.0 | 2026-04 | 重构版：引入 MagnitudeSource、IModifierOperator、ModifierPipeline |
| 1.0 | 早期 | 初版设计 |

## 目录

- [背景与问题](#背景与问题)
- [设计目标](#设计目标)
- [核心概念](#核心概念)
- [模块结构](#模块结构)
- [核心类型详解](#核心类型详解)
- [使用指南](#使用指南)
- [与 GAS 对比](#与-gas-对比)
- [性能考量](#性能考量)
- [架构演进](#架构演进)

---

## 背景与问题

### 为什么需要修改器系统？

游戏中的数值计算无处不在：

- 角色攻击力 = 基础攻击力 + 装备加成 × 技能倍率
- 护盾最大值 = 基础值 + 装备加成 + BUFF 加成 × 套装倍率
- 技能伤害 = 基础伤害 × (1 + 伤害加成%) × 属性克制系数

但修改器的应用范围不仅限于属性：

- 技能 ID 修改：强制使用特定技能
- 弹道参数修改：修改子弹速度、弹道 Prefab
- AOE 参数修改：修改范围、形状
- 布尔状态：无敌、免疫、沉默

### 现有方案的不足

| 问题 | 描述 |
|------|------|
| **强耦合属性** | 很多修改器系统和属性系统绑定，不能扩展到其他场景 |
| **操作类型固定** | 只有 Add/Mul/Override，不能自定义复杂逻辑 |
| **数值类型单一** | 只支持 float，不支持 int、enum、struct 等 |

---

## 设计目标

### 1. 通用修改器框架

修改器是一个**通用抽象**，可以应用于任何类型：

```
修改器 = 描述"谁来修改什么、以什么方式"的元数据
       + 具体的计算/处理逻辑（由 Handler 决定）
```

### 2. 单一职责

只做一件事：**修改器数组 → 计算结果**。

```
输入：ModifierData[] + BaseValue + Context
        ↓
输出：ModifierResult<T> (FinalValue, Sources, ...)
```

### 3. 零 GC

所有公共 API 返回值均为值类型（struct），不产生任何堆分配。

### 4. 业务层自主

模块不关心：
- 修改器存储在哪里
- 如何按 SourceId 批量移除
- 如何实现属性系统
- 如何管理生命周期

模块只负责计算，存储和生命周期由业务层决定。

---

## 核心概念

### 1. 修改器（Modifier）

修改器是**对一个值的增量修改**。每个修改器包含：

- **Key** — 修改哪个目标（属性、技能参数、弹道配置等）
- **Op** — 如何修改（加法/乘法/覆盖/百分比/自定义）
- **Magnitude** — 数值（或数值来源）
- **SourceId** — 来源标识（用于溯源和批量操作）
- **CustomData** — 自定义数据（用于非数值类型）

### 2. 修改器键（ModifierKey）

键用于唯一标识一个可修改的目标。32 位压缩存储：

```
[Reserved:8][Custom:8][SubCategory:8][Category:8]
```

### 3. 操作类型（ModifierOp）

| 操作 | 说明 | 公式 | 示例 |
|------|------|------|------|
| `Add` | 加法叠加 | Base + A | +100 攻击力 |
| `Mul` | 乘法叠加 | Base × M | ×1.2 伤害倍率 |
| `Override` | 覆盖 | 直接替换 | 锁定生命值 1000 |
| `PercentAdd` | 百分比加成 | Base × (1 + %) | +20% 移动速度 |
| `Custom` | 自定义 | 由 Handler 决定 | 业务层扩展 |

### 4. 修改器处理器（IModifierHandler<T>）

核心扩展接口，支持任意类型的值：

```csharp
public interface IModifierHandler<TValue>
{
    TValue Apply(TValue baseValue, in ModifierData modifier, IModifierContext context);
    int Compare(TValue a, TValue b);
    TValue Combine(in Span<TValue> values);
}
```

内置处理器：
- `NumericModifierHandler<float>` — 数值型（默认）
- `SkillIdModifierHandler<int>` — 技能 ID
- `BooleanModifierHandler<bool>` — 布尔状态

### 5. 修改器上下文（IModifierContext）

提供计算过程中需要的外部数据：

```csharp
public interface IModifierContext
{
    float GetAttribute(ModifierKey key);
    float Level { get; }
}
```

### 6. 叠加（Stacking）

同类效果可以叠加：

| 类型 | 说明 | 示例 |
|------|------|------|
| `Exclusive` | 独占，同源只能有一个 | 装备效果，后来的替换先来的 |
| `Aggregate` | 聚合，同源可叠加层数 | DOT 持续伤害，每跳叠加一层 |

---

## 模块结构

```
AbilityKit.Modifiers/
├── package.json
├── README.md
├── com.abilitykit.modifiers.asmdef
└── Runtime/
    └── Core/
        ├── ModifierKey.cs              # 修改器键（32位压缩存储）
        ├── ModifierOp.cs               # 操作类型枚举 + 扩展方法
        ├── ModifierData.cs             # 修改器数据结构 + 工厂方法
        ├── ModifierResult.cs           # 计算结果 + 来源追踪接口
        ├── ModifierStacking.cs         # 叠加逻辑（独占/聚合）
        ├── ModifierCalculator.cs       # 核心计算引擎（带缓存）
        ├── ModifierCacheAndCore.cs     # 缓存 + 计算核心
        ├── MagnitudeSource.cs          # 统一数值来源（时间衰减等）
        ├── OperatorComposer.cs         # 操作组合器
        ├── ModifierComposer.cs         # 修饰器组合类型（链式/并行/条件）
        ├── Data/
        │   └── CustomModifierData.cs  # 自定义数据槽
        ├── Handler/
        │   ├── IModifierHandler.cs    # 处理器接口 + 基类
        │   └── ModifierHandlers.cs    # 内置处理器（数值/布尔/枚举）
        ├── Magnitude/
        │   └── MagnitudeModifier.cs   # IMagnitudeModifier + 管道
        └── Source/
            └── IValueSource.cs         # IValueSource 接口 + 实现
```

### 依赖关系

```
ModifierCalculator
    ├── ModifierData
    │   ├── ModifierKey
    │   ├── ModifierOp
    │   ├── ScalableFloat
    │   └── AttributeBasedMagnitude
    ├── ModifierResult
    └── ModifierStacking

IModifierHandler<T> (可独立使用)
    └── 被 ModifierCalculator 调用
```

---

## 核心类型详解

### ModifierKey

键用于分类修改器：

```csharp
// 预定义分类
ModifierKey.ShieldMax    // 护盾最大值
ModifierKey.ShieldRegen // 护盾回复
ModifierKey.MoveSpeed   // 移动速度

// 自定义键
var key = ModifierKey.Create(categoryId: 10, subCategoryId: 2, customId: 0);

// 业务层可扩展分类
ModifierKey.Categories.Projectile = 30;
ModifierKey.Categories.AOE = 31;
```

### ModifierOp

定义修改器的操作方式：

```csharp
public enum ModifierOp : byte
{
    Add = 0,
    Mul = 1,
    Override = 2,
    PercentAdd = 3,
    Custom = 100,  // 业务层可扩展
}
```

### ModifierData

修改器数据，核心数据结构：

```csharp
public struct ModifierData
{
    // 核心字段
    public ModifierKey Key;                    // 修改目标键
    public ModifierOp Op;                     // 操作类型
    public int Priority;                      // 优先级
    public int SourceId;                      // 来源标识
    public short SourceNameIndex;             // 调试用

    // 数值来源（重构核心）
    public MagnitudeSource Magnitude;         // 统一数值来源

    // 元数据
    public ModifierMetadata Metadata;          // 调试/显示用

    // 自定义数据
    public CustomModifierData CustomData;      // 非数值类型
}
```

### 数值来源（重构核心）

统一数值来源结构 `MagnitudeSource`，支持多种计算模式：

```csharp
// 固定值
var source = MagnitudeSource.Fixed(100f);

// 时间衰减
var source = MagnitudeSource.TimeDecay(50f, 5f, DecayType.Exponential);

// 等级曲线
var source = MagnitudeSource.LevelCurve(10f, curve, 1f);

// 属性引用
var source = MagnitudeSource.Attribute(ModifierKey.Strength, 0.5f);

// 修饰器管道（支持组合）
var pipeline = ModifierPipeline.Create()
    .ThenTimeDecay(50f, 5f, DecayType.Exponential)
    .ThenLevelCurve(10f, curve);
var source = MagnitudeSource.Pipeline(pipeline);
```

### IModifierOperator（重构核心）

操作接口，替代原有的枚举硬编码逻辑：

```csharp
public interface IModifierOperator
{
    ModifierOp OpCode { get; }
    string Name { get; }
    float Apply(float baseValue, float modifierValue);
    float CalculateContribution(float baseValue, float modifierValue);
    int Priority { get; }
    bool IsTerminal { get; }    // 是否为终止操作（如 Override）
    bool IsAdditive { get; }    // 是否为加法类操作
}

// 内置操作
public readonly struct AddOperator : IModifierOperator { ... }
public readonly struct MulOperator : IModifierOperator { ... }
public readonly struct OverrideOperator : IModifierOperator { ... }
public readonly struct PercentAddOperator : IModifierOperator { ... }

// 操作注册表
public static class ModifierOperatorRegistry
{
    public static void Register(IModifierOperator op);
    public static IModifierOperator Get(ModifierOp op);
}
```

### IMagnitudeModifier（修饰器管道）

支持链式组合的修饰器接口：

```csharp
public interface IMagnitudeModifier
{
    byte ModifierTypeId { get; }
    string Name { get; }
    float Modify(IModifierContext context, float input);
    float GetBaseValue();
}

// 内置修饰器
public struct FixedModifier : IMagnitudeModifier { ... }
public struct TimeDecayModifier : IMagnitudeModifier { ... }
public struct LevelCurveModifier : IMagnitudeModifier { ... }
public struct AttributeRefModifier : IMagnitudeModifier { ... }
public struct ScaleModifier : IMagnitudeModifier { ... }

// 修饰器管道
public struct ModifierPipeline : IMagnitudeModifier
{
    public ModifierPipeline Then(IMagnitudeModifier modifier);
    public ModifierPipeline ThenTimeDecay(float initialValue, float duration, DecayType decayType);
    public ModifierPipeline ThenLevelCurve(float baseValue, float[] curve);
    public ModifierPipeline ThenAttributeRef(ModifierKey key, float coefficient);
}
```

### ModifierResult — 计算结果

```csharp
// float 版本
public struct ModifierResult
{
    public float BaseValue;
    public float AddSum;
    public float MulProduct;
    public float? OverrideValue;
    public int Count;
}

public float FinalValue => OverrideValue ?? (BaseValue + AddSum) * MulProduct;

// 泛型版本
public struct ModifierResult<T>
{
    public T BaseValue;
    public T FinalValue;
    public int Count;
    public Span<ModifierSourceEntry> Sources;
    public int SourceCount;
}
```

### IModifierHandler<T> — 处理器

```csharp
// 数值型处理器
var handler = new NumericModifierHandler();
var result = calculator.Calculate(modifiers, baseValue, handler, context);

// 技能 ID 处理器
var skillHandler = new SkillIdModifierHandler();
var result = calculator.Calculate(modifiers, defaultSkillId, skillHandler, context);

// 布尔处理器
var boolHandler = new BooleanModifierHandler();
var result = calculator.Calculate(modifiers, false, boolHandler, context);
```

### ModifierStacking — 叠加逻辑

```csharp
// 创建聚合叠加组（最多 5 层）
var stack = ModifierStacking.CreateAggregate(
    stackKey: ModifierKey.HOTHeal,
    maxStack: 5,
    entry: ModifierData.Add(key, 50f)
);

// 添加一层
stack.TryPush(newEntry);

// 计算叠加后的值
float result = stack.CalculateStackedValue(baseValue: 100f);
```

### ModifierCalculator — 计算引擎

```csharp
var calculator = new ModifierCalculator();

// 基础计算
var result = calculator.Calculate(modifiers, baseValue: 500f);

// 指定等级
var result = calculator.Calculate(modifiers, baseValue: 500f, level: 5);

// 追踪来源（零 GC）
var recorder = new DefaultRecorder(capacity: 16);
var result = calculator.Calculate(modifiers, baseValue, recorder, level: 5, captureDelegate);

// 批量计算
Span<float> bases = stackalloc float[3] { 100f, 200f, 300f };
Span<ModifierResult> results = stackalloc ModifierResult[3];
calculator.CalculateBatch(modifiers, bases, level: 5, context, results);

// 缓存控制
calculator.EnableCache = false;
calculator.Invalidate();
```

### IModifierHandler<T> — 泛型处理器

```csharp
// 数值型处理器
var handler = new NumericModifierHandler();
var result = handler.Apply(100f, modifier, context);

// 整数型处理器
var intHandler = new IntModifierHandler();

// 布尔型处理器
var boolHandler = new BooleanModifierHandler();

// 枚举型处理器
var enumHandler = new EnumModifierHandler<SkillPhase>();
```

---

## 使用指南

### 场景 1：属性系统

```csharp
public class AttributeSystem : IModifierContext
{
    private Dictionary<ModifierKey, float> _attributes = new();
    private ModifierCalculator _calculator = new();

    public float GetAttribute(ModifierKey key)
        => _attributes.TryGetValue(key, out var v) ? v : 0f;

    public float Level => _currentLevel;

    public void Recalculate(ModifierKey attrKey, float baseValue)
    {
        var modifiers = _modifierSource.GetModifiers(attrKey);
        var result = _calculator.Calculate(modifiers, baseValue, null, Level, GetAttribute);
        _attributes[attrKey] = result.FinalValue;
    }
}
```

### 场景 2：技能修改器

```csharp
public class SkillModifierSystem
{
    private SkillIdModifierHandler _handler = new();
    private ModifierCalculator _calculator = new();

    // 强制使用特定技能
    public void ForceSkill(int sourceId, int skillId)
    {
        var mod = ModifierData.Custom(
            ModifierKey.Create(ModifierKey.Categories.Skill),
            ModifierOp.Override,
            CustomModifierData.SkillId(skillId),
            sourceId
        );
        _modifiers.Add(mod);
    }
}
```

### 场景 3：布尔状态

```csharp
public class StatusSystem
{
    private BooleanModifierHandler _handler = new();
    private ModifierCalculator _calculator = new();

    // 无敌状态
    public void AddInvincible(int sourceId)
    {
        var mod = ModifierData.Override(
            ModifierKey.Create(ModifierKey.Categories.Status),
            1f,  // true
            sourceId
        );
        _modifiers.Add(mod);
    }
}
```

---

## 与 GAS 对比

| GAS 特性 | AbilityKit.Modifiers | 说明 |
|---------|---------------------|------|
| `UAttributeSet` | 业务层自管 | 属性存储和修改器来源由业务层决定 |
| `FGameplayEffectModifier` | `ModifierData` | 修改器数据结构 |
| `FScalableFloat` | `MagnitudeSource` (LevelCurve) | 等级曲线支持 |
| `FAttributeBasedMagnitude` | `MagnitudeSource` (Attribute) | 基于属性计算 |
| `FGameplayEffectStackingModule` | `ModifierStacking` | 叠加逻辑 |
| `SourceTags` / `TargetTags` | 业务层自管 | 标签条件过滤暂未实现 |
| `EvaluationChannel` | 业务层自管 | 评估通道暂未实现 |
| **Modifiers 计算逻辑** | 完整实现 | 核心计算算法一致 |
| **Handler 扩展** | 支持 | GAS 无对应功能 |
| **修饰器管道** | 支持 | 链式组合复杂数值变换 |

### 简化点

- 不强制依赖 `AbilitySystemComponent`
- 不使用 `FGameplayEffectContext` 追踪复杂上下文
- 不实现完整的 GameplayEffect 生命周期

### 优势

- **泛型 Handler**：支持任意类型的值，不限于 float
- **零 GC**：所有公共 API 返回值均为值类型
- **无反射**：纯 C# struct，可用于 Burst 编译
- **轻量**：无 Unity 依赖，可单独使用
- **可定制**：业务层完全控制存储和生命周期
- **修饰器管道**：支持复杂的数值变换组合

------

## 性能考量

### 1. 零 GC 设计

- `ModifierResult<T>`、`ModifierData` 均为 `struct`
- `IModifierRecorder` 接口驱动的来源追踪，不创建 `List<T>`
- `Span<T>` 零拷贝访问

### 2. JIT 内联优化

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public float Apply(float baseValue, in ModifierData modifier, IModifierContext context)
```

### 3. 缓存策略

内置缓存，基于：
- 修改器数量
- 修改器首尾哈希
- BaseValue

检测变化时快速失效。

### 4. Burst 兼容

纯值类型，无 `List<T>`、`string`、`UnityEngine.Object`，理论上可编译为 Burst 作业。

---

## 策略模式（Strategy）

### 设计背景

原有的 `ModifierOp` 枚举定义了基础的数值操作（Add、Mult、Override），但游戏中的修改需求远不止数值：

- 状态修改（保存原始值、设置新值、还原）
- 标签管理（添加、移除、切换）
- 列表操作（增、删、改）
- 技能ID修改
- 碰撞参数修改

如果继续在框架层扩展枚举，会导致：
1. 违反开闭原则（OCP）
2. 业务包无法在不修改框架的情况下扩展
3. 枚举膨胀，难以维护

### 设计思路

使用**策略模式**替代枚举扩展：

```
┌─────────────────────────────────────────────────────────────────┐
│                     策略模式架构                                    │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  框架层定义：                                                    │
│  ├── IStrategy (接口) - 定义策略契约                              │
│  ├── IStrategyRegistry (接口) - 管理策略注册                       │
│  ├── StrategyContext (结构) - 携带执行数据                         │
│  └── StrategyData (结构) - 可序列化的配置                         │
│                                                                  │
│  业务层实现：                                                    │
│  ├── IStrategy 实现类 - 定义具体修改逻辑                           │
│  └── 注册到 IStrategyRegistry                                    │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 核心接口

```csharp
/// <summary>
/// 策略接口 - 框架定义契约，业务层实现
/// </summary>
public interface IStrategy
{
    StrategyId StrategyId { get; }
    string Description { get; }

    // 应用策略
    StrategyApplyResult Apply(object target, in StrategyContext context);

    // 还原策略
    StrategyRevertResult Revert(object target, in StrategyContext context);

    // 计算数值（用于数值类策略）
    T Calculate<T>(T baseValue, in StrategyContext context);
}

/// <summary>
/// 策略注册表 - 业务层注册策略
/// </summary>
public interface IStrategyRegistry
{
    void Register(IStrategy strategy);
    bool TryGet(StrategyId strategyId, out IStrategy strategy);
    IReadOnlyList<IStrategy> GetAll();
}
```

### 内置策略

框架提供默认实现，业务层可直接使用或替换：

| 策略ID | 说明 | 实现类 |
|--------|------|--------|
| `numeric.add` | 数值加法 | `NumericAddStrategy` |
| `numeric.mult` | 数值乘法 | `NumericMultStrategy` |
| `numeric.override` | 数值覆盖 | `NumericOverrideStrategy` |
| `numeric.percent` | 百分比加成 | `NumericPercentStrategy` |
| `state.set` | 状态保存并设置 | `StateSetStrategy` |
| `state.restore` | 状态还原 | `StateRestoreStrategy` |
| `tag.add` | 添加标签 | `TagAddStrategy` |
| `tag.remove` | 移除标签 | `TagRemoveStrategy` |

### 状态修改示例

```csharp
// 1. 注册策略
var registry = StrategyExtensions.CreateDefaultRegistry();

// 2. 创建状态修改策略数据
var data = StrategyData.State(
    strategyId: "state.set",
    op: StrategyOperationKind.SaveAndSet,
    stateKey: "MovementMode",
    value: "Ghost",
    ownerKey: contextId
);

// 3. 执行策略
var executor = new StrategyExecutor(registry);
var result = executor.Execute(target, in data);

// 4. 还原（按 OwnerKey）
executor.RevertByOwner(target, ownerKey);
```

### 与原有系统的兼容性

ModifierData 新增 `StrategyData` 和 `Magnitude` 字段：

```csharp
public struct ModifierData
{
    // 原有字段
    public ModifierOp Op;
    // ...

    // 新增：数值来源策略（替代 MagnitudeType 枚举）
    public MagnitudeStrategyData Magnitude;
    public bool HasMagnitude => !string.IsNullOrEmpty(Magnitude.StrategyId);

    // 新增：通用策略数据
    public StrategyData StrategyData;
    public bool HasStrategyData => !string.IsNullOrEmpty(StrategyData.StrategyId);
}
```

---

## 数值来源策略（Magnitude Strategy）

### 设计背景

原有的 `MagnitudeType` 枚举定义了基础的数值来源类型：
- `None` - 固定值
- `ScalableFloat` - 等级曲线
- `AttributeBased` - 基于属性

但业务层可能需要自定义数值来源：
- 公式计算
- 外部数据引用
- 动态计算
- 随机值
- 复杂条件判断

### 设计思路

使用**数值来源策略**替代 `MagnitudeType` 枚举：

```
┌─────────────────────────────────────────────────────────────────┐
│               数值来源策略架构                                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  框架层定义：                                                    │
│  ├── IMagnitudeStrategy (接口) - 定义数值来源契约                 │
│  ├── IMagnitudeStrategyRegistry (接口) - 管理策略注册             │
│  └── MagnitudeStrategyData (结构) - 可序列化的配置               │
│                                                                  │
│  内置策略：                                                      │
│  ├── "fixed" - 固定值                                            │
│  ├── "scalable" - 等级曲线                                       │
│  ├── "attribute" - 属性引用                                      │
│  └── "formula" - 公式计算                                        │
│                                                                  │
│  业务层扩展：                                                    │
│  └── 自定义 IMagnitudeStrategy 实现                               │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 核心接口

```csharp
/// <summary>
/// 数值来源策略接口
/// </summary>
public interface IMagnitudeStrategy
{
    MagnitudeStrategyId StrategyId { get; }
    string Description { get; }

    /// <summary>
    /// 计算数值
    /// </summary>
    float Calculate(float level, IModifierContext context);

    /// <summary>
    /// 获取原始数值
    /// </summary>
    float GetBaseValue();
}

/// <summary>
/// 数值来源策略注册表
/// </summary>
public interface IMagnitudeStrategyRegistry
{
    void Register(IMagnitudeStrategy strategy);
    bool TryGet(MagnitudeStrategyId strategyId, out IMagnitudeStrategy strategy);
}
```

### 内置数值来源策略

| 策略ID | 说明 | 实现类 |
|--------|------|--------|
| `fixed` | 固定值 | `FixedMagnitudeStrategy` |
| `scalable` | 等级曲线 | `ScalableMagnitudeStrategy` |
| `attribute` | 属性引用 | `AttributeMagnitudeStrategy` |
| `formula` | 公式计算 | `FormulaMagnitudeStrategy` |

### 使用示例

```csharp
// 1. 创建注册表
var registry = MagnitudeStrategyRegistry.CreateDefault();

// 2. 注册自定义策略（业务层）
registry.Register(new RandomMagnitudeStrategy(seed: 12345));

// 3. 创建修改器
var mod = ModifierData.MagnitudeStrategy(
    key: ModifierKey.Damage,
    op: ModifierOp.Add,
    magnitude: new MagnitudeStrategyData
    {
        StrategyId = "random",
        BaseValue = 100f,
        Parameters = new[] { 0.8f, 1.2f }  // 80%~120%
    },
    sourceId: buffId
);

// 4. 计算数值
var result = calculator.Calculate(new[] { mod }, baseValue: 1000f);
```

### 业务层扩展示例

```csharp
[MagnitudeStrategyImpl("cooldown_scaling")]
public sealed class CooldownScalingStrategy : IMagnitudeStrategy
{
    public MagnitudeStrategyId StrategyId => new("cooldown_scaling");
    public string Description => "Cooldown scales with ability power";

    // 公式：BaseCooldown / (1 + AbilityPower * 0.01)
    public float Calculate(float level, IModifierContext context)
    {
        var baseCooldown = BaseValue;
        var abilityPower = context.GetAttribute(ModifierKey.AbilityPower);
        return baseCooldown / (1f + abilityPower * 0.01f);
    }

    public float GetBaseValue() => BaseValue;
    public float BaseValue { get; set; }
}
```

// 工厂方法
public static ModifierData StateStrategy(
    ModifierKey key,
    string stateKey,
    object stateValue,
    long ownerKey,
    int sourceId = 0)
{
    return new ModifierData
    {
        Key = key,
        Op = ModifierOp.Custom,
        StrategyData = StrategyData.State("state.set", ...),
        // ...
    };
}
```

### 设计优势

1. **开闭原则**：框架定义契约，业务层实现，无需修改框架即可扩展
2. **统一抽象**：数值、状态、标签、列表等都用同一套模式处理
3. **配置驱动**：`StrategyData` 可序列化，适合存储在配置文件中
4. **生命周期管理**：`IStrategyRepository` 支持按 `OwnerKey` 批量还原
5. **向后兼容**：原有 `ModifierOp` 枚举仍然可用

---

## 架构演进

### 从 v1.0 到 v2.0 的重构

#### 重构前的问题

1. **数值来源耦合在 ModifierData 中**
   - 每种来源类型都需要在 ModifierData 中添加字段
   - 新增来源类型需要修改数据结构

2. **操作类型硬编码**
   - Add/Mul/Override 等操作逻辑直接写死
   - 业务层无法自定义操作类型

3. **缺少组合抽象**
   - 无法表达"时间衰减 + 等级曲线"的组合效果
   - 复杂的数值变换需要手动拼接

#### 重构后的改进

1. **MagnitudeSource 统一抽象**
   - 所有数值来源类型统一为 MagnitudeSource
   - 支持固定值、时间衰减、等级曲线、属性引用、修饰器管道
   - 可扩展：业务层可实现自定义 MagnitudeSource

2. **IModifierOperator 操作接口**
   - 操作逻辑从枚举硬编码变为接口实现
   - 支持自定义操作注册到 ModifierOperatorRegistry
   - 职责分离：Operator 只负责"如何计算"

3. **ModifierPipeline 组合抽象**
   - 多个 IMagnitudeModifier 可以链式组合
   - 支持时间衰减 + 等级曲线 + 属性引用的复杂组合
   - 可序列化：MagnitudePipelineData 支持存储到配置

#### 重构后的架构图

```
┌─────────────────────────────────────────────────────────────────┐
│                     重构后的修饰器架构                                │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─────────────┐    ┌──────────────────┐    ┌─────────────────┐  │
│  │ModifierData │───▶│ MagnitudeSource  │───▶│  IMagnitudeModifier │
│  └─────────────┘    └──────────────────┘    └─────────────────┘  │
│         │                  │                         │            │
│         │                  │                         ▼            │
│         │                  │            ┌─────────────────────┐   │
│         │                  │            │  ModifierPipeline   │   │
│         │                  │            │  (可组合多个修饰器)   │   │
│         │                  │            └─────────────────────┘   │
│         │                  │                                        │
│         ▼                  ▼                                        │
│  ┌─────────────┐    ┌──────────────────┐                           │
│  │ModifierOp   │───▶│IModifierOperator │                           │
│  └─────────────┘    └──────────────────┘                           │
│                             │                                        │
│                             ▼                                        │
│                    ┌──────────────────┐                              │
│                    │OperatorRegistry   │                              │
│                    │(可注册自定义操作)  │                              │
│                    └──────────────────┘                              │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 设计优势

1. **开闭原则（OCP）**
   - 框架定义契约，业务层实现
   - 无需修改框架即可扩展数值来源和操作类型

2. **统一抽象**
   - 数值、状态、标签、列表等都用同一套模式处理
   - ModifierPipeline 支持复杂的组合逻辑

3. **零 GC 设计**
   - 所有公共 API 返回值均为值类型
   - 使用 Span<T> 进行批处理
   - 内置缓存避免重复计算

4. **可序列化**
   - MagnitudeSource 可存储到配置文件
   - MagnitudePipelineData 支持修饰器管道序列化

### 计划中

| 特性 | 优先级 | 说明 |
|------|--------|------|
| SourceTags / TargetTags | P1 | 来源/目标标签条件过滤 |
| EvaluationChannel | P2 | 评估通道和优先级 |
| ModifierSnapshot | P2 | 快照缓存，支持更可靠的变更检测 |

### 暂不计划

| 特性 | 原因 |
|------|------|
| GameplayEffect 生命周期 | 业务层自行实现更灵活 |
| 条件修改器（当 X 时+Y） | 复杂度过高，可由业务层包装 |
| 完整网络同步 | 由 AbilityKit.Network 包处理 |

---

## 附录

### A. 命名对照

| AbilityKit.Modifiers | GAS | 说明 |
|---------------------|-----|------|
| `ModifierData` | `FGameplayEffectModifier` | 修改器数据 |
| `ModifierKey` | `FGameplayTag` | 键 |
| `ModifierOp` | `EGameplayMod` | 操作类型 |
| `IModifierHandler<T>` | — | 修改器处理器（本框架独有） |
| `ScalableFloat` | `FScalableFloat` | 可缩放浮点值 |
| `AttributeBasedMagnitude` | `FAttributeBasedMagnitude` | 基于属性的数值 |
| `ModifierStacking` | `FGameplayEffectStackingModule` | 叠加模块 |
| `ModifierResult<T>` | `FCalculatedAttribute` | 计算结果 |
| `ModifierCalculator` | `UAbilitySystemComponent::EvaluateAttributes` | 计算引擎 |

### B. 参考资料

- [GAS Documentation - Attribute Modifiers](https://github.com/tranek/GASDocumentation)
- [Gameplay Effects and Attribute Modifiers - Epic Games](https://docs.unrealengine.com/5.3/en-US/gameplay-effects-and-attribute-modifiers-in-unreal-engine/)

