using UnityHFSM;
using UnityHFSM.Inspection;
using Xunit;

namespace AbilityKit.HFSM.Core.Tests;

public sealed class RuntimeInspectionHubTests
{
    [Fact]
    public void InstalledBackendReceivesLifecycleOperations()
    {
        var backend = new RecordingBackend();
        var runtime = new object();

        using (RuntimeInspectionHub.InstallBackend(backend))
        {
            RuntimeInspectionHub.AutoRegister(runtime);
            RuntimeInspectionHub.Unregister(runtime);
        }

        Assert.Same(runtime, backend.RegisteredRuntime);
        Assert.Same(runtime, backend.UnregisteredRuntime);
        Assert.Null(RuntimeInspectionHub.Backend);
    }

    [Fact]
    public void DisposingOldInstallationDoesNotRemoveNewBackend()
    {
        var first = new RecordingBackend();
        var second = new RecordingBackend();
        var firstInstallation = RuntimeInspectionHub.InstallBackend(first);
        var secondInstallation = RuntimeInspectionHub.InstallBackend(second);
        try
        {
            firstInstallation.Dispose();

            Assert.Same(second, RuntimeInspectionHub.Backend);
        }
        finally
        {
            secondInstallation.Dispose();
            firstInstallation.Dispose();
        }
    }

    [Fact]
    public void RootStateMachineLifecycleUsesInstalledBackend()
    {
        var backend = new RecordingBackend();
        var machine = new StateMachine { RegisterForInspection = true };
        machine.AddState("idle", new State());
        machine.SetStartState("idle");

        using (RuntimeInspectionHub.InstallBackend(backend))
        {
            machine.Init();
            machine.OnExit();
        }

        Assert.Same(machine, backend.RegisteredRuntime);
        Assert.Same(machine, backend.UnregisteredRuntime);
    }

    private sealed class RecordingBackend : IRuntimeInspectionRegistryBackend
    {
        public object? RegisteredRuntime { get; private set; }
        public object? UnregisteredRuntime { get; private set; }

        public void AutoRegister(object runtime) => RegisteredRuntime = runtime;
        public void Unregister(object runtime) => UnregisteredRuntime = runtime;
    }
}
