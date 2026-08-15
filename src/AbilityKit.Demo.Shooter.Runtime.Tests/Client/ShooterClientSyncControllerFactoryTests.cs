using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Network.Sdk;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

[Collection(ShooterClientSyncControllerFactoryTestCollection.Name)]
public sealed class ShooterClientSyncControllerFactoryTests
{
    [Theory]
    [InlineData(NetworkSyncModel.Unspecified)]
    [InlineData(NetworkSyncModel.PredictRollback)]
    public void DefaultRegistryCreatesPredictRollbackController(NetworkSyncModel syncModel)
    {
        var controller = ShooterClientSyncControllerFactory.Create(
            syncModel,
            new ShooterBattleRuntimePort(),
            new ShooterPresentationFacade(),
            tickRate: 30,
            decoder: null,
            gateway: null);

        Assert.IsType<ShooterClientPredictRollbackSyncController>(controller);
        Assert.Equal(NetworkSyncModel.PredictRollback, controller.SyncModel);
    }

    [Fact]
    public void DefaultRegistryCreatesHybridHeroPredictionController()
    {
        var controller = ShooterClientSyncControllerFactory.Create(
            NetworkSyncModel.HybridHeroPrediction,
            new ShooterBattleRuntimePort(),
            new ShooterPresentationFacade(),
            tickRate: 30,
            decoder: null,
            gateway: null);

        Assert.IsType<ShooterClientHybridHeroPredictionSyncController>(controller);
        Assert.Equal(NetworkSyncModel.HybridHeroPrediction, controller.SyncModel);
        Assert.Equal(NetworkSyncModel.HybridHeroPrediction, ((AbilityKit.Network.Runtime.Sync.IClientSyncStrategy<AbilityKit.Protocol.Shooter.ShooterPlayerCommand, ShooterRemoteSnapshotSample>)controller).SyncModel);
    }

    [Fact]
    public void DefaultRegistryCreatesControllerFromSyncProfile()
    {
        var controller = ShooterClientSyncControllerFactory.Create(
            NetworkSyncProfiles.HybridHeroPrediction,
            new ShooterBattleRuntimePort(),
            new ShooterPresentationFacade(),
            tickRate: 30,
            decoder: null,
            gateway: null);

        Assert.IsType<ShooterClientHybridHeroPredictionSyncController>(controller);
        Assert.Equal(NetworkSyncModel.HybridHeroPrediction, controller.SyncModel);
    }

    [Theory]
    [InlineData(NetworkSyncModel.AuthoritativeInterpolation)]
    [InlineData(NetworkSyncModel.BatchStateSync)]
    [InlineData(NetworkSyncModel.MassBattleLodSync)]
    public void DefaultRegistryCreatesAuthoritativeInterpolationBasedControllers(NetworkSyncModel syncModel)
    {
        var config = new InterpolationConfig(
            ticksPerSecond: 1000L,
            interpolationDelayTicks: 250L,
            bufferCapacity: 8,
            catchUpRate: 0d);

        var controller = ShooterClientSyncControllerFactory.Create(
            syncModel,
            new ShooterBattleRuntimePort(),
            new ShooterPresentationFacade(),
            tickRate: 30,
            decoder: null,
            gateway: null,
            interpolationConfig: config);

        Assert.IsType<ShooterClientAuthoritativeInterpolationSyncController>(controller);
        Assert.Equal(syncModel, controller.SyncModel);
    }

    [Fact]
    public void DefaultRegistryRejectsUnregisteredSyncModel()
    {
        var exception = Assert.Throws<NetworkSyncSessionBuildException>(() =>
            ShooterClientSyncControllerFactory.Create(
                NetworkSyncModel.Lockstep,
                new ShooterBattleRuntimePort(),
                new ShooterPresentationFacade(),
                tickRate: 30,
                decoder: null,
                gateway: null));

        Assert.Equal(NetworkSyncSessionBuildFailureReason.MissingControllerRegistration, exception.Reason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RegisterOverridesProfileBuilderUntilReset(bool useLegacyModelRegistration)
    {
        try
        {
            var builderCalled = false;
            var lockstepProfile = NetworkSyncProfiles.Lockstep;
            ShooterClientSyncControllerFactory.ShooterClientSyncControllerBuilder builder =
                (in ShooterClientSyncControllerFactoryContext context) =>
                {
                    builderCalled = true;
                    Assert.Equal(lockstepProfile, context.SyncProfile);
                    Assert.Equal(NetworkSyncModel.Lockstep, context.SyncModel);
                    Assert.Equal(45, context.TickRate);
                    Assert.NotNull(context.Runtime);
                    Assert.NotNull(context.Presentation);
                    return new ShooterClientPredictRollbackSyncController(
                        context.Runtime,
                        context.Presentation,
                        context.TickRate,
                        context.Decoder,
                        context.Gateway);
                };

            if (useLegacyModelRegistration)
            {
                ShooterClientSyncControllerFactory.Register(NetworkSyncModel.Lockstep, builder);
            }
            else
            {
                ShooterClientSyncControllerFactory.Register(lockstepProfile, builder);
            }

            var controller = ShooterClientSyncControllerFactory.Create(
                lockstepProfile,
                new ShooterBattleRuntimePort(),
                new ShooterPresentationFacade(),
                tickRate: 45,
                decoder: null,
                gateway: null);

            Assert.True(builderCalled);
            Assert.IsType<ShooterClientPredictRollbackSyncController>(controller);
        }
        finally
        {
            ShooterClientSyncControllerFactory.ResetToDefaults();
        }

        Assert.Throws<NetworkSyncSessionBuildException>(() => ShooterClientSyncControllerFactory.Create(
            NetworkSyncProfiles.Lockstep,
            new ShooterBattleRuntimePort(),
            new ShooterPresentationFacade(),
            tickRate: 30,
            decoder: null,
            gateway: null));
    }

    [Fact]
    public void CreateSessionRejectsInsufficientCapabilitiesBeforeControllerConstruction()
    {
        var options = ShooterClientSyncAssemblyOptions.Default;
        var insufficient = new NetworkSyncCapabilities(
            options.MinimumSchemaVersion,
            options.MaximumSchemaVersion,
            ClientPlaybackCapabilities.PredictRollback,
            InputPolicy.ImmediateSubmit,
            SnapshotPolicy.FullSnapshot,
            InterestPolicy.AllEntities,
            RecoveryPolicy.RequestFullSnapshot,
            ServerValidationPolicy.AuthoritativeOnly);
        options = options.WithAvailableCapabilities(in insufficient);

        var exception = Assert.Throws<NetworkSyncConfigurationException>(() =>
            ShooterClientSyncControllerFactory.CreateSession(
                in options,
                new ShooterBattleRuntimePort(),
                new ShooterPresentationFacade(),
                tickRate: 30,
                gateway: null));

        Assert.Contains(exception.Report.Issues,
            issue => issue.Code == NetworkSyncConfigurationIssueCode.MissingInputCapabilities);
    }

    [Fact]
    public void CreateSessionExposesNegotiatedProfileAndSchemaVersion()
    {
        var options = ShooterClientSyncAssemblyOptions.Default;

        var result = ShooterClientSyncControllerFactory.CreateSession(
            in options,
            new ShooterBattleRuntimePort(),
            new ShooterPresentationFacade(),
            tickRate: 30,
            gateway: null);

        Assert.Equal(options.ProfileName, result.Descriptor.ProfileName);
        Assert.Equal(options.SyncProfile, result.Descriptor.Profile);
        Assert.Equal(options.MinimumSchemaVersion, result.Descriptor.MinimumSchemaVersion);
        Assert.Equal(options.MaximumSchemaVersion, result.Descriptor.MaximumSchemaVersion);
        Assert.False(result.Descriptor.IsRemoteNegotiated);
        Assert.Equal(NetworkSyncRemoteCapabilityPolicy.Ignore, result.Descriptor.RemoteCapabilityPolicy);
    }

    [Fact]
    public void CreateSessionCanRequireRemoteCapabilityDeclaration()
    {
        var options = ShooterClientSyncAssemblyOptions.Default.WithRemoteCapabilities(
            remoteCapabilities: null,
            NetworkSyncRemoteCapabilityPolicy.Require);

        var exception = Assert.Throws<NetworkSyncSessionBuildException>(() =>
            ShooterClientSyncControllerFactory.CreateSession(
                in options,
                new ShooterBattleRuntimePort(),
                new ShooterPresentationFacade(),
                tickRate: 30,
                gateway: null));

        Assert.Equal(NetworkSyncSessionBuildFailureReason.MissingRemoteCapabilities, exception.Reason);
    }

    [Fact]
    public void CreateSessionExposesRemoteNegotiationWhenGatewayCapabilitiesAreProvided()
    {
        var options = ShooterClientSyncAssemblyOptions.Default;
        var remote = NetworkSyncCapabilities.FromProfile(
            options.SyncProfile,
            options.MinimumSchemaVersion,
            options.MaximumSchemaVersion);
        options = options.WithRemoteCapabilities(remote, NetworkSyncRemoteCapabilityPolicy.Require);

        var result = ShooterClientSyncControllerFactory.CreateSession(
            in options,
            new ShooterBattleRuntimePort(),
            new ShooterPresentationFacade(),
            tickRate: 30,
            gateway: null);

        Assert.True(result.Descriptor.IsRemoteNegotiated);
        Assert.True(result.Descriptor.RemoteCapabilities.HasValue);
        Assert.True(result.Descriptor.Negotiation.IsCompatible);
    }
}
