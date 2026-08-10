using System;
using System.Threading.Tasks;
using AbilityKit.Network.Room;
using Xunit;

namespace AbilityKit.Network.Room.Tests;

/// <summary>
/// Argument-validation contract for <see cref="GatewayMultiplayerSession.CreateAsync"/>.
/// These throw synchronously before any connection is opened, so no server harness is needed.
/// (The full connect→login→create→ready→start→subscribe flow needs an in-process gateway fixture;
/// see the integration guide's WIP section — adoption is gated on the room-flow staged-restore WIP.)
/// </summary>
public sealed class GatewayMultiplayerSessionTests
{
    private const string Host = "127.0.0.1";
    private const int Port = 1;
    private const string Account = "player-1";
    private static readonly RoomGatewayLaunchSpec Spec =
        new("region", "server", "test", "title", 2, 1, 1, 1, 1, "test-world", "client-1");

    [Fact]
    public Task CreateAsync_NullOrWhitespaceHost_Throws()
    {
        return Assert.ThrowsAsync<ArgumentException>(() =>
            GatewayMultiplayerSession.CreateAsync(null!, Port, Account, Spec));
    }

    [Fact]
    public Task CreateAsync_NonPositivePort_Throws()
    {
        return Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            GatewayMultiplayerSession.CreateAsync(Host, 0, Account, Spec));
    }

    [Fact]
    public Task CreateAsync_NullOrWhitespaceAccountId_Throws()
    {
        return Assert.ThrowsAsync<ArgumentException>(() =>
            GatewayMultiplayerSession.CreateAsync(Host, Port, "  ", Spec));
    }
}
