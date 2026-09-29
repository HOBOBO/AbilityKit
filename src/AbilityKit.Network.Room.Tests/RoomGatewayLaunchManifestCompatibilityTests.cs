using AbilityKit.Network.Room;
using Xunit;

namespace AbilityKit.Network.Room.Tests;

public sealed class RoomGatewayLaunchManifestCompatibilityTests
{
    [Fact]
    public void MatchingManifestAcceptsAndChangedRulesReject()
    {
        var metadata = new Dictionary<string, string> { ["players"] = "2" };
        var references = new[] { "tiny:arena", "tiny:rules.v1" };
        var room = new RoomGatewaySnapshot
        {
            LaunchManifestVersion = 1,
            LaunchManifestHash = RoomGatewayLaunchManifestCompatibility.ComputeHash(references, metadata)
        };

        RoomGatewayLaunchManifestCompatibility.Require(room, 1, references, metadata);
        Assert.Throws<InvalidOperationException>(() =>
            RoomGatewayLaunchManifestCompatibility.Require(room, 1,
                new[] { "tiny:arena", "tiny:rules.v2" }, metadata));
        Assert.Throws<InvalidOperationException>(() =>
            RoomGatewayLaunchManifestCompatibility.Require(room, 2, references, metadata));
    }

    [Fact]
    public void HashMatchesServerCanonicalOrdering()
    {
        var hash = RoomGatewayLaunchManifestCompatibility.ComputeHash(
            new[] { "tiny:rules.v1", "tiny:arena" },
            new Dictionary<string, string> { ["players"] = "2" });
        Assert.Equal("24b96ec404278d709fd9d5cf4c13a041c8176513f5d28643510dbc221e1ead6f", hash);
    }
}
