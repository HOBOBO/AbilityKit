# 技能库模块文档索引

> Ability-Kit 技能库模块官方文档

---

## 📚 文档列表

### 1. [技能库模块开发设计文档](./技能库模块开发设计文档.md)

**阅读对象**：首次接触技能库模块的开发者

**内容概要**：
- 为什么需要技能库模块（解决查询效率、分类同步等问题）
- 核心概念：SkillLibrary、SkillIndex、KeyedIndex、MultiKeyIndex
- 架构图和完整数据流程
- 设计模式总结
- 适用场景说明

**推荐阅读顺序**：从本文档开始

---

## 🎯 快速入门

### 想了解技能库是什么？

👉 阅读 [技能库模块开发设计文档](./技能库模块开发设计文档.md) 第一章「设计理念」

### 想学习如何使用？

👉 阅读 [技能库模块开发设计文档](./技能库模块开发设计文档.md) 第六章「使用指南」

### 想了解模块间关系？

👉 阅读 [技能库模块开发设计文档](./技能库模块开发设计文档.md) 第八章「与实体管理模块的关系」

---

## 📖 概念速查

### 核心类

| 类 | 职责 |
|------|------|
| `SkillLibrary<TKey, TData>` | 技能库容器，管理技能和索引 |
| `SkillUpdate` | 技能更新事件结构 |

### 索引类

| 类 | 职责 |
|------|------|
| `ISkillIndex` | 索引观察者接口 |
| `IKeyedSkillIndex<TIndexKey, TKey>` | 可查询索引接口 |
| `DerivedKeyedSkillIndex` | 单键索引，一个技能一个分类键 |
| `DerivedMultiKeySkillIndex` | 多键索引，一个技能可以有多个标签 |

### 方法

| 方法 | 用途 |
|------|------|
| `lib.Add(key, data)` | 添加技能 |
| `lib.Remove(key)` | 删除技能 |
| `lib.Update(key, newData, update)` | 更新技能 |
| `lib.CreateDerivedKeyedIndex<T>(selector)` | 创建单键索引 |
| `lib.CreateDerivedMultiKeyIndex<T>(selector)` | 创建多键索引 |
| `index.Get(key)` | 按键查询 |

---

## 🔗 相关文档

- [实体管理模块](../com.abilitykit.combat.entitymanager/Document/) - 实体查询系统
- [能力管线模块](../com.abilitykit.pipeline/Document/能力管线模块开发设计文档.md) - 技能执行管线
- [目标系统模块](../com.abilitykit.combat.targeting/Document/) - 目标选择系统

---

## 💡 典型使用场景

| 场景 | 说明 |
|------|------|
| MOBA 技能管理 | 按学院、标签、冷却查询技能 |
| RPG 天赋系统 | 按类型、品质、等级查询天赋 |
| 卡牌游戏 | 按属性查询卡牌 |
| 物品系统 | 按类型、品质、等级查询物品 |

---

## 📁 源码路径

```
com.abilitykit.combat.skilllibrary/Runtime/SkillLibrary/
├── SkillLibrary.cs           # 核心实现
├── ISkillIndex.cs          # 索引接口
├── IKeyedSkillIndex.cs     # 可查询索引接口
├── DerivedKeyedSkillIndex.cs        # 单键索引
├── DerivedMultiKeySkillIndex.cs     # 多键索引
├── SkillUpdate.cs          # 更新事件
└── SkillLibraryExample.cs   # 使用示例
```

---

*最后更新：2026-03-19*
