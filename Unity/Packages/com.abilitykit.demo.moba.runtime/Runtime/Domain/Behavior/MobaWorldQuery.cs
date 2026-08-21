using System;
using System.Collections.Generic;
using AbilityKit.Ability.Behavior;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Moba.Behavior
{
    /// <summary>
    /// Generic behavior framework string contracts that cross configuration and diagnostic boundaries.
    /// </summary>
    public static class MobaBehaviorContracts
    {
        public static class Phase
        {
            public const string Channeling = "Channeling";
            public const string Follow = "Follow";
            public const string StateMachine = "StateMachine";
        }

        public static class State
        {
            public const string Channeling = "Channeling";
            public const string Following = "Following";
            public const string Patrol = "Patrol";
            public const string Moving = "Moving";
            public const string Chase = "Chase";
            public const string Chasing = "Chasing";
        }

        public static class InterruptReason
        {
            public const string OwnerDied = "OwnerDied";
            public const string LostControl = "LostControl";
            public const string Silenced = "Silenced";
            public const string TargetInvalid = "TargetInvalid";
            public const string TargetDied = "TargetDied";
            public const string OutOfRange = "OutOfRange";
            public const string CustomCondition = "CustomCondition";
            public const string ConditionFailed = "ConditionFailed";
        }

        public static class ContextKey
        {
            public const string MaxRange = "MaxRange";
            public const string WorldQuery = "MobaWorldQuery";
            public const string CurrentState = "currentState";
        }

        public static class WorldDataKey
        {
            public const string Alive = "alive";
            public const string HitPoints = "HP";
            public const string Team = "Team";
            public const string Buffs = "Buffs";
            public const string Tags = "Tags";
            public const string MoveSpeed = "MoveSpeed";
        }
    }

    /// <summary>
    /// MOBA 世界查询
    /// 由业务层实现，整合 MOBA 所需的数据源
    /// 
    /// 注意：完全独立于 Triggering 模块
    /// 通过 IWorldQuery 接口访问数据
    /// </summary>
    public class MobaWorldQuery : IWorldQuery
    {
        /// <summary>
        /// 实体管理器接口
        /// </summary>
        public interface IEntityManager
        {
            bool Exists(long entityId);
            Vec3 GetPosition(long entityId);
            void SetPosition(long entityId, Vec3 position);
            Vec3 GetForward(long entityId);
            void SetForward(long entityId, Vec3 forward);
        }
        
        /// <summary>
        /// Buff 管理器接口
        /// </summary>
        public interface IBuffManager
        {
            bool HasBuff(long entityId, string buffId);
            bool HasTag(long entityId, string tag);
        }
        
        /// <summary>
        /// 属性系统接口
        /// </summary>
        public interface IAttributeSystem
        {
            float GetAttribute(long entityId, string attributeId);
            bool IsAlive(long entityId);
            int GetTeam(long entityId);
        }
        
        private readonly IEntityManager _entityManager;
        private readonly IBuffManager _buffManager;
        private readonly IAttributeSystem _attributeSystem;
        private readonly bool _allowMutations;
        
        public MobaWorldQuery(
            IEntityManager entityManager,
            IBuffManager buffManager,
            IAttributeSystem attributeSystem,
            bool allowMutations = true)
        {
            _entityManager = entityManager;
            _buffManager = buffManager;
            _attributeSystem = attributeSystem;
            _allowMutations = allowMutations;
        }
        
        public Vec3 GetPosition(BehaviorEntityId id)
        {
            EnsureEntityExists(id);
            return _entityManager.GetPosition(id.Value);
        }
        
        public void SetPosition(BehaviorEntityId id, Vec3 position)
        {
            EnsureMutationsAllowed();
            EnsureEntityExists(id);
            _entityManager.SetPosition(id.Value, position);
        }
        
        public Vec3 GetForward(BehaviorEntityId id)
        {
            EnsureEntityExists(id);
            return _entityManager.GetForward(id.Value);
        }
        
        public void SetForward(BehaviorEntityId id, Vec3 forward)
        {
            EnsureMutationsAllowed();
            EnsureEntityExists(id);
            _entityManager.SetForward(id.Value, forward);
        }
        
        public float GetDistance(BehaviorEntityId a, BehaviorEntityId b)
        {
            var posA = GetPosition(a);
            var posB = GetPosition(b);
            var delta = posA - posB;
            return global::AbilityKit.Core.Mathematics.DeterministicMathBridge.Magnitude(in delta);
        }

        public float GetDistanceToPosition(BehaviorEntityId entityId, Vec3 position)
        {
            var entityPos = GetPosition(entityId);
            var delta = entityPos - position;
            return global::AbilityKit.Core.Mathematics.DeterministicMathBridge.Magnitude(in delta);
        }
        
        public bool EntityExists(BehaviorEntityId id) => _entityManager.Exists(id.Value);
        
        public T GetData<T>(BehaviorEntityId id, string key, T defaultValue = default)
        {
            EnsureEntityExists(id);
            switch (key)
            {
                case MobaBehaviorContracts.WorldDataKey.Alive:
                    return CastData<T>(key, _attributeSystem.IsAlive(id.Value));
                case MobaBehaviorContracts.WorldDataKey.HitPoints:
                    return CastData<T>(key, _attributeSystem.GetAttribute(
                        id.Value,
                        MobaBehaviorContracts.WorldDataKey.HitPoints));
                case MobaBehaviorContracts.WorldDataKey.Team:
                    return CastData<T>(key, _attributeSystem.GetTeam(id.Value));
                case MobaBehaviorContracts.WorldDataKey.MoveSpeed:
                    return CastData<T>(key, _attributeSystem.GetAttribute(
                        id.Value,
                        MobaBehaviorContracts.WorldDataKey.MoveSpeed));
                case MobaBehaviorContracts.WorldDataKey.Buffs:
                case MobaBehaviorContracts.WorldDataKey.Tags:
                    throw new NotSupportedException(
                        $"World data key '{key}' is exposed through the dedicated query methods and has no collection snapshot contract.");
                default:
                    throw new ArgumentException($"Unknown MOBA world data key '{key ?? "<null>"}'.", nameof(key));
            }
        }
        
        public void SetData<T>(BehaviorEntityId id, string key, T value)
        {
            EnsureMutationsAllowed();
            EnsureEntityExists(id);
            throw new NotSupportedException(
                $"MOBA world data key '{key ?? "<null>"}' cannot be mutated through the generic behavior query.");
        }
        
        public bool HasData(BehaviorEntityId id, string key)
        {
            if (!EntityExists(id)) return false;
            switch (key)
            {
                case MobaBehaviorContracts.WorldDataKey.Alive:
                case MobaBehaviorContracts.WorldDataKey.HitPoints:
                case MobaBehaviorContracts.WorldDataKey.Team:
                case MobaBehaviorContracts.WorldDataKey.MoveSpeed:
                    return true;
                case MobaBehaviorContracts.WorldDataKey.Buffs:
                case MobaBehaviorContracts.WorldDataKey.Tags:
                    return false;
                default:
                    throw new ArgumentException($"Unknown MOBA world data key '{key ?? "<null>"}'.", nameof(key));
            }
        }
        
        // ==================== MOBA 业务扩展 ====================
        
        public bool IsAlive(BehaviorEntityId id)
        {
            EnsureEntityExists(id);
            return _attributeSystem.IsAlive(id.Value);
        }
        
        public int GetTeam(BehaviorEntityId id)
        {
            EnsureEntityExists(id);
            return _attributeSystem.GetTeam(id.Value);
        }
        
        public bool IsEnemy(BehaviorEntityId a, BehaviorEntityId b)
        {
            var teamA = GetTeam(a);
            var teamB = GetTeam(b);
            return teamA != 0 && teamB != 0 && teamA != teamB;
        }
        
        public bool IsAlly(BehaviorEntityId a, BehaviorEntityId b) => GetTeam(a) == GetTeam(b);
        
        public bool HasBuff(BehaviorEntityId id, string buffId)
        {
            EnsureEntityExists(id);
            return _buffManager.HasBuff(id.Value, buffId);
        }
        
        public bool HasTag(BehaviorEntityId id, string tag)
        {
            EnsureEntityExists(id);
            return _buffManager.HasTag(id.Value, tag);
        }
        
        public float GetMoveSpeed(BehaviorEntityId id, float defaultValue = 5f)
        {
            EnsureEntityExists(id);
            return _attributeSystem.GetAttribute(id.Value, MobaBehaviorContracts.WorldDataKey.MoveSpeed);
        }

        private static T CastData<T>(string key, object value)
        {
            if (value is T typed) return typed;
            throw new InvalidCastException(
                $"World data key '{key}' contains {value.GetType().Name}, not {typeof(T).Name}.");
        }

        private void EnsureEntityExists(BehaviorEntityId id)
        {
            if (!_entityManager.Exists(id.Value))
            {
                throw new InvalidOperationException($"Behavior entity {id.Value} does not exist.");
            }
        }

        private void EnsureMutationsAllowed()
        {
            if (!_allowMutations)
            {
                throw new InvalidOperationException(
                    "This world query is read-only. Decisions must emit an intent instead of mutating the logic world.");
            }
        }
    }
    
    /// <summary>
    /// MOBA 业务查询扩展
    /// 提供 MOBA 特有的查询方法
    /// </summary>
    public static class MobaWorldQueryExtensions
    {
        /// <summary>
        /// 实体是否存活
        /// </summary>
        public static bool IsAlive(this IWorldQuery query, BehaviorEntityId id)
        {
            if (query is MobaWorldQuery moba)
                return moba.IsAlive(id);

            var hp = query.GetData<float>(id, MobaBehaviorContracts.WorldDataKey.HitPoints, -1);
            return hp > 0;
        }
        
        /// <summary>
        /// 获取队伍
        /// </summary>
        public static int GetTeam(this IWorldQuery query, BehaviorEntityId id)
        {
            if (query is MobaWorldQuery moba)
                return moba.GetTeam(id);

            return query.GetData<int>(id, MobaBehaviorContracts.WorldDataKey.Team, 0);
        }
        
        /// <summary>
        /// 是否是敌人
        /// </summary>
        public static bool IsEnemy(this IWorldQuery query, BehaviorEntityId a, BehaviorEntityId b)
        {
            if (query is MobaWorldQuery moba)
                return moba.IsEnemy(a, b);
            
            return GetTeam(query, a) != GetTeam(query, b);
        }
        
        /// <summary>
        /// 是否有 Buff
        /// </summary>
        public static bool HasBuff(this IWorldQuery query, BehaviorEntityId id, string buffId)
        {
            if (query is MobaWorldQuery moba)
                return moba.HasBuff(id, buffId);

            var buffs = query.GetData<List<string>>(id, MobaBehaviorContracts.WorldDataKey.Buffs);
            return buffs != null && buffs.Contains(buffId);
        }
        
        /// <summary>
        /// 是否有标签
        /// </summary>
        public static bool HasTag(this IWorldQuery query, BehaviorEntityId id, string tag)
        {
            if (query is MobaWorldQuery moba)
                return moba.HasTag(id, tag);

            var tags = query.GetData<HashSet<string>>(id, MobaBehaviorContracts.WorldDataKey.Tags);
            return tags != null && tags.Contains(tag);
        }

        public static bool HasAnyTag(this IWorldQuery query, BehaviorEntityId id, IReadOnlyList<string> tags)
        {
            if (query == null || tags == null) return false;

            for (int i = 0; i < tags.Count; i++)
            {
                if (query.HasTag(id, tags[i])) return true;
            }

            return false;
        }
        
        /// <summary>
        /// 是否可以移动
        /// </summary>
        public static bool CanMove(this IWorldQuery query, BehaviorEntityId id)
        {
            return query.IsAlive(id)
                && !query.HasAnyTag(id, MobaGameplayTagCatalog.MoveBlockedAliases);
        }
        
        /// <summary>
        /// 是否可以施法
        /// </summary>
        public static bool CanCast(this IWorldQuery query, BehaviorEntityId id)
        {
            return query.IsAlive(id)
                && !query.HasAnyTag(id, MobaGameplayTagCatalog.CastBlockedAliases);
        }
        
        /// <summary>
        /// 是否可以控制
        /// </summary>
        public static bool CanBeControlled(this IWorldQuery query, BehaviorEntityId id)
        {
            return !query.HasAnyTag(id, MobaGameplayTagCatalog.ControlBlockedAliases);
        }
        
        /// <summary>
        /// 获取移动速度
        /// </summary>
        public static float GetMoveSpeed(this IWorldQuery query, BehaviorEntityId id, float defaultValue = 5f)
        {
            if (query is MobaWorldQuery moba)
                return moba.GetMoveSpeed(id, defaultValue);

            return query.GetData<float>(id, MobaBehaviorContracts.WorldDataKey.MoveSpeed, defaultValue);
        }
    }
    
    /// <summary>
    /// MOBA 行为决策器
    /// </summary>
    public static class MobaBehaviorDecisions
    {
        /// <summary>
        /// 创建引导决策
        /// </summary>
        public static DelegateDecision CreateChannelingDecision(
            Func<BehaviorEntityId, BehaviorEntityId?, IWorldQuery, bool> canContinue)
        {
            return new DelegateDecision(MobaBehaviorContracts.Phase.Channeling, (ctx, world) =>
            {
                if (!world.IsAlive(ctx.OwnerId))
                    return DecisionResult.Interrupt(MobaBehaviorContracts.InterruptReason.OwnerDied);

                if (ctx.TargetId.HasValue && !world.EntityExists(ctx.TargetId.Value))
                    return DecisionResult.Interrupt(MobaBehaviorContracts.InterruptReason.TargetInvalid);

                if (ctx.TargetId.HasValue && world is MobaWorldQuery moba && !moba.IsAlive(ctx.TargetId.Value))
                    return DecisionResult.Interrupt(MobaBehaviorContracts.InterruptReason.TargetDied);

                if (!world.CanBeControlled(ctx.OwnerId))
                    return DecisionResult.Interrupt(MobaBehaviorContracts.InterruptReason.LostControl);
                
                if (ctx.TargetId.HasValue)
                {
                    var maxRange = ctx.GetConfig<float>(MobaBehaviorContracts.ContextKey.MaxRange, 0);
                    if (maxRange > 0)
                    {
                        var distance = world.GetDistance(ctx.OwnerId, ctx.TargetId.Value);
                        if (distance > maxRange)
                            return DecisionResult.Interrupt(MobaBehaviorContracts.InterruptReason.OutOfRange);
                    }
                }

                if (canContinue(ctx.OwnerId, ctx.TargetId, world))
                    return DecisionResult.Continue(MobaBehaviorContracts.State.Channeling);

                return DecisionResult.Interrupt(MobaBehaviorContracts.InterruptReason.ConditionFailed);
            });
        }
        
        /// <summary>
        /// 创建跟随决策
        /// </summary>
        public static DelegateDecision CreateFollowDecision(
            float stopDistance = 1f,
            float? moveSpeed = null)
        {
            return new DelegateDecision(MobaBehaviorContracts.Phase.Follow, (ctx, world) =>
            {
                if (!ctx.TargetId.HasValue)
                    return DecisionResult.Complete();
                
                if (!world.EntityExists(ctx.TargetId.Value))
                    return DecisionResult.Interrupt(MobaBehaviorContracts.InterruptReason.TargetInvalid);

                if (world is MobaWorldQuery moba && !moba.IsAlive(ctx.TargetId.Value))
                    return DecisionResult.Interrupt(MobaBehaviorContracts.InterruptReason.TargetDied);
                
                var targetPos = world.GetPosition(ctx.TargetId.Value);
                var ownerPos = world.GetPosition(ctx.OwnerId);
                var distance = world.GetDistanceToPosition(ctx.OwnerId, targetPos);
                
                if (distance <= stopDistance)
                    return DecisionResult.Complete();
                
                var speed = moveSpeed ?? world.GetMoveSpeed(ctx.OwnerId, 5f);
                return DecisionResult.Continue(MobaBehaviorContracts.State.Following)
                    .WithMovement(targetPos, ctx.TargetId, speed);
            });
        }
        
        /// <summary>
        /// 创建巡逻决策
        /// </summary>
        public static DelegateDecision CreatePatrolDecision(
            Vec3[] waypoints,
            float stopDistance = 0.5f,
            float? moveSpeed = null)
        {
            int currentIndex = 0;
            
            return new DelegateDecision(MobaBehaviorContracts.State.Patrol, (ctx, world) =>
            {
                if (waypoints == null || waypoints.Length == 0)
                    return DecisionResult.Complete();
                
                if (!world.CanMove(ctx.OwnerId))
                    return DecisionResult.Continue(MobaBehaviorContracts.State.Patrol);
                
                var targetPos = waypoints[currentIndex];
                var ownerPos = world.GetPosition(ctx.OwnerId);
                var distance = world.GetDistanceToPosition(ctx.OwnerId, targetPos);
                
                if (distance <= stopDistance)
                {
                    currentIndex = (currentIndex + 1) % waypoints.Length;
                    return DecisionResult.Continue(MobaBehaviorContracts.State.Patrol);
                }

                var speed = moveSpeed ?? world.GetMoveSpeed(ctx.OwnerId, 3f);
                return DecisionResult.Continue(MobaBehaviorContracts.State.Moving)
                    .WithMovement(targetPos, null, speed);
            });
        }
        
        /// <summary>
        /// 创建追击决策
        /// </summary>
        public static DelegateDecision CreateChaseDecision(
            float attackRange,
            float? moveSpeed = null)
        {
            return new DelegateDecision(MobaBehaviorContracts.State.Chase, (ctx, world) =>
            {
                if (!ctx.TargetId.HasValue)
                    return DecisionResult.Complete();
                
                if (!world.EntityExists(ctx.TargetId.Value))
                    return DecisionResult.Interrupt(MobaBehaviorContracts.InterruptReason.TargetInvalid);

                if (world is MobaWorldQuery moba && !moba.IsAlive(ctx.TargetId.Value))
                    return DecisionResult.Interrupt(MobaBehaviorContracts.InterruptReason.TargetDied);
                
                var targetPos = world.GetPosition(ctx.TargetId.Value);
                var ownerPos = world.GetPosition(ctx.OwnerId);
                var distance = world.GetDistanceToPosition(ctx.OwnerId, targetPos);
                
                if (distance <= attackRange)
                    return DecisionResult.Complete();
                
                if (!world.CanMove(ctx.OwnerId))
                    return DecisionResult.Continue(MobaBehaviorContracts.State.Chase);

                var speed = moveSpeed ?? world.GetMoveSpeed(ctx.OwnerId, 5f);
                return DecisionResult.Continue(MobaBehaviorContracts.State.Chasing)
                    .WithMovement(targetPos, ctx.TargetId, speed);
            });
        }
    }
}
