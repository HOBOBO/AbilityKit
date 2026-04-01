# AbilityKit.Modifiers 设计文档

## 目录

- [背景与问题](#背景与问题)
- [设计目标](#设计目标)
- [核心概念](#核心概念)
- [模块结构](#模块结构)
- [核心类型详解](#核心类型详解)
- [使用指南](#使用指南)
- [与 GAS 对比](#与-gas-对比)
- [性能考量](#性能考量)
- [扩展方向](#扩展方向)

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
        ├── ModifierKey.cs              # 修改器键
        ├── ModifierOp.cs               # 操作类型枚举
        ├── ModifierData.cs             # 修改器数据结构 + MagnitudeSource
        ├── ModifierResult.cs           # 计算结果 + 来源追踪接口
        ├── ModifierStacking.cs         # 叠加逻辑
        ├── ModifierCalculator.cs       # 核心计算引擎
        ├── NumericModifierHandler.cs   # 默认数值型处理器 + 示例处理器
        └── IModifierHandler.cs         # Handler 接口 + Context 接口
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
    public ModifierKey Key;                    // 修改目标键
    public ModifierOp Op;                     // 操作类型
    public MagnitudeType MagnitudeSource;     // 数值来源类型
    public float Value;                       // 固定值
    public ScalableFloat ScalableValue;        // 等级曲线
    public AttributeBasedMagnitude AttrValue;  // 基于属性
    public int Priority;                       // 优先级
    public int SourceId;                       // 来源标识
    public int SourceNameIndex;                // 调试用
    public StackingConfig? Stacking;          // 叠加配置
    public CustomModifierData CustomData;      // 自定义数据
}
```

### 数值来源

支持三种数值计算方式：

#### 1. 固定值

```csharp
ModifierData.Add(key, 100f);  // 直接加 100
```

#### 2. ScalableFloat（等级曲线）

```csharp
var sf = new ScalableFloat
{
    BaseValue = 100f,
    Coefficient = 1f,
    Curve = new[] { 1f, 0.5f, 5f, 1.0f, 10f, 1.5f }
};

float value = sf.Calculate(level: 7);  // → 125
```

#### 3. AttributeBasedMagnitude（基于属性）

```csharp
var attrBased = new AttributeBasedMagnitude
{
    AttributeKey = ModifierKey.Create(1),  // 攻击力
    CaptureType = AttributeCaptureType.Current,
    Coefficient = 0.5f  // 50% 转化
};
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

// 泛型版本（使用 Handler）
var handler = new NumericModifierHandler();
var result = calculator.Calculate(modifiers, baseValue, handler, context, sources);
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
| `FScalableFloat` | `ScalableFloat` | 等级曲线支持 |
| `FAttributeBasedMagnitude` | `AttributeBasedMagnitude` | 基于属性计算 |
| `FGameplayEffectStackingModule` | `ModifierStacking` | 叠加逻辑 |
| `SourceTags` / `TargetTags` | 业务层自管 | 标签条件过滤暂未实现 |
| `EvaluationChannel` | 业务层自管 | 评估通道暂未实现 |
| **Modifiers 计算逻辑** | ✅ 完整实现 | 核心计算算法一致 |
| **Handler 扩展** | ✅ 支持 | GAS 无对应功能 |

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

---

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

## 扩展方向

### 已支持

| 特性 | 说明 |
|------|------|
| 泛型 Handler | 通过 IModifierHandler<T> 支持任意类型 |
| 自定义 Op | ModifierOp.Custom 可扩展 |
| CustomData | 用于存储非数值数据 |

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
