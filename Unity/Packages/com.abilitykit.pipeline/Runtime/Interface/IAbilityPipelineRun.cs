namespace AbilityKit.Pipeline
{
    public interface IAbilityPipelineRun<TCtx>
    {
        EAbilityPipelineState State { get; }

        TCtx Context { get; }

        AbilityPipelinePhaseId CurrentPhaseId { get; }

        bool IsPaused { get; }

        void Tick(float deltaTime);

        void Pause();

        void Resume();

        void Interrupt();

        void Cancel();
    }
}
