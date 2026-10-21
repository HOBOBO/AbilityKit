using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;

namespace AbilityKit.Demo.Moba.Services.Projectile.Launch
{
    public readonly struct MobaProjectileLaunchRequest
    {
        public MobaProjectileLaunchRequest(
            int casterActorId,
            ProjectileLauncherMO launcher,
            ProjectileMO projectile,
            int countPerShot,
            float fanAngleDeg,
            int durationMs,
            int continuousProcessId,
            bool trackTarget,
            in Vec3 spawnPosition,
            in Vec3 direction,
            in ProjectileSourceContext sourceContext,
            float maxDistance = 0f)
        {
            CasterActorId = casterActorId;
            Launcher = launcher;
            Projectile = projectile;
            CountPerShot = countPerShot;
            FanAngleDeg = fanAngleDeg;
            DurationMs = durationMs;
            ContinuousProcessId = continuousProcessId;
            TrackTarget = trackTarget;
            SpawnPosition = spawnPosition;
            Direction = direction;
            SourceContext = sourceContext;
            MaxDistance = maxDistance;
        }

        public int CasterActorId { get; }
        public ProjectileLauncherMO Launcher { get; }
        public ProjectileMO Projectile { get; }
        public int CountPerShot { get; }
        public float FanAngleDeg { get; }
        public int DurationMs { get; }
        public int ContinuousProcessId { get; }
        public bool TrackTarget { get; }
        public Vec3 SpawnPosition { get; }
        public Vec3 Direction { get; }
        public ProjectileSourceContext SourceContext { get; }
        public float MaxDistance { get; }

        public int LauncherId => Launcher != null ? Launcher.Id : 0;
        public int ProjectileId => Projectile != null ? Projectile.Id : 0;
    }
}
