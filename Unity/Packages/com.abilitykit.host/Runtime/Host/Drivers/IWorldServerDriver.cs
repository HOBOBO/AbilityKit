using AbilityKit.Ability.FrameSync;

namespace AbilityKit.Ability.Host.Drivers
{
    public interface IWorldServerDriver
    {
        FrameIndex Frame { get; }

        void Tick(float deltaTime);
    }
}
