using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Demo.Moba.Systems
{
    [WorldSystem(order: MobaSystemOrder.BrainTick, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaBrainSystem : WorldSystemBase
    {
        private IFrameTime _frameTime;
        private IWorldClock _clock;
        private MobaBrainService _brains;
        private Entitas.IGroup<global::ActorEntity> _group;

        public MobaBrainSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _frameTime);
            Services.TryResolve(out _clock);
            Services.TryResolve(out _brains);
            _group = Contexts.Actor().GetGroup(global::ActorMatcher.AllOf(
                global::ActorComponentsLookup.ActorId,
                global::ActorComponentsLookup.ActorBrain));
        }

        protected override void OnExecute()
        {
            if (_brains == null || _group == null) return;

            var entities = _group.GetEntities();
            for (var i = 0; i < entities.Length; i++)
            {
                if (!entities[i].hasActorStateMachine)
                {
                    _brains.EnsureBehavior(entities[i]);
                }
            }

            var dt = ResolveDeltaTime();
            if (dt <= 0f) return;

            _brains.Tick(dt, ResolveFrame());
        }

        private float ResolveDeltaTime()
        {
            if (_clock != null) return _clock.DeltaTime;
            return _frameTime != null ? _frameTime.DeltaTime : 0f;
        }

        private long ResolveFrame()
        {
            return _frameTime != null ? _frameTime.Frame.Value : 0L;
        }
    }
}
