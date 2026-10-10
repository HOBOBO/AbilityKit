namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public readonly struct AwardTeamKillScoreArgs
    {
        public readonly int KeyBaseId;
        public readonly double Points;

        public AwardTeamKillScoreArgs(int keyBaseId, double points)
        {
            KeyBaseId = keyBaseId;
            Points = points;
        }
    }
}
