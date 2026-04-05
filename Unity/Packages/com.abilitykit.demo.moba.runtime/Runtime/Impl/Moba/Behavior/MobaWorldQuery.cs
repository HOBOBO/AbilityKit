using System;
using System.Collections.Generic;
using AbilityKit.Ability.Behavior;
using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Moba.Behavior
{
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
        
        public MobaWorldQuery(
            IEntityManager entityManager,
            IBuffManager buffManager,
            IAttributeSystem attributeSystem)
        {
            _entityManager = entityManager;
            _buffManager = buffManager;
            _attributeSystem = attributeSystem;
        }
        
        public Vec3 GetPosition(BehaviorEntityId id) => 
            _entityManager.Exists(id.Value) 
                ? _entityManager.GetPosition(id.Value) 
                : Vec3.Zero;
        
        public void SetPosition(BehaviorEntityId id, Vec3 position)
        {
            if (_entityManager.Exists(id.Value))
                _entityManager.SetPosition(id.Value, position);
        }
        
        public Vec3 GetForward(BehaviorEntityId id) => 
            _entityManager.Exists(id.Value) 
                ? _entityManager.GetForward(id.Value) 
                : Vec3.Forward;
        
        public void SetForward(BehaviorEntityId id, Vec3 forward)
        {
            if (_entityManager.Exists(id.Value))
                _entityManager.SetForward(id.Value, forward);
        }
        
        public float GetDistance(BehaviorEntityId a, BehaviorEntityId b)
        {
            var posA = GetPosition(a);
            var posB = GetPosition(b);
            return (posA - posB).Magnitude;
        }
        
        public float GetDistanceToPosition(BehaviorEntityId entityId, Vec3 position)
        {
            var entityPos = GetPosition(entityId);
            return (entityPos - position).Magnitude;
        }
        
        public bool EntityExists(BehaviorEntityId id) => _entityManager.Exists(id.Value);
        
        public T GetData<T>(BehaviorEntityId id, string key, T defaultValue = default) => defaultValue;
        
        public void SetData<T>(BehaviorEntityId id, string key, T value) { }
        
        public bool HasData(BehaviorEntityId id, string key) => false;
        
        // ==================== MOBA 业务扩展 ====================
        
        public bool IsAlive(BehaviorEntityId id) => _attributeSystem.IsAlive(id.Value);
        
        public int GetTeam(BehaviorEntityId id) => _attributeSystem.GetTeam(id.Value);
        
        public bool IsEnemy(BehaviorEntityId a, BehaviorEntityId b)
        {
            var teamA = GetTeam(a);
            var teamB = GetTeam(b);
            return teamA != 0 && teamB != 0 && teamA != teamB;
        }
        
        public bool IsAlly(BehaviorEntityId a, BehaviorEntityId b) => GetTeam(a) == GetTeam(b);
        
        public bool HasBuff(BehaviorEntityId id, string buffId) => _buffManager.HasBuff(id.Value, buffId);
        
        public bool HasTag(BehaviorEntityId id, string tag) => _buffManager.HasTag(id.Value, tag);
        
        public float GetMoveSpeed(BehaviorEntityId id, float defaultValue = 5f) => 
            _attributeSystem.GetAttribute(id.Value, "MoveSpeed");
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
            
            // 回退到属性查询
            var hp = query.GetData<float>(id, "HP", -1);
            return hp > 0;
        }
        
        /// <summary>
        /// 获取队伍
        /// </summary>
        public static int GetTeam(this IWorldQuery query, BehaviorEntityId id)
        {
            if (query is MobaWorldQuery moba)
                return moba.GetTeam(id);
            
            return query.GetData<int>(id, "Team", 0);
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
            
            var buffs = query.GetData<List<string>>(id, "Buffs");
            return buffs != null && buffs.Contains(buffId);
        }
        
        /// <summary>
        /// 是否有标签
        /// </summary>
        public static bool HasTag(this IWorldQuery query, BehaviorEntityId id, string tag)
        {
            if (query is MobaWorldQuery moba)
                return moba.HasTag(id, tag);
            
            var tags = query.GetData<HashSet<string>>(id, "Tags");
            return tags != null && tags.Contains(tag);
        }
        
        /// <summary>
        /// 是否可以移动
        /// </summary>
        public static bool CanMove(this IWorldQuery query, BehaviorEntityId id)
        {
            if (!query.IsAlive(id)) return false;
            if (query.HasTag(id, "Stunned")) return false;
            if (query.HasTag(id, "Rooted")) return false;
            if (query.HasTag(id, "Feared")) return false;
            if (query.HasTag(id, "Asleep")) return false;
            return true;
        }
        
        /// <summary>
        /// 是否可以施法
        /// </summary>
        public static bool CanCast(this IWorldQuery query, BehaviorEntityId id)
        {
            if (!query.IsAlive(id)) return false;
            if (query.HasTag(id, "Stunned")) return false;
            if (query.HasTag(id, "Silenced")) return false;
            if (query.HasTag(id, "Feared")) return false;
            if (query.HasTag(id, "Asleep")) return false;
            return true;
        }
        
        /// <summary>
        /// 是否可以控制
        /// </summary>
        public static bool CanBeControlled(this IWorldQuery query, BehaviorEntityId id)
        {
            if (query.HasTag(id, "Stunned")) return false;
            if (query.HasTag(id, "Feared")) return false;
            if (query.HasTag(id, "Charmed")) return false;
            if (query.HasTag(id, "Sleeping")) return false;
            return true;
        }
        
        /// <summary>
        /// 获取移动速度
        /// </summary>
        public static float GetMoveSpeed(this IWorldQuery query, BehaviorEntityId id, float defaultValue = 5f)
        {
            if (query is MobaWorldQuery moba)
                return moba.GetMoveSpeed(id, defaultValue);
            
            return query.GetData<float>(id, "MoveSpeed", defaultValue);
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
            return new DelegateDecision("Channeling", (ctx, world) =>
            {
                if (!world.IsAlive(ctx.OwnerId))
                    return DecisionResult.Interrupt("OwnerDied");
                
                if (ctx.TargetId.HasValue && !world.EntityExists(ctx.TargetId.Value))
                    return DecisionResult.Interrupt("TargetInvalid");
                
                if (ctx.TargetId.HasValue && world is MobaWorldQuery moba && !moba.IsAlive(ctx.TargetId.Value))
                    return DecisionResult.Interrupt("TargetDied");
                
                if (!world.CanBeControlled(ctx.OwnerId))
                    return DecisionResult.Interrupt("LostControl");
                
                if (ctx.TargetId.HasValue)
                {
                    var maxRange = ctx.GetConfig<float>("MaxRange", 0);
                    if (maxRange > 0)
                    {
                        var distance = world.GetDistance(ctx.OwnerId, ctx.TargetId.Value);
                        if (distance > maxRange)
                            return DecisionResult.Interrupt("OutOfRange");
                    }
                }
                
                if (canContinue(ctx.OwnerId, ctx.TargetId, world))
                    return DecisionResult.Continue("Channeling");
                
                return DecisionResult.Interrupt("ConditionFailed");
            });
        }
        
        /// <summary>
        /// 创建跟随决策
        /// </summary>
        public static DelegateDecision CreateFollowDecision(
            float stopDistance = 1f,
            float? moveSpeed = null)
        {
            return new DelegateDecision("Follow", (ctx, world) =>
            {
                if (!ctx.TargetId.HasValue)
                    return DecisionResult.Complete();
                
                if (!world.EntityExists(ctx.TargetId.Value))
                    return DecisionResult.Interrupt("TargetInvalid");
                
                if (world is MobaWorldQuery moba && !moba.IsAlive(ctx.TargetId.Value))
                    return DecisionResult.Interrupt("TargetDied");
                
                var targetPos = world.GetPosition(ctx.TargetId.Value);
                var ownerPos = world.GetPosition(ctx.OwnerId);
                var distance = world.GetDistanceToPosition(ctx.OwnerId, targetPos);
                
                if (distance <= stopDistance)
                    return DecisionResult.Complete();
                
                var speed = moveSpeed ?? world.GetMoveSpeed(ctx.OwnerId, 5f);
                return DecisionResult.Continue("Following")
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
            
            return new DelegateDecision("Patrol", (ctx, world) =>
            {
                if (waypoints == null || waypoints.Length == 0)
                    return DecisionResult.Complete();
                
                if (!world.CanMove(ctx.OwnerId))
                    return DecisionResult.Continue("Patrol");
                
                var targetPos = waypoints[currentIndex];
                var ownerPos = world.GetPosition(ctx.OwnerId);
                var distance = world.GetDistanceToPosition(ctx.OwnerId, targetPos);
                
                if (distance <= stopDistance)
                {
                    currentIndex = (currentIndex + 1) % waypoints.Length;
                    return DecisionResult.Continue("Patrol");
                }
                
                var speed = moveSpeed ?? world.GetMoveSpeed(ctx.OwnerId, 3f);
                return DecisionResult.Continue("Moving")
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
            return new DelegateDecision("Chase", (ctx, world) =>
            {
                if (!ctx.TargetId.HasValue)
                    return DecisionResult.Complete();
                
                if (!world.EntityExists(ctx.TargetId.Value))
                    return DecisionResult.Interrupt("TargetInvalid");
                
                if (world is MobaWorldQuery moba && !moba.IsAlive(ctx.TargetId.Value))
                    return DecisionResult.Interrupt("TargetDied");
                
                var targetPos = world.GetPosition(ctx.TargetId.Value);
                var ownerPos = world.GetPosition(ctx.OwnerId);
                var distance = world.GetDistanceToPosition(ctx.OwnerId, targetPos);
                
                if (distance <= attackRange)
                    return DecisionResult.Complete();
                
                if (!world.CanMove(ctx.OwnerId))
                    return DecisionResult.Continue("Chase");
                
                var speed = moveSpeed ?? world.GetMoveSpeed(ctx.OwnerId, 5f);
                return DecisionResult.Continue("Chasing")
                    .WithMovement(targetPos, ctx.TargetId, speed);
            });
        }
    }
}
