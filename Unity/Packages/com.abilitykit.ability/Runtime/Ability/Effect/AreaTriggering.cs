namespace AbilityKit.Ability.Share.Effect
{
    public static class AreaTriggering
    {
        public static class Events
        {
            public const string Spawn = "area.spawn";
            public const string Enter = "area.enter";
            public const string Stay = "area.stay";
            public const string Exit = "area.exit";
            public const string Expire = "area.expire";
        }

        public static class Args
        {
            public const string AreaId = "area.id";
            public const string OwnerId = "area.ownerId";
            public const string Frame = "area.frame";

            public const string Center = "area.center";
            public const string Radius = "area.radius";

            public const string Collider = "area.collider";
        }
    }
}
