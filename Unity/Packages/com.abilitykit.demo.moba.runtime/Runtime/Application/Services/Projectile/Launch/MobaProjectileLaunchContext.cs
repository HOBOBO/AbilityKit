using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;

namespace AbilityKit.Demo.Moba.Services.Projectile.Launch
{
    public readonly struct MobaProjectileLaunchFacts
    {
        public MobaProjectileLaunchFacts(
            in MobaProjectileLaunchRequest request,
            ProjectileLauncherMO launcher,
            ProjectileMO projectile,
            int launcherActorId,
            global::ActorEntity launcherEntity,
            in ProjectileSpawnParams baseSpawn,
            in ProjectileSourceContext launcherSource,
            MobaParameterRecord skillRecord)
        {
            Request = request;
            Launcher = launcher;
            Projectile = projectile;
            LauncherActorId = launcherActorId;
            LauncherEntity = launcherEntity;
            BaseSpawn = baseSpawn;
            LauncherSource = launcherSource;
            SkillRecord = skillRecord;
        }

        public MobaProjectileLaunchRequest Request { get; }
        public ProjectileLauncherMO Launcher { get; }
        public ProjectileMO Projectile { get; }
        public int LauncherActorId { get; }
        public global::ActorEntity LauncherEntity { get; }
        public ProjectileSpawnParams BaseSpawn { get; }
        public ProjectileSourceContext LauncherSource { get; }
        public MobaParameterRecord SkillRecord { get; }
    }

    public readonly struct MobaProjectileLaunchSchedule
    {
        public MobaProjectileLaunchSchedule(
            int startFrame,
            long endTimeMs,
            int intervalFrames,
            int repeatCount,
            int bulletsPerShot,
            float requestFanAngleDeg,
            float frameDurationMs)
        {
            if (repeatCount <= 0) throw new System.ArgumentOutOfRangeException(nameof(repeatCount));
            if (repeatCount > 1 && intervalFrames <= 0) throw new System.ArgumentOutOfRangeException(nameof(intervalFrames));
            if (bulletsPerShot <= 0) throw new System.ArgumentOutOfRangeException(nameof(bulletsPerShot));
            if (requestFanAngleDeg < 0f) throw new System.ArgumentOutOfRangeException(nameof(requestFanAngleDeg));
            StartFrame = startFrame;
            EndTimeMs = endTimeMs;
            IntervalFrames = intervalFrames;
            RepeatCount = repeatCount;
            BulletsPerShot = bulletsPerShot;
            RequestFanAngleDeg = requestFanAngleDeg;
            FrameDurationMs = frameDurationMs;
        }

        public int StartFrame { get; }
        public long EndTimeMs { get; }
        public int IntervalFrames { get; }
        public int RepeatCount { get; }
        public int BulletsPerShot { get; }
        public float RequestFanAngleDeg { get; }
        public float FrameDurationMs { get; }
    }

    public readonly struct MobaProjectileLaunchServices
    {
        public MobaProjectileLaunchServices(
            IProjectileService projectiles,
            MobaProjectileLinkService links,
            MobaSkillParamModifierService skillParamModifiers,
            AbilityKit.Ability.World.Services.IWorldRandom random,
            IMobaProjectileLaunchRuntime runtime)
        {
            Projectiles = projectiles;
            Links = links;
            SkillParamModifiers = skillParamModifiers;
            Random = random;
            Runtime = runtime;
        }

        public IProjectileService Projectiles { get; }
        public MobaProjectileLinkService Links { get; }
        public MobaSkillParamModifierService SkillParamModifiers { get; }
        public AbilityKit.Ability.World.Services.IWorldRandom Random { get; }
        public IMobaProjectileLaunchRuntime Runtime { get; }
    }

    public readonly struct MobaProjectileLaunchContext
    {
        public MobaProjectileLaunchContext(
            in MobaProjectileLaunchFacts facts,
            in MobaProjectileLaunchSchedule schedule,
            in MobaProjectileLaunchServices services)
        {
            Facts = facts;
            Schedule = schedule;
            Services = services;
        }

        public MobaProjectileLaunchFacts Facts { get; }
        public MobaProjectileLaunchSchedule Schedule { get; }
        public MobaProjectileLaunchServices Services { get; }

        public MobaProjectileLaunchRequest Request => Facts.Request;
        public ProjectileLauncherMO Launcher => Facts.Launcher;
        public ProjectileMO Projectile => Facts.Projectile;
        public int LauncherActorId => Facts.LauncherActorId;
        public global::ActorEntity LauncherEntity => Facts.LauncherEntity;
        public ProjectileSpawnParams BaseSpawn => Facts.BaseSpawn;
        public ProjectileSourceContext LauncherSource => Facts.LauncherSource;
        public int StartFrame => Schedule.StartFrame;
        public long EndTimeMs => Schedule.EndTimeMs;
        public int IntervalFrames => Schedule.IntervalFrames;
        public int RepeatCount => Schedule.RepeatCount;
        public int BulletsPerShot => Schedule.BulletsPerShot;
        public float RequestFanAngleDeg => Schedule.RequestFanAngleDeg;
        public IProjectileService Projectiles => Services.Projectiles;
        public MobaProjectileLinkService Links => Services.Links;
        public MobaSkillParamModifierService SkillParamModifiers => Services.SkillParamModifiers;
        public AbilityKit.Ability.World.Services.IWorldRandom Random => Services.Random;
        public IMobaProjectileLaunchRuntime Runtime => Services.Runtime;
        public MobaParameterRecord SkillRecord => Facts.SkillRecord;
        public float FrameDurationMs => Schedule.FrameDurationMs;
        public int TotalCount => RepeatCount * BulletsPerShot;
    }
}
