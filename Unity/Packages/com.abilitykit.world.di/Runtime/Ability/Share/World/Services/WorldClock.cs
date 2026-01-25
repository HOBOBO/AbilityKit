namespace AbilityKit.Ability.World.Services
{
    public sealed class WorldClock : IWorldClock
    {
        public float DeltaTime { get; private set; }
        public float Time { get; private set; }

        public void Tick(float deltaTime)
        {
            DeltaTime = deltaTime;
            Time += deltaTime;
        }
    }
}
