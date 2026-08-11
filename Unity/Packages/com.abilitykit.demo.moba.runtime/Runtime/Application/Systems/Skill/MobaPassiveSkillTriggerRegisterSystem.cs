using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services.Passive;
using Entitas;

namespace AbilityKit.Demo.Moba.Systems
{
    /// <summary>
    /// 被动技能运行时注册入口：响应 SkillLoadout 变化，并委托被动生命周期服务同步 listener 与常驻触发器计划。
    /// </summary>
    [WorldSystem(order: MobaSystemOrder.PassiveSkillTriggers, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaPassiveSkillTriggerRegisterSystem : ReactiveWorldSystemBase<global::ActorEntity>
    {
        private IFrameTime _frameTime;
        private MobaPassiveSkillLifecycleService _passives;

        public MobaPassiveSkillTriggerRegisterSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override IGroup<global::ActorEntity> CreateGroup(global::Entitas.IContexts contexts)
        {
            var c = (global::Contexts)contexts;
            return c.actor.GetGroup(ActorMatcher.AllOf(ActorComponentsLookup.ActorId, ActorComponentsLookup.SkillLoadout));
        }

        protected override bool ShouldReactToReplace(int componentIndex)
        {
            return componentIndex == ActorComponentsLookup.SkillLoadout;
        }

        protected override void OnEntityChanged(global::ActorEntity entity)
        {
            EnsureServices();
            if (_passives == null) return;
            _passives.SyncActorPassives(entity, GetFrame());
        }

        protected override void OnEntityRemovedFromGroup(global::ActorEntity entity)
        {
            EnsureServices();
            if (_passives == null) return;
            _passives.UnregisterActor(entity, GetFrame());
        }

        protected override void OnTearDown()
        {
            try
            {
                EnsureServices();

                var group = Group;
                if (group != null && _passives != null)
                {
                    var frame = GetFrame();
                    var entities = group.GetEntities();
                    if (entities != null)
                    {
                        for (int i = 0; i < entities.Length; i++)
                        {
                            _passives.UnregisterActor(entities[i], frame);
                        }
                    }
                }

                _passives?.ReleaseAllOwnedPassiveContexts(GetFrame());
            }
            finally
            {
                base.OnTearDown();
            }
        }

        private void EnsureServices()
        {
            if (_frameTime == null) Services.TryResolve(out _frameTime);
            if (_passives == null) Services.TryResolve(out _passives);
        }

        private int GetFrame()
        {
            if (_frameTime != null) return _frameTime.Frame.Value;
            throw new InvalidOperationException("MobaPassiveSkillTriggerRegisterSystem requires IFrameTime for passive lifecycle frames.");
        }
    }
}
