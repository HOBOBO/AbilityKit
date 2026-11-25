using System;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Attributes.Core;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.EntitasAdapters;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Motion;
using AbilityKit.Demo.Moba.Systems.Motion;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 单角色隔离世界。复用正式普通移动系统，不安装技能、碰撞、输入分发或表现事件系统。
    /// </summary>
    internal sealed class StateSyncMovementSandbox : IDisposable
    {
        private readonly EntitasWorld _world;
        private readonly ActorEntity _actor;
        private readonly MotionPipeline _pipeline = new MotionPipeline();
        private readonly AttributeContext _attributes = new AttributeContext();
        private readonly float _fixedDelta;
        private float _speed = -1f;

        internal StateSyncMovementSandbox(float fixedDelta)
        {
            _fixedDelta = fixedDelta;
            var options = new WorldCreateOptions(new WorldId("state-sync-movement"), "state-sync-movement")
                .SetEntitasContextsFactory(new MobaEntitasContextsFactory());
            options.Modules.Add(new MovementModule());
            _world = new EntitasWorld(options);
            try
            {
                _world.Initialize();
                _actor = ((Contexts)_world.Contexts).actor.CreateEntity();
                _actor.AddActorId(1);
                _world.Services.Resolve<MobaActorRegistry>().Register(1, _actor);
                _attributes.SetBase(MobaAttributeIds.MOVE_SPEED, 0);
                _actor.AddAttributeGroup(_attributes.GetGroupFor(MobaAttributeIds.MOVE_SPEED), _attributes);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal bool Step(ReadOnlySpan<(float X, float Z)> inputs, ref MovementState candidate)
        {
            if (!candidate.Eligible) return false;
            if (_speed != candidate.Speed)
            {
                _attributes.SetBase(MobaAttributeIds.MOVE_SPEED, candidate.Speed);
                _speed = candidate.Speed;
            }
            _actor.ReplaceTransform(candidate.Pose);
            _actor.ReplaceMoveInput(candidate.MoveX, candidate.MoveZ);
            var motion = new MotionState(candidate.Pose.Position) { Forward = candidate.Pose.Forward };
            _actor.ReplaceMotion(_pipeline, motion, default, _pipeline.Solver, null, null, true, default);
            foreach (var input in inputs) _actor.ReplaceMoveInput(input.X, input.Z);
            _world.Tick(_fixedDelta);
            candidate = new MovementState(_actor.transform.Value, _actor.moveInput.Dx, _actor.moveInput.Dz,
                candidate.Speed, true);
            return true;
        }

        public void Dispose()
        {
            try { _world.Dispose(); }
            finally { _pipeline.Dispose(); }
        }

        internal readonly struct MovementState : IEquatable<MovementState>
        {
            internal readonly Transform3 Pose;
            internal readonly float MoveX, MoveZ, Speed;
            internal readonly bool Eligible;

            internal MovementState(Transform3 pose, float moveX, float moveZ, float speed, bool eligible)
            {
                Pose = pose;
                MoveX = moveX;
                MoveZ = moveZ;
                Speed = speed;
                Eligible = eligible;
            }

            public bool Equals(MovementState other) => Pose.Equals(other.Pose) && MoveX == other.MoveX &&
                MoveZ == other.MoveZ && Speed == other.Speed && Eligible == other.Eligible;
        }

        private sealed class MovementModule : IWorldModule, IEntitasSystemsInstaller
        {
            public void Configure(WorldContainerBuilder builder)
            {
                builder.Register(WorldLifetime.Scoped, _ => new MobaActorRegistry());
                builder.Register(WorldLifetime.Scoped, _ => new MobaLocomotionRuntimeService());
            }

            public void Install(Entitas.IContexts contexts, Entitas.Systems systems, IWorldResolver services)
            {
                systems.Add(new MobaMotionLocomotionInputSystem(contexts, services));
                systems.Add(new MobaMotionTickSystem(contexts, services));
            }
        }
    }
}
