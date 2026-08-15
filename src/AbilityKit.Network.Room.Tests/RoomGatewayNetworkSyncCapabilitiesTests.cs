using AbilityKit.Network.Room;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Network.Room.Tests;

public sealed class RoomGatewayNetworkSyncCapabilitiesTests
{
    [Fact]
    public void FromWire_LegacyMetadata_ReturnsNull()
    {
        Assert.Null(RoomGatewayNetworkSyncCapabilitiesConverter.FromWire(null));
        Assert.Null(RoomGatewayNetworkSyncCapabilitiesConverter.FromWire(
            new WireNetworkSyncCapabilities { MetadataVersion = 0 }));
    }

    [Fact]
    public void FromWire_VersionOne_ConvertsEveryCapability()
    {
        var wire = CreateValidWire("Moba.AuthoritativeRemoteInterpolation");

        var result = RoomGatewayNetworkSyncCapabilitiesConverter.FromWire(wire);

        Assert.NotNull(result);
        Assert.Equal(1, result.MetadataVersion);
        Assert.Equal(wire.ProfileName, result.ProfileName);
        Assert.Equal(wire.MinimumSchemaVersion, result.Capabilities.MinimumSchemaVersion);
        Assert.Equal(wire.MaximumSchemaVersion, result.Capabilities.MaximumSchemaVersion);
        Assert.Equal((ClientPlaybackCapabilities)wire.ClientPlayback, result.Capabilities.ClientPlayback);
        Assert.Equal((InputPolicy)wire.Input, result.Capabilities.Input);
        Assert.Equal((SnapshotPolicy)wire.Snapshot, result.Capabilities.Snapshot);
        Assert.Equal((InterestPolicy)wire.Interest, result.Capabilities.Interest);
        Assert.Equal((RecoveryPolicy)wire.Recovery, result.Capabilities.Recovery);
        Assert.Equal((ServerValidationPolicy)wire.ServerValidation, result.Capabilities.ServerValidation);
    }

    [Fact]
    public void FromWire_UnknownVersion_IsRejectedStructurally()
    {
        var wire = CreateValidWire("profile");
        wire.MetadataVersion = 2;

        var error = Assert.Throws<RoomGatewaySyncCapabilityException>(
            () => RoomGatewayNetworkSyncCapabilitiesConverter.FromWire(wire));

        Assert.Equal(RoomGatewaySyncCapabilityErrorCode.UnknownMetadataVersion, error.ErrorCode);
    }

    [Fact]
    public void FromWire_UnknownPolicyBit_IsRejectedStructurally()
    {
        var wire = CreateValidWire("profile");
        wire.Snapshot |= 1 << 20;

        var error = Assert.Throws<RoomGatewaySyncCapabilityException>(
            () => RoomGatewayNetworkSyncCapabilitiesConverter.FromWire(wire));

        Assert.Equal(RoomGatewaySyncCapabilityErrorCode.InvalidCapabilities, error.ErrorCode);
        Assert.NotNull(error.ValidationReport);
    }

    [Fact]
    public void EnsureProfile_Mismatch_IsRejectedStructurally()
    {
        var capabilities = RoomGatewayNetworkSyncCapabilitiesConverter.FromWire(
            CreateValidWire("server-profile"));

        var error = Assert.Throws<RoomGatewaySyncCapabilityException>(
            () => capabilities!.EnsureProfile("client-profile"));

        Assert.Equal(RoomGatewaySyncCapabilityErrorCode.ProfileMismatch, error.ErrorCode);
    }

    [Fact]
    public void WireRoomSnapshot_RoundTrip_PreservesCapabilities()
    {
        var snapshot = new WireRoomSnapshot
        {
            Summary = new WireRoomSummary { RoomId = "room-capabilities" },
            SyncCapabilities = CreateValidWire("Moba.AuthoritativeRemoteInterpolation")
        };

        var payload = WireRoomGatewayBinary.Serialize(snapshot);
        var restored = WireRoomGatewayBinary.Deserialize<WireRoomSnapshot>(payload);

        Assert.True(restored.SyncCapabilities.HasValue);
        Assert.Equal(1, restored.SyncCapabilities.Value.MetadataVersion);
        Assert.Equal("Moba.AuthoritativeRemoteInterpolation", restored.SyncCapabilities.Value.ProfileName);
        Assert.Equal(
            (int)(ReliableEventCapabilities.OrderedDelivery |
                ReliableEventCapabilities.AutomaticAcknowledgement |
                ReliableEventCapabilities.PersistentCheckpoint |
                ReliableEventCapabilities.AuthoritativeBaselineRecovery),
            restored.SyncCapabilities.Value.ReliableEvent);
    }

    private static WireNetworkSyncCapabilities CreateValidWire(string profileName)
    {
        return new WireNetworkSyncCapabilities
        {
            MetadataVersion = 1,
            ProfileName = profileName,
            MinimumSchemaVersion = 0,
            MaximumSchemaVersion = 1,
            ClientPlayback = (int)ClientPlaybackCapabilities.AuthoritativeInterpolation,
            Input = (int)InputPolicy.ImmediateSubmit,
            Snapshot = (int)(SnapshotPolicy.FullSnapshot | SnapshotPolicy.DeltaSnapshot |
                SnapshotPolicy.FixedRateStateStream | SnapshotPolicy.EventStream),
            Interest = (int)InterestPolicy.AllEntities,
            Recovery = (int)RecoveryPolicy.RequestFullSnapshot,
            ServerValidation = (int)(ServerValidationPolicy.AuthoritativeOnly | ServerValidationPolicy.InputValidation),
            ReliableEvent = (int)(ReliableEventCapabilities.OrderedDelivery |
                ReliableEventCapabilities.AutomaticAcknowledgement |
                ReliableEventCapabilities.PersistentCheckpoint |
                ReliableEventCapabilities.AuthoritativeBaselineRecovery)
        };
    }
}
