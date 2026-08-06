# MOBA 技能管线模块文档索引

> Ability-Kit MOBA 技能管线模块官方文档

---

## 📚 文档列表

### 1. [MOBA 技能管线模块开发设计文档](./MOBA技能管线模块开发设计文档.md)

**阅读对象**：希望了解如何基于通用能力管线实现具体技能流程的开发者

**内容概要**：
- 为什么需要技能管线（解决技能逻辑耦合、难以复用等问题）
- 核心概念：SkillPipelineContext、TimelinePhase、Executor
- 架构图和完整执行流程
- 与通用能力管线的关系
- 适用场景说明

**推荐阅读顺序**：从本文档开始

---

### 2. [MOBA 投射物发射器扩展设计文档](./投射物发射器扩展设计文档.md)

**阅读对象**：需要扩展 MOBA 投射物发射流程、设计复杂弹幕/蓄力/准备态发射/多阶段发射的开发者

**内容概要**：
- 为什么发射器发射过程需要接入持续行为系统
- 核心概念：LaunchContinuous、LaunchContext、LaunchSequence、PatternProvider
- 默认间隔发射流程与中断清理规则
- 准备后释放、多阶段弹幕、复杂形状发射的扩展落点
- 后续配置表演进建议

**推荐阅读顺序**：理解持续行为系统后阅读本文档

---

## 🎯 快速入门

### 想了解技能管线是什么？

👉 阅读 [MOBA 技能管线模块开发设计文档](./MOBA技能管线模块开发设计文档.md) 第一章「设计理念」

### 想学习如何使用？

👉 阅读 [MOBA 技能管线模块开发设计文档](./MOBA技能管线模块开发设计文档.md) 第六章「使用指南」

### 想了解与通用管线的关系？

👉 阅读 [MOBA 技能管线模块开发设计文档](./MOBA技能管线模块开发设计文档.md) 第九章「与通用能力管线的关系」

### 想了解投射物发射器如何扩展？

👉 阅读 [MOBA 投射物发射器扩展设计文档](./投射物发射器扩展设计文档.md) 第三章「总体架构」和第六章「如何扩展不同需求」

---

## 📖 概念速查

### 核心类

| 类 | 职责 |
|------|------|
| `SkillExecutor` | 技能执行器，发起技能释放 |
| `SkillPipelineRunner` | 管线运行器，管理技能生命周期 |
| `SkillPipelineContext` | 执行上下文，携带技能执行信息 |
| `SkillCastContext` | 施法上下文，记录施法唯一标识 |

### 阶段

| 类 | 职责 |
|------|------|
| `SkillTimelinePhase` | 时间轴阶段，按时间触发事件 |
| `SkillCastApplyEffectPhase` | 效果应用阶段，应用效果到目标 |
| `SkillFlowChecksPhase` | 条件检查阶段，验证释放条件 |

### 库

| 类 | 职责 |
|------|------|
| `IMobaSkillPipelineLibrary` | 技能库接口 |
| `TableDrivenMobaSkillPipelineLibrary` | 配置驱动实现 |

---

## 🔗 相关文档

- [能力管线模块](../com.abilitykit.pipeline/Document/能力管线模块开发设计文档.md) - 通用能力管线框架
- [投射物系统](../com.abilitykit.combat.projectile/Document/投射物系统模块开发设计文档.md) - 投射物管理
- [目标查找模块](../../com.abilitykit.combat.targeting/Documentation~/Manual.md) - 目标选择

---

## 💡 典型使用场景

| 场景 | 说明 |
|------|------|
| MOBA 技能 | 指向、引导、弹道、区域 |
| MMO 技能 | 读条、引导、多段伤害 |
| ARPG 技能 | 连招、蓄力、释放 |
| 帧同步游戏 | 确定性技能执行 |

---

## 📁 源码路径

```
com.abilitykit.demo.moba.runtime/Runtime/Impl/Moba/Services/Skill/
├── Pipeline/
│   ├── SkillPipelineRunner.cs       # 管线运行器
│   ├── SkillPipelineContext.cs      # 执行上下文
│   ├── SkillCastPipeline.cs        # 技能管线特化
│   ├── SkillExecutor.cs            # 技能执行器
│   └── IMobaSkillPipelineLibrary.cs # 技能库接口
├── Phases/
│   ├── SkillTimelinePhase.cs        # 时间轴阶段
│   ├── SkillCastApplyEffectPhase.cs # 效果应用阶段
│   └── SkillFlowChecksPhase.cs      # 条件检查阶段
└── Events/
    ├── SkillLifecycleEvents.cs      # 生命周期事件
    └── MobaSkillTriggering.cs       # 事件触发
```

---

*最后更新：2026-03-19*
