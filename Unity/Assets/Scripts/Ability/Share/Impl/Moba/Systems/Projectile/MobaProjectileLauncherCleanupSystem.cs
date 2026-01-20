using AbilityKit.Ability.Share.Common.Projectile;
using AbilityKit.Ability.Share.Impl.Moba.Services.Projectile;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems.Projectile
{
    [WorldSystem(order: MobaSystemOrder.ProjectileLauncherCleanup, Phase = WorldSystemPhase.PostExecute)]
    public sealed class MobaProjectileLauncherCleanupSystem : WorldSystemBase
    {
        private MobaActorRegistry _registry;
        private IProjectileService _projectiles;
        private IFrameTime _frameTime;

        private Entitas.IGroup<global::ActorEntity> _launchers;

        public MobaProjectileLauncherCleanupSystem(global::Contexts contexts, IWorldServices services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryGet(out _registry);
            Services.TryGet(out _projectiles);
            Services.TryGet(out _frameTime);
            _launchers = Contexts.actor.GetGroup(ActorMatcher.ProjectileLauncher);
        }

        protected override void OnExecute()
        {
            if (_registry == null) return;

            var entities = _launchers.GetEntities();
            if (entities == null || entities.Length == 0) return;

            var nowMs = GetNowMs();

            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null || !e.hasActorId || !e.hasProjectileLauncher) continue;

                var plc = e.projectileLauncher;
                if (plc.ActiveBullets > 0) continue;
                if (plc.EndTimeMs > 0 && nowMs < plc.EndTimeMs) continue;

                if (_projectiles != null && plc.ScheduleId > 0)
                {
                    try { _projectiles.CancelSchedule(new ProjectileScheduleId(plc.ScheduleId)); }
                    catch { }
                }

                _registry.Unregister(e.actorId.Value);

                try { e.Destroy(); }
                catch { }
            }
        }

        private long GetNowMs()
        {
            if (_frameTime == null) return 0L;
            return (long)(_frameTime.Time * 1000f);
        }
    }
}
