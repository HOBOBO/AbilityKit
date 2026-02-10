namespace AbilityKit.Ability.Impl.Moba.Effects.Runtime
{
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
    }
}
