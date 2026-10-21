namespace AbilityKit.Demo.Moba.Share
{
    public enum GameplayClockPhase
    {
        NotStarted = 0,
        Running = 1,
        Ending = 2,
        Ended = 3,
    }

    public readonly struct GameplayClockData
    {
        public bool HasValue { get; }
        public int GameplayId { get; }
        public GameplayClockPhase Phase { get; }
        public int DurationMilliseconds { get; }
        public int RemainingMilliseconds { get; }
        public int ElapsedMilliseconds { get; }
        public string MatchId { get; }
        public int SnapshotFrame { get; }
        public bool IsVisible => GameplayId > 0 &&
            (Phase == GameplayClockPhase.Running || Phase == GameplayClockPhase.Ended);

        public GameplayClockData(int gameplayId, GameplayClockPhase phase,
            int durationMilliseconds, int remainingMilliseconds,
            string matchId = null, int snapshotFrame = 0, int elapsedMilliseconds = 0)
        {
            HasValue = true;
            GameplayId = gameplayId;
            Phase = phase;
            DurationMilliseconds = durationMilliseconds;
            RemainingMilliseconds = remainingMilliseconds;
            ElapsedMilliseconds = elapsedMilliseconds;
            MatchId = matchId ?? string.Empty;
            SnapshotFrame = snapshotFrame;
        }
    }
}
