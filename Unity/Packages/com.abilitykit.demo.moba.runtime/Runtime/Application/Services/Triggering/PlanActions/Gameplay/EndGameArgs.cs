namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public readonly struct EndGameArgs
    {
        public readonly int ReasonId;
        public readonly int WinTeamId;
        public readonly bool WinTeamFromEvent;

        public EndGameArgs(int reasonId, int winTeamId, bool winTeamFromEvent = false)
        {
            ReasonId = reasonId;
            WinTeamId = winTeamId;
            WinTeamFromEvent = winTeamFromEvent;
        }
    }
}
