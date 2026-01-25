namespace AbilityKit.Ability.Share.Effect
{
    public static class ProjectileTriggering
    {
        public static class Events
        {
            public const string Hit = "projectile.hit";
            public const string Exit = "projectile.exit";
            public const string Tick = "projectile.tick";
            public const string Spawn = "projectile.spawn";
        }

        public static class Args
        {
            public const string ProjectileId = "projectile.id";
            public const string OwnerId = "projectile.ownerId";
            public const string TemplateId = "projectile.templateId";
            public const string LauncherActorId = "projectile.launcherActorId";
            public const string RootActorId = "projectile.rootActorId";
            public const string Frame = "projectile.frame";

            public const string HitCollider = "projectile.hit.collider";
            public const string HitDistance = "projectile.hit.distance";
            public const string HitPoint = "projectile.hit.point";
            public const string HitNormal = "projectile.hit.normal";

            public const string HitCount = "projectile.hit.count";
            public const string HitDecayRate = "projectile.hit.decayRate";

            public const string ExitReason = "projectile.exit.reason";
            public const string ExitPosition = "projectile.exit.position";

            public const string TickPosition = "projectile.tick.position";
            public const string SpawnPosition = "projectile.spawn.position";
            public const string SpawnDirection = "projectile.spawn.direction";
        }
    }
}
