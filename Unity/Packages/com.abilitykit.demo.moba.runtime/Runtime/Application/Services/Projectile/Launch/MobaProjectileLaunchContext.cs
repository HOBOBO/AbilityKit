using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;

namespace AbilityKit.Demo.Moba.Services.Projectile.Launch
{
    public readonly struct MobaProjectileLaunchContext
    {
        public MobaProjectileLaunchContext(
            in MobaProjectileLaunchRequest request,
            ProjectileLauncherMO launcher,
            ProjectileMO projectile,
            int launcherActorId,
            global::ActorEntity launcherEntity,
            in ProjectileSpawnParams baseSpawn,
            in ProjectileSourceContext launcherSource,
            int startFrame,
            long endTimeMs,
            int intervalFrames,
            int repeatCount,
            int bulletsPerShot,
            float requestFanAngleDeg,
            IProjectileService projectiles,
            MobaProjectileLinkService links,
            MobaSkillParamModifierService skillParamModifiers,
            AbilityKit.Ability.World.Services.IWorldRandom random,
            IMobaProjectileLaunchRuntime runtime,
            MobaParameterRecord skillRecord = default,
            float frameDurationMs = 0f)
        {
            if (repeatCount <= 0) throw new System.ArgumentOutOfRangeException(nameof(repeatCount), repeatCount, "Projectile repeat count must be positive.");
            if (repeatCount > 1 && intervalFrames <= 0) throw new System.ArgumentOutOfRangeException(nameof(intervalFrames), intervalFrames, "Repeated projectile launch requires a positive interval.");
            if (bulletsPerShot <= 0) throw new System.ArgumentOutOfRangeException(nameof(bulletsPerShot), bulletsPerShot, "Projectile bullets per shot must be positive.");
            if (requestFanAngleDeg < 0f) throw new System.ArgumentOutOfRangeException(nameof(requestFanAngleDeg), requestFanAngleDeg, "Projectile fan angle cannot be negative.");

            Request = request;
            Launcher = launcher;
            Projectile = projectile;
            LauncherActorId = launcherActorId;
            LauncherEntity = launcherEntity;
            BaseSpawn = baseSpawn;
            LauncherSource = launcherSource;
            StartFrame = startFrame;
            EndTimeMs = endTimeMs;
            IntervalFrames = intervalFrames;
            RepeatCount = repeatCount;
            BulletsPerShot = bulletsPerShot;
            RequestFanAngleDeg = requestFanAngleDeg;
            Projectiles = projectiles;
            Links = links;
            SkillParamModifiers = skillParamModifiers;
            Random = random;
            Runtime = runtime;
            SkillRecord = skillRecord;
            FrameDurationMs = frameDurationMs;
        }

        public MobaProjectileLaunchRequest Request { get; }
        public ProjectileLauncherMO Launcher { get; }
        public ProjectileMO Projectile { get; }
        public int LauncherActorId { get; }
        public global::ActorEntity LauncherEntity { get; }
        public ProjectileSpawnParams BaseSpawn { get; }
        public ProjectileSourceContext LauncherSource { get; }
        public int StartFrame { get; }
        public long EndTimeMs { get; }
        public int IntervalFrames { get; }
        public int RepeatCount { get; }
        public int BulletsPerShot { get; }
        public float RequestFanAngleDeg { get; }
        public IProjectileService Projectiles { get; }
        public MobaProjectileLinkService Links { get; }
        public MobaSkillParamModifierService SkillParamModifiers { get; }
        public AbilityKit.Ability.World.Services.IWorldRandom Random { get; }
        public IMobaProjectileLaunchRuntime Runtime { get; }
        public MobaParameterRecord SkillRecord { get; }
        public float FrameDurationMs { get; }
        public int TotalCount => RepeatCount * BulletsPerShot;
    }
}
