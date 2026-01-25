namespace AbilityKit.Ability.World.Entitas
{
    public static class WorldSystemOrder
    {
        public const int ModuleStep = 1000;

        public const int Early = 100;
        public const int Normal = 500;
        public const int Late = 900;

        public const int CoreBase = 0 * ModuleStep;
        public const int MobaBase = 1 * ModuleStep;
        public const int AbilityBase = 2 * ModuleStep;
        public const int SnapshotBase = 3 * ModuleStep;
        public const int RollbackBase = 4 * ModuleStep;
        public const int DebugBase = 9 * ModuleStep;
    }
}
