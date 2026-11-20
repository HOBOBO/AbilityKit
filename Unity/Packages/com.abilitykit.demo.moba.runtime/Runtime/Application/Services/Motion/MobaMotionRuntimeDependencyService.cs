#nullable enable
using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Combat.Collision;
using AbilityKit.Combat.MotionSystem.Collision;
using AbilityKit.Combat.MotionSystem.Constraints;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Demo.Moba.Services.Motion
{
    /// <summary>
    /// 持有本世界默认移动求解器及其构造依赖，不调度模拟、不保存帧历史。
    /// 碰撞服务暂不可用时保留原初始化的空求解器语义。
    /// </summary>
    [WorldService(typeof(MobaMotionRuntimeDependencyService))]
    public sealed class MobaMotionRuntimeDependencyService : IWorldInitializable
    {
        private IWorldResolver? _services;
        private ConfigurableMotionSolver? _solver;
        private ICollisionWorld? _collisionWorld;
        private MobaActorRegistry? _actors;

        /// <summary>
        /// 绑定本世界解析器；实际求解器仍在原 MotionInit 时点按需创建。
        /// </summary>
        public void OnInit(IWorldResolver services) => _services = services;

        /// <summary>
        /// 返回同一世界的默认求解器；不执行碰撞查询或来源推进。
        /// </summary>
        public IMotionSolver? ResolveDefaultSolver()
        {
            if (_solver != null) return _solver;
            if (_services == null || !_services.TryResolve<ICollisionService>(out var collision) || collision?.World == null)
                return null;
            _services.TryResolve(out _actors);
            _collisionWorld = collision.World;
            /*
             * 既有适配器允许没有 ActorRegistry 的纯碰撞世界；此处保留原业务语义。
             */
            _solver = new ConfigurableMotionSolver(new MobaMotionCollisionWorldAdapter(_collisionWorld, _actors!), ResolveConstraints);
            return _solver;
        }

        /// <summary>
        /// 仅校验本服务实际拥有的标准依赖；不按类型推断任意自定义求解器等价。
        /// </summary>
        internal bool OwnsStandardSolver(IMotionSolver? solver)
        {
            if (_solver == null || !ReferenceEquals(solver, _solver) || _solver.Diagnostics != null || _services == null)
                return false;
            return _services.TryResolve<ICollisionService>(out var collision) && ReferenceEquals(collision?.World, _collisionWorld) &&
                _services.TryResolve<MobaActorRegistry>(out var actors) && ReferenceEquals(actors, _actors);
        }

        private static MotionConstraints ResolveConstraints(int moverId, in MotionState state, in MotionOutput input, float dt)
        {
            var collision = new MotionCollisionConstraints(true, false, MotionEndOverlapPolicy.AllowInside,
                0.5f, 0f, MobaCollisionLayers.WorldMask, 0, true, 2, 1f);
            return new MotionConstraints(collision, MotionLeashConstraints.Disabled);
        }

        /// <summary>
        /// 释放世界级缓存；不影响其它世界和普通业务的配置方式。
        /// </summary>
        public void Dispose()
        {
            _solver = null;
            _collisionWorld = null;
            _actors = null;
            _services = null;
        }
    }
}
