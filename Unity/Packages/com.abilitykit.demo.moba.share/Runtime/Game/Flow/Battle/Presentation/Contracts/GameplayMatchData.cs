namespace AbilityKit.Demo.Moba.Share
{
    public static class GameplayScoreKeys
    {
        public const int Team1 = 1001;
        public const int Team2 = 1002;
    }

    public readonly struct GameplayMatchData
    {
        public int GameplayId { get; }
        public GameplayClockPhase Phase { get; }
        public double Team1Score { get; }
        public double Team2Score { get; }
        public int WinTeamId { get; }
        public int EndFrame { get; }
        public string EndReason { get; }
        public string MatchId { get; }
        public int SnapshotFrame { get; }

        public GameplayMatchData(int gameplayId, GameplayClockPhase phase,
            double team1Score, double team2Score, int winTeamId, int endFrame, string endReason,
            string matchId = null, int snapshotFrame = 0)
        {
            GameplayId = gameplayId;
            Phase = phase;
            Team1Score = team1Score;
            Team2Score = team2Score;
            WinTeamId = winTeamId;
            EndFrame = endFrame;
            EndReason = endReason ?? string.Empty;
            MatchId = matchId ?? string.Empty;
            SnapshotFrame = snapshotFrame;
        }
    }
}
