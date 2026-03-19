# 能力管线模块文档索引

> Ability-Kit 能力管线模块官方文档

---

## 📚 文档列表

### 1. [能力管线模块开发设计文档](./能力管线模块开发设计文档.md)

**阅读对象**：首次接触能力管线模块的开发者

**内容概要**：
- 为什么需要能力管线模块（解决技能逻辑散落、阶段管理混乱等问题）
- 核心概念：Pipeline、Phase、Run、Context
- 架构图和执行流程
- 阶段类型详解：瞬时、持续、可中断、组合
- 完整使用示例
- 设计模式总结

**推荐阅读顺序**：从本文档开始

---

## 🎯 快速入门

### 想了解能力管线是什么？

👉 阅读 [能力管线模块开发设计文档](./能力管线模块开发设计文档.md) 第一章「设计理念」

### 想学习如何使用？

👉 阅读 [能力管线模块开发设计文档](./能力管线模块开发设计文档.md) 第六章「使用指南」

### 想了解阶段类型？

👉 阅读 [能力管线模块开发设计文档](./能力管线模块开发设计文档.md) 第五章「阶段类型详解」

---

## 📖 概念速查

### 核心类

| 类 | 职责 |
|------|------|
| `AbilityPipeline<TCtx>` | 管线容器（蓝图） |
| `AbilityPipelineRun<TCtx>` | 执行实例（一次运行） |
| `AbilityPipelineContext` | 执行上下文（数据容器） |
| `AbilityPipelinePhaseBase` | 阶段基类 |
| `IAbilityConditionNode` | 条件节点接口 |

### 基础阶段

| 阶段 | 用途 |
|------|------|
| `InstantPhase` | 瞬时执行，立即完成 |
| `DurationalPhase` | 持续执行，达到时长后完成 |
| `InterruptiblePhase` | 支持中断的阶段 |
| `DelayPhase` | 简单延时 |
| `RepeatPhase` | 重复执行 |

### 组合阶段

| 阶段 | 用途 |
|------|------|
| `SequencePhase` | 顺序执行子阶段 |
| `ParallelPhase` | 并行执行子阶段 |
| `ConditionalPhase` | 根据条件选择分支 |

### 条件节点

| 条件 | 用途 |
|------|------|
| `AndCondition` | A 且 B |
| `OrCondition` | A 或 B |
| `NotCondition` | 非 A |
| `LambdaCondition` | 自定义条件 |

### 状态

| 状态 | 含义 |
|------|------|
| `Executing` | 执行中 |
| `Paused` | 已暂停 |
| `Completed` | 已完成 |
| `Interrupted` | 已中断 |
| `Aborted` | 已中止 |

---

## 🔗 相关文档

- [Host模块开发设计文档](../com.abilitykit.host.extension/Document/Host模块开发设计文档.md) - 运行时框架
- [Flow模块开发设计文档](../com.abilitykit.flow/Document/Flow模块开发设计文档.md) - 流程管理
- [通用录像模块开发设计文档](../com.abilitykit.world.record/Document/通用录像模块开发设计文档.md) - 录制回放

---

## 💡 典型使用场景

| 场景 | 说明 |
|------|------|
| MOBA 技能系统 | 引导、施放、冷却等阶段管理 |
| RPG 技能树 | 前置技能、条件解锁 |
| Buff/Debuff 系统 | 持续时间、叠加规则 |
| 复杂动画序列 | 动画、音效、特效、伤害的协调 |
| 对话/剧情系统 | 顺序展示、条件分支 |

---

## 📁 源码路径

```
com.abilitykit.pipeline/Runtime/
├── AbilityPipeline.cs              # 核心实现
├── InstantAbilityPipeline.cs       # 瞬时管线
├── Phase/
│   ├── AbilityPipelinePhaseBase.cs  # 阶段基类
│   ├── AbilitySequencePhase.cs      # 顺序阶段
│   ├── AbilityParallelPhase.cs      # 并行阶段
│   ├── AbilityConditionalPhase.cs   # 条件阶段
│   └── ...
├── Interface/
│   ├── IAbilityPipeline.cs        # 管线接口
│   ├── IAbilityPipelineRun.cs     # 执行接口
│   └── ...
└── Debug/
    └── AbilityPipelineLiveRegistry.cs  # 调试工具
```

---

*最后更新：2026-03-19*
