using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;

namespace AbilityKit.Demo.Moba.Systems
{
    [WorldSystem(order: MobaSystemOrder.SkillPipelines - 1, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaSkillCastCancelRequestSystem : WorldSystemBase
    {
        private SkillExecutor _skills;
        private MobaAuthorityFrameService _authority;
        private IFrameTime _time;

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

            _group = Contexts.Actor().GetGroup(ActorMatcher.AllOf(
                ActorComponentsLookup.SkillCastCancelRequest,
                ActorComponentsLookup.SkillCastOwnerActorId,
                ActorComponentsLookup.SkillCastSkillId));
        }

        protected override void OnExecute()
        {
            if (_skills == null || _group == null) return;

            var frame = 0;
            try { frame = _authority != null ? _authority.PredictedFrame.Value : (_time != null ? _time.Frame.Value : 0); }
            catch { frame = 0; }

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

                try { _skills.CancelBySkillId(e.skillCastOwnerActorId.Value, e.skillCastSkillId.Value); }
                catch { }

                try { e.ReplaceSkillCastCancelRequest(int.MinValue, req.Reason); }
                catch { }

                if (e.hasSkillCastStage)
                {
                    try { e.ReplaceSkillCastStage(SkillCastStage.Cancelled); }
                    catch { }
                }
            }
        }
    }
}
