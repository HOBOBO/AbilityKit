# Ability-Kit 模块文档总览

> Ability-Kit 框架官方文档索引

---

## 总览文档

| 文档 | 定位 |
|------|------|
| [技术选型文档](./技术选型文档.md) | 从零开发通用战斗框架的技术选型，涵盖逻辑表现分离、ECS、管线系统、依赖注入、帧同步等核心设计 |

---

## 内部推广分级

AbilityKit 当前按“先基础包收口，再推进 Starter，再扩大到战斗/同步组合”的顺序推广。业务项目不应直接全量引入 `Unity/Packages`，而应按包分级和推荐组合选择依赖。

| 等级 | 包 | 当前定位 | 推广建议 |
|------|------|------|------|
| P0 基础底座 | `com.abilitykit.core`、`com.abilitykit.world.di` | 日志、事件、对象池、数值、Marker、World 生命周期和服务装配 | 先补齐 README、样例、测试命令，作为所有 Starter 的第一层依赖 |
| P1 技能核心 | `com.abilitykit.triggering`、`com.abilitykit.pipeline`、`com.abilitykit.attributes` | 事件触发、技能阶段编排、属性/修饰器能力 | 作为 `SkillCore` 组合试点，不依赖 Demo 包运行 |
| P2 流程与状态 | `com.abilitykit.flow`、`com.abilitykit.hfsm` | 跨帧流程、状态迁移、角色/玩法状态管理 | 作为增强层接入，避免把简单技能都强制接到状态机 |
| P3 战斗领域 | `com.abilitykit.combat.targeting`、`com.abilitykit.combat.projectile`、`com.abilitykit.combat.damage`、`com.abilitykit.combat.skilllibrary`、`com.abilitykit.combat.entitymanager` | 目标、投射物、伤害、技能索引、实体索引 | 在 `SkillCore` 跑通后按玩法需要接入 |
| P4 同步与服务端 | `com.abilitykit.world.framesync`、`com.abilitykit.world.snapshot`、`com.abilitykit.world.statesync`、`com.abilitykit.record`、`com.abilitykit.protocol`、`com.abilitykit.host`、`com.abilitykit.host.extension` | 帧同步、快照、回放、协议、Host 和服务端组合 | 只对多人、回放、权威服项目推广 |
| 示例/参考 | `com.abilitykit.demo.*`、`Server/Orleans` | MOBA、Shooter、Orleans 参考实现 | 作为最佳实践阅读，不作为默认业务依赖 |

## 推荐组合

| 组合 | 包含模块 | 适用场景 | 验收标准 |
|------|------|------|------|
| `Foundation` | `core` + `world.di` | 干净项目启动、基础设施验证、服务作用域验证 | 能在纯 C# 或 Unity 中运行最小示例，输出结构化日志，不依赖 Demo |
| `SkillCore` | `Foundation` + `triggering` + `pipeline` + `attributes` | 技能、Buff、被动、事件规则的最小战斗核心 | 能跑 2 到 3 个技能、1 个 Buff、1 个触发规则和对应测试 |
| `BattleRuntime` | `SkillCore` + `combat.targeting` + `combat.projectile` + `combat.damage` | 中大型战斗玩法、命中、投射物和伤害链路 | 能验证目标选择、命中、伤害和 Trace 输出 |
| `SyncRuntime` | `BattleRuntime` + `framesync` + `snapshot` + `statesync` + `record` + `protocol` | 多人同步、回放、重连、状态恢复 | 能验证输入帧、快照应用、状态哈希和回放 |
| `ServerRuntime` | `protocol` + `host` + `host.extension` + 项目服务端适配 | 权威服、房间服、网关服务 | 能启动房间/战斗宿主，并通过 Smoke 验证基础流程 |

## Starter 推进顺序

第一版 Starter 只证明基础包可以独立启动；技能核心不重复造新示例，而是收编 `Samples.Logic` 中已有的 Pipeline、Triggering、Modifiers/属性正式示例与 Web 导出能力。

1. `Foundation Starter`：只接 `core`、`world.di`，展示日志、事件、对象池、World 服务注册和一次宿主驱动 Tick。
2. `SkillCore 路线`：复用 `pipeline/basic-phases`、`triggering/basic-event-trigger`、`triggering/condition-blackboard`、`modifiers/attribute-basic` 等现有示例，展示一次技能释放、属性变化、触发规则和结构化输出。
3. `BattleRuntime Starter`：增加目标、投射物和伤害，展示一个可测试的命中链路。
4. `SyncRuntime Starter`：增加输入帧、快照和回放，只面向需要多人同步的项目。

Starter 必须满足：不依赖 `demo.moba.*`、不依赖 `demo.shooter.*`、可纯 C# 运行、可被 Unity 宿主接入、输出可被测试或 Web/Unity 面板消费。

---

## 模块文档列表

### 1. [Host 模块](./com.abilitykit.host.extension/Document/)

| 文档 | 定位 |
|------|------|
| [开发设计文档](./com.abilitykit.host.extension/Document/Host模块开发设计文档.md) | 整体架构、设计理念、数据流程 |
| [扩展指南](./com.abilitykit.host.extension/Document/Host模块扩展指南.md) | Hook、Feature、Blueprint 扩展机制 |

**核心内容**：游戏服务器运行时框架，管理世界、客户端连接、消息广播

---

### 2. [Flow 模块](./com.abilitykit.flow/Document/)

| 文档 | 定位 |
|------|------|
| [开发设计文档](./com.abilitykit.flow/Document/Flow模块开发设计文档.md) | 设计理念、核心概念、节点详解 |
| [扩展指南](./com.abilitykit.flow/Document/Flow模块扩展指南.md) | 自定义节点、组合模式、协作示例 |

**核心内容**：流程编排引擎，用节点树组织异步/时间驱动的逻辑

---

### 3. [通用录像模块](./com.abilitykit.record/Document/)

| 文档 | 定位 |
|------|------|
| [开发设计文档](./com.abilitykit.record/Document/通用录像模块开发设计文档.md) | 设计理念、录制/回放机制 |

**核心内容**：帧同步游戏的输入录制和回放，支持确定性验证

---

### 4. [能力管线模块](./com.abilitykit.pipeline/Document/)

| 文档 | 定位 |
|------|------|
| [开发设计文档](./com.abilitykit.pipeline/Document/能力管线模块开发设计文档.md) | 设计理念、阶段类型、组合模式 |

**核心内容**：技能/Buff 系统，用管线阶段组织复杂的能力逻辑

---

### 5. [实体管理模块](./com.abilitykit.combat.entitymanager/Document/)

| 文档 | 定位 |
|------|------|
| [开发设计文档](./com.abilitykit.combat.entitymanager/Document/实体管理模块开发设计文档.md) | 设计理念、索引机制、查询优化 |

**核心内容**：用索引表实现高效实体查询，支持单键和多键索引

---

### 6. [技能库模块](./com.abilitykit.combat.skilllibrary/Document/)

| 文档 | 定位 |
|------|------|
| [开发设计文档](./com.abilitykit.combat.skilllibrary/Document/技能库模块开发设计文档.md) | 设计理念、索引机制、查询优化 |

**核心内容**：用索引表实现高效技能查询，支持单键和多键索引

---

### 7. [触发器模块](./com.abilitykit.triggering/Document/)

| 文档 | 定位 |
|------|------|
| [开发设计文档](./com.abilitykit.triggering/Document/触发器模块开发设计文档.md) | 设计理念、事件驱动、条件表达式 |

**核心内容**：基于事件的触发器引擎，支持 RPN 条件表达式、确定性回放

---

### 8. [属性系统模块](./com.abilitykit.attributes/Document/)

| 文档 | 定位 |
|------|------|
| [开发设计文档](./com.abilitykit.attributes/Document/属性系统模块开发设计文档.md) | 设计理念、修饰器叠加、公式约束 |

**核心内容**：高性能属性系统，支持 Buff/Debuff 管理、自定义公式、脏标记优化

---

### 10. [目标查找模块](./com.abilitykit.combat.targeting/Documentation~/)

| 文档 | 定位 |
|------|------|
| [运行时手册](./com.abilitykit.combat.targeting/Documentation~/Manual.md) | 当前管线契约、性能边界与扩展方式 |
| [组合示例](./com.abilitykit.combat.targeting/Documentation~/Examples.md) | 常见查询组合方式 |
| [历史开发设计文档](./com.abilitykit.combat.targeting/Documentation~/Design/目标查找模块开发设计文档.md) | 早期设计理念与演进背景 |

**核心内容**：通用目标查找框架，支持圆形/扇形/矩形范围、流式处理、零GC

---

### 11. [Core 模块](./com.abilitykit.core/Documentation~/)

| 文档 | 定位 |
|------|------|
| [数值系统开发设计文档](./com.abilitykit.core/Documentation~/数值系统模块开发设计文档.md) | 设计理念、轻量修饰器、伤害计算 |

**核心内容**：轻量级数值修饰系统，用于伤害计算和效果加成，与属性系统互补

---

### 12. [移动系统模块](./com.abilitykit.world.motion/Document/)

| 文档 | 定位 |
|------|------|
| [开发设计文档](./com.abilitykit.world.motion/Document/移动系统模块开发设计文档.md) | 设计理念、来源组合、碰撞求解 |

**核心内容**：通用移动系统框架，支持多来源组合、分组优先级、碰撞解耦、确定性设计

---

### 13. [投射物系统模块](./com.abilitykit.combat.projectile/Document/)

| 文档 | 定位 |
|------|------|
| [开发设计文档](./com.abilitykit.combat.projectile/Document/投射物系统模块开发设计文档.md) | 设计理念、命中策略、范围效果 |

**核心内容**：高性能投射物系统，支持对象池、帧同步、命中策略、范围效果

---

### 14. [MOBA 技能管线示例](./com.abilitykit.demo.moba.runtime/Document/)

| 文档 | 定位 |
|------|------|
| [开发设计文档](./com.abilitykit.demo.moba.runtime/Document/MOBA技能管线模块开发设计文档.md) | 设计理念、时间轴阶段、技能工厂 |

**核心内容**：基于通用能力管线的 MOBA 技能实现，支持时间轴事件、技能库、效果追踪

---

### 15. [表现层游戏流程示例](./com.abilitykit.demo.moba.view.runtime/Document/)

| 文档 | 定位 |
|------|------|
| [开发设计文档](./com.abilitykit.demo.moba.view.runtime/Document/表现层游戏流程模块开发设计文档.md) | 设计理念、Feature 机制、数据流 |

**核心内容**：基于分层状态机和 Feature 扩展的表现层游戏流程管理

### 16. [World 依赖注入模块](./com.abilitykit.world.di/Document/)

| 文档 | 定位 |
|------|------|
| [开发设计文档](./com.abilitykit.world.di/Document/World依赖注入与组合系统开发设计文档.md) | 设计理念、服务容器、生命周期隔离、模块化注册 |

**核心内容**：逻辑世界的服务依赖注入框架，支持 Singleton/Scoped/Transient 三种生命周期，提供模块化服务注册

---

## 🎯 快速导航

### 想了解 Ability-Kit 是什么？

👉 阅读 [Host 模块开发设计文档](./com.abilitykit.host.extension/Document/Host模块开发设计文档.md)

### 想开发新功能？

| 需求 | 推荐文档 |
|------|----------|
| 扩展 Host 模块 | [Host 模块扩展指南](./com.abilitykit.host.extension/Document/Host模块扩展指南.md) |
| 编排复杂流程 | [Flow 模块扩展指南](./com.abilitykit.flow/Document/Flow模块扩展指南.md) |
| 开发自定义节点 | [Flow 模块扩展指南 - 第二章](./com.abilitykit.flow/Document/Flow模块扩展指南.md#二自定义节点开发) |
| 管理世界服务依赖 | [World DI 开发设计文档](./com.abilitykit.world.di/Document/World依赖注入与组合系统开发设计文档.md) |

### 想理解架构设计？

| 主题 | 推荐文档 |
|------|----------|
| 为什么用 Hook/Feature | [Host 模块扩展指南 - 设计理念](./com.abilitykit.host.extension/Document/Host模块扩展指南.md#一设计理念host-为什么是一个扩展框架) |
| 为什么用节点树 | [Flow 模块开发设计文档 - 设计理念](./com.abilitykit.flow/Document/Flow模块开发设计文档.md#一设计理念为什么要做-flow-模块) |
| 为什么用 Scope | [World DI 开发设计文档 - 生命周期](./com.abilitykit.world.di/Document/World依赖注入与组合系统开发设计文档.md#23-什么是生命周期lifetime) |
| 为什么用 Module | [World DI 开发设计文档 - 模块系统](./com.abilitykit.world.di/Document/World依赖注入与组合系统开发设计文档.md#四五-iworldmodule---模块接口) |
| 为什么分 Track | [通用录像模块开发设计文档 - 设计理念](./com.abilitykit.record/Document/通用录像模块开发设计文档.md#一设计理念为什么要做录像模块) |
| 为什么用 Phase | [能力管线模块开发设计文档 - 设计理念](./com.abilitykit.pipeline/Document/能力管线模块开发设计文档.md#一设计理念为什么要做-pipeline-模块) |
| 为什么用索引 | [技能库模块开发设计文档 - 设计理念](./com.abilitykit.combat.skilllibrary/Document/技能库模块开发设计文档.md#一设计理念为什么要做技能库模块) |
| 为什么用 Trigger | [触发器模块开发设计文档 - 设计理念](./com.abilitykit.triggering/Document/触发器模块开发设计文档.md#一设计理念为什么要做触发器模块) |
| 为什么用修饰器 | [属性系统模块开发设计文档 - 设计理念](./com.abilitykit.attributes/Document/属性系统模块开发设计文档.md#一设计理念为什么要做属性系统模块) |
| 为什么用轻量修饰器 | [数值系统模块开发设计文档 - 设计理念](./com.abilitykit.core/Documentation~/数值系统模块开发设计文档.md#一设计理念为什么要做数值系统) |
| 为什么用管线模式 | [目标查找模块开发设计文档 - 设计理念](./com.abilitykit.combat.targeting/Documentation~/Design/目标查找模块开发设计文档.md#一设计理念为什么要做目标查找模块) |
| 为什么用来源组合 | [移动系统模块开发设计文档 - 设计理念](./com.abilitykit.world.motion/Document/移动系统模块开发设计文档.md#一设计理念为什么要做移动系统模块) |
| 为什么用对象池 | [投射物系统模块开发设计文档 - 设计理念](./com.abilitykit.combat.projectile/Document/投射物系统模块开发设计文档.md#一设计理念为什么要做投射物系统模块) |
| 为什么用阶段化 | [MOBA 技能管线开发设计文档 - 设计理念](./com.abilitykit.demo.moba.runtime/Document/MOBA技能管线模块开发设计文档.md#一设计理念为什么要做技能管线) |
| 为什么用分层状态机 | [表现层游戏流程开发设计文档 - 设计理念](./com.abilitykit.demo.moba.view.runtime/Document/表现层游戏流程模块开发设计文档.md#一设计理念为什么要用分层状态机管理游戏流程) |

---

## 📐 模块关系图

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         Ability-Kit 框架架构                            │
│                                                                         │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │                      游戏应用层                                   │   │
│   │                                                                 │   │
│   │  ┌───────────────┐  ┌───────────────┐  ┌───────────────┐      │   │
│   │  │   技能系统    │  │   房间系统    │  │   战斗系统    │      │   │
│   │  │  (Pipeline)  │  │    (Host)    │  │  (Pipeline)  │      │   │
│   │  └───────────────┘  └───────────────┘  └───────────────┘      │   │
│   │                                                                 │   │
│   └─────────────────────────────────────────────────────────────────┘   │
│                               │                                         │
│                               ▼                                         │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │                       引擎层                                       │   │
│   │                                                                 │   │
│   │  ┌───────────────┐  ┌───────────────┐  ┌───────────────┐      │   │
│   │  │   流程编排    │  │   运行时管理   │  │   录制回放    │      │   │
│   │  │    (Flow)     │  │    (Host)    │  │  (Record)    │      │   │
│   │  └───────────────┘  └───────────────┘  └───────────────┘      │   │
│   │                                                                 │   │
│   └─────────────────────────────────────────────────────────────────┘   │
│                               │                                         │
│                               ▼                                         │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │                       核心层                                       │   │
│   │                                                                 │   │
│   │  ┌───────────────┐  ┌───────────────┐  ┌───────────────┐      │   │
│   │  │   实体系统    │  │   帧同步     │  │   网络通信    │      │   │
│   │  │   (World)    │  │  (FrameSync) │  │  (Network)   │      │   │
│   │  └───────────────┘  └───────────────┘  └───────────────┘      │   │
│   │                                                                 │   │
│   └─────────────────────────────────────────────────────────────────┘   │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 📖 文档风格指南

所有文档都遵循以下设计原则：

| 原则 | 说明 |
|------|------|
| **问题驱动** | 先讲"为什么需要"，再讲"怎么用" |
| **大量图示** | 用 ASCII 图解释抽象概念 |
| **通俗类比** | 用生活实例帮助理解 |
| **代码模板** | 提供可直接使用的代码 |
| **最佳实践** | 明确告诉你要做什么、不要做什么 |

---

## 🔧 文档贡献指南

如果你想修改或补充文档：

1. 每个模块的文档在 `Document/` 目录下
2. Markdown 格式，支持飞书直接粘贴
3. 代码示例使用 C# 语法高亮
4. 流程图使用 Mermaid 或 ASCII 图

---

## 📅 更新日志

| 日期 | 模块 | 说明 |
|------|------|------|
| 2026-03-19 | All | 初始文档创建 |

---

*最后更新：2026-03-19*
