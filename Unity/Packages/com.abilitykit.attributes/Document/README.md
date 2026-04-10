# 属性系统模块文档索引

> Ability-Kit 属性系统模块官方文档

---

## 📚 文档列表

### 1. [属性系统模块开发设计文档](./属性系统模块开发设计文档.md)

**阅读对象**：首次接触属性系统模块的开发者

**内容概要**：
- 为什么需要属性系统模块（解决属性计算散落、buff管理混乱等问题）
- 核心概念：Context、Group、Modifier、Formula、Constraint
- 架构图和完整计算流程
- 设计模式总结
- 适用场景说明

**推荐阅读顺序**：从本文档开始

---

## 🎯 快速入门

### 想了解属性系统是什么？

👉 阅读 [属性系统模块开发设计文档](./属性系统模块开发设计文档.md) 第一章「设计理念」

### 想学习如何使用？

👉 阅读 [属性系统模块开发设计文档](./属性系统模块开发设计文档.md) 第六章「使用指南」

### 想了解修饰器叠加规则？

👉 阅读 [属性系统模块开发设计文档](./属性系统模块开发设计文档.md) 第四章「核心组件详解」

---

## 📖 与 AbilityKit.Modifiers 的集成

属性系统基于 AbilityKit.Modifiers 构建，享受统一的修改器计算框架。

### 架构关系

```
┌─────────────────────────────────────────────────────┐
│              AbilityKit.Attributes                    │
│  ┌─────────────────────────────────────────────┐  │
│  │ 业务层友好的抽象：                               │  │
│  │ - AttributeContext  ← 属性存储、生命周期        │  │
│  │ - AttributeRegistry  ← 属性注册管理            │  │
│  │ - AttributeDef  ← 属性定义                    │  │
│  │ - IAttributeFormula  ← 公式接口                │  │
│  │ - IAttributeConstraint  ← 约束接口            │  │
│  │ - AttributeEffect  ← 便捷工厂（使用 ModifierData）│  │
│  └─────────────────────────────────────────────┘  │
│                       ↓ 使用                        │
│  ┌─────────────────────────────────────────────┐  │
│  │           AbilityKit.Modifiers               │  │
│  │  ModifierData    ← 通用数据结构              │  │
│  │  ModifierResult  ← 计算结果                  │  │
│  │  ModifierCalculator ← 通用计算引擎            │  │
│  │  ModifierKey    ← 修改器键                  │  │
│  └─────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────┘
```

### 重构说明

> **v0.0.2 重构说明**：attributes 模块已降级为 modifiers 模块的使用者，不再包含独立的修改器计算逻辑。

| 旧版（已废弃） | 新版 |
|---------------|------|
| `AttributeModifier` | 直接使用 `ModifierData` |
| `AttributeModifierSet` | 直接使用 `ModifierResult` |
| `AttributeEffectHandle` | 使用 `int` (SourceId) |
| 双分支计算逻辑 | 统一使用 `ModifierCalculator` |

---

## 📖 概念速查

### 核心类

| 类 | 职责 |
|------|------|
| `Attributes` | 静态门面类，提供简单 API |
| `AttributeRegistry` | 属性注册表，管理所有属性定义 |
| `AttributeContext` | 属性上下文，管理实体的所有属性 |
| `AttributeGroup` | 属性组，管理一组相关属性 |
| `AttributeInstance` | 属性实例，具体的属性值 |
| `AttributeEffect` | 效果，使用 ModifierData 的便捷工厂 |

### 修改器操作

| 操作 | 说明 |
|------|------|
| `ModifierOp.Add` | 直接加到基础值 |
| `ModifierOp.Mul` | 乘法叠加 |
| `ModifierOp.PercentAdd` | 百分比加成 |
| `ModifierOp.Override` | 强制覆盖 |

### 扩展接口

| 接口 | 职责 |
|------|------|
| `IAttributeFormula` | 自定义计算公式 |
| `IAttributeConstraint` | 自定义取值约束 |

### 计算公式

```
value = (Base + AddSum) * MulProduct
value = OverrideValue (如果有 Override)
```

---

## 🔗 相关文档

- [修改器模块](../com.abilitykit.modifiers/README.md) - 通用修改器计算框架
- [实体管理模块](../com.abilitykit.combat.entitymanager/Document/) - 实体查询系统
- [技能库模块](../com.abilitykit.combat.skilllibrary/Document/) - 技能数据管理
- [能力管线模块](../com.abilitykit.pipeline/Document/能力管线模块开发设计文档.md) - 技能执行

---

## 💡 典型使用场景

| 场景 | 说明 |
|------|------|
| RPG 属性系统 | HP、攻击、防御等基础属性 |
| MOBA 战斗系统 | 攻击力加成、护甲、魔抗等 |
| Buff/Debuff 系统 | 各种属性增益/减益 |
| 装备系统 | 装备属性叠加 |
| 天赋系统 | 天赋点对属性的影响 |

---

## 📁 源码路径

```
com.abilitykit.attributes/Runtime/Ability/Share/Common/AttributeSystem/
├── Attributes.cs                        # 静态门面类
├── AttributeId.cs                       # 属性ID
├── AttributeDef.cs                      # 属性定义
├── AttributeContext.cs                  # 属性上下文（实现 IModifierContext）
├── AttributeGroup.cs                    # 属性组
├── AttributeInstance.cs                  # 属性实例（使用 ModifierCalculator）
├── AttributeEffect.cs                   # 效果（使用 ModifierData）
├── AttributeRegistry.cs                  # 注册表
├── IAttributeFormula.cs                 # 公式接口（使用 ModifierResult）
├── DefaultAttributeFormula.cs           # 默认公式
├── AttributeExpressionFormula.cs        # 表达式公式
├── IAttributeConstraint.cs              # 约束接口
├── RangeAttributeConstraint.cs          # 范围约束
├── IAttributeDependencyProvider.cs      # 依赖提供者
├── IAttributeProvider.cs               # 属性提供者接口
└── IAttributeConstraint.cs              # 约束接口
```

---

*最后更新：2026-04-09*
