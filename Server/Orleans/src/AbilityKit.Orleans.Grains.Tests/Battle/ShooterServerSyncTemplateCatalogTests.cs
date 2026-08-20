using AbilityKit.Ability.Host.Extensions.Server.BattleHost;
using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Orleans.Contracts.Shooter;
using AbilityKit.Orleans.Grains.Gameplays.Shooter.Battle;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed class ShooterServerSyncTemplateCatalogTests
{
    [Fact]
    public void SnapshotPolicy_WhenPublishIntervalIsConfigured_OnlyPublishesScheduledFrames()
    {
        var policy = new BattleSnapshotSyncPolicy(snapshotInterval: 5, fullSnapshotInterval: 20);

        Assert.False(policy.ShouldPublish(frame: 5, observerCount: 0, worldTicked: true));
        Assert.False(policy.ShouldPublish(frame: 5, observerCount: 1, worldTicked: false));
        Assert.False(policy.ShouldPublish(frame: 4, observerCount: 1, worldTicked: true));
        Assert.True(policy.ShouldPublish(frame: 5, observerCount: 1, worldTicked: true));
        Assert.True(policy.ShouldCreateFullSnapshot(20));
        Assert.False(policy.ShouldCreateFullSnapshot(15));
    }

    [Fact]
    public void Catalog_DefaultStateSync_PublishesPackedAuthorityWithPeriodicFullSnapshots()
    {
        var policy = ShooterServerSyncTemplateCatalog.Resolve(null);
        var pushOptions = policy.CreatePushOptions("ideal");
        var snapshotPolicy = new BattleSnapshotSyncPolicy(
            policy.SnapshotIntervalFrames,
            policy.FullSnapshotIntervalFrames);

        Assert.Equal(ShooterServerProtocol.StateSyncAuthorityTemplate, policy.TemplateId);
        Assert.Equal(1, policy.SnapshotIntervalFrames);
        Assert.Equal(30, policy.FullSnapshotIntervalFrames);
        Assert.Equal(ShooterStateSyncPushPayloadMode.Packed, pushOptions.PayloadMode);
        Assert.False(snapshotPolicy.ShouldCreateFullSnapshot(1));
        Assert.True(snapshotPolicy.ShouldCreateFullSnapshot(30));
    }

    [Fact]
    public void Catalog_BatchAndMassBattle_UseDistinctPureStateBudgetsAndCadence()
    {
        var batch = ShooterServerSyncTemplateCatalog.Resolve(ShooterServerProtocol.BatchStateLowFrequencyTemplate);
        var mass = ShooterServerSyncTemplateCatalog.Resolve(ShooterServerProtocol.MassBattleLodAoiTemplate);
        var batchOptions = batch.CreatePushOptions("mobile4g");
        var massOptions = mass.CreatePushOptions("limitedbw");

        Assert.Equal(60, batch.SnapshotIntervalFrames);
        Assert.Equal(300, batch.FullSnapshotIntervalFrames);
        Assert.Equal(ShooterStateSyncPushPayloadMode.PureState, batchOptions.PayloadMode);
        Assert.Equal(1024, batchOptions.ResolvePureStateSettings().ActiveSyncBudget);
        Assert.False(batchOptions.UseObserverAoi);

        Assert.Equal(3, mass.SnapshotIntervalFrames);
        Assert.Equal(450, mass.FullSnapshotIntervalFrames);
        Assert.Equal(ShooterStateSyncPushPayloadMode.PureState, massOptions.PayloadMode);
        Assert.Equal(NetworkConditionProfile.LimitedBandwidth.BandwidthKbps, massOptions.NetworkCondition.BandwidthKbps);
        var massSettings = massOptions.ResolvePureStateSettings();
        Assert.Equal(2048, massSettings.ActiveSyncBudget);
        Assert.Equal(20000, massSettings.MaxEntityCount);
        Assert.Equal(3, massSettings.DeltaIntervalFrames);
        Assert.Equal(3, massSettings.InterpolationDelayFrames);
        Assert.Equal(3, massSettings.NearLodIntervalFrames);
        Assert.Equal(9, massSettings.MidLodIntervalFrames);
        Assert.Equal(30, massSettings.FarLodIntervalFrames);
        Assert.Equal(24f, massOptions.AoiVisibleRadius);
        Assert.Equal(30f, massOptions.AoiBoundaryRadius);
        Assert.True(massOptions.UseObserverAoi);
    }

    [Fact]
    public void SyncProfile_ProjectsShooterCadenceIntoGenericBattleTemplates()
    {
        var profile = ShooterServerSyncTemplateCatalog.CreateSyncProfile();
        var batch = profile.ResolveTemplate(ShooterServerProtocol.BatchStateLowFrequencyTemplate);
        var mass = profile.ResolveTemplate(ShooterServerProtocol.MassBattleLodAoiTemplate);

        Assert.Equal(60, batch.SnapshotIntervalFrames);
        Assert.Equal(300, batch.FullSnapshotIntervalFrames);
        Assert.Equal(3, mass.SnapshotIntervalFrames);
        Assert.Equal(450, mass.FullSnapshotIntervalFrames);
    }
}
