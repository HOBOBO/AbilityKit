namespace AbilityKit.Effects.Core
{
    public readonly struct EffectQueryContext
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

        public static EffectQueryContext CreateForLauncher(int actorId, int skillId, int launcherId) => new(actorId, skillId, launcherId, 0);
        public static EffectQueryContext CreateForProjectile(int actorId, int skillId, int launcherId, int projectileId) => new(actorId, skillId, launcherId, projectileId);
    }
}
