using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World;

namespace AbilityKit.Demo.Moba.Systems
{
    [WorldSystem(order: MobaSystemOrder.SkillPipelines - 1, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaSkillCastCancelRequestSystem : WorldSystemBase
    {
        private SkillCastCoordinator _skills;
        private MobaAuthorityFrameService _authority;
        private IFrameTime _time;
        private MobaWorldSystemServices _systemServices;

        private global::Entitas.IGroup<global::ActorEntity> _group;

        public MobaSkillCastCancelRequestSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _skills);
            Services.TryResolve(out _authority);
            Services.TryResolve(out _time);
            _systemServices = MobaWorldSystemExecution.Resolve(Services);

            _group = Contexts.Actor().GetGroup(ActorMatcher.AllOf(
                ActorComponentsLookup.SkillCastCancelRequest,
                ActorComponentsLookup.SkillCastOwnerActorId,
                ActorComponentsLookup.SkillCastSkillId));
        }

        protected override void OnExecute()
        {
            MobaWorldSystemExecution.Require(
                _skills != null && _group != null,
                Services,
                nameof(MobaSkillCastCancelRequestSystem),
                "skill.cast.cancel.execute",
                "SkillCastCoordinator and cancel request group",
                $"hasSkills={_skills != null}, hasGroup={_group != null}");

            var frame = ResolveFrame();

            var entities = _group.GetEntities();
            if (entities == null || entities.Length == 0) return;

            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null) continue;
                if (!e.hasSkillCastCancelRequest || !e.hasSkillCastOwnerActorId) continue;

                var req = e.skillCastCancelRequest;
                if (req.Frame == int.MinValue) continue;
                if (req.Frame > 0 && frame > 0 && req.Frame > frame) continue;

                try
                {
                    _skills.CancelBySkillId(e.skillCastOwnerActorId.Value, e.skillCastSkillId.Value);
                }
                catch (Exception ex)
                {
                    ReportException(ex, "skill.cast.cancel.request", e.skillCastOwnerActorId.Value, e.skillCastSkillId.Value);
                }

                try
                {
                    e.ReplaceSkillCastCancelRequest(int.MinValue, req.Reason);
                }
                catch (Exception ex)
                {
                    ReportException(ex, "skill.cast.cancel.markConsumed", e.skillCastOwnerActorId.Value, e.skillCastSkillId.Value);
                }

                if (e.hasSkillCastStage)
                {
                    try
                    {
                        e.ReplaceSkillCastStage(SkillCastStage.Cancelled);
                    }
                    catch (Exception ex)
                    {
                        ReportException(ex, "skill.cast.cancel.markStage", e.skillCastOwnerActorId.Value, e.skillCastSkillId.Value);
                    }
                }
            }
        }

        private int ResolveFrame()
        {
            MobaWorldSystemExecution.Require(
                _authority != null || _time != null,
                Services,
                nameof(MobaSkillCastCancelRequestSystem),
                "skill.cast.cancel.resolveFrame",
                "MobaAuthorityFrameService or IFrameTime",
                $"hasAuthority={_authority != null}, hasFrameTime={_time != null}");

            if (_authority != null) return _authority.PredictedFrame.Value;
            return _time.Frame.Value;
        }

        private void ReportException(Exception ex, string operation, int actorId = 0, int skillId = 0)
        {
            MobaWorldSystemExecution.HandleException(
                in _systemServices,
                ex,
                nameof(MobaSkillCastCancelRequestSystem),
                operation,
                actorId: actorId,
                skillId: skillId);
        }
    }
}

