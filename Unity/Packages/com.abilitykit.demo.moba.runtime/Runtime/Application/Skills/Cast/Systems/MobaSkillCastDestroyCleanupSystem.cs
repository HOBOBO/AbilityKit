using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World;

namespace AbilityKit.Demo.Moba.Systems
{
    [WorldSystem(order: MobaSystemOrder.SkillPipelines + 2, Phase = WorldSystemPhase.PostExecute)]
    public sealed class MobaSkillCastDestroyCleanupSystem : WorldSystemBase
    {
        private MobaAuthorityFrameService _authority;
        private IFrameTime _time;
        private MobaWorldSystemServices _systemServices;

        private global::Entitas.IGroup<global::ActorEntity> _group;

        public MobaSkillCastDestroyCleanupSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _authority);
            Services.TryResolve(out _time);
            _systemServices = MobaWorldSystemExecution.Resolve(Services);
            _group = Contexts.Actor().GetGroup(ActorMatcher.SkillCastDestroyRequest);
        }

        protected override void OnExecute()
        {
            MobaWorldSystemExecution.Require(
                _group != null,
                Services,
                nameof(MobaSkillCastDestroyCleanupSystem),
                "skill.cast.destroy.cleanup",
                "skill cast destroy request group");

            var confirmed = ResolveConfirmedFrame();

            var entities = _group.GetEntities();
            if (entities == null || entities.Length == 0) return;

            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null) continue;
                if (!e.hasSkillCastDestroyRequest) continue;

                var req = e.skillCastDestroyRequest;
                if (confirmed < req.MinConfirmedFrame) continue;

                try
                {
                    e.Destroy();
                }
                catch (Exception ex)
                {
                    ReportException(ex, "skill.cast.destroy.entity", e.hasSkillCastOwnerActorId ? e.skillCastOwnerActorId.Value : 0, e.hasSkillCastSkillId ? e.skillCastSkillId.Value : 0, e.hasSkillCastInstanceId ? e.skillCastInstanceId.Value : 0L);
                }
            }
        }

        private int ResolveConfirmedFrame()
        {
            MobaWorldSystemExecution.Require(
                _authority != null || _time != null,
                Services,
                nameof(MobaSkillCastDestroyCleanupSystem),
                "skill.cast.destroy.resolveFrame",
                "MobaAuthorityFrameService or IFrameTime",
                $"hasAuthority={_authority != null}, hasFrameTime={_time != null}");

            if (_authority != null) return _authority.ConfirmedFrame.Value;
            return _time.Frame.Value;
        }

        private void ReportException(Exception ex, string operation, int actorId = 0, int skillId = 0, long runtimeId = 0L)
        {
            MobaWorldSystemExecution.HandleException(
                in _systemServices,
                ex,
                nameof(MobaSkillCastDestroyCleanupSystem),
                operation,
                actorId: actorId,
                skillId: skillId,
                runtimeId: runtimeId);
        }
    }
}
