namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public readonly struct ScheduleHeroRespawnArgs
    {
        public readonly int DelayMilliseconds;
        public readonly int SpawnIndex;
        public readonly float HealthRatio;
        public readonly int MaxRespawns;

        public ScheduleHeroRespawnArgs(int delayMilliseconds, int spawnIndex, float healthRatio, int maxRespawns)
        {
            DelayMilliseconds = delayMilliseconds;
            SpawnIndex = spawnIndex;
            HealthRatio = healthRatio;
            MaxRespawns = maxRespawns;
        }
    }
}
