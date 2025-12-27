namespace AbilityKit.Ability.World.Services
{
    public interface IWorldClock
    {
        float DeltaTime { get; }
        float Time { get; }
        void Tick(float deltaTime);
    }
}
