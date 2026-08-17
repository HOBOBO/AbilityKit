namespace AbilityKit.Game.Flow
{
    public sealed class InputSubmissionStatsSnapshot
    {
        public int CompletedCount;
        public int AcceptedCount;
        public int RejectedCount;
        public int FailedCount;
        public int LastServerFrame;
        public int LastAcceptedFrame;
        public int LastReasonCode;
        public bool LastShouldResync;
        public string LastStatus;
        public string LastMessage;
        public string LastFailure;
    }
}
