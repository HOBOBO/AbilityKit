using AbilityKit.Network.Room;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Network.Sdk;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Network.Room.Tests;

public sealed class RoomGatewayNetworkSyncSessionBindingTests
{
    [Fact]
    public void DefaultBinding_RepresentsUninitializedState()
    {
        var binding = default(RoomGatewayNetworkSyncSessionBinding);

        Assert.Equal(RoomGatewayNetworkSyncBindingState.Uninitialized, binding.State);
        Assert.False(binding.UsesRemoteCapabilities);
        Assert.Null(binding.Declaration);
        Assert.Null(binding.RemoteCapabilities);
    }

    [Fact]
    public void Create_WithoutDeclaration_UsesLegacyFallback()
    {
        var binding = RoomGatewayNetworkSyncSessionBinding.Create(null, "profile");

        Assert.Equal(RoomGatewayNetworkSyncBindingState.LegacyFallback, binding.State);
        Assert.False(binding.UsesRemoteCapabilities);
        Assert.Null(binding.RemoteCapabilities);
        Assert.Equal(NetworkSyncRemoteCapabilityPolicy.NegotiateWhenAvailable, binding.Policy);
    }

    [Fact]
    public void Create_WithDeclaration_ValidatesProfileAndUsesRemoteCapabilities()
    {
        var declaration = CreateDeclaration("profile");

        var binding = RoomGatewayNetworkSyncSessionBinding.Create(declaration, "PROFILE");

        Assert.Equal(RoomGatewayNetworkSyncBindingState.RemoteDeclared, binding.State);
        Assert.True(binding.UsesRemoteCapabilities);
        Assert.Same(declaration, binding.Declaration);
        Assert.Equal(declaration.Capabilities.MinimumSchemaVersion, binding.RemoteCapabilities!.Value.MinimumSchemaVersion);
    }

    [Fact]
    public void Create_Ignore_DoesNotRequireProfileOrExposeRemoteCapabilities()
    {
        var binding = RoomGatewayNetworkSyncSessionBinding.Create(
            CreateDeclaration("server-profile"),
            expectedProfileName: string.Empty,
            NetworkSyncRemoteCapabilityPolicy.Ignore);

        Assert.Equal(RoomGatewayNetworkSyncBindingState.Ignored, binding.State);
        Assert.Null(binding.RemoteCapabilities);
    }

    [Fact]
    public void Create_RequireWithoutDeclaration_PreservesStrictPolicyForBuilder()
    {
        var binding = RoomGatewayNetworkSyncSessionBinding.Create(
            null,
            "profile",
            NetworkSyncRemoteCapabilityPolicy.Require);
        var options = new NetworkSyncSessionOptions();

        binding.ApplyTo(options);

        Assert.Equal(RoomGatewayNetworkSyncBindingState.MissingRequired, binding.State);
        Assert.Null(options.RemoteCapabilities);
        Assert.Equal(NetworkSyncRemoteCapabilityPolicy.Require, options.RemoteCapabilityPolicy);
    }

    [Fact]
    public void Create_ProfileMismatch_UsesStructuredRoomError()
    {
        var error = Assert.Throws<RoomGatewaySyncCapabilityException>(() =>
            RoomGatewayNetworkSyncSessionBinding.Create(
                CreateDeclaration("server-profile"),
                "client-profile"));

        Assert.Equal(RoomGatewaySyncCapabilityErrorCode.ProfileMismatch, error.ErrorCode);
    }

    [Fact]
    public void ApplyTo_CopiesRemoteCapabilitiesAndPolicy()
    {
        var binding = RoomGatewayNetworkSyncSessionBinding.Create(
            CreateDeclaration("profile"),
            "profile");
        var options = new NetworkSyncSessionOptions
        {
            RemoteCapabilityPolicy = NetworkSyncRemoteCapabilityPolicy.Ignore
        };

        binding.ApplyTo(options);

        Assert.True(options.RemoteCapabilities.HasValue);
        Assert.Equal(NetworkSyncRemoteCapabilityPolicy.NegotiateWhenAvailable, options.RemoteCapabilityPolicy);
    }

    private static RoomGatewayNetworkSyncCapabilities CreateDeclaration(string profileName)
    {
        return RoomGatewayNetworkSyncCapabilitiesConverter.FromWire(new WireNetworkSyncCapabilities
        {
            MetadataVersion = 1,
            ProfileName = profileName,
            MinimumSchemaVersion = 0,
            MaximumSchemaVersion = 1,
            ClientPlayback = (int)ClientPlaybackCapabilities.AuthoritativeInterpolation,
            Input = (int)InputPolicy.ImmediateSubmit,
            Snapshot = (int)(SnapshotPolicy.FullSnapshot | SnapshotPolicy.EventStream),
            Interest = (int)InterestPolicy.AllEntities,
            Recovery = (int)RecoveryPolicy.RequestFullSnapshot,
            ServerValidation = (int)ServerValidationPolicy.AuthoritativeOnly,
            ReliableEvent = (int)(ReliableEventCapabilities.OrderedDelivery |
                ReliableEventCapabilities.AutomaticAcknowledgement |
                ReliableEventCapabilities.PersistentCheckpoint |
                ReliableEventCapabilities.AuthoritativeBaselineRecovery)
        })!;
    }
}
