# AbilityKit.Modifiers

通用修改器计算框架，对标 GAS 的 Attribute Modifiers 机制，并支持泛化扩展。

## 核心职责

只做一件事：**将修改器数组应用到基础值上，产生计算结果**。

但不仅限于属性系统——通过 `IModifierHandler<T>` 接口，可以处理任意类型：
- 数值（float）
- 技能 ID（int）
- 布尔状态（bool）
- 自定义结构体

## 设计原则

- **泛型 Handler**：通过 `IModifierHandler<T>` 支持任意类型的值
- **无 GC**：所有返回值均为值类型，不含 List/Array/String 等堆分配类型
- **业务层决定存储**：修改器放在哪、怎么组织、怎么按 SourceId 批量移除，都是调用方的事

## 模块结构

```
AbilityKit.Modifiers/
└── Runtime/
    └── Core/
        ├── ModifierKey.cs              # 修改器键
        ├── ModifierOp.cs                # 操作类型
        ├── ModifierData.cs              # 修改器数据（含 MagnitudeSource）
        ├── ModifierResult.cs            # 计算结果 + 来源追踪接口
        ├── ModifierStacking.cs          # 叠加逻辑
        ├── ModifierCalculator.cs        # 计算引擎
        ├── NumericModifierHandler.cs    # 默认数值型处理器 + 示例处理器
        └── IModifierHandler.cs          # Handler 接口 + Context 接口
```

## 核心类型

### ModifierKey

键用于分类修改器。32 位压缩存储 = `[Reserved:8][Custom:8][SubCategory:8][Category:8]`

```csharp
// 预定义
ModifierKey.ShieldMax    // 护盾最大值
ModifierKey.MoveSpeed   // 移动速度
ModifierKey.DOTDamage   // DOT 伤害

// 自定义
var key = ModifierKey.Create(categoryId, subCategoryId, customId);

// 业务层可扩展分类
ModifierKey.Categories.Projectile = 30;
ModifierKey.Categories.AOE = 31;
```

### ModifierOp

| 操作 | 说明 | 公式 |
|------|------|------|
| `Add` | 加法叠加 | Base + A |
| `Mul` | 乘法叠加 | Base × M |
| `Override` | 覆盖 | 直接替换 |
| `PercentAdd` | 百分比加成 | Base × (1 + %) |
| `Custom` | 自定义 | 由 Handler 决定 |

**计算优先级**：Override > Mul > Add/PercentAdd

### ModifierData

```csharp
// 固定值
ModifierData data = ModifierData.Add(key, 100f, sourceId: 1);
data = ModifierData.Mul(key, 1.2f, sourceId: 2);

// 可缩放值（等级曲线）
data = ModifierData.AddScalable(key, new ScalableFloat { BaseValue = 100f, Curve = new[] { 1f, 0.5f, 5f, 1.0f, 10f, 1.5f } }, sourceId: 3);

// 基于属性
var attrBased = new AttributeBasedMagnitude { AttributeKey = ModifierKey.Create(10), CaptureType = AttributeCaptureType.Current, Coefficient = 0.5f };
data = ModifierData.AddAttributeBased(key, attrBased, sourceId: 4);

// 自定义修改器
data = ModifierData.Custom(key, ModifierOp.Custom, CustomModifierData.SkillId(999), sourceId: 5);

// 获取生效数值
float value = data.GetMagnitude(level: 5, context);
```

### IModifierHandler<T> — 处理器

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

```csharp
// 数值型（默认）
var handler = new NumericModifierHandler();
var result = calculator.Calculate(modifiers, baseValue, handler, context);

// 技能 ID
var skillHandler = new SkillIdModifierHandler();
var result = calculator.Calculate(modifiers, defaultSkillId, skillHandler, context);

// 布尔状态
var boolHandler = new BooleanModifierHandler();
var result = calculator.Calculate(modifiers, false, boolHandler, context);
```

### ScalableFloat（等级曲线）

```csharp
var sf = new ScalableFloat
{
    BaseValue = 100f,
    Coefficient = 1f,
    Curve = new[] { 1f, 0.5f, 5f, 1.0f, 10f, 1.5f }  // 1级0.5x，5级1.0x，10级1.5x
};

float value = sf.Calculate(level: 7);  // 插值计算
```

### AttributeBasedMagnitude（基于属性）

```csharp
var attrBased = new AttributeBasedMagnitude
{
    AttributeKey = ModifierKey.Create(10),  // 攻击力属性
    CaptureType = AttributeCaptureType.Current, // 当前值
    Coefficient = 0.5f  // 50% 转化
};

float value = attrBased.Calculate(key => GetAttribute(key));  // 返回 攻击力 × 0.5
```

### ModifierStacking（叠加）

```csharp
var stack = ModifierStacking.CreateAggregate(
    stackKey: ModifierKey.Create(100),
    maxStack: 5,
    initialCount: 1,
    entry: ModifierData.Add(key, 50f)
);

// 添加叠加
stack.TryPush(newEntry);

// 计算叠加后的值
float final = stack.CalculateStackedValue(baseValue: 100f);

// 展开为修改器数组
var modifiers = new ModifierData[5];
int count = stack.ExpandTo(modifiers);
```

### ModifierResult

```csharp
// float 版本
ModifierResult result = calculator.Calculate(modifiers, baseValue: 500f, level: 5, captureDelegate);
float finalValue = result.FinalValue;
float addSum = result.AddSum;
float mulProduct = result.MulProduct;
float percentChange = result.PercentChange;

// 泛型版本
ModifierResult<T> result = calculator.Calculate(modifiers, baseValue, handler, context, sources);
T finalValue = result.FinalValue;
```

### 来源追踪（零 GC）

```csharp
var recorder = new DefaultRecorder(capacity: 16);
var result = calculator.Calculate(modifiers, baseValue, recorder, level: 5, captureDelegate);

for (int i = 0; i < recorder.Count; i++)
{
    ref readonly var entry = ref recorder.GetEntry(i);
    Console.WriteLine($"{entry.Op} {entry.Value} from Src#{entry.SourceId}");
}
```

## 使用方式

```csharp
// 1. 业务层自己管理修改器列表
var modifiers = GetModifiersForKey(key);

// 2. 调用计算器
var calculator = new ModifierCalculator();
var result = calculator.Calculate(modifiers, baseValue: 500f, level: 1, captureDelegate);

// result = (500 + AddSum) × MulProduct = FinalValue

// 3. 泛型版本（处理非数值类型）
var skillHandler = new SkillIdModifierHandler();
var result = calculator.Calculate(modifiers, defaultSkillId, skillHandler, context);
```

## 与 GAS 的对比

| GAS 特性 | 当前实现 |
|---------|---------|
| AttributeSet / DataRegistry | 业务层自管 |
| GameplayEffect.Modifiers[] | ModifierData[] |
| ScalableFloat | ✅ ScalableFloat |
| AttributeBasedMagnitude | ✅ AttributeBasedMagnitude |
| Stacking | ✅ ModifierStacking |
| SourceTags / TargetTags | 业务层自管 |
| EvaluationChannel | 业务层自管 |
| **Modifiers 计算逻辑** | ✅ 核心实现 |
| **Handler 扩展** | ✅ 支持（GAS 无对应功能） |

---

## 扩展示例

### 自定义技能 ID 处理器

```csharp
public struct SkillIdModifierHandler : IModifierHandler<int>
{
    public int Apply(int baseValue, in ModifierData modifier, IModifierContext context)
    {
        if (modifier.Op == ModifierOp.Override && modifier.CustomData.CustomTypeId == 1)
        {
            return modifier.CustomData.IntValue;  // 强制使用指定技能
        }
        return baseValue;
    }

    public int Compare(int a, int b) => a.CompareTo(b);
    public int Combine(in Span<int> values) => values.Length > 0 ? values[0] : 0;
}

// 使用
var handler = new SkillIdModifierHandler();
var mod = ModifierData.Custom(
    ModifierKey.Create(ModifierKey.Categories.Skill),
    ModifierOp.Override,
    CustomModifierData.SkillId(999)
);
```
