using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Demo.Tiny.Samples;
using AbilityKit.Demo.Tiny.View;
using AbilityKit.Network.Room;

namespace AbilityKit.Demo.Tiny.Client;

public static class TinySessionSmoke
{
    public static Task<int> RunAsync(string host, int port, string prefix, string mode) =>
        RunCoreAsync(host, port, prefix, mode, chapterHash: null, evidencePath: null);

    public static Task<int> RunHybridMismatchAsync(
        string host, int port, string prefix, string evidencePath) =>
        RunCoreAsync(host, port, prefix, "hybrid", chapterHash: null,
            evidencePath: evidencePath);

    public static Task<int> RunChapterAsync(
        string host, int port, string prefix, TinySyncMode mode, uint chapterHash)
    {
        if (mode is not (TinySyncMode.Frame or TinySyncMode.Hybrid))
            throw new ArgumentOutOfRangeException(nameof(mode));
        return RunCoreAsync(host, port, prefix, mode.ToString().ToLowerInvariant(), chapterHash,
            evidencePath: null);
    }

    private static async Task<int> RunCoreAsync(
        string host, int port, string prefix, string mode, uint? chapterHash,
        string? evidencePath)
    {
        if (!Enum.TryParse<TinySyncMode>(mode, true, out var syncMode)) return 2;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        TinyHybridSnapshotFaultGateway? faultGateway = null;
        try
        {
            using var aliceLogin = await TinyNetworkClient.ConnectAsync(
                host, port, prefix + "-alice", deadline.Token);
            var bobLogin = await TinyNetworkClient.ConnectAsync(
                host, port, prefix + "-bob", deadline.Token);
            try
            {
                using var alice = await ConnectSessionAsync(aliceLogin, host, port, deadline.Token,
                    evidencePath == null ? null : gateway =>
                        faultGateway = new TinyHybridSnapshotFaultGateway(gateway));
                var bob = await ConnectSessionAsync(bobLogin, host, port, deadline.Token);
                try
                {
                    await alice.CreateRoomAsync(syncMode, deadline.Token);
                    await bob.JoinRoomAsync(alice.RoomId, deadline.Token);
                    await alice.SetReadyAsync(deadline.Token);
                    await bob.SetReadyAsync(deadline.Token);
                    await alice.PollAsync(deadline.Token);
                    Require(alice.CanStart, "Owner cannot start after both players are ready.");
                    await alice.BeginLoadingAsync(deadline.Token);

                    await WaitUntilAsync(async () =>
                    {
                        await alice.PollAsync(deadline.Token);
                        await bob.PollAsync(deadline.Token);
                        alice.Tick(0.025f);
                        bob.Tick(0.025f);
                        if (syncMode == TinySyncMode.State)
                        {
                            alice.TryGetNewSnapshot(out _);
                            bob.TryGetNewSnapshot(out _);
                        }
                        return alice.CanSubmitInput && bob.CanSubmitInput;
                    }, deadline.Token);
                    Require(alice.SyncMode == syncMode && bob.SyncMode == syncMode,
                        "Clients negotiated the wrong sync mode.");

                    if (evidencePath != null)
                        return await TinyHybridMismatchSmoke.RunAsync(
                            alice, bob, faultGateway!, aliceLogin.SessionToken, evidencePath, deadline.Token);

                    var attack = new TinyInput(1, 0, true);
                    var submission = alice.SubmitInputAsync(attack, deadline.Token);
                    if (syncMode == TinySyncMode.Hybrid)
                    {
                        Require(alice.TryGetNewSnapshot(out var predicted) &&
                            TinyStateExample.ReadActor(in predicted, bob.PlayerId).Hp == 90,
                            "Hybrid session did not expose local prediction before network confirmation.");
                    }
                    await submission;

                    var aliceConverged = false;
                    var bobConverged = false;
                    await WaitUntilAsync(() =>
                    {
                        alice.Tick(0.025f);
                        bob.Tick(0.025f);
                        if (alice.TryGetNewSnapshot(out var a))
                            aliceConverged |= TinyStateExample.ReadActor(in a, bob.PlayerId).Hp == 90 &&
                                TinyStateExample.ReadActor(in a, alice.PlayerId).X == 0;
                        if (bob.TryGetNewSnapshot(out var b))
                            bobConverged |= TinyStateExample.ReadActor(in b, bob.PlayerId).Hp == 90 &&
                                TinyStateExample.ReadActor(in b, alice.PlayerId).X == 0;
                        return Task.FromResult(aliceConverged && bobConverged);
                    }, deadline.Token);

                    if (chapterHash.HasValue)
                    {
                        await WaitUntilAsync(() =>
                        {
                            alice.Tick(0.025f);
                            bob.Tick(0.025f);
                            return Task.FromResult(alice.Telemetry.AuthoritativeFrame > 0 &&
                                bob.Telemetry.AuthoritativeFrame > 0 &&
                                (syncMode != TinySyncMode.Hybrid || alice.Telemetry.LocalPredictions > 0));
                        }, deadline.Token);
                        var owner = alice.Telemetry;
                        var guest = bob.Telemetry;
                        Require(!owner.NeedsFullSnapshot && !guest.NeedsFullSnapshot,
                            "Chapter session requested an unexpected full snapshot.");
                        Console.WriteLine($"Tiny {mode} chapter passed: room={alice.RoomId}, " +
                            $"clients=2, authoritativeFrame={Math.Min(owner.AuthoritativeFrame, guest.AuthoritativeFrame)}, " +
                            $"chapterHash={chapterHash.Value}, predictions={owner.LocalPredictions}, " +
                            $"ownerRollbacks={owner.Rollbacks}, guestRollbacks={guest.Rollbacks}");
                        return 0;
                    }

                    bob.Dispose();
                    bobLogin.Dispose();
                    using var restoredLogin = await TinyNetworkClient.ConnectAsync(
                        host, port, prefix + "-bob", deadline.Token);
                    using var restored = await ConnectSessionAsync(
                        restoredLogin, host, port, deadline.Token);
                    await restored.RestoreAsync(deadline.Token);
                    Require(restored.RoomId == alice.RoomId && restored.BattleId == alice.BattleId,
                        "Restored session did not bind the original battle.");
                    var recovered = false;
                    await WaitUntilAsync(() =>
                    {
                        restored.Tick(0.025f);
                        recovered |= restored.TryGetNewSnapshot(out var snapshot) &&
                            TinyStateExample.ReadActor(in snapshot, restored.PlayerId).Hp == 90 &&
                            TinyStateExample.ReadActor(in snapshot, alice.PlayerId).X == 0;
                        return Task.FromResult(recovered);
                    }, deadline.Token);
                    Console.WriteLine($"Tiny session {mode} passed: room={alice.RoomId}, " +
                        $"clients=2, predicted={alice.Telemetry.LocalPredictions}, reconnect=full");
                    return 0;
                }
                finally { bob.Dispose(); }
            }
            finally { bobLogin.Dispose(); }
        }
        catch (Exception exception)
        {
            if (evidencePath != null && !File.Exists(evidencePath))
                TinyHybridMismatchSmoke.WriteFailure(evidencePath, exception);
            Console.Error.WriteLine($"Tiny session {mode} failed: {exception}");
            return 1;
        }
    }

    private static async Task<TinyBattleSession> ConnectSessionAsync(
        TinyNetworkClient login, string host, int port, CancellationToken cancellationToken,
        Func<ITinyBattleGateway, ITinyBattleGateway>? decorate = null)
    {
        var gateway = await TinyGatewayClient.ConnectAsync(host, port, cancellationToken);
        return new TinyBattleSession(new DemoMultiplayerLaunchRequest(
            host, port, "dev", "local", login.AccountId, login.SessionToken,
            TimeSpan.FromSeconds(10)), decorate?.Invoke(gateway) ?? gateway);
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, CancellationToken cancellationToken)
    {
        while (!await condition())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(25, cancellationToken);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
