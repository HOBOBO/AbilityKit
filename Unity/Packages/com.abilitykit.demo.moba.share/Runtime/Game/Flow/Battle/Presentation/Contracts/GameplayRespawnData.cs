namespace AbilityKit.Demo.Moba.Share
{
    public readonly struct GameplayRespawnData
    {
        public readonly int ActorId;
        public readonly int RemainingMilliseconds;

        public GameplayRespawnData(int actorId, int remainingMilliseconds)
        {
            ActorId = actorId;
            RemainingMilliseconds = remainingMilliseconds;
        }
    }
}
