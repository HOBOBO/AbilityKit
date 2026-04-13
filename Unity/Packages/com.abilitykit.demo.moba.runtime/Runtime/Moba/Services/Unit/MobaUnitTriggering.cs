namespace AbilityKit.Demo.Moba.Services
{
    public static class MobaUnitTriggering
    {
        public static class Events
        {
            public const string Spawn = "unit.spawn";
            public const string Despawn = "unit.despawn";
            public const string Die = "unit.die";
        }

        public static class Args
        {
            public const string ActorId = "unit.actorId";
            public const string Team = "unit.team";
            public const string MainType = "unit.mainType";
            public const string UnitSubType = "unit.subType";
            public const string OwnerPlayerId = "unit.ownerPlayerId";
        }
    }
}
