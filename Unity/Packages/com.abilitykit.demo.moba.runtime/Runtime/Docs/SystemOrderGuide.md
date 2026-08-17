# 系统执行顺序规范

本文档定义了 moba.runtime 战斗逻辑世界层的 ECS 系统执行顺序标准。

## 1. 概述

系统（System）执行顺序通过 `MobaSystemOrder` 常量类集中管理，基于 `WorldSystemOrder` 的整数偏移量实现。

## 2. 排序架构

```
WorldSystemOrder.CoreBase (框架基准)
    │
    └── MobaSystemOrder.Base = CoreBase + 1000 (业务层基准)
            │
            ├── WorldSystemPhase.PreExecute  (Early)
            │       ├── EntityManagerSync (-100)
            │       └── MotionInit (-90)
            │
            ├── WorldSystemPhase.Execute    (Normal)
            │       ├── MotionInput (10)
            │       ├── MotionTick (50)
            │       ├── SkillInput (80)
            │       ├── SkillPipelines (100)
            │       ├── Effects (200)
            │       ├── BuffApply (300)
            │       └── BuffTick (310)
            │
            └── WorldSystemPhase.PostExecute (Late)
                    ├── ProjectileSync (+10)
                    ├── AreaSync (+11)
                    └── EntityCleanup (+5)
```

## 3. 系统排序常量

```csharp
public static class MobaSystemOrder
{
    // 基准值 = 框架 CoreBase + 1000
    public const int Base = WorldSystemOrder.CoreBase + 1000;

    // ========== PreExecute (Early) ==========
    public const int EntityManagerSync = Base + WorldSystemOrder.Early + 5;
    public const int MotionInit = Base + WorldSystemOrder.Early + 10;

    // ========== Execute (Normal) ==========
    public const int MotionLocomotionInput = Base + WorldSystemOrder.Normal + 10;
    public const int MotionTick = Base + WorldSystemOrder.Normal + 50;
    public const int PassiveSkillTriggers = Base + WorldSystemOrder.Normal + 85;
    public const int SkillPipelines = Base + WorldSystemOrder.Normal + 100;
    public const int EffectsStep = Base + WorldSystemOrder.Normal + 200;
    public const int BuffCommandsDrain = Base + WorldSystemOrder.Normal + 295;
    public const int BuffsApply = Base + WorldSystemOrder.Normal + 300;
    public const int BuffsTick = Base + WorldSystemOrder.Normal + 310;

    // ========== Execute (Normal) ==========
    public const int ContinuousPeriodicTick = Base + WorldSystemOrder.Normal + 315;
    public const int EntityManagerCleanup = Base + WorldSystemOrder.Late + 5;
    public const int ProjectileSync = Base + WorldSystemOrder.Late + 10;
    public const int AreaSync = Base + WorldSystemOrder.Late + 11;
    public const int ProjectileLauncherCleanup = Base + WorldSystemOrder.Late + 15;
}
```

## 4. Phase 定义

| Phase | 执行时机 | 使用场景 |
|-------|---------|---------|
| `WorldSystemPhase.PreExecute` | 主循环前 | 实体同步、初始化 |
| `WorldSystemPhase.Execute` | 主循环 | 核心游戏逻辑 |
| `WorldSystemPhase.PostExecute` | 主循环后 | 清理、同步 |

## 5. 系统注册模板

### 5.1 基础系统注册

```csharp
[WorldSystem(order: MobaSystemOrder.MySystem, Phase = WorldSystemPhase.Execute)]
public sealed class MySystem : WorldSystemBase
{
    protected override void OnExecute()
    {
        // 实现游戏逻辑
    }
}
```

### 5.2 多阶段系统

```csharp
[WorldSystem(order: MobaSystemOrder.MySystemInit, Phase = WorldSystemPhase.PreExecute)]
public sealed class MySystemInit : WorldSystemBase
{
    protected override void OnInit()
    {
        // 初始化逻辑
    }
}

[WorldSystem(order: MobaSystemOrder.MySystemTick, Phase = WorldSystemPhase.Execute)]
public sealed class MySystemTick : WorldSystemBase
{
    protected override void OnExecute()
    {
        // 每帧逻辑
    }
}

[WorldSystem(order: MobaSystemOrder.MySystemCleanup, Phase = WorldSystemPhase.PostExecute)]
public sealed class MySystemCleanup : WorldSystemBase
{
    protected override void OnCleanup()
    {
        // 清理逻辑
    }
}
```

## 6. 执行顺序设计原则

### 6.1 输入优先

```
移动输入 → 技能输入 → 技能释放 → 伤害计算 → Buff 应用 → 位置同步
```

### 6.2 数据流顺序

```
Entity Sync (Pre) → Input Processing → Core Logic → Effects → Buffs → Cleanup (Post)
```

### 6.3 分离读写

| 类型 | 说明 | 示例 |
|-----|------|------|
| Reader | 只读数据 | 查询实体位置 |
| Writer | 修改数据 | 应用伤害 |

> 重要：Entitas 默认单线程执行，无需显式分离读写。

## 7. 添加新系统的检查清单

添加新系统时，确保：

- [ ] 在 `MobaSystemOrder` 中定义常量
- [ ] 选择正确的 Phase（Pre/Execute/Post）
- [ ] 计算正确的偏移量（避免冲突）
- [ ] 使用 `[WorldSystem]` 特性注册
- [ ] 继承 `WorldSystemBase`
- [ ] 实现必要的生命周期方法

## 8. 系统分类参考

### PreExecute（早期）

| 系统 | Order | 职责 |
|-----|-------|------|
| EntityManagerSync | Base + Early + 5 | 实体管理器同步 |
| MotionInit | Base + Early + 10 | 移动系统初始化 |

### Execute（常规）

| 系统 | Order | 职责 |
|-----|-------|------|
| MotionLocomotionInput | Base + Normal + 10 | 移动输入处理 |
| MotionTick | Base + Normal + 50 | 移动 tick |
| PassiveSkillTriggers | Base + Normal + 85 | 被动技能触发 |
| SkillPipelines | Base + Normal + 100 | 技能管道执行 |
| EffectsStep | Base + Normal + 200 | 效果步骤 |
| BuffsApply | Base + Normal + 300 | Buff 应用 |
| BuffsTick | Base + Normal + 310 | Buff tick |

### PostExecute（后期）

| 系统 | Order | 职责 |
|-----|-------|------|
| ContinuousPeriodicTick | Base + Normal + 315 | 正式持续周期行为 tick |
| EntityManagerCleanup | Base + Late + 5 | 实体管理器清理 |
| ProjectileSync | Base + Late + 10 | 投射物同步 |
| AreaSync | Base + Late + 11 | 区域同步 |

## 9. 常见问题

### 9.1 系统执行顺序冲突

**问题**：多个系统使用相同 Order 值

**解决**：确保 Order 值唯一，参考现有系统添加适当偏移

### 9.2 依赖系统未执行

**问题**：系统 A 依赖系统 B 的输出，但 A 先执行

**解决**：将系统 A 的 Order 设置为 B + 适当偏移

### 9.3 循环依赖

**问题**：系统 A 依赖 B，B 依赖 A

**解决**：重构逻辑，消除循环依赖，或合并为一个系统
