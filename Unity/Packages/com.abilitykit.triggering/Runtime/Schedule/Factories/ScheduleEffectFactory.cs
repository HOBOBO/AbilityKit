using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Behavior;
using AbilityKit.Triggering.Runtime.Behavior.Composite;
using AbilityKit.Triggering.Runtime.Behavior.Schedule;
using AbilityKit.Triggering.Runtime.Config.Actions;
using AbilityKit.Triggering.Runtime.Config.Plans;
using AbilityKit.Triggering.Runtime.Config.Schedule;
using AbilityKit.Triggering.Runtime.Config.Values;
using AbilityKit.Triggering.Runtime.Factory;
using AbilityKit.Triggering.Runtime.Schedule;
using AbilityKit.Triggering.Runtime.Schedule.Behavior;
using AbilityKit.Triggering.Runtime.Schedule.Data;

namespace AbilityKit.Triggering.Runtime.Schedule.Factories
{
    // ========================================================================
    // 行为上下文工厂
    // ========================================================================

    /// <summary>
    /// 行为上下文工厂接口
    /// </summary>
    public interface IBehaviorContextFactory
    {
        IBehaviorContext CreateContext(object args);
    }

    /// <summary>
    /// 简单行为上下文工厂
    /// </summary>
    public sealed class SimpleBehaviorContextFactory : IBehaviorContextFactory
    {
        private readonly IActionRegistry _actionRegistry;
        private readonly IValueResolver _valueResolver;
        private readonly IBlackboardResolver _blackboard;

        public SimpleBehaviorContextFactory(
            IActionRegistry actionRegistry,
            IValueResolver valueResolver,
            IBlackboardResolver blackboard = null)
        {
            _actionRegistry = actionRegistry;
            _valueResolver = valueResolver;
            _blackboard = blackboard ?? new SimpleBlackboard();
        }

        public IBehaviorContext CreateContext(object args)
        {
            return new SimpleBehaviorContext(args, _actionRegistry, _valueResolver, _blackboard);
        }
    }

    /// <summary>
    /// 简单行为上下文
    /// </summary>
    public sealed class SimpleBehaviorContext : IBehaviorContext
    {
        public object Args { get; }
        public IBlackboardResolver Blackboards { get; }
        public IActionRegistry Actions { get; }
        public IValueResolver Values { get; }

        public SimpleBehaviorContext(
            object args,
            IActionRegistry actions,
            IValueResolver values,
            IBlackboardResolver blackboards)
        {
            Args = args;
            Actions = actions;
            Values = values;
            Blackboards = blackboards;
        }
    }

    /// <summary>
    /// 简单黑板实现
    /// </summary>
    public class SimpleBlackboard : IBlackboardResolver
    {
        private readonly Dictionary<(int, string), object> _data = new();

        public bool TryGetValue<T>(int boardId, string key, out T value)
        {
            var kvp = (boardId, key);
            if (_data.TryGetValue(kvp, out var obj) && obj is T typed)
            {
                value = typed;
                return true;
            }
            value = default;
            return false;
        }

        public void SetValue<T>(int boardId, string key, T value)
        {
            var kvp = (boardId, key);
            if (value == null)
                _data.Remove(kvp);
            else
                _data[kvp] = value;
        }
    }

    // ========================================================================
    // 调度效果工厂接口
    // ========================================================================

    /// <summary>
    /// 调度效果工厂接口
    /// 用于创建各种业务调度效果
    /// </summary>
    public interface IScheduleEffectFactory
    {
        /// <summary>
        /// 创建 Buff 周期伤害调度
        /// </summary>
        IScheduleEffect CreateBuffPeriodicDamage(int buffId, BuffPeriodicConfig config, IBehaviorContext context);

        /// <summary>
        /// 创建子弹飞行调度
        /// </summary>
        IScheduleEffect CreateBulletFlight(int bulletId, BulletFlightConfig config, IBehaviorContext context);

        /// <summary>
        /// 创建 AOE 区域伤害调度
        /// </summary>
        IScheduleEffect CreateAOEDamage(int aoeId, AOEConfig config, IBehaviorContext context);
    }

    // ========================================================================
    // 配置数据结构
    // ========================================================================

    /// <summary>
    /// Buff 周期配置
    /// </summary>
    public class BuffPeriodicConfig
    {
        public int TriggerId { get; set; }
        public double DamagePerTick { get; set; }
        public double PeriodMs { get; set; } = 1000;
        public double DurationMs { get; set; } = -1;
        public int MaxExecutions { get; set; } = -1;
        public int TargetId { get; set; }
        public int CasterId { get; set; }
    }

    /// <summary>
    /// 子弹飞行配置
    /// </summary>
    public class BulletFlightConfig
    {
        public int TriggerId { get; set; }
        public double Speed { get; set; } = 10;
        public double MaxDistance { get; set; } = 100;
        public double CollisionRadius { get; set; } = 1;
        public double LifetimeMs { get; set; } = 5000;
        public int TargetId { get; set; }
        public int OwnerId { get; set; }
        public int Damage { get; set; }
        public int[] HitEffectActionIds { get; set; }
    }

    /// <summary>
    /// AOE 区域配置
    /// </summary>
    public class AOEConfig
    {
        public int TriggerId { get; set; }
        public double PositionX { get; set; }
        public double PositionY { get; set; }
        public double Radius { get; set; } = 5;
        public double DamagePerTick { get; set; } = 50;
        public double PeriodMs { get; set; } = 500;
        public double DurationMs { get; set; } = 3000;
        public double WarningDelayMs { get; set; } = 500;
        public int[] AffectedTargetTags { get; set; }
        public int OwnerId { get; set; }
    }

    // ========================================================================
    // 默认调度效果工厂实现
    // ========================================================================

    /// <summary>
    /// 默认调度效果工厂
    /// 使用 SchedulableBehaviorScheduleAdapter 适配器
    /// </summary>
    public sealed class DefaultScheduleEffectFactory : IScheduleEffectFactory
    {
        private readonly BehaviorFactory _behaviorFactory;
        private readonly IValueResolver _valueResolver;
        private readonly IActionRegistry _actionRegistry;

        public DefaultScheduleEffectFactory(
            BehaviorFactory behaviorFactory,
            IValueResolver valueResolver,
            IActionRegistry actionRegistry)
        {
            _behaviorFactory = behaviorFactory ?? throw new ArgumentNullException(nameof(behaviorFactory));
            _valueResolver = valueResolver ?? throw new ArgumentNullException(nameof(valueResolver));
            _actionRegistry = actionRegistry ?? throw new ArgumentNullException(nameof(actionRegistry));
        }

        /// <summary>
        /// 创建 Buff 周期伤害调度
        /// </summary>
        public IScheduleEffect CreateBuffPeriodicDamage(int buffId, BuffPeriodicConfig config, IBehaviorContext context)
        {
            // 1. 构建行为配置
            var planConfig = new TriggerPlanConfig
            {
                TriggerId = config.TriggerId,
                Schedule = new ScheduleConfig
                {
                    DurationMs = (float)config.DurationMs,
                    PeriodMs = (float)config.PeriodMs,
                    MaxExecutions = config.MaxExecutions
                },
                Actions = CreateBuffActions(config)
            };

            // 2. 创建周期行为
            var behavior = new PeriodicTriggerBehavior(
                planConfig,
                _behaviorFactory,
                _valueResolver,
                _actionRegistry,
                null
            );

            // 3. 创建适配器
            return new SchedulableBehaviorScheduleAdapter(
                behavior,
                context,
                new BuffEffectCallbacks(buffId)
            );
        }

        /// <summary>
        /// 创建子弹飞行调度
        /// </summary>
        public IScheduleEffect CreateBulletFlight(int bulletId, BulletFlightConfig config, IBehaviorContext context)
        {
            // 1. 构建并行行为
            var planConfig = new TriggerPlanConfig
            {
                TriggerId = config.TriggerId,
                Schedule = new ScheduleConfig
                {
                    DurationMs = (float)config.LifetimeMs,
                    PeriodMs = 16, // 每帧
                    MaxExecutions = -1
                }
            };

            var parallelBehavior = new ParallelBehavior(
                planConfig,
                _behaviorFactory,
                _valueResolver,
                _actionRegistry,
                null
            );

            // 2. 添加移动子行为
            parallelBehavior.AddChild(CreateBulletMoveBehavior(bulletId, config));

            // 3. 添加碰撞检测子行为
            parallelBehavior.AddChild(CreateBulletCollisionBehavior(bulletId, config));

            // 4. 创建适配器
            return new SchedulableBehaviorScheduleAdapter(
                parallelBehavior,
                context,
                new BulletEffectCallbacks(bulletId)
            );
        }

        /// <summary>
        /// 创建 AOE 区域伤害调度
        /// </summary>
        public IScheduleEffect CreateAOEDamage(int aoeId, AOEConfig config, IBehaviorContext context)
        {
            // 1. 构建行为配置
            var planConfig = new TriggerPlanConfig
            {
                TriggerId = config.TriggerId,
                Schedule = new ScheduleConfig
                {
                    DurationMs = (float)(config.DurationMs + config.WarningDelayMs),
                    PeriodMs = (float)config.PeriodMs,
                    MaxExecutions = (int)(config.DurationMs / config.PeriodMs)
                },
                Actions = CreateAOEActions(config)
            };

            // 2. 创建周期行为
            var behavior = new PeriodicTriggerBehavior(
                planConfig,
                _behaviorFactory,
                _valueResolver,
                _actionRegistry,
                null
            );

            // 3. 创建适配器
            return new SchedulableBehaviorScheduleAdapter(
                behavior,
                context,
                new AOEEffectCallbacks(aoeId)
            );
        }

        // ========================================================================
        // 辅助方法
        // ========================================================================

        private List<ActionCallConfig> CreateBuffActions(BuffPeriodicConfig config)
        {
            return new List<ActionCallConfig>
            {
                new ActionCallConfig
                {
                    ActionId = new ActionId(1001), // ApplyDamage
                    Arity = 2,
                    Args = new List<ValueRefConfig>
                    {
                        ValueRefConfig.Const(config.DamagePerTick),
                        ValueRefConfig.ContextField(config.TargetId)
                    }
                },
                new ActionCallConfig
                {
                    ActionId = new ActionId(1002), // UpdateBuffProgress
                    Arity = 1,
                    Args = new List<ValueRefConfig>
                    {
                        ValueRefConfig.ContextField(0) // 进度值
                    }
                }
            };
        }

        private ITriggerBehavior CreateBulletMoveBehavior(int bulletId, BulletFlightConfig config)
        {
            return new SimpleTriggerBehavior(
                new TriggerPlanConfig
                {
                    TriggerId = config.TriggerId,
                    Schedule = new ScheduleConfig { PeriodMs = 16 },
                    Actions = new List<ActionCallConfig>
                    {
                        new ActionCallConfig
                        {
                            ActionId = new ActionId(2001), // MoveBullet
                            Arity = 2,
                            Args = new List<ValueRefConfig>
                            {
                                ValueRefConfig.Const(config.Speed),
                                ValueRefConfig.ContextField(bulletId)
                            }
                        }
                    }
                },
                _behaviorFactory,
                _valueResolver,
                _actionRegistry,
                null
            );
        }

        private ITriggerBehavior CreateBulletCollisionBehavior(int bulletId, BulletFlightConfig config)
        {
            return new SimpleTriggerBehavior(
                new TriggerPlanConfig
                {
                    TriggerId = config.TriggerId,
                    Schedule = new ScheduleConfig { PeriodMs = 16 },
                    Actions = new List<ActionCallConfig>
                    {
                        new ActionCallConfig
                        {
                            ActionId = new ActionId(2002), // CheckBulletCollision
                            Arity = 3,
                            Args = new List<ValueRefConfig>
                            {
                                ValueRefConfig.Const(config.CollisionRadius),
                                ValueRefConfig.Const(config.Damage),
                                ValueRefConfig.ContextField(bulletId)
                            }
                        }
                    }
                },
                _behaviorFactory,
                _valueResolver,
                _actionRegistry,
                null
            );
        }

        private List<ActionCallConfig> CreateAOEActions(AOEConfig config)
        {
            return new List<ActionCallConfig>
            {
                new ActionCallConfig
                {
                    ActionId = new ActionId(3001), // ApplyAOEDamage
                    Arity = 4,
                    Args = new List<ValueRefConfig>
                    {
                        ValueRefConfig.Const(config.PositionX),
                        ValueRefConfig.Const(config.PositionY),
                        ValueRefConfig.Const(config.Radius),
                        ValueRefConfig.Const(config.DamagePerTick)
                    }
                }
            };
        }
    }

    // ========================================================================
    // 回调实现
    // ========================================================================

    /// <summary>
    /// Buff 效果回调
    /// </summary>
    public sealed class BuffEffectCallbacks : IScheduleEffectCallbacks
    {
        private readonly int _buffId;

        public BuffEffectCallbacks(int buffId)
        {
            _buffId = buffId;
        }

        public void OnCompleted(in ScheduleContext context)
        {
            // Buff 结束，通知业务层
            // 例如：移除 Buff 效果、刷新冷却等
        }

        public void OnInterrupted(in ScheduleContext context, string reason)
        {
            // Buff 被中断（如被驱散）
        }
    }

    /// <summary>
    /// 子弹效果回调
    /// </summary>
    public sealed class BulletEffectCallbacks : IScheduleEffectCallbacks
    {
        private readonly int _bulletId;

        public BulletEffectCallbacks(int bulletId)
        {
            _bulletId = bulletId;
        }

        public void OnCompleted(in ScheduleContext context)
        {
            // 子弹消失（超时或命中）
        }

        public void OnInterrupted(in ScheduleContext context, string reason)
        {
            // 子弹被取消
        }
    }

    /// <summary>
    /// AOE 效果回调
    /// </summary>
    public sealed class AOEEffectCallbacks : IScheduleEffectCallbacks
    {
        private readonly int _aoeId;

        public AOEEffectCallbacks(int aoeId)
        {
            _aoeId = aoeId;
        }

        public void OnCompleted(in ScheduleContext context)
        {
            // AOE 区域消失
        }

        public void OnInterrupted(in ScheduleContext context, string reason)
        {
            // AOE 被取消
        }
    }

    // ========================================================================
    // 简化使用示例
    // ========================================================================

    /// <summary>
    /// 调度使用示例
    /// </summary>
    public static class ScheduleUsageExamples
    {
        public static void ExampleUsage(
            IScheduleManager scheduleManager,
            IScheduleEffectFactory factory,
            IBehaviorContextFactory contextFactory)
        {
            // ===== Buff 周期伤害示例 =====
            var buffContext = contextFactory.CreateContext(new { BuffId = 1, TargetId = 100, CasterId = 200 });
            var buffConfig = new BuffPeriodicConfig
            {
                TriggerId = 1001,
                DamagePerTick = 100,
                PeriodMs = 1000,
                DurationMs = 5000,
                MaxExecutions = 5,
                TargetId = 100,
                CasterId = 200
            };

            var buffEffect = factory.CreateBuffPeriodicDamage(1, buffConfig, buffContext);
            var buffHandle = scheduleManager.RegisterPeriodic(
                intervalMs: (float)buffConfig.PeriodMs,
                maxExecutions: buffConfig.MaxExecutions,
                businessId: 1,
                effect: buffEffect
            );

            // ===== 子弹飞行示例 =====
            var bulletContext = contextFactory.CreateContext(new { BulletId = 2, OwnerId = 200 });
            var bulletConfig = new BulletFlightConfig
            {
                TriggerId = 2001,
                Speed = 20,
                MaxDistance = 100,
                CollisionRadius = 1,
                LifetimeMs = 3000,
                TargetId = 100,
                OwnerId = 200,
                Damage = 150
            };

            var bulletEffect = factory.CreateBulletFlight(2, bulletConfig, bulletContext);
            var bulletHandle = scheduleManager.RegisterContinuous(
                intervalMs: 16,
                businessId: 2,
                effect: bulletEffect
            );

            // ===== AOE 区域示例 =====
            var aoeContext = contextFactory.CreateContext(new { AOEId = 3, OwnerId = 200 });
            var aoeConfig = new AOEConfig
            {
                TriggerId = 3001,
                PositionX = 10,
                PositionY = 5,
                Radius = 5,
                DamagePerTick = 50,
                PeriodMs = 500,
                DurationMs = 3000,
                WarningDelayMs = 500,
                OwnerId = 200
            };

            var aoeEffect = factory.CreateAOEDamage(3, aoeConfig, aoeContext);
            var aoeHandle = scheduleManager.Register(
                ScheduleRegisterRequest.Periodic(
                    intervalMs: (float)aoeConfig.PeriodMs,
                    maxExecutions: -1,
                    delayMs: (float)aoeConfig.WarningDelayMs,
                    speed: 1.0f,
                    businessId: 3
                ),
                aoeEffect
            );
        }
    }
}
