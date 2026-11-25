# AbilityKit Attributes

> 文档类型：包入口与首次使用
> 核校状态：2026-11-25 核对包依赖、注册、上下文、修改器和静默求值契约；.NET 相关测试 13 项通过，未验证 Unity Player。

`com.abilitykit.attributes` 提供属性定义、基础值、修饰器、公式与约束。它不负责玩法 Buff 生命周期、实体绑定或状态恢复编排。

## 安装与接入

从项目配置的包来源安装本包；Unity 版本及 `com.abilitykit.core`、`com.abilitykit.modifiers` 依赖以 [`package.json`](package.json) 为准。

在应用启动时注册一次；应用完成所有属性注册后统一调用 `AttrRegistry.Freeze()`，各运行对象使用各自的 `AttributeContext`。

```csharp
using AbilityKit.Attributes;
using AbilityKit.Attributes.Core;
using AbilityKit.Modifiers;

public static class AttributeQuickStart
{
    public static AttributeId RegisterAttack() =>
        AttrRegistry.Register(new AttributeDef("Battle.Attack", "Battle", 100f));

    public static float ReadAttack(AttributeId attack)
    {
        var attributes = new AttributeContext();
        attributes.SetBase(attack, 100f);
        attributes.AddModifier(attack, ModifierOp.Add, 20f);
        return attributes.GetValue(attack);
    }
}
```

`ReadAttack` 返回 `120`。

注册表生命周期、修改器句柄和静默实时读取边界见[包内使用契约](Documentation~/Usage.md)。

## 文档

- [使用契约与限制](Documentation~/Usage.md)：注册表、修改器和静默实时求值。
- [历史文档索引](Document/README.md)：保留旧路径；设计背景不替代现行契约。
- [变更记录](CHANGELOG.md)。
