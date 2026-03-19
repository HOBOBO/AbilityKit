# Ability-Kit MOBA Buff 与被动技能模块开发设计文档

> **阅读对象**：希望了解 MOBA 游戏中 Buff 系统与被动技能触发机制如何设计的开发者
>
> **文档目标**：让你理解"Buff 的生命周期管理"、"效果阶段执行"、"被动技能的触发机制"、"与技能管线的协作关系"

---

## 一、设计理念：Buff 与被动技能在游戏中的作用

### 1.1 传统实现的问题

```
❌ 传统 Buff 实现的问题：

┌─────────────────────────────────────────────────────────────────────────┐
│                                                                         │
│  1. Buff 逻辑耦合在技能代码中                                           │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │  public void OnSkillHit(Skill skill, Unit target)              │  │
│  │  {                                                              │  │
│  │      target.AddBuff("Poison", duration: 5f);                   │  │
│  │      target.PoisonDamage += 10f;                               │  │
│  │  }                                                              │  │
│  │                                                                  │  │
│  │  问题：Buff 效果和触发逻辑耦合，难以复用                         │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
│  2. Buff 状态管理混乱                                                   │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │  问题：                                                           │  │
│  │  - 叠加规则不统一（覆盖 vs 刷新 vs 叠加）                        │  │
│  │  - 移除时机不明确（时间到 vs 主动移除 vs 被驱散）               │  │
│  │  - 生命周期回调分散在各处                                        │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
│  3. 被动技能缺少统一管理                                                │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │  问题：                                                           │  │
│  │  - 触发条件硬编码                                                │  │
│  │  - 冷却管理混乱                                                  │  │
│  │  - 与 Buff 系统重复设计                                          │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 1.2 Ability-Kit 的解决方案

```
✅ Ability-Kit 的设计思路：

┌─────────────────────────────────────────────────────────────────────────┐
│                                                                         │
│  【Buff 系统】                                                          │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │                                                                  │  │
│  │   核心概念：Buff = 配置化 + 阶段化 + 效果化                        │  │
│  │                                                                  │  │
│  │   配置化：BuffMO 定义 Buff 的所有属性                            │  │
│  │   阶段化：OnAdd → OnInterval → OnRemove                          │  │
│  │   效果化：每个阶段关联 Effect 列表                               │  │
│  │                                                                  │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
│  【被动技能系统】                                                       │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │                                                                  │  │
│  │   核心概念：被动技能 = 被动监听 + 触发器 + 技能管线               │  │
│  │                                                                  │  │
│  │   被动监听：PassiveSkillTriggerListener                         │  │
│  │   触发器：TriggerIds → Trigger 计划                             │  │
│  │   技能管线：复用主动技能的技能管线                               │  │
│  │                                                                  │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
│  【共享机制】                                                           │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │                                                                  │  │
│  │   EffectSourceContext：统一的上下文管理                         │  │
│  │   TriggerSystem：统一的事件触发机制                             │  │
│  │   OngoingTriggerPlans：持续的触发计划                           │  │
│  │                                                                  │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 二、Buff 系统架构

### 2.1 核心组件关系

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         Buff 系统组件关系图                              │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ┌──────────────────┐                                                  │
│  │    BuffMO        │  ← 配置数据（由配置表生成）                      │
│  │                  │                                                  │
│  │  - Id           │                                                  │
│  │  - Name         │                                                  │
│  │  - DurationMs   │                                                  │
│  │  - IntervalMs   │                                                  │
│  │  - OnAddEffects │                                                  │
│  │  - OnInterval   │                                                  │
│  │    Effects      │                                                  │
│  │  - OnRemove     │                                                  │
│  │    Effects      │                                                  │
│  │  - TriggerIds   │                                                  │
│  │  - Stacking     │                                                  │
│  │    Policy       │                                                  │
│  └────────┬─────────┘                                                  │
│           │                                                             │
│           ▼                                                             │
│  ┌──────────────────┐                                                  │
│  │  BuffRuntime    │  ← 运行时实例（存储在实体组件中）                 │
│  │                  │                                                  │
│  │  - BuffId       │                                                  │
│  │  - Remaining    │  剩余持续时间                                     │
│  │  - Interval     │  周期剩余时间                                     │
│  │    Remaining    │                                                  │
│  │  - SourceId     │  来源标识                                        │
│  │  - StackCount   │  叠加层数                                        │
│  │  - SourceContext│  EffectSource 上下文                              │
│  └────────┬─────────┘                                                  │
│           │                                                             │
│           ▼                                                             │
│  ┌──────────────────┐                                                  │
│  │  BuffsComponent  │  ← Entitas 组件                                 │
│  │  (Entity 持有)   │                                                  │
│  │                  │                                                  │
│  │  Active: List<  │                                                  │
│  │    BuffRuntime> │                                                  │
│  └────────┬─────────┘                                                  │
│           │                                                             │
│           ▼                                                             │
│  ┌──────────────────┐                                                  │
│  │  ApplyBuffRequest│  ← 申请组件（触发应用的条件）                    │
│  │  (Entity 持有)   │                                                  │
│  │                  │                                                  │
│  │  - BuffId       │                                                  │
│  │  - SourceId     │                                                  │
│  │  - DurationMs   │                                                  │
│  │  - Origin...    │                                                  │
│  └──────────────────┘                                                  │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 2.2 Buff 生命周期

```
┌─────────────────────────────────────────────────────────────────────────┐
│                          Buff 生命周期                                  │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │                                                                 │  │
│  │                        ApplyBuffRequest                         │  │
│  │                        （申请应用）                             │  │
│  │                              │                                  │  │
│  │                              ▼                                  │  │
│  │  ┌─────────────────────────────────────────────────────────┐   │  │
│  │  │                  MobaBuffApplySystem                    │   │  │
│  │  │                   (Order: BuffsApply)                   │   │  │
│  │  │                                                         │   │  │
│  │  │  1. 查找/创建 BuffRuntime                              │   │  │
│  │  │  2. 应用叠加策略（Create/Refresh/Replace）              │   │  │
│  │  │  3. 创建 EffectSourceContext                           │   │  │
│  │  │  4. 执行 OnAddEffects                                   │   │  │
│  │  │  5. 启动周期性效果                                      │   │  │
│  │  │  6. 注册触发计划                                        │   │  │
│  │  │  7. 发布事件                                            │   │  │
│  │  └─────────────────────────────────────────────────────────┘   │  │
│  │                              │                                  │  │
│  │                              ▼                                  │  │
│  │  ┌─────────────────────────────────────────────────────────┐   │  │
│  │  │                    活跃中                                 │   │  │
│  │  │                                                         │   │  │
│  │  │  MobaBuffTickSystem                                     │   │  │
│  │  │  (Order: BuffsTick)                                    │   │  │
│  │  │                                                         │   │  │
│  │  │  每帧：                                                 │   │  │
│  │  │  ├── 减少 Remaining                                     │   │  │
│  │  │  ├── 周期效果触发（Interval）                          │   │  │
│  │  │  └── 检查是否到期                                       │   │  │
│  │  │                                                         │   │  │
│  │  └─────────────────────────────────────────────────────────┘   │  │
│  │                              │                                  │  │
│  │              ┌───────────────┴───────────────┐                  │  │
│  │              │                               │                  │  │
│  │              ▼                               ▼                  │  │
│  │  ┌───────────────────────┐    ┌───────────────────────┐      │  │
│  │  │  时间到期              │    │  RemoveBuffRequest    │      │  │
│  │  │  Expired              │    │  (主动移除)           │      │  │
│  │  └───────────┬───────────┘    └───────────┬───────────┘      │  │
│  │              │                            │                  │  │
│  │              └───────────────┬─────────────┘                  │  │
│  │                              ▼                                │  │
│  │  ┌─────────────────────────────────────────────────────────┐   │  │
│  │  │                  MobaBuffRemoveSystem                   │   │  │
│  │  │                   (Order: BuffsRemove)                 │   │  │
│  │  │                                                         │   │  │
│  │  │  1. 结束 EffectSourceContext                           │   │  │
│  │  │  2. 停止周期性效果                                      │   │  │
│  │  │  3. 移除触发计划                                        │   │  │
│  │  │  4. 执行 OnRemoveEffects                               │   │  │
│  │  │  5. 清理监听器                                         │   │  │
│  │  │  6. 发布事件                                            │   │  │
│  │  └─────────────────────────────────────────────────────────┘   │  │
│  │                              │                                  │  │
│  │                              ▼                                  │  │
│  │                    ┌─────────────────┐                        │  │
│  │                    │     移除完成     │                        │  │
│  │                    └─────────────────┘                        │  │
│  │                                                                 │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 2.3 叠加策略

```csharp
namespace AbilityKit.Ability.Impl.Moba
{
    /// <summary>
    /// Buff 叠加策略
    /// </summary>
    public enum BuffStackingPolicy
    {
        /// <summary>
        /// 不叠加，刷新持续时间
        /// </summary>
        RefreshDuration = 0,

        /// <summary>
        /// 不叠加，替换现有
        /// </summary>
        Replace = 1,

        /// <summary>
        /// 叠加层数
        /// </summary>
        Stack = 2,

        /// <summary>
        /// 不叠加，保持现有
        /// </summary>
        Ignore = 3,
    }

    /// <summary>
    /// Buff 刷新策略
    /// </summary>
    public enum BuffRefreshPolicy
    {
        /// <summary>
        /// 刷新所有时间
        /// </summary>
        RefreshAll = 0,

        /// <summary>
        /// 刷新剩余时间
        /// </summary>
        RefreshRemaining = 1,

        /// <summary>
        /// 不刷新
        /// </summary>
        NoRefresh = 2,
    }
}
```

---

## 三、效果阶段执行

### 3.1 阶段定义

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         Buff 效果阶段                                    │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  【OnAdd 阶段】应用时执行                                                │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │                                                                  │  │
│  │  时机：Buff 被应用到目标时                                       │  │
│  │  用途：                                                           │  │
│  │  - 施加属性修改                                                  │  │
│  │  - 触发特效 Cue                                                  │  │
│  │  - 授予标签                                                      │  │
│  │                                                                  │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
│  【OnInterval 阶段】周期性执行                                          │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │                                                                  │  │
│  │  时机：Buff 持续期间，每隔 IntervalMs 执行一次                   │  │
│  │  用途：                                                           │  │
│  │  - 持续伤害（DOT）                                               │  │
│  │  - 持续治疗                                                      │  │
│  │  - 周期性触发效果                                                │  │
│  │                                                                  │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
│  【OnRemove 阶段】移除时执行                                            │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │                                                                  │  │
│  │  时机：Buff 被移除时                                             │  │
│  │  用途：                                                           │  │
│  │  - 移除属性修改                                                  │  │
│  │  - 清理特效                                                      │  │
│  │  - 移除标签                                                      │  │
│  │                                                                  │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 3.2 阶段效果执行器

```csharp
namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    /// <summary>
    /// Buff 阶段效果执行器
    /// </summary>
    internal sealed class BuffStageEffectExecutor
    {
        private readonly MobaEffectInvokerService _invoker;

        public void Execute(
            IReadOnlyList<int> effectIds,
            int buffId,
            int sourceActorId,
            int targetActorId,
            long sourceContextId)
        {
            if (_invoker == null) return;
            if (effectIds == null || effectIds.Count == 0) return;

            for (int i = 0; i < effectIds.Count; i++)
            {
                var effectId = effectIds[i];
                if (effectId <= 0) continue;

                // 执行效果，传入 BuffId 作为共享数据
                _invoker.Execute(
                    effectId: effectId,
                    sourceActorId: sourceActorId,
                    targetActorId: targetActorId,
                    contextKind: (int)EffectContextKind.Buff,
                    sourceContextId: sourceContextId,
                    configure: ctx =>
                    {
                        ctx.SharedData[MobaBuffTriggering.Args.BuffId] = buffId;
                    });
            }
        }
    }
}
```

### 3.3 配置数据示例

```csharp
// BuffMO 配置示例
public sealed class BuffMO
{
    public int Id { get; }                    // Buff 唯一标识
    public string Name { get; }               // Buff 名称
    public int DurationMs { get; }            // 持续时间（毫秒）
    public int IntervalMs { get; }            // 周期执行间隔（毫秒）

    // 效果阶段
    public IReadOnlyList<int> OnAddEffects { get; }       // 应用时执行的效果
    public IReadOnlyList<int> OnIntervalEffects { get; }  // 周期执行的效果
    public IReadOnlyList<int> OnRemoveEffects { get; }     // 移除时执行的效果

    // 叠加配置
    public BuffStackingPolicy StackingPolicy { get; }      // 叠加策略
    public BuffRefreshPolicy RefreshPolicy { get; }         // 刷新策略
    public int MaxStacks { get; }                          // 最大叠加层数

    // 触发器
    public IReadOnlyList<int> TriggerIds { get; }          // 关联的触发器 ID
    public IReadOnlyList<int> Tags { get; }                // Buff 标签
}
```

---

## 四、Buff 系统服务组件

### 4.1 组件职责表

| 组件 | 职责 |
|------|------|
| `BuffRepository` | 实体 Buff 列表的存储和访问 |
| `BuffStackingPolicyApplier` | 应用叠加策略（创建/刷新/替换） |
| `BuffContextService` | EffectSourceContext 的创建和销毁 |
| `BuffEventPublisher` | 发布 Buff 相关事件到 EventBus |
| `BuffPeriodicEffectBinder` | 绑定周期性效果到 OngoingEffectService |
| `BuffStageEffectExecutor` | 执行各阶段的效果列表 |
| `MobaBuffService` | 命令队列管理，处理命令洪泛 |

### 4.2 Buff 上下文服务

```csharp
namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    /// <summary>
    /// Buff 上下文服务
    /// 负责 EffectSourceContext 的生命周期管理
    /// </summary>
    internal sealed class BuffContextService
    {
        private readonly EffectSourceRegistry _effectSource;
        private readonly ITriggerActionRunner _actionRunner;
        private readonly IFrameTime _frameTime;

        /// <summary>
        /// 确保 Buff 运行时具有有效的 EffectSourceContext
        /// </summary>
        public void EnsureBuffContext(
            BuffRuntime rt,
            int buffId,
            int sourceActorId,
            int targetActorId,
            object originSource,
            object originTarget,
            long parentContextId)
        {
            if (rt.SourceContextId != 0) return; // 已有上下文，跳过

            var frame = GetFrame();

            if (parentContextId != 0)
            {
                // 创建子上下文
                rt.SourceContextId = _effectSource.CreateChild(
                    parentContextId,
                    kind: EffectSourceKind.Buff,
                    configId: buffId,
                    sourceActorId: sourceActorId,
                    targetActorId: targetActorId,
                    frame: frame,
                    originSource: originSource,
                    originTarget: originTarget);
            }
            else
            {
                // 创建根上下文
                rt.SourceContextId = _effectSource.CreateRoot(
                    kind: EffectSourceKind.Buff,
                    configId: buffId,
                    sourceActorId: sourceActorId,
                    targetActorId: targetActorId,
                    frame: frame,
                    originSource: originSource,
                    originTarget: originTarget);
            }
        }

        /// <summary>
        /// 结束 Buff 上下文
        /// </summary>
        public void EndByRuntime(BuffRuntime rt, EffectSourceEndReason reason)
        {
            if (rt.SourceContextId == 0) return;

            // 1. 取消所有关联的动作
            _actionRunner?.CancelByOwnerKey(rt.SourceContextId);

            // 2. 结束 EffectSource
            _effectSource?.End(rt.SourceContextId, GetFrame(), reason);

            rt.SourceContextId = 0;
        }
    }
}
```

### 4.3 事件发布

```csharp
namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    /// <summary>
    /// Buff 事件发布器
    /// </summary>
    internal sealed class BuffEventPublisher
    {
        private readonly IEventBus _eventBus;

        /// <summary>
        /// 发布应用/刷新事件
        /// </summary>
        public void PublishApplyOrRefresh(
            BuffMO buff,
            int sourceActorId,
            int targetActorId,
            float durationSeconds,
            BuffRuntime runtime) { /* ... */ }

        /// <summary>
        /// 发布移除事件
        /// </summary>
        public void PublishRemove(
            BuffMO buff,
            int sourceActorId,
            int targetActorId,
            BuffRuntime runtime,
            EffectSourceEndReason reason) { /* ... */ }

        /// <summary>
        /// 发布周期触发事件
        /// </summary>
        public void PublishInterval(
            BuffMO buff,
            int sourceActorId,
            int targetActorId,
            BuffRuntime runtime) { /* ... */ }

        /// <summary>
        /// 发布每个效果的独立事件
        /// </summary>
        public void PublishPerEffect(
            string baseEventId,
            IReadOnlyList<int> effectIds,
            string stage,
            int sourceActorId,
            int targetActorId,
            BuffRuntime runtime) { /* ... */ }
    }
}
```

---

## 五、被动技能触发系统

### 5.1 被动技能架构

```
┌─────────────────────────────────────────────────────────────────────────┐
│                       被动技能触发架构                                  │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ┌──────────────────┐                                                  │
│  │ PassiveSkillMO   │  ← 配置数据                                      │
│  │                  │                                                  │
│  │  - Id           │                                                  │
│  │  - CooldownMs   │  冷却时间                                         │
│  │  - TriggerIds   │  触发器列表                                       │
│  └────────┬─────────┘                                                  │
│           │                                                             │
│           ▼                                                             │
│  ┌──────────────────┐                                                  │
│  │ PassiveSkill     │  ← 被动技能运行时                                │
│  │ Runtime          │                                                  │
│  │                  │                                                  │
│  │  - PassiveSkillId│                                                  │
│  │  - Level         │                                                  │
│  │  - Cooldown      │                                                  │
│  │    EndTimeMs    │                                                  │
│  └────────┬─────────┘                                                  │
│           │                                                             │
│           ▼                                                             │
│  ┌──────────────────┐                                                  │
│  │ SkillLoadout     │  ← 技能栏（Entity 组件）                         │
│  │ Component        │                                                  │
│  │                  │                                                  │
│  │  ActiveSkills[] │  主动技能列表                                     │
│  │  PassiveSkills[]│  被动技能列表                                     │
│  └────────┬─────────┘                                                  │
│           │                                                             │
│           ▼                                                             │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │              PassiveSkillTriggerListenerManager                    │  │
│  │                                                           │      │  │
│  │  负责：                                                        │      │  │
│  │  1. 注册被动技能监听器                                          │      │  │
│  │  2. 创建 EffectSourceContext                                   │      │  │
│  │  3. 注销时清理资源                                             │      │  │
│  └────────┬────────────────────────────────────────────────────────┘  │
│           │                                                             │
│           ▼                                                             │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │              PassiveSkillTriggerEventArgs                         │  │
│  │                                                           │      │  │
│  │  被动触发时传递的事件参数：                                   │      │  │
│  │  - PassiveSkillId                                            │      │  │
│  │  - TriggerId                                                  │      │  │
│  │  - SourceContextId                                           │      │  │
│  │  - SourceActorId / TargetActorId                            │      │  │
│  │  - SkillId / SkillSlot / SkillLevel                         │      │  │
│  │  - AimPos / AimDir                                           │      │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 5.2 被动技能监听器管理器

```csharp
namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    /// <summary>
    /// 被动技能触发监听器管理器
    /// </summary>
    internal sealed class PassiveSkillTriggerListenerManager
    {
        private readonly MobaConfigDatabase _configs;
        private readonly EffectSourceRegistry _effectSource;
        private readonly ITriggerActionRunner _actionRunner;

        /// <summary>
        /// 尝试注册被动技能监听器
        /// </summary>
        public void TryRegister(
            ActorEntity entity,
            int frame,
            List<Registration> outRegistrations)
        {
            if (entity == null) return;
            if (!entity.hasSkillLoadout) return;

            var passiveSkills = entity.skillLoadout.PassiveSkills;
            var listeners = EnsureListenerContainer(entity);

            // 移除不再需要的监听器
            RemoveObsoleteListeners(listeners, desired, frame);

            // 注册新的被动技能
            for (int i = 0; i < passiveSkills.Length; i++)
            {
                var rt = passiveSkills[i];
                if (ContainsListener(listeners, rt.PassiveSkillId)) continue;

                var l = new PassiveSkillTriggerListenerRuntime
                {
                    PassiveSkillId = rt.PassiveSkillId,
                };

                // 确保有 EffectSourceContext
                EnsurePassiveSkillContext(entity, listeners, rt.PassiveSkillId, l, frame);

                listeners.Add(l);
                outRegistrations?.Add(new Registration(mo, l));
            }
        }

        /// <summary>
        /// 确保被动技能有 EffectSourceContext
        /// </summary>
        private void EnsurePassiveSkillContext(
            ActorEntity entity,
            List<PassiveSkillTriggerListenerRuntime> listeners,
            int passiveSkillId,
            PassiveSkillTriggerListenerRuntime l,
            int frame)
        {
            if (l.SourceContextId != 0) return;

            l.SourceContextId = _effectSource.CreateRoot(
                kind: EffectSourceKind.System,
                configId: passiveSkillId,
                sourceActorId: entity.actorId.Value,
                targetActorId: entity.actorId.Value,
                frame: frame);
        }
    }
}
```

### 5.3 被动技能触发事件

```csharp
namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    /// <summary>
    /// 被动技能触发事件参数
    /// </summary>
    public readonly struct PassiveSkillTriggerEventArgs
    {
        public readonly int PassiveSkillId;
        public readonly int TriggerId;
        public readonly long SourceContextId;
        public readonly int SourceActorId;
        public readonly int TargetActorId;
        public readonly int SkillId;
        public readonly int SkillSlot;
        public readonly int SkillLevel;
        public readonly Vec3 AimPos;
        public readonly Vec3 AimDir;
        public readonly int IsExternalEvent;
        public readonly EffectSourceKind OriginKind;
        public readonly int OriginConfigId;
        public readonly long OriginContextId;
        public readonly int OriginSourceActorId;
        public readonly int OriginTargetActorId;
    }
}
```

---

## 六、触发计划（Ongoing Trigger Plans）

### 6.1 触发计划概念

```
┌─────────────────────────────────────────────────────────────────────────┐
│                       Ongoing Trigger Plans                              │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  【问题】：当一个 Buff 或被动技能被移除时，如何清理它注册的触发器？        │
│                                                                         │
│  【解决方案】：OngoingTriggerPlans                                       │
│                                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │                                                                 │  │
│  │  OngoingTriggerPlansComponent                                   │  │
│  │  ├── Revision: int                                             │  │
│  │  └── Active: List<OngoingTriggerPlanEntry>                     │  │
│  │                                                                 │  │
│  │  OngoingTriggerPlanEntry                                        │  │
│  │  ├── OwnerKey: long       // 来源 Key（如 SourceContextId）     │  │
│  │  └── TriggerIds: int[]    // 关联的触发器 ID 列表              │  │
│  │                                                                 │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
│  【工作流程】：                                                         │
│                                                                         │
│  1. 应用 Buff/注册被动技能时：                                          │
│     - 创建 OngoingTriggerPlanEntry（OwnerKey + TriggerIds）            │
│     - 添加到 entity.ongoingTriggerPlans.Active                        │
│                                                                         │
│  2. 移除 Buff/注销被动技能时：                                         │
│     - 查找 OwnerKey 对应的 Entry                                      │
│     - 移除该 Entry（同时移除所有关联的触发器）                          │
│     - 更新 Revision                                                    │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 6.2 触发计划管理

```csharp
namespace AbilityKit.Ability.Share.Impl.Moba.Systems.Buffs
{
    /// <summary>
    /// 更新实体的触发计划
    /// </summary>
    private static void TryUpsertOngoingTriggerPlans(
        ActorEntity e,
        long ownerKey,
        BuffMO buff)
    {
        if (buff.TriggerIds == null || buff.TriggerIds.Count == 0)
        {
            // 没有触发器，移除现有的
            RemoveOngoingTriggerPlansEntry(e, ownerKey);
            return;
        }

        var ids = new int[buff.TriggerIds.Count];
        for (int i = 0; i < buff.TriggerIds.Count; i++)
            ids[i] = buff.TriggerIds[i];

        var oldList = e.hasOngoingTriggerPlans
            ? e.ongoingTriggerPlans.Active
            : null;

        var newList = /* 构建新列表，替换/添加对应 OwnerKey 的 Entry */;

        // 更新实体组件
        var rev = e.hasOngoingTriggerPlans
            ? e.ongoingTriggerPlans.Revision + 1
            : 1;

        if (e.hasOngoingTriggerPlans)
            e.ReplaceOngoingTriggerPlans(newList, rev);
        else
            e.AddOngoingTriggerPlans(newList, rev);
    }
}
```

---

## 七、技能管线集成

### 7.1 技能管线执行

```
┌─────────────────────────────────────────────────────────────────────────┐
│                       技能管线执行流程                                  │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  MobaSkillPipelineStepSystem                                           │
│  (Order: SkillPipelines)                                               │
│                                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │                                                                 │  │
│  │  SkillPipelineRunner                                            │  │
│  │                                                                 │  │
│  │  ┌─────────────┐                                               │  │
│  │  │ PreCast    │  ← 前摇阶段（可选）                           │  │
│  │  │ Pipeline   │                                               │  │
│  │  └──────┬──────┘                                               │  │
│  │         │                                                       │  │
│  │         │ 成功                                                  │  │
│  │         ▼                                                       │  │
│  │  ┌─────────────┐                                               │  │
│  │  │ Cast        │  ← 施法阶段                                   │  │
│  │  │ Pipeline    │                                               │  │
│  │  │             │  包含 Timeline：                              │  │
│  │  │             │  - TimelineEvent[]                           │  │
│  │  │             │  - 按时间顺序触发                              │  │
│  │  └──────┬──────┘                                               │  │
│  │         │                                                       │  │
│  │         │ 完成/失败                                              │  │
│  │         ▼                                                       │  │
│  │  ┌─────────────┐                                               │  │
│  │  │ Cleanup     │  ← 清理阶段                                   │  │
│  │  │             │                                               │  │
│  │  └─────────────┘                                               │  │
│  │                                                                 │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 7.2 技能管线运行快照

```csharp
namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    /// <summary>
    /// 技能管线运行快照
    /// 用于同步和调试
    /// </summary>
    public readonly struct RunningSnapshot
    {
        public readonly long InstanceId;          // 实例 ID
        public readonly int OwnerActorId;        // 施法者
        public readonly int SkillId;            // 技能 ID
        public readonly int SkillSlot;          // 技能槽位
        public readonly int SkillLevel;         // 技能等级
        public readonly int StartFrame;         // 开始帧
        public readonly int Sequence;           // 序列号
        public readonly int TargetActorId;      // 目标
        public readonly Vec3 AimPos;            // 目标位置
        public readonly Vec3 AimDir;            // 目标方向
        public readonly SkillCastStage Stage;   // 当前阶段
        public readonly int ElapsedMs;          // 已流逝时间
        public readonly int NextEventIndex;     // 下一个事件索引
    }
}
```

---

## 八、执行时序图

### 8.1 Buff 完整应用时序

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        Buff 应用完整时序                                 │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  施法者                    目标实体               系统                    │
│    │                          │                    │                     │
│    │  释放技能                 │                    │                     │
│    │  ─────────────────────────▶│                    │                     │
│    │                          │                    │                     │
│    │                          │  ApplyBuffRequest   │                     │
│    │                          │  ──────────────────▶│                     │
│    │                          │                    │                     │
│    │                          │                    │  MobaBuffApplySystem│
│    │                          │                    │  ┌─────────────────┐│
│    │                          │                    │  │ 1.查找Buff配置  ││
│    │                          │                    │  │ 2.查找/创建     ││
│    │                          │                    │  │   BuffRuntime   ││
│    │                          │                    │  │ 3.应用叠加策略  ││
│    │                          │                    │  │ 4.创建Context   ││
│    │                          │                    │  │ 5.执行OnAdd     ││
│    │                          │                    │  │   Effects       ││
│    │                          │                    │  │ 6.启动周期效果  ││
│    │                          │                    │  │ 7.注册触发计划  ││
│    │                          │                    │  │ 8.发布事件      ││
│    │                          │                    │  └─────────────────┘│
│    │                          │                    │                     │
│    │                          │   Buff 已应用     │                     │
│    │  ◀────────────────────────│                    │                     │
│    │                          │                    │                     │
│    │                          │                    │  MobaBuffTickSystem │
│    │                          │                    │  ┌─────────────────┐│
│    │                          │                    │  │ 每帧Tick:       ││
│    │                          │                    │  │ - Remaining--  ││
│    │                          │                    │  │ - Interval倒计时││
│    │                          │                    │  │ - 到期则移除   ││
│    │                          │                    │  └─────────────────┘│
│    │                          │                    │                     │
└─────────────────────────────────────────────────────────────────────────┘
```

### 8.2 被动技能触发时序

```
┌─────────────────────────────────────────────────────────────────────────┐
│                      被动技能触发时序                                    │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  目标实体               触发器系统              被动技能系统            │
│    │                          │                    │                     │
│    │  SkillLoadout 变化       │                    │                     │
│    │  ───────────────────────▶│                    │                     │
│    │                          │                    │                     │
│    │                          │                    │ MobaPassiveSkill    │
│    │                          │                    │ TriggerRegister      │
│    │                          │                    │ ┌─────────────────┐ │
│    │                          │                    │ │ 1.遍历被动技能  │ │
│    │                          │                    │ │ 2.注册监听器   │ │
│    │                          │                    │ │ 3.创建Context  │ │
│    │                          │                    │ │ 4.更新触发计划 │ │
│    │                          │                    │ └─────────────────┘ │
│    │                          │                    │                     │
│    │  触发事件                 │                    │                     │
│    │  (如：受到伤害)           │                    │                     │
│    │  ───────────────────────▶│                    │                     │
│    │                          │                    │                     │
│    │                          │ 查找匹配触发器     │                     │
│    │                          │ ──────────────────▶│                     │
│    │                          │                    │                     │
│    │                          │                    │ PassiveSkill        │
│    │                          │                    │ TriggerEventArgs    │
│    │                          │                    │ ◀──────────────────│
│    │                          │                    │                     │
│    │                          │                    │ 触发技能管线执行    │
│    │                          │                    │                     │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 九、设计总结

### 9.1 核心优势

| 特性 | 说明 |
|------|------|
| **配置化** | Buff 和被动技能完全由配置表驱动 |
| **阶段化** | 统一的阶段定义（OnAdd/OnInterval/OnRemove） |
| **效果化** | 每个阶段关联 Effect 列表，支持复用 |
| **上下文管理** | EffectSourceContext 统一管理效果来源 |
| **触发器集成** | Buff 和被动技能支持触发器扩展 |
| **回滚友好** | 触发计划支持按 OwnerKey 清理 |

### 9.2 系统执行顺序

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        系统执行顺序                                      │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  MobaSystemOrder 枚举定义：                                             │
│                                                                         │
│  public static class MobaSystemOrder                                   │
│  {                                                                      │
│      public const int BuffsApply = 100;     // Buff 应用                │
│      public const int PassiveSkillTriggers = 200;  // 被动技能注册      │
│      public const int BuffsTick = 300;      // Buff 周期Tick             │
│      public const int SkillPipelines = 400;  // 技能管线执行            │
│      public const int BuffCommandsDrain = 500;  // Buff命令消耗         │
│      public const int BuffsRemove = 600;    // Buff 移除                │
│  }                                                                      │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 9.3 扩展点

1. **新的叠加策略**：扩展 `BuffStackingPolicy` 枚举
2. **新的效果类型**：在 Effect 模块添加新效果组件
3. **新的触发条件**：在 Triggering 模块添加新触发器
4. **新的冷却管理**：扩展被动技能冷却服务

---

## 十、文件清单

| 文件路径 | 说明 |
|----------|------|
| **Buff 系统** | |
| `Runtime/Impl/Moba/Systems/Buffs/MobaBuffApplySystem.cs` | Buff 应用系统 |
| `Runtime/Impl/Moba/Systems/Buffs/MobaBuffTickSystem.cs` | Buff 周期Tick系统 |
| `Runtime/Impl/Moba/Systems/Buffs/MobaBuffRemoveSystem.cs` | Buff 移除系统 |
| `Runtime/Impl/Moba/Systems/Buffs/MobaBuffCommandDrainSystem.cs` | Buff 命令消耗系统 |
| `Runtime/Impl/Moba/Services/Buffs/BuffRepository.cs` | Buff 仓库 |
| `Runtime/Impl/Moba/Services/Buffs/BuffStackingPolicyApplier.cs` | 叠加策略应用器 |
| `Runtime/Impl/Moba/Services/Buffs/BuffContextService.cs` | Buff 上下文服务 |
| `Runtime/Impl/Moba/Services/Buffs/BuffEventPublisher.cs` | Buff 事件发布器 |
| `Runtime/Impl/Moba/Services/Buffs/BuffPeriodicEffectBinder.cs` | 周期效果绑定器 |
| `Runtime/Impl/Moba/Services/Buffs/BuffStageEffectExecutor.cs` | 阶段效果执行器 |
| **被动技能系统** | |
| `Runtime/Impl/Moba/Systems/Skill/MobaPassiveSkillTriggerRegisterSystem.cs` | 被动技能注册系统 |
| `Runtime/Impl/Moba/Systems/Skill/PassiveSkillTriggerListenerManager.cs` | 被动技能监听器管理器 |
| `Runtime/Impl/Moba/Systems/Skill/PassiveSkillTriggerEventArgs.cs` | 被动技能触发事件参数 |
| `Runtime/Impl/Moba/Systems/Skill/MobaSkillPipelineStepSystem.cs` | 技能管线步骤系统 |
| **配置与组件** | |
| `Runtime/Impl/Moba/Config/MO/BuffMO.cs` | Buff 配置对象 |
| `Runtime/Impl/Moba/Config/MO/PassiveSkillMO.cs` | 被动技能配置对象 |
| `Runtime/Impl/Moba/Conponents/BuffComponent.cs` | Buff 运行时组件 |
| `Runtime/Impl/Moba/Conponents/SkillRuntime.cs` | 技能运行时组件 |
| `Runtime/Impl/Moba/Conponents/OngoingTriggerPlansComponent.cs` | 触发计划组件 |
| `Runtime/Impl/Moba/Services/Skill/Pipeline/SkillPipelineRunner.cs` | 技能管线运行器 |
