using AbilityKit.Demo.Shooter.View;
using AbilityKit.Demo.Shooter.View.Hosting;
using AbilityKit.Demo.Shooter.View.PlayMode;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Sdk;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

public sealed class ShooterClientSyncAssemblyOptionsTests
{
    [Fact]
    public void DerivedOptionsPreserveReliableEventCheckpointStore()
    {
        var store = new InMemoryReliableEventCheckpointStore();
        var lifecycleOptions = new ReliableEventCheckpointLifecycleOptions
        {
            FailurePolicy = ReliableEventCheckpointFlushFailurePolicy.ThrowAfterPublish,
            SerializeConcurrentFlushes = false,
            TreatReportedStoreFailureAsFlushFailure = false
        };
        var options = ShooterClientSyncAssemblyOptions.Default
            .WithReliableEventCheckpointStore(store)
            .WithReliableEventCheckpointLifecycleOptions(lifecycleOptions)
            .WithInterpolationConfig(null)
            .WithDecoder(null)
            .WithSyncModel(NetworkSyncModel.PredictRollback)
            .WithRemoteCapabilities(null, NetworkSyncRemoteCapabilityPolicy.Ignore);

        Assert.Same(store, options.ReliableEventCheckpointStore);
        Assert.Same(lifecycleOptions, options.ReliableEventCheckpointLifecycleOptions);

        var profile = options.SyncProfile;
        var capabilities = options.AvailableCapabilities;
        var derivedOptions = new[]
        {
            options.WithDecoder(null),
            options.WithInterpolationConfig(null),
            options.WithSyncModel(NetworkSyncModel.AuthoritativeInterpolation),
            options.WithSyncProfile(in profile),
            options.WithAvailableCapabilities(in capabilities),
            options.WithSchemaVersionRange(options.MinimumSchemaVersion, options.MaximumSchemaVersion),
            options.WithProfileCatalog(options.ProfileName, options.ProfileCatalog),
            options.WithRemoteCapabilities(null, NetworkSyncRemoteCapabilityPolicy.Ignore),
            options.WithReliableEventCheckpointStore(store)
        };

        Assert.All(derivedOptions, derived =>
        {
            Assert.Same(store, derived.ReliableEventCheckpointStore);
            Assert.Same(lifecycleOptions, derived.ReliableEventCheckpointLifecycleOptions);
        });
    }

    [Fact]
    public void RemoteLaunchOptionsForwardReliableEventCheckpointStore()
    {
        var store = new InMemoryReliableEventCheckpointStore();
        var lifecycleOptions = new ReliableEventCheckpointLifecycleOptions();
        var options = new ShooterRemoteStateSyncLaunchOptions(
            ShooterPlayModeSessionOptions.Default,
            new ShooterClientNetworkEndpoint("127.0.0.1", 41001),
            reliableEventCheckpointStore: store,
            reliableEventCheckpointLifecycleOptions: lifecycleOptions);

        var assemblyOptions = options.CreateClientSyncAssemblyOptions();

        Assert.Same(store, options.ReliableEventCheckpointStore);
        Assert.Same(store, assemblyOptions.ReliableEventCheckpointStore);
        Assert.Same(lifecycleOptions, options.ReliableEventCheckpointLifecycleOptions);
        Assert.Same(lifecycleOptions, assemblyOptions.ReliableEventCheckpointLifecycleOptions);
    }
}
