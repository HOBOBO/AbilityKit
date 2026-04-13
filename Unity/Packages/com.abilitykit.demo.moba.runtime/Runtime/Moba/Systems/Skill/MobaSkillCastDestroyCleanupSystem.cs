using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;

namespace AbilityKit.Demo.Moba.Systems
{
    [WorldSystem(order: MobaSystemOrder.SkillPipelines + 2, Phase = WorldSystemPhase.PostExecute)]
    public sealed class MobaSkillCastDestroyCleanupSystem : WorldSystemBase
    {
        private MobaAuthorityFrameService _authority;
        private IFrameTime _time;

        private global::Entitas.IGroup<global::ActorEntity> _group;

        public MobaSkillCastDestroyCleanupSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _authority);
            Services.TryResolve(out _time);
            _group = Contexts.Actor().GetGroup(ActorMatcher.SkillCastDestroyRequest);
        }

        protected override void OnExecute()
        {
            if (_group == null) return;

            var confirmed = 0;
            try { confirmed = _authority != null ? _authority.ConfirmedFrame.Value : (_time != null ? _time.Frame.Value : 0); }
            catch { confirmed = 0; }

            var entities = _group.GetEntities();
            if (entities == null || entities.Length == 0) return;

            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null) continue;
                if (!e.hasSkillCastDestroyRequest) continue;

                var req = e.skillCastDestroyRequest;
                if (confirmed < req.MinConfirmedFrame) continue;

                try { e.Destroy(); }
                catch { }
            }
        }
    }
}
