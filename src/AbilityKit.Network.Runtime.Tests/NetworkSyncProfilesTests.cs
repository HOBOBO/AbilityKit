using System;
using AbilityKit.Network.Runtime.Sync;
using Xunit;

namespace AbilityKit.Network.Runtime.Tests;

public sealed class NetworkSyncProfilesTests
{
    [Fact]
    public void FromCompatibilityModel_MapsEveryKnownModelToProfileWithSameCompatibilityModel()
    {
        foreach (var model in Enum.GetValues<NetworkSyncModel>())
        {
            var profile = NetworkSyncProfiles.FromCompatibilityModel(model);

            Assert.Equal(model, profile.CompatibilityModel);
        }
    }

    [Fact]
    public void PredictRollback_ProfileKeepsClientPredictionAndAuthoritativeCorrectionPolicies()
    {
        var profile = NetworkSyncProfiles.FromCompatibilityModel(NetworkSyncModel.PredictRollback);

        Assert.Equal(NetworkSyncProfiles.PredictRollback, profile);
        Assert.Equal(ClientPlaybackPolicy.PredictRollback, profile.ClientPlayback);
        Assert.True(profile.Input.HasFlag(InputPolicy.ImmediateSubmit));
        Assert.True(profile.Input.HasFlag(InputPolicy.ServerRemapAcceptedFrame));
        Assert.True(profile.Snapshot.HasFlag(SnapshotPolicy.FullSnapshot));
        Assert.True(profile.Snapshot.HasFlag(SnapshotPolicy.AuthorityOverride));
        Assert.Equal(InterestPolicy.AllEntities, profile.Interest);
        Assert.True(profile.Recovery.HasFlag(RecoveryPolicy.CatchUpToServerFrame));
        Assert.True(profile.Recovery.HasFlag(RecoveryPolicy.RequestFullSnapshot));
        Assert.Equal(ServerValidationPolicy.AuthoritativeOnly, profile.ServerValidation);
    }

    [Fact]
    public void AuthoritativeInterpolation_ProfileKeepsStateStreamAndRecoveryPolicies()
    {
        var profile = NetworkSyncProfiles.FromCompatibilityModel(NetworkSyncModel.AuthoritativeInterpolation);

        Assert.Equal(NetworkSyncProfiles.AuthoritativeInterpolation, profile);
        Assert.Equal(ClientPlaybackPolicy.AuthoritativeInterpolation, profile.ClientPlayback);
        Assert.Equal(InputPolicy.NoClientInput, profile.Input);
        Assert.True(profile.Snapshot.HasFlag(SnapshotPolicy.FixedRateStateStream));
        Assert.Equal(InterestPolicy.AllEntities, profile.Interest);
        Assert.True(profile.Recovery.HasFlag(RecoveryPolicy.RequestKeyFrame));
        Assert.True(profile.Recovery.HasFlag(RecoveryPolicy.RequestFullSnapshot));
        Assert.Equal(ServerValidationPolicy.AuthoritativeOnly, profile.ServerValidation);
    }

    [Fact]
    public void ServerRewindLagCompensation_ProfileKeepsServerValidationPolicy()
    {
        var profile = NetworkSyncProfiles.FromCompatibilityModel(NetworkSyncModel.ServerRewindLagCompensation);

        Assert.Equal(NetworkSyncProfiles.ServerRewindLagCompensation, profile);
        Assert.Equal(ClientPlaybackPolicy.None, profile.ClientPlayback);
        Assert.True(profile.Input.HasFlag(InputPolicy.ImmediateSubmit));
        Assert.True(profile.Input.HasFlag(InputPolicy.ServerRemapAcceptedFrame));
        Assert.Equal(SnapshotPolicy.None, profile.Snapshot);
        Assert.Equal(InterestPolicy.None, profile.Interest);
        Assert.Equal(RecoveryPolicy.None, profile.Recovery);
        Assert.Equal(ServerValidationPolicy.LagCompensatedHitValidation, profile.ServerValidation);
    }

    [Fact]
    public void FromCompatibilityModel_ThrowsForUnknownModel()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NetworkSyncProfiles.FromCompatibilityModel((NetworkSyncModel)999));
    }

    [Fact]
    public void NetworkSyncProfile_UsesValueEquality()
    {
        var first = new NetworkSyncProfile(
            NetworkSyncModel.BatchStateSync,
            ClientPlaybackPolicy.AuthoritativeInterpolation,
            InputPolicy.NoClientInput,
            SnapshotPolicy.BatchSnapshot | SnapshotPolicy.KeyFrameSnapshot | SnapshotPolicy.EventStream,
            InterestPolicy.AllEntities,
            RecoveryPolicy.RequestKeyFrame | RecoveryPolicy.RequestFullSnapshot,
            ServerValidationPolicy.AuthoritativeOnly,
            ReliableEventPolicy.OrderedDelivery | ReliableEventPolicy.ExternalAcknowledgement |
            ReliableEventPolicy.PersistentCheckpoint | ReliableEventPolicy.BufferedOutOfOrder |
            ReliableEventPolicy.AuthoritativeBaselineRecovery);
        var second = NetworkSyncProfiles.BatchStateSync;

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.False(first != second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }
}
