using System;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba.Config.Core;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Services.Projectile.Launch
{
    /// <summary>
    /// 按同会话不可变配置目录恢复动态发射策略，重绑原 Modifier 和世界随机服务。
    /// 本解析器不保存随机状态，不执行发射，也不扩大 Cast 或 Continuous 的重建范围。
    /// </summary>
    [WorldService(typeof(IProjectileSchedulePatternResolver))]
    public sealed class MobaProjectileSchedulePatternResolver : IProjectileSchedulePatternResolver, IService
    {
        internal const int ModifierPatternKind = 1024;
        private readonly MobaConfigDatabase _configs;
        private readonly IWorldRandom _random;
        private readonly MobaSkillParamModifierService _modifiers;

        /// <summary>
        /// 关联原世界依赖；描述声明使用某依赖时，预检必须确认该依赖存在。
        /// </summary>
        public MobaProjectileSchedulePatternResolver(MobaConfigDatabase configs, IWorldRandom random,
            MobaSkillParamModifierService modifiers)
        { _configs = configs; _random = random; _modifiers = modifiers; }

        /// <summary>
        /// 在隔离数据中检查版本、请求参数、配置及依赖，不取样 Modifier 或随机数。
        /// </summary>
        public void Validate(in ProjectileSchedulePatternSnapshot snapshot, bool isProvider, in ProjectileSpawnParams baseSpawn)
        {
            if (!isProvider) throw new InvalidOperationException("MOBA schedule descriptor requires a dynamic provider.");
            var p = Read(in snapshot);
            if (p.ProjectileId != baseSpawn.TemplateId || p.HasModifiers && baseSpawn.RootActorId <= 0)
                throw new InvalidOperationException("MOBA schedule descriptor does not match its spawn parameters.");
            ValidateDependencies(in p);
        }

        private void ValidateDependencies(in MobaModifierProjectilePatternSnapshot p)
        {
            if (_configs == null || !_configs.TryGetProjectile(p.ProjectileId, out var projectile) || projectile == null ||
                p.HasRandom && _random == null || p.HasModifiers && _modifiers == null)
                throw new InvalidOperationException("MOBA schedule is missing its immutable config, modifier service or world random.");
            if (projectile.Speed != p.ConfigSpeed || projectile.LifetimeMs != p.ConfigLifetimeMs ||
                projectile.SpawnRandomOffsetX != p.RandomOffsetX || projectile.SpawnRandomOffsetY != p.RandomOffsetY ||
                projectile.SpawnRandomOffsetZ != p.RandomOffsetZ)
                throw new InvalidOperationException("MOBA schedule configuration changed; rebuild the rollback baseline.");
        }

        /// <summary>
        /// 本解析器不接受冻结模式描述，避免把动态取样错误转换为捕获时冻结。
        /// </summary>
        public IProjectileSpawnPattern RestorePattern(in ProjectileSchedulePatternSnapshot snapshot)
        { throw new InvalidOperationException("MOBA schedule descriptor is not a frozen pattern."); }

        /// <summary>
        /// 静默重建动态提供者；正常发射时继续按原 EventSample 与 CreateSnapshot 合同取样。
        /// </summary>
        public IProjectileSpawnPatternProvider RestoreProvider(in ProjectileSchedulePatternSnapshot snapshot)
        {
            var p = Read(in snapshot);
            ValidateDependencies(in p);
            _configs.TryGetProjectile(p.ProjectileId, out var projectile);
            return new MobaModifierProjectileSpawnPatternProvider(p.HasModifiers ? _modifiers : null, projectile,
                p.LauncherTemplateId, p.HasRandom ? _random : null, p.CountPerShot, p.FanAngleDeg,
                p.Slot, p.EffectiveSkillId, p.FrameDurationMs, p.BaseSkillId);
        }

        private static MobaModifierProjectilePatternSnapshot Read(in ProjectileSchedulePatternSnapshot snapshot)
        {
            if (snapshot.Kind != ModifierPatternKind || snapshot.Data == null || snapshot.Data.Length == 0)
                throw new InvalidOperationException("Unsupported MOBA projectile schedule descriptor.");
            var p = MemoryPackSerializer.Deserialize<MobaModifierProjectilePatternSnapshot>(snapshot.Data);
            if (p.Version != 1 || p.ProjectileId <= 0 || p.LauncherTemplateId <= 0 || p.CountPerShot <= 0 ||
                p.FanAngleDeg < 0 || float.IsNaN(p.FanAngleDeg) || float.IsInfinity(p.FanAngleDeg) ||
                float.IsNaN(p.FrameDurationMs) || float.IsInfinity(p.FrameDurationMs))
                throw new InvalidOperationException("Invalid MOBA projectile schedule request data.");
            return p;
        }

        internal static void ValidateLauncher(in ProjectileSchedulePatternSnapshot snapshot, int launcherTemplateId)
        {
            if (snapshot.Kind == ModifierPatternKind && Read(in snapshot).LauncherTemplateId != launcherTemplateId)
                throw new InvalidOperationException("MOBA schedule has a different launcher template.");
        }

        /// <summary>
        /// 不拥有依赖的释放职责。
        /// </summary>
        public void Dispose() { }
    }

    /// <summary>
    /// MOBA 动态发射请求及配置依赖证据，不含 Modifier 结果或随机状态副本。
    /// </summary>
    [MemoryPackable]
    internal partial struct MobaModifierProjectilePatternSnapshot
    {
        public int Version;
        public int ProjectileId;
        public int LauncherTemplateId;
        public int Slot;
        public int BaseSkillId;
        public int EffectiveSkillId;
        public float FrameDurationMs;
        public int CountPerShot;
        public float FanAngleDeg;
        public bool HasModifiers;
        public bool HasRandom;
        public float ConfigSpeed;
        public int ConfigLifetimeMs;
        public float RandomOffsetX;
        public float RandomOffsetY;
        public float RandomOffsetZ;
    }
}
