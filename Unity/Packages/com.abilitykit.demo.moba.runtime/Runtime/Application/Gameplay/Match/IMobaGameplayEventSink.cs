namespace AbilityKit.Demo.Moba.Gameplay
{
    public interface IMobaGameplayEventSink
    {
        void OnGameplayStarted(int frameIndex);

        void OnGameplayEnded(in MobaGameplayResult result);
    }
}
