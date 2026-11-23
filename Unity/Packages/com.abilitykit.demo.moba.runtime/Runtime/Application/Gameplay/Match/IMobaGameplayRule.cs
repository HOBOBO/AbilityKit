namespace AbilityKit.Demo.Moba.Gameplay
{
    public interface IMobaGameplayRule
    {
        string RuleId { get; }

        void Start(MobaGameplayService gameplay);

        void Tick(float deltaTime);

        void Stop();
    }
}
