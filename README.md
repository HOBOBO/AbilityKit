# Ability-Kit

> 通用游戏战斗框架 | Logic-Presentation Separation | Frame-Synchronized

**Ability-Kit** 是一个基于 Unity UPM 的通用游戏战斗框架，专注于**技能系统、战斗逻辑**。框架采用模块化设计，提供数据驱动的技能编排、事件触发系统、流程引擎等核心能力，支持按需组合以适配不同类型的游戏（MOBA、MMO、ARPG、RTS 等）。核心战斗逻辑以纯 C# runtime 形式实现，可脱离 Unity 环境运行（例如服务器/工具链/单元测试）；与 Unity 强相关的部分主要集中在表现层与少量适配层。

---

## 核心特性

| 特性 | 说明 |
|------|------|
| **逻辑与表现分离** | 纯 C# 逻辑层可在服务器、客户端、编辑器环境下运行，通过事件与表现层解耦 |
| **帧同步确定性** | 支持帧同步、回滚、客户端预测、断线重连，保证多人战斗的确定性 |
| **数据驱动** | 技能、效果、触发器均可通过配置定义，配合可视化编辑器提升效率 |
| **高度可扩展** | 模块化设计，支持 Hook/Feature/Blueprint 扩展机制，按需裁剪 |
| **高性能** | 索引表查询、对象池、流式处理、零 GC 分配优化 |

---

## 架构总览

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              Ability-Kit 框架                               │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                          游戏应用层                                   │   │
│  │                                                                      │   │
│  │   ┌──────────────┐  ┌──────────────┐  ┌──────────────┐             │   │
│  │   │   技能系统   │  │   战斗系统   │  │   录像回放   │             │   │
│  │   │  (Pipeline) │  │  (Combat)   │  │  (Record)   │             │   │
│  │   └──────────────┘  └──────────────┘  └──────────────┘             │   │
│  │                                                                      │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                    │                                        │
│                                    ▼                                        │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                          引擎层                                       │   │
│  │                                                                      │   │
│  │   ┌──────────────┐  ┌──────────────┐  ┌──────────────┐             │   │
│  │   │   流程引擎   │  │   触发系统   │  │   状态机     │             │   │
│  │   │    (Flow)    │  │ (Triggering) │  │   (HFSM)     │             │   │
│  │   └──────────────┘  └──────────────┘  └──────────────┘             │   │
│  │                                                                      │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                    │                                        │
│                                    ▼                                        │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                          世界层                                       │   │
│  │                                                                      │   │
│  │   ┌──────────────┐  ┌──────────────┐  ┌──────────────┐             │   │
│  │   │   依赖注入   │  │    ECS      │  │   帧同步     │             │   │
│  │   │  (World.DI)  │  │ (World.ECS) │  │(FrameSync)  │             │   │
│  │   └──────────────┘  └──────────────┘  └──────────────┘             │   │
│  │                                                                      │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                    │                                        │
│                                    ▼                                        │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                          核心层                                       │   │
│  │                                                                      │   │
│  │   ┌──────────────┐  ┌──────────────┐  ┌──────────────┐             │   │
│  │   │   数学库     │  │   属性系统   │  │   效果系统   │             │   │
│  │   │    (Math)    │  │(Attributes) │  │  (Effects)   │             │   │
│  │   └──────────────┘  └──────────────┘  └──────────────┘             │   │
│  │                                                                      │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 模块速览

### 核心基础设施

| 模块 | 说明 |
|------|------|
| `com.abilitykit.core` | 数学库（Vec2/Vec3/Quat/Transform3）、对象池、日志、事件系统、GameplayTag、数值系统 |
| `com.abilitykit.attributes` | 属性系统，支持 Buff/Debuff、自定义公式、脏标记优化 |
| `com.abilitykit.effects` | 效果系统核心：EffectScope、EffectInstance、EffectRegistry |

### 世界管理层

| 模块 | 说明 |
|------|------|
| `com.abilitykit.world.di` | 依赖注入容器，支持 Singleton/Scoped/Transient 三种生命周期 |
| `com.abilitykit.world.ecs` | 轻量级 ECS 框架：Entity、EntityWorld、ComponentTypeId |
| `com.abilitykit.world.framesync` | 帧同步：FrameSync、Rollback、ClientPrediction、输入历史 |
| `com.abilitykit.world.snapshot` | 快照路由：WorldStateSnapshot 编解码 |
| `com.abilitykit.world.record` | 录像回放：Session、Container、Track，支持输入录制、状态哈希采样 |

### 技能与战斗层

| 模块 | 说明 |
|------|------|
| `com.abilitykit.pipeline` | 技能管线编排：Phase 基类、Timeline、Conditional、Parallel 等 |
| `com.abilitykit.triggering` | 事件触发器：ITrigger、EventBus、TriggerRunner，支持 RPN 条件表达式 |
| `com.abilitykit.ability.runtime` | 技能运行时：Ability、Effect、Triggering、EffectSource |
| `com.abilitykit.ability.explain` | 技能解释/调试框架：Forest、Tree + Navigation Protocol |
| `com.abilitykit.motion` | 移动系统：MotionPipeline、来源组合、碰撞求解 |

| 模块 | 说明 |
|------|------|
| `com.abilitykit.combat.entitymanager` | 实体管理器：索引表实现高效查询 |
| `com.abilitykit.combat.skilllibrary` | 技能库：索引表实现高效技能查询 |
| `com.abilitykit.combat.targeting` | 目标查找：圆形/扇形/矩形范围、流式处理、零 GC |
| `com.abilitykit.combat.projectile` | 投射物系统：对象池、帧同步、命中策略、范围效果 |
| `com.abilitykit.combat.damage` | 伤害系统：DamagePipeline、自定义伤害公式 |

### 运行时与流程层

| 模块 | 说明 |
|------|------|
| `com.abilitykit.host` | 服务器端抽象：World 管理、客户端连接、消息广播 |
| `com.abilitykit.host.extension` | Host 扩展：FrameSync、Rollback、Session、Hook、Feature、Blueprint |
| `com.abilitykit.flow` | 流程编排引擎：Sequence、Race、Parallel、If、Timeout、UsingResource |
| `com.abilitykit.hfsm` | 分层状态机：Hierarchical FSM |

### 网络层

| 模块 | 说明 |
|------|------|
| `com.abilitykit.network.runtime` | 网络运行时抽象 |
| `com.abilitykit.protocol` | 协议定义：客户端/服务器共享协议 |
| `com.abilitykit.protocol.moba` | MOBA 协议定义 |
| `com.abilitykit.protocol.memorypack` | MemoryPack 序列化实现 |

### 示例

| 模块 | 说明 |
|------|------|
| `com.abilitykit.demo.moba.runtime` | MOBA 演示运行时：技能系统、战斗规则、实体管理 |
| `com.abilitykit.demo.moba.view.runtime` | MOBA 演示表现层：Unity 表现、动画、特效、UI |

---

## 核心概念

### World（世界）

World 是独立的游戏逻辑空间，类似"游戏房间"。每个 World 包含 Entities（数据）和 Systems（逻辑），通过 WorldId 标识。框架支持多世界并行管理。

```csharp
// 创建世界
var worldId = await worldManager.CreateWorldAsync();

// 在世界中获取服务
var clock = worldManager.Resolve<IWorldClock>(worldId);
```

### Dependency Injection（依赖注入）

框架提供 WorldContainer 全局容器和 WorldScope 作用域，支持三种生命周期：

| 生命周期 | 说明 |
|----------|------|
| Singleton | 全局单例，整个应用生命周期内唯一 |
| Scoped | 作用域单例，在 World 生命周期内唯一 |
| Transient | 瞬时实例，每次请求都创建新实例 |

### Pipeline（管线系统）

Pipeline 用于编排技能执行流程，Phase（阶段）是基本单元：

| 阶段类型 | 说明 |
|----------|------|
| InstantPhase | 瞬时执行，无需时间 |
| DurationalPhase | 持续执行，占用时间 |
| TimelinePhase | 时间轴阶段，支持关键帧事件 |
| ConditionalBranch | 条件分支，根据条件选择执行路径 |
| Parallel | 并行执行，多个阶段同时运行 |

### Triggering（触发器系统）

基于事件的触发器引擎，支持确定性回放：

```csharp
// 定义触发器
public class OnDamageReceivedTrigger : Trigger<DamageEvent> { }

// 触发条件
[TriggerCondition("Source.Tag == 'Enemy' && Event.Damage > 100")]
public class BossRageTrigger : Trigger<DamageEvent> { }
```

### Flow（流程引擎）

节点树组织异步/时间驱动的逻辑：

```csharp
// 顺序执行
Sequence(
    Log("开始施法"),
    Wait(0.5f),
    If(() => hasTarget,
        Do(() => ApplyEffect()),
        Do(() => ShowMiss())
    ),
    Finally(() => ClearState())
);
```

### FrameSync（帧同步）

确定性保证，支持回滚和预测：

| 功能 | 说明 |
|------|------|
| Rollback | 帧回滚，修正客户端与服务端不一致 |
| ClientPrediction | 客户端预测，提前执行操作 |
| InputHistory | 输入历史，支持断线重连 |

---

## 快速开始

### 环境要求

- Unity 2022.3 LTS 或更高版本
- .NET Standard 2.1

### 安装

1. 打开 Unity Package Manager
2. 点击 `+` → `Add package from git URL`
3. 添加所需的包 URL（参见各模块的 README）

### 创建第一个技能

```csharp
// 1. 定义技能
public class FireballAbility : Ability
{
    protected override AbilityPipeline CreatePipeline()
    {
        return Sequence(
            // 吟唱阶段
            new DurationalPhase("Cast", duration: 0.5f),
            // 施法阶段
            new TimelinePhase("Execute")
                .AddEvent(0.0f, () => SpawnProjectile())
                .AddEvent(0.3f, () => PlayEffect())
                .AddEvent(0.5f, () => DealDamage())
        );
    }
}

// 2. 注册技能
abilityRegistry.Register<FireballAbility>("Fireball");
```

### 配置触发器

```csharp
// 触发条件使用 RPN 表达式
[TriggerCondition("Source.Tag == 'Hero' && Event.Damage > 100 && Source.Health < 0.3")]
public class LowHealthTrigger : Trigger<DamageEvent>
{
    protected override void Execute(DamageEvent evt, ExecCtx ctx)
    {
        // 触发时激活狂怒效果
        ctx.Source.GetComponent<AttributeComponent>()
            .Modify("AttackBonus", ModifierType.PercentAdd, 50);
    }
}
```

---

## 文档导航

详细设计文档位于各模块的 `Document/` 目录下：

| 模块 | 文档 |
|------|------|
| [技术选型](./Unity/Packages/技术选型文档.md) | 从零开发战斗框架的技术选型 |
| [Host 模块](./Unity/Packages/com.abilitykit.host.extension/Document/) | 游戏服务器运行时框架 |
| [World DI](./Unity/Packages/com.abilitykit.world.di/Document/) | 依赖注入与组合系统 |
| [Flow 模块](./Unity/Packages/com.abilitykit.flow/Document/) | 流程编排引擎 |
| [Pipeline](./Unity/Packages/com.abilitykit.pipeline/Document/) | 技能管线编排 |
| [Triggering](./Unity/Packages/com.abilitykit.triggering/Document/) | 事件触发器系统 |
| [Targeting](./Unity/Packages/com.abilitykit.combat.targeting/Document/) | 目标查找框架 |
| [Projectile](./Unity/Packages/com.abilitykit.combat.projectile/Document/) | 投射物系统 |
| [帧同步](./Unity/Packages/com.abilitykit.host.extension/Runtime/FrameSync/README.md) | 帧同步与回滚 |

---

## License

MIT License
