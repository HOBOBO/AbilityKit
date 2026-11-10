using AbilityKit.Ability.World;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Demo.Moba.Services.Triggering.PlanActions;

namespace AbilityKit.Demo.Moba.Systems.Triggering
{
    [WorldSystem(order: MobaSystemOrder.TriggerExecutionTick, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaTriggerExecutionTickSystem : WorldSystemBase
    {
        private IWorldClock _clock;
        private MobaTriggerExecutionRuntimeService _runtime;
        private MobaAuthorityFrameService _authority;
        private Entitas.IGroup<global::ActorEntity> _actors;

        public MobaTriggerExecutionTickSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _clock);
            Services.TryResolve(out _runtime);
            Services.TryResolve(out _authority);
            _actors = Contexts.Actor().GetGroup(global::ActorMatcher.AllOf(global::ActorComponentsLookup.ActorId));
        }

        protected override void OnExecute()
        {
            if (_runtime == null) return;
            if (_clock != null && _clock.DeltaTime > 0f) _runtime.Tick(_clock.DeltaTime * 1000f);
            // Trigger plans may write HFSM parameters after the skill pipeline
            // step. Reconcile every actor at the end of the trigger batch so a
            // completed short cast cannot leave a ready attack slot behind until
            // an unrelated future tick. The trigger remains the source of the
            // parameter write; this call only closes the generic synchronization
            // boundary between trigger execution and the character HFSM.
            if (_actors != null)
            {
                var actors = _actors.GetEntities();
                for (var i = 0; i < actors.Length; i++)
                {
                    var actor = actors[i];
                    if (actor != null && actor.hasActorId)
                        MobaCharacterHfsmParameterActionRuntime.Reevaluate(Services, actor.actorId.Value);
                }
            }
            if (_authority != null && _authority.TryGetFrames(out var confirmed, out var predicted))
                _runtime.TrimConfirmedHistory(confirmed, predicted, _authority.RollbackHistoryFrames);
        }
    }
}
