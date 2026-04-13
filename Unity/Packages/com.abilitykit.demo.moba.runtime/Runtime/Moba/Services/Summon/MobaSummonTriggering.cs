namespace AbilityKit.Demo.Moba.Services
{
    public static class MobaSummonTriggering
    {
        public static class Events
        {
            public const string Spawned = "summon.spawn";
            public const string Despawned = "summon.despawn";
            public const string Died = "summon.die";

            public static string SpawnedByOwner(int rootOwnerActorId) => $"summon.spawn.owner.{rootOwnerActorId}";
            public static string DespawnedByOwner(int rootOwnerActorId) => $"summon.despawn.owner.{rootOwnerActorId}";
            public static string DiedByOwner(int rootOwnerActorId) => $"summon.die.owner.{rootOwnerActorId}";
        }

        public static class Args
        {
            public const string SummonActorId = "summon.actorId";
            public const string SummonId = "summon.id";
            public const string OwnerActorId = "summon.ownerActorId";
            public const string RootOwnerActorId = "summon.rootOwnerActorId";
            public const string Reason = "summon.reason";
        }
    }
}
