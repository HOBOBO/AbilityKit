using AbilityKit.Ability.World;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Triggering;

namespace AbilityKit.Demo.Moba.Systems.Triggering
{
    [WorldSystem(order: MobaSystemOrder.TriggerExecutionTick, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaTriggerExecutionTickSystem : WorldSystemBase
    {
        private IWorldClock _clock;
        private MobaTriggerExecutionRuntimeService _runtime;
        private MobaAuthorityFrameService _authority;

        public MobaTriggerExecutionTickSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _clock);
            Services.TryResolve(out _runtime);
            Services.TryResolve(out _authority);
        }

        protected override void OnExecute()
        {
            if (_runtime == null) return;
            if (_clock != null && _clock.DeltaTime > 0f) _runtime.Tick(_clock.DeltaTime * 1000f);
            if (_authority != null && _authority.TryGetFrames(out var confirmed, out var predicted))
                _runtime.TrimConfirmedHistory(confirmed, predicted, _authority.RollbackHistoryFrames);
        }
    }
}
