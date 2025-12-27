namespace AbilityKit.Ability.FrameSync
{
    public interface IFrameDriver
    {
        FrameIndex Frame { get; }
        void Step(float deltaTime);
    }
}
