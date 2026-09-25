using AbilityKit.Demo.Tiny.FrameSync;
using AbilityKit.Network.Room;
using AbilityKit.Network.Runtime;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Demo.Tiny.Client;

internal static class TinyFrameSmoke
{
    private const int SmokeInputLeadFrames = 12;

    public static async Task<int> RunAsync(string host, int port, string prefix, string mode)
    {
        if (mode is not ("frame" or "hybrid"))
        {
            Console.Error.WriteLine("Tiny mode must be state, frame or hybrid.");
            return 2;
        }

        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try
        {
            using var alice = await TinyNetworkClient.ConnectAsync(host, port, prefix + "-alice", deadline.Token);
            using var bob = await TinyNetworkClient.ConnectAsync(host, port, prefix + "-bob", deadline.Token);
            var template = mode == "frame" ? TinySyncTemplates.Frame : TinySyncTemplates.Hybrid;
            var model = mode == "frame" ? NetworkSyncModel.Lockstep : NetworkSyncModel.HybridHeroPrediction;
            var tags = new Dictionary<string, string>
            {
                [RoomGatewaySyncTagKeys.SyncTemplateId] = template,
                [RoomGatewaySyncTagKeys.SyncModel] = ((int)model).ToString(),
                [RoomGatewaySyncTagKeys.InputDelayFrames] =
                    TinySyncSettings.InputDelayFrames.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };
            var created = await alice.Rooms.CreateRoomAsync(new RoomGatewayCreateRequest(
                alice.SessionToken, "dev", "local", "tiny", "Tiny Battle", false, 2, tags),
                cancellationToken: deadline.Token);
            Require(created.Success, "Create room: " + created.Message);
            var roomId = created.RoomId;
            Require((await alice.Rooms.JoinRoomAsync(new RoomGatewayJoinRequest(
                alice.SessionToken, "dev", "local", roomId), cancellationToken: deadline.Token)).Success,
                "Owner join failed.");
            Require((await bob.Rooms.JoinRoomAsync(new RoomGatewayJoinRequest(
                bob.SessionToken, "dev", "local", roomId), cancellationToken: deadline.Token)).Success,
                "Guest join failed.");
            Require((await alice.Rooms.SetReadyAsync(new RoomGatewayReadyRequest(
                alice.SessionToken, roomId, true), cancellationToken: deadline.Token)).Success,
                "Owner ready failed.");
            Require((await bob.Rooms.SetReadyAsync(new RoomGatewayReadyRequest(
                bob.SessionToken, roomId, true), cancellationToken: deadline.Token)).Success,
                "Guest ready failed.");

            var flow = new RoomGatewaySessionFlow(alice.Rooms);
            var loading = await flow.BeginLoadingAsync(new RoomGatewayBeginLoadingRequest(
                alice.SessionToken, roomId, null, Guid.NewGuid().ToString("N")),
                cancellationToken: deadline.Token);
            Require(loading.Success && loading.Snapshot != null, "Begin loading failed: " + loading.Message);
            var manifest = loading.Snapshot!;
            Require((await flow.ReportAssetsLoadedAsync(new RoomGatewayReportAssetsLoadedRequest(
                alice.SessionToken, roomId, manifest.LaunchGeneration, manifest.LaunchManifestVersion,
                manifest.LaunchManifestHash, Guid.NewGuid().ToString("N")),
                cancellationToken: deadline.Token)).Success, "Owner loading failed.");
            var guestFlow = new RoomGatewaySessionFlow(bob.Rooms);
            Require((await guestFlow.ReportAssetsLoadedAsync(new RoomGatewayReportAssetsLoadedRequest(
                bob.SessionToken, roomId, manifest.LaunchGeneration, manifest.LaunchManifestVersion,
                manifest.LaunchManifestHash, Guid.NewGuid().ToString("N")),
                cancellationToken: deadline.Token)).Success, "Guest loading failed.");

            var started = await flow.WaitForBattleStartAsync(alice.SessionToken, roomId,
                TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(15), deadline.Token);
            Require(started.Success && started.Snapshot != null, "Battle did not start.");
            var battle = started.Snapshot!;
            Require(battle.SyncCapabilities?.ProfileName == model.ToString(),
                "Server selected the wrong sync profile.");
            var aliceId = battle.Players.Single(player => player.AccountId == alice.AccountId).PlayerId;
            var bobId = battle.Players.Single(player => player.AccountId == bob.AccountId).PlayerId;

            await SubscribeAsync(alice, roomId, battle, deadline.Token);
            await SubscribeAsync(bob, roomId, battle, deadline.Token);
            await RequestBaselineAsync(alice, roomId, battle, deadline.Token);
            await RequestBaselineAsync(bob, roomId, battle, deadline.Token);
            await WaitForBaselineAsync(alice, bob, deadline.Token);
            Require(alice.TryGetSnapshot(alice.LatestFrame, out var aliceBaseline), "Owner baseline missing.");
            Require(bob.TryGetSnapshot(bob.LatestFrame, out var bobBaseline), "Guest baseline missing.");
            var a = new FrameVerifier(aliceBaseline);
            var b = new FrameVerifier(bobBaseline);

            var inputFrame = Math.Max(alice.LatestNetworkFrame + SmokeInputLeadFrames,
                aliceBaseline.Frame + SmokeInputLeadFrames);
            var attack = new TinyInput(1, 0, true);
            WireRoomSubmitFrameInputRes submitted = default;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                if (mode == "hybrid")
                {
                    a.PredictLocalInput(inputFrame, aliceId, attack);
                    Require(a.TargetHp(bobId) == 90, "Hybrid did not predict local input before sending.");
                }
                submitted = await alice.WireRooms.SubmitFrameInputAsync(new WireRoomSubmitFrameInputReq
                {
                    SessionToken = alice.SessionToken,
                    RoomId = roomId,
                    BattleId = battle.BattleId,
                    WorldId = battle.WorldId,
                    Frame = inputFrame,
                    PlayerId = aliceId,
                    InputOpCode = TinyBattle.InputOpCode,
                    Payload = attack.Encode()
                }, cancellationToken: deadline.Token);
                if (submitted.Accepted || submitted.Reason != 3) break;
                inputFrame = Math.Max(inputFrame + 1, submitted.ServerFrame + SmokeInputLeadFrames);
                a = new FrameVerifier(aliceBaseline);
            }
            Require(submitted.Accepted,
                "Frame input rejected: " + submitted.Reason + " (server frame " + submitted.ServerFrame + ")");

            while (!deadline.IsCancellationRequested)
            {
                alice.Tick(0.025f);
                bob.Tick(0.025f);
                while (alice.TryDequeueFrame(out var frame)) a.Apply(frame);
                while (bob.TryDequeueFrame(out var frame)) b.Apply(frame);
                if (a.LastFrame >= inputFrame && b.LastFrame >= inputFrame &&
                    a.TargetHp(bobId) == 90 && b.TargetHp(bobId) == 90 &&
                    (mode == "frame" ? a.Replayed : a.MatchedLocalInput) && b.Replayed)
                {
                    Require(a.StateHashAt(inputFrame + 1) == b.StateHashAt(inputFrame + 1),
                        "Clients diverged after rollback.");
                    bob.Dispose();
                    using var recovered = await TinyNetworkClient.ConnectAsync(host, port,
                        prefix + "-bob", deadline.Token);
                    var restore = await recovered.Rooms.RestoreRoomAsync(new RoomGatewayRestoreRoomRequest(
                        recovered.SessionToken, "dev", "local"), cancellationToken: deadline.Token);
                    Require(restore.Success && restore.IsInBattle && restore.BattleId == battle.BattleId,
                        "Frame room restore failed: " + restore.Message);
                    await SubscribeAsync(recovered, roomId, battle, deadline.Token);
                    await RequestBaselineAsync(recovered, roomId, battle, deadline.Token);
                    await WaitForBaselineAsync(alice, recovered, deadline.Token);
                    Require(recovered.TryGetSnapshot(recovered.LatestFrame, out var recoveredSnapshot),
                        "Recovered Tiny baseline missing.");
                    var recoveredState = TinyBattleStateCodec.Decode(recoveredSnapshot.Payload!);
                    Require(recoveredState.Actors.Single(actor => actor.PlayerId == bobId).Hp == 90,
                        "Recovered Tiny baseline lost authoritative damage.");
                    Console.WriteLine($"Tiny {mode} TCP smoke passed: room={roomId}, frame={inputFrame}, localPrediction={mode == "hybrid"}, remoteReplay=1, targetHp=90, reconnect=full");
                    return 0;
                }
                await Task.Delay(25, deadline.Token);
            }
            throw new TimeoutException("Authoritative frame did not reconcile on both clients.");
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Tiny {mode} TCP smoke failed: {exception.Message}");
            return 1;
        }
    }

    private static async Task SubscribeAsync(TinyNetworkClient client, string roomId,
        RoomGatewaySnapshot battle, CancellationToken cancellationToken)
    {
        Require((await client.Rooms.SubscribeStateSyncAsync(new RoomGatewayStateSyncSubscriptionRequest(
            client.SessionToken, battle.BattleId, roomId), cancellationToken: cancellationToken)).Success,
            "State baseline subscription failed.");
        var frame = await client.WireRooms.SubscribeFrameSyncAsync(new WireRoomSubscribeFrameSyncReq
        {
            SessionToken = client.SessionToken,
            RoomId = roomId,
            BattleId = battle.BattleId,
            WorldId = battle.WorldId
        }, cancellationToken: cancellationToken);
        Require(frame.Success, "Frame subscription failed: " + frame.Message);
    }

    private static async Task RequestBaselineAsync(TinyNetworkClient client, string roomId,
        RoomGatewaySnapshot battle, CancellationToken cancellationToken)
    {
        var response = await client.WireRooms.RequestFullStateSyncAsync(new WireRequestFullStateSyncReq
        {
            SessionToken = client.SessionToken,
            RoomId = roomId,
            BattleId = battle.BattleId,
            WorldId = battle.WorldId,
            Reason = "Tiny frame smoke baseline"
        }, cancellationToken: cancellationToken);
        Require(response.Success && response.Accepted, "Full baseline request failed.");
    }

    private static async Task WaitForBaselineAsync(TinyNetworkClient alice, TinyNetworkClient bob,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            alice.Tick(0.025f);
            bob.Tick(0.025f);
            if (alice.LatestFrame >= 0 && bob.LatestFrame >= 0 &&
                alice.TryGetSnapshot(alice.LatestFrame, out var a) &&
                bob.TryGetSnapshot(bob.LatestFrame, out var b) &&
                a.PayloadOpCode == TinyBattleStateCodec.PayloadOpCode &&
                b.PayloadOpCode == TinyBattleStateCodec.PayloadOpCode)
                return;
            await Task.Delay(25, cancellationToken);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class FrameVerifier
    {
        private readonly TinyBattle _battle = new();
        private readonly TinyFrameSyncSession _session;
        private readonly int _baselineFrame;
        private int _predictedLocalFrame = -1;
        public int LastFrame { get; private set; }
        public bool Replayed { get; private set; }
        public bool MatchedLocalInput { get; private set; }
        public uint StateHashAt(int frame)
        {
            Require(_session.TryGetStateHash(frame, out var hash),
                "Authoritative frame is outside rollback history: " + frame);
            return hash;
        }

        public FrameVerifier(WireStateSyncSnapshotPush baseline)
        {
            Require(baseline.Payload != null, "Tiny baseline payload missing.");
            _battle.RestoreState(TinyBattleStateCodec.Decode(baseline.Payload!));
            _session = new TinyFrameSyncSession(_battle);
            _baselineFrame = baseline.Frame;
            LastFrame = baseline.Frame - 1;
        }

        public void PredictLocalInput(int frame, uint playerId, TinyInput input)
        {
            Require(frame >= _session.Frame && frame - LastFrame <= 32,
                "Hybrid prediction exceeds its frame history.");
            while (_session.Frame < frame)
                _session.Predict(Array.Empty<TinyFrameInput>());
            _session.Predict(new[] { new TinyFrameInput(playerId, input) });
            _predictedLocalFrame = frame;
        }

        public void Apply(WireRoomFramePush frame)
        {
            var target = frame.Frame + 1;
            if (frame.Frame <= LastFrame || target <= _baselineFrame) return;
            Require(frame.Frame - LastFrame <= 64, "Frame gap exceeds rollback history.");
            while (_session.Frame < target)
                _session.Predict(Array.Empty<TinyFrameInput>());
            var inputs = (frame.Inputs ?? Array.Empty<WireRoomFrameInput>())
                .Select(item => new TinyFrameInput(item.PlayerId, TinyInput.Decode(item.Payload)))
                .ToArray();
            var result = _session.ApplyAuthoritative(target, inputs, frame.StateHash);
            Require(result != TinyReconcileResult.NeedsFullSnapshot,
                $"Frame reconciliation failed: frame={frame.Frame}, hash={frame.StateHash}, local={_session.StateHash}");
            Replayed |= result == TinyReconcileResult.Replayed;
            MatchedLocalInput |= frame.Frame == _predictedLocalFrame &&
                result == TinyReconcileResult.Matched;
            LastFrame = frame.Frame;
        }

        public int TargetHp(uint playerId) =>
            _battle.Actors.Single(actor => actor.PlayerId == playerId).Hp;
    }
}
