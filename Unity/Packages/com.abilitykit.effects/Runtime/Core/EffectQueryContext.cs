using System;

namespace AbilityKit.Effects.Core
{
    /// <summary>
    /// 效果查询上下文
    /// 封装查询效果时所需的上下文信息（Actor、Skill、Launcher、Projectile）
    /// </summary>
    [Serializable]
    public readonly struct EffectQueryContext : IEquatable<EffectQueryContext>
    {
        public readonly int ActorId;
        public readonly int SkillId;
        public readonly int LauncherId;
        public readonly int ProjectileId;

        public EffectQueryContext(int actorId, int skillId, int launcherId, int projectileId)
        {
            ActorId = actorId;
            SkillId = skillId;
            LauncherId = launcherId;
            ProjectileId = projectileId;
        }

        #region 工厂方法

        /// <summary>
        /// 创建空的查询上下文
        /// </summary>
        public static EffectQueryContext Empty => default;

        /// <summary>
        /// 创建用于发射器的查询上下文
        /// </summary>
        public static EffectQueryContext CreateForLauncher(int actorId, int skillId, int launcherId) =>
            new EffectQueryContext(actorId, skillId, launcherId, 0);

        /// <summary>
        /// 创建用于弹丸的查询上下文
        /// </summary>
        public static EffectQueryContext CreateForProjectile(int actorId, int skillId, int launcherId, int projectileId) =>
            new EffectQueryContext(actorId, skillId, launcherId, projectileId);

        /// <summary>
        /// 创建单位级别的查询上下文（仅ActorId）
        /// </summary>
        public static EffectQueryContext CreateForActor(int actorId) =>
            new EffectQueryContext(actorId, 0, 0, 0);

        #endregion

        #region 状态检查

        /// <summary>
        /// 是否为空上下文
        /// </summary>
        public bool IsEmpty => ActorId == 0 && SkillId == 0 && LauncherId == 0 && ProjectileId == 0;

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

        #endregion

        #region 转换方法

        /// <summary>
        /// 创建包含所有作用域的查询（Global + Actor + Skill + Launcher + Projectile）
        /// </summary>
        public int GetAllScopes(EffectScopeKey[] buffer, int bufferOffset)
        {
            int count = bufferOffset;
            buffer[count++] = default; // Global (KindId=0, Id=0)

            if (ActorId != 0) buffer[count++] = new EffectScopeKey(1, ActorId);
            if (SkillId != 0) buffer[count++] = new EffectScopeKey(2, SkillId);
            if (LauncherId != 0) buffer[count++] = new EffectScopeKey(3, LauncherId);
            if (ProjectileId != 0) buffer[count++] = new EffectScopeKey(4, ProjectileId);

            return count - bufferOffset;
        }

        #endregion

        #region 值相等

        public bool Equals(EffectQueryContext other) =>
            ActorId == other.ActorId &&
            SkillId == other.SkillId &&
            LauncherId == other.LauncherId &&
            ProjectileId == other.ProjectileId;

        public override bool Equals(object obj) => obj is EffectQueryContext other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(ActorId, SkillId, LauncherId, ProjectileId);

        public static bool operator ==(in EffectQueryContext left, in EffectQueryContext right) => left.Equals(right);
        public static bool operator !=(in EffectQueryContext left, in EffectQueryContext right) => !left.Equals(right);

        #endregion

        public override string ToString() =>
            $"EffectCtx(Actor={ActorId}, Skill={SkillId}, Launcher={LauncherId}, Projectile={ProjectileId})";
    }
}
