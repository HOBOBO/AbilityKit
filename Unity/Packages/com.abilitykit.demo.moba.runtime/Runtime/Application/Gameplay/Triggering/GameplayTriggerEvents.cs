using AbilityKit.Core.Eventing;
using AbilityKit.Demo.Moba.Systems;

namespace AbilityKit.Demo.Moba.Gameplay.Triggering
{
    public static class GameplayTriggerEvents
    {
        public const string Started = "gameplay.started";
        public const string Tick = "gameplay.tick";
        public const string TimeExpired = "gameplay.time_expired";
        public const string ScoreChanged = "gameplay.score_changed";
        public const string Ended = "gameplay.ended";

        public const string FrameIndexField = "frame_index";
        public const string ElapsedSecondsField = "elapsed_seconds";
        public const string DeltaSecondsField = "delta_seconds";
        public const string WinTeamIdField = "win_team_id";
        public const string TeamIdField = "team_id";
        public const string ScoreField = "score";

        public static EventKey<GameplayLifecycleEventArgs> GetKey(string eventName)
        {
            return new EventKey<GameplayLifecycleEventArgs>(TriggeringConstants.GetEventId(eventName));
        }
    }

    public sealed class GameplayLifecycleEventArgs
    {
        public int FrameIndex { get; }

        public float ElapsedSeconds { get; }

        public float DeltaSeconds { get; }

        public string Reason { get; }

        public int WinTeamId { get; }
        public int TeamId { get; }
        public double Score { get; }

        public GameplayLifecycleEventArgs(int frameIndex, float elapsedSeconds, float deltaSeconds, string reason,
            int winTeamId = 0, int teamId = 0, double score = 0d)
        {
            FrameIndex = frameIndex;
            ElapsedSeconds = elapsedSeconds;
            DeltaSeconds = deltaSeconds;
            Reason = reason;
            WinTeamId = winTeamId;
            TeamId = teamId;
            Score = score;
        }
    }
}
