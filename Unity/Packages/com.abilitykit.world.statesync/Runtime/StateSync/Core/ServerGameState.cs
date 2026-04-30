namespace AbilityKit.Ability.StateSync
{
    public readonly struct ServerGameState
    {
        public int Frame { get; }
        public long Timestamp { get; }
        public byte[] StateData { get; }
        public StateHash Hash { get; }
        public bool IsKeyFrame { get; }
        public int PlayerCount { get; }

        public ServerGameState(
            int frame,
            long timestamp,
            byte[] stateData,
            StateHash hash,
            bool isKeyFrame,
            int playerCount = 0)
        {
            Frame = frame;
            Timestamp = timestamp;
            StateData = stateData;
            Hash = hash;
            IsKeyFrame = isKeyFrame;
            PlayerCount = playerCount;
        }
    }
}
