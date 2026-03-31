using System;
using AbilityKit.Effects.Core;
using AbilityKit.Ability.Impl.Moba.Effects.Model;

namespace AbilityKit.Ability.Impl.Moba.Effects.Runtime
{
    /// <summary>
    /// Moba游戏效果查询上下文
    /// 封装查询效果时所需的上下文信息
    /// </summary>
    internal readonly struct MobaEffectQueryContext
    {
        public readonly int ActorId;
        public readonly int SkillId;
        public readonly int LauncherId;
        public readonly int ProjectileId;

        public MobaEffectQueryContext(int actorId, int skillId, int launcherId, int projectileId)
        {
            ActorId = actorId;
            SkillId = skillId;
            LauncherId = launcherId;
            ProjectileId = projectileId;
        }

        public static MobaEffectQueryContext CreateForLauncher(int actorId, int skillId, int launcherId) => new(actorId, skillId, launcherId, 0);
        public static MobaEffectQueryContext CreateForProjectile(int actorId, int skillId, int launcherId, int projectileId) => new(actorId, skillId, launcherId, projectileId);

        public static MobaEffectQueryContext Empty => default;

        /// <summary>
        /// 是否有有效的Actor
        /// </summary>
        public bool HasActor => ActorId != 0;

        /// <summary>
        /// 是否有有效的Skill
        /// </summary>
        public bool HasSkill => SkillId != 0;

        /// <summary>
        /// 是否有有效的Launcher
        /// </summary>
        public bool HasLauncher => LauncherId != 0;

        /// <summary>
        /// 是否有有效的Projectile
        /// </summary>
        public bool HasProjectile => ProjectileId != 0;

        /// <summary>
        /// 是否是用于弹丸的上下文
        /// </summary>
        public bool IsProjectileContext => ProjectileId != 0;

        /// <summary>
        /// 获取所有相关的作用域（用于构建快照）
        /// </summary>
        public int GetAllScopes(EffectScopeKey[] buffer, int bufferOffset)
        {
            int count = bufferOffset;
            buffer[count++] = default; // Global (KindId=0, Id=0)

            if (ActorId != 0) buffer[count++] = MobaEffectScopeKeys.Unit(ActorId);
            if (SkillId != 0) buffer[count++] = MobaEffectScopeKeys.SkillId(SkillId);
            if (LauncherId != 0) buffer[count++] = MobaEffectScopeKeys.LauncherId(LauncherId);
            if (ProjectileId != 0) buffer[count++] = MobaEffectScopeKeys.ProjectileId(ProjectileId);

            return count - bufferOffset;
        }

        public override string ToString() =>
            $"MobaEffectCtx(Actor={ActorId}, Skill={SkillId}, Launcher={LauncherId}, Projectile={ProjectileId})";
    }
}
