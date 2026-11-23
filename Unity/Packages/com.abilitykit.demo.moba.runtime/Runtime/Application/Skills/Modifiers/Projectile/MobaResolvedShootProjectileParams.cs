namespace AbilityKit.Demo.Moba.Services
{
    public readonly struct MobaResolvedShootProjectileParams
    {
        public MobaResolvedShootProjectileParams(int launcherId, int projectileId, int countPerShot, float fanAngleDeg, int durationMs)
        {
            if (launcherId <= 0) throw new System.ArgumentOutOfRangeException(nameof(launcherId), launcherId, "Projectile launcher id must be positive.");
            if (projectileId <= 0) throw new System.ArgumentOutOfRangeException(nameof(projectileId), projectileId, "Projectile id must be positive.");
            if (countPerShot <= 0) throw new System.ArgumentOutOfRangeException(nameof(countPerShot), countPerShot, "Projectile count per shot must be positive.");
            if (fanAngleDeg < 0f) throw new System.ArgumentOutOfRangeException(nameof(fanAngleDeg), fanAngleDeg, "Projectile fan angle cannot be negative.");
            if (durationMs < 0) throw new System.ArgumentOutOfRangeException(nameof(durationMs), durationMs, "Projectile duration cannot be negative.");

            LauncherId = launcherId;
            ProjectileId = projectileId;
            CountPerShot = countPerShot;
            FanAngleDeg = fanAngleDeg;
            DurationMs = durationMs;
        }

        public int LauncherId { get; }
        public int ProjectileId { get; }
        public int CountPerShot { get; }
        public float FanAngleDeg { get; }
        public int DurationMs { get; }
    }
}
