# Marker 标记系统

## 概述

`Marker` 是 AbilityKit 框架提供的统一类型标记机制。通过在类型上标记自定义 Attribute，框架可自动识别并处理这些类型，支持自动注册、代码生成、编辑器工具等多种场景。

## 核心组件

| 文件 | 说明 |
|------|------|
| `MarkerAttribute.cs` | 基类 Attribute，所有框架标记 Attribute 都应继承此类 |
| `IMarkerRegistry.cs` | Registry 接口 |
| `MarkerRegistry.cs` | 基于类型的注册表，直接存储所有扫描到的 Type |
| `KeyedMarkerRegistry.cs` | 基于 Key-Type 的注册表，通过 Key（如 string）查找 Type |
| `MarkerScanner.cs` | 扫描器，扫描程序集并调用 Attribute.OnScanned |

## 命名空间

```
AbilityKit.Common.Marker
```

## 使用方式

### 1. 框架层：定义 Attribute + Registry

```csharp
namespace AbilityKit.Ability.Triggering
{
    public sealed class TriggerActionRegistry : KeyedMarkerRegistry<string, TriggerActionAttribute>
    {
        public static readonly TriggerActionRegistry Instance = new();
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class TriggerActionAttribute : MarkerAttribute
    {
        public string ActionType { get; }

        public TriggerActionAttribute(string actionType)
        {
            ActionType = actionType;
        }

        public override void OnScanned(Type implType, IMarkerRegistry registry)
        {
            if (registry is TriggerActionRegistry r)
            {
                r.Register(ActionType, implType);
            }
        }
    }
}
```

### 2. 业务层：标记类型

```csharp
namespace MyGame.Skills.Actions
{
    [TriggerAction("damage_apply")]
    public sealed class DamageApplyAction : ITriggerAction
    {
        public void Execute(TriggerContext ctx) { /* ... */ }
    }

    [TriggerAction("heal_apply")]
    public sealed class HealApplyAction : ITriggerAction
    {
        public void Execute(TriggerContext ctx) { /* ... */ }
    }
}
```

### 3. 启动时扫描

```csharp
public static class TriggeringBootstrap
{
    public static void RegisterAll()
    {
        MarkerScanner<TriggerActionAttribute>.ScanAll(
            TriggerActionRegistry.Instance);
    }
}
```

### 4. 运行时使用

```csharp
if (TriggerActionRegistry.Instance.TryGet("damage_apply", out var type))
{
    var action = (ITriggerAction)Activator.CreateInstance(type);
    action.Execute(context);
}
```

## 设计原则

1. **框架定义模式，业务使用**：框架层定义 Attribute + Registry 模式，业务包只需标记类型
2. **处理逻辑在 Attribute.OnScanned**：框架层在 Attribute 子类中实现具体处理逻辑
3. **统一扫描入口**：通过 `MarkerScanner<TAttr>.Scan()` 一次性完成扫描
4. **零运行时开销**：扫描在启动时完成，运行时仅是普通的字典查找

## 适用场景

| 场景 | 推荐 Registry |
|------|-------------|
| 所有实现类型都同等重要，只需遍历 | `MarkerRegistry<TAttr>` |
| 需要通过 Key（如 string）查找实现 | `KeyedMarkerRegistry<TKey, TAttr>` |
| Key 是 string（如 actionType） | `KeyedMarkerRegistry<string, TAttr>` |
| Key 是 int（如枚举值） | `KeyedMarkerRegistry<int, TAttr>` |
