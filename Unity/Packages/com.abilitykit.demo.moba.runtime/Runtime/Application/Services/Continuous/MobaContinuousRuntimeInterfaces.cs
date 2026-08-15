using AbilityKit.Continuous;

namespace AbilityKit.Demo.Moba.Services
{
    public interface IMobaTickableContinuous : IContinuous
    {
        void TickManaged(float deltaTimeSeconds);
    }

    public interface IMobaContinuousIntervalState
    {
        float IntervalRemainingSeconds { get; set; }
    }

    public interface IMobaContinuousRuntimeStateSync
    {
        void SyncManagedState();
    }

    public interface IMobaContinuousExecutionContextProvider : IMobaCombatExecutionContextProvider, IMobaContextSourceProvider
    {
    }

    public interface IMobaContinuousIntervalHandler
    {
        bool CanHandle(IContinuous continuous);
        void OnInterval(IContinuous continuous, IMobaContinuousPeriodicConfig periodicConfig, in MobaCombatExecutionContext executionContext);
    }

    public interface IMobaContinuousTickProcessor
    {
        void Tick(IContinuous continuous, float deltaTimeSeconds);
    }
}
