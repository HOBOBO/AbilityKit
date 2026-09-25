using System.Text.Json;
using AbilityKit.Demo.Tiny.View;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Demo.Tiny.Client;

internal static class TinyHybridMismatchSmoke
{
    private sealed class Evidence
    {
        public int SchemaVersion { get; init; } = 1;
        public string Scenario { get; init; } = "hybrid-snapshot-mismatch";
        public bool Passed { get; set; }
        public string? Failure { get; set; }
        public string? RoomId { get; set; }
        public ulong WorldId { get; set; }
        public int PredictedFrame { get; set; }
        public uint PredictedHash { get; set; }
        public uint InjectedHash { get; set; }
        public uint CorrectedHash { get; set; }
        public int CorrectionsAfterInjection { get; set; }
        public int AuthenticFrame { get; set; }
        public uint AuthenticHash { get; set; }
        public int ConvergedFrame { get; set; }
        public uint OwnerHash { get; set; }
        public uint GuestHash { get; set; }
        public int TargetHp { get; set; }
        public int DroppedFrames { get; set; }
        public int RecoveryRequests { get; set; }
    }

    public static async Task<int> RunAsync(
        TinyBattleSession owner, TinyBattleSession guest,
        TinyHybridSnapshotFaultGateway fault, string sessionToken, string evidencePath,
        CancellationToken cancellationToken)
    {
        var evidence = new Evidence { RoomId = owner.RoomId };
        try
        {
            var submission = owner.SubmitInputAsync(new TinyInput(1, 0, true), cancellationToken);
            Require(owner.TryGetNewSnapshot(out var predicted), "Local prediction was not visible.");
            Require(Actor(predicted, guest.PlayerId).Hp == 90,
                "Hybrid did not predict the attack before confirmation.");
            var predictedState = CreateState(predicted, owner.PlayerId, guest.PlayerId, 90);
            evidence.WorldId = predicted.WorldId;
            evidence.PredictedFrame = predicted.Frame;
            evidence.PredictedHash = Hash(predictedState);
            Require(owner.Telemetry.StateHash == evidence.PredictedHash,
                "Predicted presentation and state hash disagree.");
            await submission;

            var faultState = CreateState(predicted, owner.PlayerId, guest.PlayerId, 100);
            evidence.InjectedHash = Hash(faultState);
            Require(evidence.InjectedHash != evidence.PredictedHash,
                "Fault snapshot did not change the predicted state.");
            fault.Inject(new WireStateSyncSnapshotPush
            {
                WorldId = predicted.WorldId,
                Frame = predicted.Frame,
                IsFullSnapshot = true,
                PayloadOpCode = TinyBattleStateCodec.PayloadOpCode,
                Payload = TinyBattleStateCodec.Encode(faultState)
            });
            owner.Tick(0.025f);
            evidence.CorrectedHash = owner.Telemetry.StateHash;
            evidence.CorrectionsAfterInjection = owner.Telemetry.SnapshotCorrections;
            Require(evidence.CorrectionsAfterInjection == 1 &&
                    evidence.CorrectedHash == evidence.InjectedHash &&
                    !owner.NeedsFullSnapshot,
                "Injected snapshot did not produce one in-place correction.");

            await fault.RequestAuthenticSnapshotAsync(sessionToken, owner.RoomId,
                owner.BattleId, predicted.WorldId, predicted.Frame, cancellationToken);

            var guestHashes = new Dictionary<int, (uint Hash, int TargetHp)>();
            while (!cancellationToken.IsCancellationRequested)
            {
                guest.Tick(0.025f);
                if (guest.TryGetNewSnapshot(out var guestView) && !guest.NeedsFullSnapshot)
                    guestHashes[guestView.Frame] = (guest.Telemetry.StateHash,
                        (int)Actor(guestView, guest.PlayerId).Hp);

                owner.Tick(0.025f);
                if (fault.AuthenticFrame > predicted.Frame &&
                    owner.TryGetNewSnapshot(out var ownerView) &&
                    !owner.NeedsFullSnapshot && !guest.NeedsFullSnapshot &&
                    guestHashes.TryGetValue(ownerView.Frame, out var guestAtFrame) &&
                    fault.TryGetAuthenticHash(ownerView.Frame, out var authenticHash) &&
                    guestAtFrame.Hash == owner.Telemetry.StateHash &&
                    guestAtFrame.Hash == authenticHash &&
                    guestAtFrame.TargetHp == 90 &&
                    (int)Actor(ownerView, guest.PlayerId).Hp == 90 &&
                    owner.Telemetry.SnapshotCorrections == 1 &&
                    owner.Telemetry.RecoveryRequests == 0 &&
                    guest.Telemetry.RecoveryRequests == 0)
                {
                    evidence.AuthenticFrame = fault.AuthenticFrame;
                    evidence.AuthenticHash = fault.AuthenticHash;
                    evidence.ConvergedFrame = ownerView.Frame;
                    evidence.OwnerHash = owner.Telemetry.StateHash;
                    evidence.GuestHash = guestAtFrame.Hash;
                    evidence.TargetHp = guestAtFrame.TargetHp;
                    evidence.DroppedFrames = fault.DroppedFrames;
                    evidence.RecoveryRequests = owner.Telemetry.RecoveryRequests;
                    fault.ReleaseFrameStream();
                    owner.Tick(0.025f);
                    Require(!owner.NeedsFullSnapshot,
                        "Frame stream did not resume after authentic convergence.");
                    evidence.Passed = true;
                    Save(evidencePath, evidence);
                    Console.WriteLine($"Tiny hybrid snapshot mismatch passed: room={owner.RoomId}, " +
                        $"predictedFrame={evidence.PredictedFrame}, injectedHash={evidence.InjectedHash}, " +
                        $"authenticFrame={evidence.AuthenticFrame}, convergedFrame={evidence.ConvergedFrame}, " +
                        $"hash={evidence.OwnerHash}, corrections=1, clients=2");
                    return 0;
                }
                await Task.Delay(25, cancellationToken);
            }
            throw new TimeoutException("Clients did not converge after the authentic snapshot.");
        }
        catch (Exception exception)
        {
            evidence.Failure = exception.ToString();
            evidence.DroppedFrames = fault.DroppedFrames;
            evidence.RecoveryRequests = owner.Telemetry.RecoveryRequests;
            Save(evidencePath, evidence);
            throw;
        }
    }

    public static void WriteFailure(string path, Exception exception) =>
        Save(path, new Evidence { Failure = exception.ToString() });

    private static TinyBattleState CreateState(
        WireStateSyncSnapshotPush view, uint ownerId, uint guestId, int guestHp) =>
        new(view.Frame, new[]
        {
            StateActor(Actor(view, ownerId), ownerId, TinyBattle.AttackCooldownFrames),
            StateActor(Actor(view, guestId), guestId, 0, guestHp)
        });

    private static TinyActorState StateActor(
        WireStateSyncActorSnapshot actor, uint id, int cooldown, int? hp = null) =>
        new(id, (int)actor.X, (int)actor.Z, hp ?? (int)actor.Hp, cooldown);

    private static WireStateSyncActorSnapshot Actor(WireStateSyncSnapshotPush view, uint id) =>
        view.Actors?.Single(actor => actor.ActorId == id) ??
        throw new InvalidOperationException("Tiny actor missing from presentation.");

    private static uint Hash(TinyBattleState state)
    {
        var battle = new TinyBattle();
        battle.RestoreState(state);
        return battle.ComputeHash();
    }

    private static void Save(string path, Evidence evidence)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, JsonSerializer.Serialize(evidence,
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
