# 数值系统模块文档索引

> Ability-Kit 数值系统模块官方文档

---

## 📚 文档列表

### 1. [数值系统模块开发设计文档](./数值系统模块开发设计文档.md)

**阅读对象**：首次接触数值系统的开发者

**内容概要**：
- 数值系统 vs 属性系统的关系（互补而非互斥）
- 核心概念：NumberValue、Modifier、Handle、Effect
- 架构图和完整计算流程
- 设计模式总结
- 适用场景说明

**推荐阅读顺序**：从本文档开始

---

## 🎯 快速入门

### 想了解数值系统是什么？

👉 阅读 [数值系统模块开发设计文档](./数值系统模块开发设计文档.md) 第一章「设计理念」

### 想学习如何使用？

👉 阅读 [数值系统模块开发设计文档](./数值系统模块开发设计文档.md) 第六章「使用指南」

### 想了解与属性系统的关系？

👉 阅读 [数值系统模块开发设计文档](./数值系统模块开发设计文档.md) 第七章「与属性系统的关系」

---

## 📖 概念速查

### 核心类

| 类 | 职责 |
|------|------|
| `NumberValue` | 数值容器，管理基础值和修饰器 |
| `NumberValueMode` | 计算模式选择器 |
| `NumberModifier` | 修饰器，包含操作和数值 |
| `NumberModifierHandle` | 修饰器句柄，用于移除 |
| `NumberEffect` | 效果包，多个修饰器的组合 |
| `NumberEffectHandle` | 效果句柄，实现 IDisposable |

### 修饰器操作

| 操作 | 说明 |
|------|------|
| `Add` | 直接加到基础值 |
| `Mul` | 乘法叠加 |
| `FinalAdd` | 最终加法 |
| `Override` | 强制覆盖 |

### 计算模式

| 模式 | 说明 |
|------|------|
| `BaseOnly` | 只返回基础值 |
| `BaseAdd` | Base + Add + FinalAdd |
| `BaseAddMul` | (Base+Add)*(1+Mul)+FinalAdd |
| `OverrideOnly` | Override 或 Base |

### 计算公式

```
damage = (BaseDamage + FlatBonus) * (1 + PctBonus) + FinalBonus
```

---

## 🔗 相关文档

- [属性系统模块](../com.abilitykit.attributes/Document/) - 持久属性系统，与数值系统互补
- [能力管线模块](../com.abilitykit.pipeline/Document/能力管线模块开发设计文档.md) - 技能执行管线
- [触发器模块](../com.abilitykit.triggering/Document/触发器模块开发设计文档.md) - 事件触发系统

---

## 💡 典型使用场景

| 场景 | 说明 |
|------|------|
| 伤害计算 | 基础伤害 + 各类加成 |
| Buff/Debuff | 效果叠加和移除 |
| 技能加成 | 多种加成的组合 |
| 临时计算 | 不需要持久化的中间结果 |
| 管线处理 | Pipeline 中的数据处理 |

---

## 📁 源码路径

```
com.abilitykit.core/Runtime/Core/Common/Numbers/
├── NumberValue.cs           # 核心数值容器
├── NumberValueMode.cs       # 计算模式枚举
├── NumberModifier.cs        # 修饰器结构
└── NumberEffect.cs          # 效果包
```

---

*最后更新：2026-03-19*
