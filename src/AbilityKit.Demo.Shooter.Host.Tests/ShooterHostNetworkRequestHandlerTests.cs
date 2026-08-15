using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Network;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Demo.Shooter.Host;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Network.Protocol;
using AbilityKit.Protocol.Shooter;
using Xunit;

namespace AbilityKit.Demo.Shooter.Host.Tests;

public sealed class ShooterHostNetworkRequestHandlerTests
{
    [Fact]
    public async Task BoundSession_RoutesInputAndOverridesPayloadPlayerIdentity()
    {
        const string worldId = "shooter-host-test";
        var worldHost = new ShooterWorldHost();
        var world = worldHost.CreateBattleWorld(worldId);
        Assert.True(world.Services.TryResolve<IShooterBattleRuntimePort>(out var runtime));
        Assert.NotNull(runtime);
        var start = CreateStartPayload();
        Assert.True(runtime!.StartGame(in start));

        using var network = new InProcessHostNetwork(
            UnsupportedHostMessageCodec.Instance,
            new ShooterHostNetworkRequestHandler());
        network.Connections.Attach(worldHost.HostRuntime);
        network.Connections.OnClientConnected += connection =>
        {
            var serverConnection = Assert.IsType<HostNetworkServerConnection>(connection);
            var binding = new ShooterHostSessionBinding(worldId, 1);
            ShooterHostSessionBindings.Bind(serverConnection.Session, in binding);
        };
        network.Start();

        using var client = network.CreateClientConnection();
        var responseTask = CaptureResponse(client, sequence: 11);
        client.Open("inprocess", 1);
        var spoofed = new ShooterPlayerCommand(999, 1f, 0f, 1f, 0f, false);
        var request = new ShooterHostInputRequest(worldId, runtime.CurrentFrame, new[] { spoofed });
        client.Send(
            (uint)ShooterOpCodes.Input.PlayerCommand,
            ShooterHostInputCodec.Serialize(in request),
            (ushort)NetworkPacketFlags.Request,
            11);

        var response = await responseTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(response.Accepted);
        Assert.Equal(1, response.AcceptedCommandCount);

        worldHost.Tick(1f / 30f);
        Assert.True(runtime.TryGetPlayer(1, out var localPlayer));
        Assert.True(localPlayer.X > -2f);
        Assert.True(runtime.TryGetPlayer(2, out var otherPlayer));
        Assert.Equal(2f, otherPlayer.X);

        worldHost.DestroyBattleWorld(worldId);
    }

    [Fact]
    public async Task UnboundSession_IsRejectedBeforeWorldLookup()
    {
        var worldHost = new ShooterWorldHost();
        using var network = new InProcessHostNetwork(
            UnsupportedHostMessageCodec.Instance,
            new ShooterHostNetworkRequestHandler());
        network.Connections.Attach(worldHost.HostRuntime);
        network.Start();

        using var client = network.CreateClientConnection();
        var responseTask = CaptureResponse(client, sequence: 12);
        client.Open("inprocess", 1);
        var command = new ShooterPlayerCommand(1, 0f, 0f, 1f, 0f, false);
        var request = new ShooterHostInputRequest("missing", 0, new[] { command });
        client.Send(
            (uint)ShooterOpCodes.Input.PlayerCommand,
            ShooterHostInputCodec.Serialize(in request),
            (ushort)NetworkPacketFlags.Request,
            12);

        var response = await responseTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(response.Accepted);
        Assert.Equal((int)ShooterHostInputReasonCode.SessionNotBound, response.ReasonCode);
    }

    [Fact]
    public async Task MultipleCommands_AreRejectedWithoutSubmittingInput()
    {
        const string worldId = "shooter-host-multiple-command-test";
        var worldHost = new ShooterWorldHost();
        var world = worldHost.CreateBattleWorld(worldId);
        Assert.True(world.Services.TryResolve<IShooterBattleRuntimePort>(out var runtime));
        Assert.NotNull(runtime);
        var start = CreateStartPayload();
        Assert.True(runtime!.StartGame(in start));

        using var network = new InProcessHostNetwork(
            UnsupportedHostMessageCodec.Instance,
            new ShooterHostNetworkRequestHandler());
        network.Connections.Attach(worldHost.HostRuntime);
        network.Connections.OnClientConnected += connection =>
        {
            var serverConnection = Assert.IsType<HostNetworkServerConnection>(connection);
            var binding = new ShooterHostSessionBinding(worldId, 1);
            ShooterHostSessionBindings.Bind(serverConnection.Session, in binding);
        };
        network.Start();

        using var client = network.CreateClientConnection();
        var responseTask = CaptureResponse(client, sequence: 13);
        client.Open("inprocess", 1);
        var command = new ShooterPlayerCommand(1, 1f, 0f, 1f, 0f, false);
        var request = new ShooterHostInputRequest(
            worldId,
            runtime.CurrentFrame,
            new[] { command, command });
        client.Send(
            (uint)ShooterOpCodes.Input.PlayerCommand,
            ShooterHostInputCodec.Serialize(in request),
            (ushort)NetworkPacketFlags.Request,
            13);

        var response = await responseTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(response.Accepted);
        Assert.Equal(0, response.AcceptedCommandCount);
        Assert.Equal((int)ShooterHostInputReasonCode.InvalidPayload, response.ReasonCode);

        worldHost.Tick(1f / 30f);
        Assert.True(runtime.TryGetPlayer(1, out var localPlayer));
        Assert.Equal(-2f, localPlayer.X);

        worldHost.DestroyBattleWorld(worldId);
    }

    private static Task<ShooterHostInputResponse> CaptureResponse(
        AbilityKit.Network.Abstractions.IConnection client,
        uint sequence)
    {
        var completion = new TaskCompletionSource<ShooterHostInputResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        client.PacketReceived += (opCode, receivedSequence, payload) =>
        {
            if (opCode == (uint)ShooterOpCodes.Input.PlayerCommand && receivedSequence == sequence)
            {
                completion.TrySetResult(ShooterHostInputCodec.DeserializeResponse(payload));
            }
        };
        return completion.Task;
    }

    private static ShooterStartGamePayload CreateStartPayload()
    {
        return new ShooterStartGamePayload(
            "host-test",
            30,
            123,
            new[]
            {
                new ShooterStartPlayer(1, "P1", -2f, 0f),
                new ShooterStartPlayer(2, "P2", 2f, 0f),
            });
    }
}
