using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Demo.Shooter.View.Hosting;
using AbilityKit.Demo.Shooter.View.PlayMode;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Sdk;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

public sealed class ShooterClientSyncAssemblyOptionsTests
{
    [Theory]
    [InlineData(NetworkSyncModel.AuthoritativeInterpolation)]
    [InlineData(NetworkSyncModel.BatchStateSync)]
    [InlineData(NetworkSyncModel.MassBattleLodSync)]
    public void AuthoritativeProfilesDefaultToLightweightPredictionBuffers(NetworkSyncModel syncModel)
    {
        var options = ShooterClientSyncAssemblyOptions.ForModel(syncModel);

        Assert.Same(ShooterClientPredictionBufferOptions.Disabled, options.PredictionBufferOptions);
    }

    [Theory]
    [InlineData(NetworkSyncModel.PredictRollback)]
    [InlineData(NetworkSyncModel.HybridHeroPrediction)]
    public void PredictiveProfilesRetainFullPredictionBuffers(NetworkSyncModel syncModel)
    {
        var options = ShooterClientSyncAssemblyOptions.ForModel(syncModel);

        Assert.Same(ShooterClientPredictionBufferOptions.Default, options.PredictionBufferOptions);
    }

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
        var predictionBufferOptions = new ShooterClientPredictionBufferOptions(
            ShooterClientPredictionBufferFeatures.RollbackSnapshots,
            inputHistoryCapacity: 0,
            rollbackSnapshotCapacity: 32,
            stateHashHistoryCapacity: 0);
        var options = ShooterClientSyncAssemblyOptions.Default
            .WithReliableEventCheckpointStore(store)
            .WithReliableEventCheckpointLifecycleOptions(lifecycleOptions)
            .WithPredictionBufferOptions(predictionBufferOptions)
            .WithInterpolationConfig(null)
            .WithDecoder(null)
            .WithSyncModel(NetworkSyncModel.PredictRollback)
            .WithRemoteCapabilities(null, NetworkSyncRemoteCapabilityPolicy.Ignore);

        Assert.Same(store, options.ReliableEventCheckpointStore);
        Assert.Same(lifecycleOptions, options.ReliableEventCheckpointLifecycleOptions);
        Assert.Same(predictionBufferOptions, options.PredictionBufferOptions);

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
            options.WithReliableEventCheckpointStore(store),
            options.WithPredictionBufferOptions(predictionBufferOptions)
        };

        Assert.All(derivedOptions, derived =>
        {
            Assert.Same(store, derived.ReliableEventCheckpointStore);
            Assert.Same(lifecycleOptions, derived.ReliableEventCheckpointLifecycleOptions);
            Assert.Same(predictionBufferOptions, derived.PredictionBufferOptions);
        });
    }

    [Fact]
    public void SessionFactoryForwardsPredictionBufferOptionsToFrameSyncController()
    {
        var predictionBufferOptions = ShooterClientPredictionBufferOptions.Disabled;
        var assemblyOptions = ShooterClientSyncAssemblyOptions
            .ForModel(NetworkSyncModel.PredictRollback)
            .WithPredictionBufferOptions(predictionBufferOptions);
        var session = new ShooterClientSession(
            new ShooterBattleRuntimePort(),
            ShooterPresentationSessionContext.CreateFromFacade(new ShooterPresentationFacade()),
            tickRate: 30,
            in assemblyOptions,
            gateway: null);

        Assert.True(session.TryGetFrameSync(out var frameSync));
        Assert.NotNull(frameSync);
        Assert.Same(predictionBufferOptions, frameSync!.PredictionBufferOptions);
        Assert.False(frameSync.HasFrameworkInputHistory);
        Assert.False(frameSync.HasRollbackSnapshotHistory);
        Assert.False(frameSync.HasStateHashHistory);
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
