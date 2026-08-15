using System.Collections.Generic;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Runtime.Application.Services.Triggering;

namespace AbilityKit.Demo.Moba.Systems.Triggering
{
    [WorldSystem(order: MobaSystemOrder.OngoingTriggerPlansReconcile, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaOngoingTriggerPlansReconcileSystem : WorldSystemBase
    {
        private readonly List<OngoingTriggerPlanEntry> _activePlans = new List<OngoingTriggerPlanEntry>(16);
        private MobaTriggerPlanReconcileService _reconcileService;
        private global::Entitas.IGroup<global::ActorEntity> _group;

        public MobaOngoingTriggerPlansReconcileSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _reconcileService);
            _group = Contexts.Actor().GetGroup(ActorMatcher.AllOf(ActorComponentsLookup.ActorId, ActorComponentsLookup.OngoingTriggerPlans));
        }

        protected override void OnExecute()
        {
            if (_reconcileService == null) return;

            _activePlans.Clear();
            var entities = _group.GetEntities();
            if (entities != null && entities.Length > 0)
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    var e = entities[i];
                    if (e == null || !e.hasOngoingTriggerPlans) continue;

                    var list = e.ongoingTriggerPlans.Active;
                    if (list == null || list.Count == 0) continue;

                    for (int j = 0; j < list.Count; j++)
                    {
                        _activePlans.Add(list[j]);
                    }
                }
            }

            _reconcileService.Reconcile(_activePlans);
        }
    }
}
