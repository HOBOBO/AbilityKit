# 表现层游戏流程模块文档索引

> Ability-Kit 表现层游戏流程模块官方文档

---

## 📚 文档列表

### 1. [表现层游戏流程模块开发设计文档](./表现层游戏流程模块开发设计文档.md)

**阅读对象**：希望了解表现层如何用分层状态机管理游戏流程的开发者

**内容概要**：
- 为什么需要分层状态机（解决状态逻辑堆砌、难以复用等问题）
- 核心概念：GameFlowDomain、IGamePhaseFeature、Feature 机制
- 架构图和完整数据流
- Feature 扩展方式
- 适用场景说明

**推荐阅读顺序**：从本文档开始

---

## 🎯 快速入门

### 想了解游戏流程是什么？

👉 阅读 [表现层游戏流程模块开发设计文档](./表现层游戏流程模块开发设计文档.md) 第一章「设计理念」

### 想学习如何使用？

👉 阅读 [表现层游戏流程模块开发设计文档](./表现层游戏流程模块开发设计文档.md) 第六章「使用指南」

### 想了解 Feature 机制？

👉 阅读 [表现层游戏流程模块开发设计文档](./表现层游戏流程模块开发设计文档.md) 第四章「核心组件详解 - IGamePhaseFeature」

---

## 📖 概念速查

### 核心类

| 类 | 职责 |
|------|------|
| `GameFlowDomain` | 根状态机，管理游戏顶层状态 |
| `IGamePhaseFeature` | 功能模块接口，支持动态挂载/卸载 |
| `GamePhaseContext` | 阶段上下文，提供依赖查找 |
| `BattleContext` | 战斗上下文，存放战斗数据 |

### 战斗 Feature

| 类 | 职责 |
|------|------|
| `BattleContextFeature` | 上下文管理 |
| `BattleSessionFeature` | 会话核心（帧同步） |
| `BattleSyncFeature` | 同步系统 |
| `BattleInputFeature` | 输入系统 |
| `BattleViewFeature` | 视图系统 |
| `BattleHudFeature` | HUD 界面 |

### 会话子控制器

| 类 | 职责 |
|------|------|
| `SessionOrchestrator` | 帧时间调度 |
| `SessionNetAdapterController` | 网络适配 |
| `SessionDispatchersController` | 事件分发 |
| `SessionReplayController` | 回放录制 |

---

## 🔗 相关文档

- [MOBA 技能管线](../com.abilitykit.demo.moba.runtime/Document/MOBA技能管线模块开发设计文档.md) - 技能执行
- [Host 模块](../com.abilitykit.host.extension/Document/Host模块开发设计文档.md) - 扩展框架
- [Flow 模块](../com.abilitykit.flow/Document/Flow模块开发设计文档.md) - 流程编排

---

## 💡 典型使用场景

| 场景 | 说明 |
|------|------|
| MOBA 游戏流程 | 启动、大厅、战斗、结束 |
| RTS 游戏流程 | 准备、战斗、结算 |
| MMORPG 流程 | 登录、选角、战斗 |
| 帧同步游戏 | 确定性状态管理 |

---

## 📁 源码路径

```
com.abilitykit.demo.moba.view.runtime/Runtime/Game/
├── GameEntry.cs                    # 游戏入口
├── GameFlowDomain.cs              # 根状态机
├── Flow/
│   ├── GameFlowDomain.cs         # 流程域
│   └── Battle/
│       ├── Features/
│       │   ├── BattleContext.cs  # 战斗上下文
│       │   ├── BattleSessionFeature/  # 会话核心
│       │   ├── BattleViewFeature/    # 视图系统
│       │   ├── BattleInputFeature.cs # 输入系统
│       │   └── BattleHudFeature.cs  # HUD
│       └── Session/
│           ├── Core/             # 会话核心
│           └── SubFeatures/      # 子特性
└── ViewEvents/                   # 视图事件
```

---

*最后更新：2026-03-19*
