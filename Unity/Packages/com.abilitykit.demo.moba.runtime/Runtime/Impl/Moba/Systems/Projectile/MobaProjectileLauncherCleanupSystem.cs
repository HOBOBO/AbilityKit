using AbilityKit.Core.Common.Projectile;
using AbilityKit.Ability.Share.Impl.Moba.Services.Projectile;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.Impl.Moba;
using AbilityKit.Core.Common.Log;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems.Projectile
{
    [WorldSystem(order: MobaSystemOrder.ProjectileLauncherCleanup, Phase = WorldSystemPhase.PostExecute)]
    public sealed class MobaProjectileLauncherCleanupSystem : WorldSystemBase
    {
        private MobaActorRegistry _registry;
        private IProjectileService _projectiles;
        private IFrameTime _frameTime;

        private global::Entitas.IGroup<global::ActorEntity> _launchers;

        public MobaProjectileLauncherCleanupSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _registry);
            Services.TryResolve(out _projectiles);
            Services.TryResolve(out _frameTime);
            _launchers = Contexts.Actor().GetGroup(ActorMatcher.ProjectileLauncher);
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
                    catch (System.Exception ex) { Log.Exception(ex, $"[MobaProjectileLauncherCleanupSystem] CancelSchedule failed (scheduleId={plc.ScheduleId})"); }
                }

                _registry.Unregister(e.actorId.Value);

                try { e.Destroy(); }
                catch (System.Exception ex) { Log.Exception(ex, "[MobaProjectileLauncherCleanupSystem] destroy launcher entity failed"); }
            }
        }

        private long GetNowMs()
        {
            if (_frameTime == null) return 0L;
            return (long)(_frameTime.Time * 1000f);
        }
    }
}

