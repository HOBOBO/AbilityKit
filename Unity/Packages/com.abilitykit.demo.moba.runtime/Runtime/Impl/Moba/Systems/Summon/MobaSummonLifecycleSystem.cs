using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems.Summon
{
    [WorldSystem(order: MobaSystemOrder.SummonLifecycle, Phase = WorldSystemPhase.PostExecute)]
    public sealed class MobaSummonLifecycleSystem : WorldSystemBase
    {
        private MobaSummonService _summons;
        private MobaActorRegistry _registry;
        private AbilityKit.Ability.FrameSync.IFrameTime _frameTime;
        private AbilityKit.Ability.World.Services.IWorldClock _clock;
        private global::Entitas.IGroup<global::ActorEntity> _group;

        public MobaSummonLifecycleSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _summons);
            Services.TryResolve(out _registry);
            Services.TryResolve(out _frameTime);
            Services.TryResolve(out _clock);

            var ctx = (global::Contexts)Contexts;
            _group = ctx.actor.GetGroup(ActorMatcher.AllOf(
                ActorComponentsLookup.SummonMeta,
                ActorComponentsLookup.OwnerLink));
        }

        protected override void OnExecute()
        {
            if (_summons == null) return;
            if (_registry == null) return;

            var nowMs = NowMs();

            var entities = _group.GetEntities();
            if (entities == null || entities.Length == 0) return;

            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null) continue;
                if (!e.hasActorId) continue;

                var actorId = e.actorId.Value;
                if (actorId <= 0) continue;

                // timeout
                if (e.hasLifetime && e.lifetime != null && e.lifetime.EndTimeMs > 0 && nowMs > 0 && nowMs >= e.lifetime.EndTimeMs)
                {
                    _summons.TryDespawn(actorId, SummonDespawnReason.Timeout);
                    continue;
                }

                // owner dead
                if (e.hasSummonMeta && e.summonMeta != null && e.summonMeta.DespawnOnOwnerDie)
                {
                    if (e.hasOwnerLink && e.ownerLink != null)
                    {
                        var ownerActorId = e.ownerLink.OwnerActorId;
                        if (ownerActorId > 0)
                        {
                            if (!_registry.TryGet(ownerActorId, out var owner) || owner == null)
                            {
                                _summons.TryDespawn(actorId, SummonDespawnReason.OwnerDead);
                                continue;
                            }
                        }
                    }
                }
            }
        }

        private long NowMs()
        {
            if (_frameTime != null)
            {
                return (long)System.MathF.Round(_frameTime.Time * 1000f);
            }
            if (_clock != null)
            {
                return (long)System.MathF.Round(_clock.Time * 1000f);
            }
            return 0L;
        }
    }
}

