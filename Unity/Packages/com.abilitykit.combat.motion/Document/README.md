# 移动系统模块文档索引

> Ability-Kit 移动系统模块官方文档

---

## 📚 文档列表

### 1. [移动系统模块开发设计文档](./移动系统模块开发设计文档.md)

**阅读对象**：首次接触移动系统模块的开发者

**内容概要**：
- 为什么需要移动系统模块（解决移动逻辑堆砌、效果无法叠加等问题）
- 核心概念：MotionSource、MotionPipeline、MotionSolver
- 架构图和完整 Tick 流程
- 分组和叠加策略
- 设计模式总结
- 适用场景说明

**推荐阅读顺序**：从本文档开始

---

## 🎯 快速入门

### 想了解移动系统是什么？

👉 阅读 [移动系统模块开发设计文档](./移动系统模块开发设计文档.md) 第一章「设计理念」

### 想学习如何使用？

👉 阅读 [移动系统模块开发设计文档](./移动系统模块开发设计文档.md) 第六章「使用指南」

### 想了解分组和叠加策略？

👉 阅读 [移动系统模块开发设计文档](./移动系统模块开发设计文档.md) 第四章「核心组件详解 - MotionGroups」

---

## 📖 概念速查

### 核心类

| 类 | 职责 |
|------|------|
| `MotionPipeline` | 移动协调器，协调所有来源 |
| `IMotionSource` | 移动来源接口 |
| `IMotionSolver` | 碰撞求解器接口 |
| `MotionState` | 移动状态（位置、速度、朝向） |
| `MotionOutput` | 移动结果（期望位移、实际位移） |

### 移动来源

| 类 | 职责 |
|------|------|
| `LocomotionMotionSource` | 玩家输入移动 |
| `TrajectoryMotionSource` | 轨迹移动（冲刺） |
| `FixedDeltaMotionSource` | 固定位移（击退） |
| `PathFollowerMotionSource` | 路径跟随 |
| `ScaledMotionSource` | 缩放来源（混合移动） |

### 轨迹

| 类 | 职责 |
|------|------|
| `ITrajectory3D` | 轨迹接口 |
| `LinearTrajectory3D` | 直线轨迹 |
| `WaypointTrajectory3D` | 多点路径轨迹 |

### 移动分组

| Group | 叠加策略 | 说明 |
|-------|----------|------|
| `Locomotion` | Additive | 玩家输入，可叠加 |
| `Ability` | Exclusive | 技能位移，独占 |
| `Control` | Override | 控制效果，抑制其他 |
| `Path` | Exclusive | 寻路移动，独占 |

---

## 🔗 相关文档

- [目标查找模块](../com.abilitykit.combat.targeting/Document/) - 目标选择和查找
- [实体管理模块](../com.abilitykit.combat.entitymanager/Document/) - 实体查询系统
- [数值系统](../com.abilitykit.core/Document/数值系统模块开发设计文档.md) - 伤害计算

---

## 💡 典型使用场景

| 场景 | 说明 |
|------|------|
| MOBA 英雄移动 | 输入、冲刺、技能位移 |
| RPG 角色移动 | 走路、跑步、翻滚、击退 |
| RTS 单位移动 | 寻路、阵型、强制移动 |
| 帧同步游戏 | 确定性移动、回放验证 |
| 平台游戏 | 跳跃、重力、碰撞 |

---

## 📁 源码路径

```
com.abilitykit.world.motion/Runtime/MotionSystem/
├── Core/
│   ├── MotionPipeline.cs         # 移动协调器
│   ├── IMotionSource.cs         # 移动来源接口
│   ├── LocomotionMotionSource.cs # 输入移动
│   ├── MotionGroups.cs         # 预定义分组
│   ├── MotionStacking.cs       # 叠加策略
│   └── MotionPipelinePolicy.cs  # 跨组抑制策略
├── Trajectory/
│   ├── ITrajectory3D.cs       # 轨迹接口
│   ├── LinearTrajectory3D.cs  # 直线轨迹
│   └── TrajectoryMotionSource.cs # 轨迹移动
├── Generic/
│   ├── WaypointTrajectory3D.cs # 多点轨迹
│   ├── FixedDeltaMotionSource.cs # 固定位移
│   └── ScaledMotionSource.cs   # 缩放来源
└── Collision/
    ├── IMotionSolver.cs       # 碰撞求解器接口
    └── ConfigurableMotionSolver.cs # 可配置碰撞
```

---

*最后更新：2026-03-19*
