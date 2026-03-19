# 投射物系统模块文档索引

> Ability-Kit 投射物系统模块官方文档

---

## 📚 文档列表

### 1. [投射物系统模块开发设计文档](./投射物系统模块开发设计文档.md)

**阅读对象**：首次接触投射物系统模块的开发者

**内容概要**：
- 为什么需要投射物系统模块（解决GC压力、碰撞逻辑散落等问题）
- 核心概念：ProjectileWorld、HitPolicy、HitFilter、AreaWorld
- 架构图和完整 Tick 流程
- 设计模式总结
- 适用场景说明

**推荐阅读顺序**：从本文档开始

---

## 🎯 快速入门

### 想了解投射物系统是什么？

👉 阅读 [投射物系统模块开发设计文档](./投射物系统模块开发设计文档.md) 第一章「设计理念」

### 想学习如何使用？

👉 阅读 [投射物系统模块开发设计文档](./投射物系统模块开发设计文档.md) 第六章「使用指南」

### 想了解命中策略？

👉 阅读 [投射物系统模块开发设计文档](./投射物系统模块开发设计文档.md) 第四章「核心组件详解 - IProjectileHitPolicy」

---

## 📖 概念速查

### 核心类

| 类 | 职责 |
|------|------|
| `ProjectileService` | 投射物服务，提供发射和事件接口 |
| `ProjectileWorld` | 投射物世界，管理对象池和Tick |
| `ProjectileSpawnParams` | 生成参数，定义投射物属性 |
| `ProjectileId` | 投射物ID |

### 命中

| 类 | 职责 |
|------|------|
| `IProjectileHitPolicy` | 命中策略接口 |
| `ExitOnHitPolicy` | 击中消失 |
| `PierceHitPolicy` | 穿透策略 |
| `IProjectileHitFilter` | 命中过滤器接口 |

### 发射

| 类 | 职责 |
|------|------|
| `IProjectileSpawnPattern` | 发射模式接口 |
| `SingleShotPattern` | 单发 |
| `BurstPattern` | 连发 |
| `FanPattern` | 扇形 |
| `ScatterPattern` | 散射 |

### 范围

| 类 | 职责 |
|------|------|
| `AreaWorld` | 范围效果世界 |
| `AreaSpawnParams` | 范围生成参数 |

---

## 🔗 相关文档

- [目标查找模块](../com.abilitykit.combat.targeting/Document/) - 目标选择和查找
- [移动系统模块](../com.abilitykit.world.motion/Document/) - 实体移动管理
- [数值系统](../com.abilitykit.core/Document/数值系统模块开发设计文档.md) - 伤害计算

---

## 💡 典型使用场景

| 场景 | 说明 |
|------|------|
| MOBA 投射物 | 箭、弹、飞弹 |
| FPS 子弹 | 枪械、火箭筒 |
| 范围效果 | 火球、冰霜AOE |
| 回旋武器 | 回旋镖、飞斧 |
| 帧同步游戏 | 确定性回放 |

---

## 📁 源码路径

```
com.abilitykit.combat.projectile/Runtime/Projectile/
├── Runtime/
│   ├── Projectile.cs             # 投射物实体
│   ├── ProjectileWorld.cs        # 投射物世界
│   ├── ProjectileSpawnParams.cs  # 生成参数
│   └── ProjectileExitReason.cs  # 退出原因
├── Services/
│   └── IProjectileService.cs     # 服务接口
├── Policies/
│   ├── IProjectileHitPolicy.cs   # 命中策略接口
│   └── ExitOnHitPolicy.cs       # 击中消失策略
├── Filters/
│   └── IProjectileHitFilter.cs   # 命中过滤接口
├── Emitters/
│   └── IProjectileEmitter.cs     # 发射器接口
├── Patterns/
│   ├── IProjectileSpawnPattern.cs # 发射模式接口
│   ├── SingleShotPattern.cs       # 单发
│   ├── BurstPattern.cs           # 连发
│   └── FanPattern.cs             # 扇形
└── Area/
    ├── AreaWorld.cs              # 范围效果世界
    └── AreaSpawnParams.cs        # 范围参数
```

---

*最后更新：2026-03-19*
