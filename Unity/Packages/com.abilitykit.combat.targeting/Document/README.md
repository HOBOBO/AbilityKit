# 目标查找模块文档索引

> Ability-Kit 目标查找模块官方文档

---

## 📚 文档列表

### 1. [目标查找模块开发设计文档](./目标查找模块开发设计文档.md)

**阅读对象**：首次接触目标查找模块的开发者

**内容概要**：
- 为什么需要目标查找模块（解决逻辑散落、条件难组合等问题）
- 核心概念：Provider、Rule、Scorer、Selector
- 架构图和完整查找流程
- 形状系统详解
- 设计模式总结
- 适用场景说明

**推荐阅读顺序**：从本文档开始

---

## 🎯 快速入门

### 想了解目标查找是什么？

👉 阅读 [目标查找模块开发设计文档](./目标查找模块开发设计文档.md) 第一章「设计理念」

### 想学习如何使用？

👉 阅读 [目标查找模块开发设计文档](./目标查找模块开发设计文档.md) 第六章「使用指南」

### 想了解形状系统？

👉 阅读 [目标查找模块开发设计文档](./目标查找模块开发设计文档.md) 第四章「核心组件详解 - 形状规则」

---

## 📖 概念速查

### 核心类

| 类 | 职责 |
|------|------|
| `TargetSearchEngine` | 查找引擎，协调整个流程 |
| `SearchQuery` | 查询配置，定义查找条件 |
| `SearchContext` | 查找上下文，提供服务和数据 |
| `SearchHit` | 命中结果结构 |

### 候选源

| 类 | 职责 |
|------|------|
| `ICandidateProvider` | 候选源接口 |
| `IndexedListCandidateProvider` | 从索引列表获取候选 |
| `UnionDistinctCandidateProvider` | 并集去重 |
| `IntersectCandidateProvider` | 交集 |
| `ExceptCandidateProvider` | 差集 |

### 过滤规则

| 类 | 职责 |
|------|------|
| `ITargetRule` | 规则接口 |
| `CircleShapeRule` | 圆形区域过滤 |
| `SectorShapeRule` | 扇形区域过滤 |
| `OrientedRectShapeRule` | 定向矩形过滤 |
| `ExcludeEntityRule` | 排除指定实体 |

### 评分器

| 类 | 职责 |
|------|------|
| `ITargetScorer` | 评分器接口 |
| `ZeroScorer` | 零分（不评分） |
| `DistanceToEntityScorer2D` | 距离评分 |
| `SeededHashRandomScorer` | 确定性随机评分 |

### 选择器

| 类 | 职责 |
|------|------|
| `ITargetSelector` | 选择器接口 |
| `TopKByScoreSelector` | 排序后取 TopK |
| `StreamingTopKSelector` | 流式取 TopK |

---

## 🔗 相关文档

- [实体管理模块](../com.abilitykit.combat.entitymanager/Document/) - 实体查询系统
- [能力管线模块](../com.abilitykit.pipeline/Document/能力管线模块开发设计文档.md) - 技能执行
- [触发器模块](../com.abilitykit.triggering/Document/触发器模块开发设计文档.md) - 事件触发

---

## 💡 典型使用场景

| 场景 | 说明 |
|------|------|
| MOBA 技能目标 | 圆形、扇形、定向矩形范围 |
| RPG 范围攻击 | AOE 技能的目标选择 |
| AI 视野检测 | 扇形视野内的敌人 |
| 锁定系统 | 优先选择最近/血量最低的目标 |
| 录像回放 | 确定性随机用于结果一致 |

---

## 📁 源码路径

```
com.abilitykit.combat.targeting/Runtime/SearchTarget/
├── TargetSearchEngine.cs         # 查找引擎
├── SearchQuery.cs               # 查询配置
├── SearchContext.cs             # 上下文
├── Providers/                  # 候选源提供者
│   ├── ICandidateProvider.cs
│   ├── IndexedListCandidateProvider.cs
│   ├── UnionDistinctCandidateProvider.cs
│   └── ...
├── Rules/                      # 过滤规则
│   ├── ITargetRule.cs
│   ├── CircleShapeRule.cs
│   ├── SectorShapeRule.cs
│   └── ...
├── Scorers/                    # 评分器
│   ├── ITargetScorer.cs
│   ├── DistanceToEntityScorer2D.cs
│   └── ...
├── Selectors/                  # 选择器
│   ├── ITargetSelector.cs
│   └── StreamingTopKByScoreSelector.cs
└── Shapes/                    # 形状系统
    ├── ShapeFrame2D.cs
    └── ...
```

---

*最后更新：2026-03-19*
