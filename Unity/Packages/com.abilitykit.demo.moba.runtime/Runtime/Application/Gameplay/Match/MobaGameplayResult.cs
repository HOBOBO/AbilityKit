namespace AbilityKit.Demo.Moba.Gameplay
{
    public readonly struct MobaGameplayResult
    {
        public readonly string Reason;
        public readonly int WinTeamId;
        public readonly int EndFrame;
        public readonly float ElapsedSeconds;

        public MobaGameplayResult(string reason, int winTeamId, int endFrame, float elapsedSeconds)
        {
            Reason = string.IsNullOrEmpty(reason) ? "unknown" : reason;
            WinTeamId = winTeamId;
            EndFrame = endFrame;
            ElapsedSeconds = elapsedSeconds;
        }
    }
}
