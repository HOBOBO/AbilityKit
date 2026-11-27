using AbilityKit.Ability.StateSync.Prediction.Reconciliation;
using AbilityKit.BattleScenario.Editor;
using Host = AbilityKit.Demo.Moba.BattleScenario.Runner.LocalStateSyncMovementHost;

namespace AbilityKit.Demo.Moba.BattleScenario.Runner;

internal static class LocalStateSyncMovementReplay
{
    internal const string FileName = "moba-local-movement.corrected.statesync-replay.json";
    internal const string RecoveryFileName = "moba-local-movement.proof-recovery.statesync-replay.json";
    private const int TickRate = 20;
    private const int PreviewModelId = 3005;

    internal static LocalStateSyncReplay Record()
    {
        const float deltaTime = 1f / TickRate;
        using var host = new Host(deltaTime);
        using var authority = new Host.MovementWorld("movement-replay-authority", deltaTime);
        var checkpoints = new List<LocalStateSyncReplay.Checkpoint>();
        var rejected = new Host.Input(1, 0, -1);
        var forward = new Host.Input(2, 0, 1);
        var diagonal = new Host.Input(3, 1, 1);

        authority.Step([]);
        var snapshot = new Host.Snapshot(101, 1, authority.Capture(), rejected.Sequence);
        host.Advance(rejected);
        checkpoints.Add(Capture(host, "预测 Tick 1 · 向后移动（权威拒绝 #1）"));
        host.Advance(forward);
        checkpoints.Add(Capture(host, "预测 Tick 2 · 向前移动 #2"));
        host.Advance();
        checkpoints.Add(Capture(host, "预测 Tick 3 · 无新输入，延续移动"));

        authority.Step([forward]);
        var authorityTick2 = authority.Capture();
        authority.Step([]);
        var result = host.Receive(snapshot) ?? throw new InvalidOperationException("Local movement snapshot was not applied.");
        Require(result.Comparison == Host.Comparison.Mismatch && result.RebasedLiveWorld &&
            result.Authority == snapshot.State && result.Steps.Length == 2 && host.TerminalSequence == rejected.Sequence,
            "Expected a same-Tick mismatch and a committed correction.");
        Require(result.Steps[0] == new Host.ReplaySample(2, result.Authority, authorityTick2, 1) &&
            result.Steps[1] == new Host.ReplaySample(3, authorityTick2, authority.Capture(), 0) &&
            host.IsPredicting && host.StateTick == 3 && host.Current == authority.Capture(),
            "Replay did not match the independent authority at each Tick.");

        var corrected = Capture(host, "交付 Tick 1 权威快照 · 终结 #1，从锚点重演至 Tick 3");
        IncludeAnchor(corrected, snapshot, result, imports: 1);
        corrected.ComparisonTick = snapshot.Tick;
        IncludeDelivery(corrected, result);
        checkpoints.Add(corrected);

        host.Advance(diagonal);
        authority.Step([diagonal]);
        Require(host.IsPredicting && host.StateTick == 4 && host.Current == authority.Capture(),
            "The live world did not continue prediction from the corrected state.");
        var continued = Capture(host, "追平后继续预测 Tick 4 · 接收斜向移动 #3");
        IncludeAnchor(continued, snapshot, result, imports: 1);
        checkpoints.Add(continued);

        return CreateReplay("corrected", checkpoints);
    }

    internal static LocalStateSyncReplay RecordProofRecovery()
    {
        const float deltaTime = 1f / TickRate;
        using var host = new Host(deltaTime);
        using var authority = new Host.MovementWorld("movement-proof-recovery-authority", deltaTime);
        var checkpoints = new List<LocalStateSyncReplay.Checkpoint>();
        var rejected = new Host.Input(1, 0, -1);
        var forward = new Host.Input(2, 0, 1);
        var diagonal = new Host.Input(3, 1, 1);

        authority.Step([]);
        var withoutProof = new Host.Snapshot(101, 1, authority.Capture(), null);
        host.Advance(rejected);
        checkpoints.Add(Capture(host, "预测 Tick 1 · 向后移动 #1"));
        host.Advance();
        checkpoints.Add(Capture(host, "预测 Tick 2 · 延续向后移动"));

        var fallback = host.Receive(withoutProof) ?? throw new InvalidOperationException("Proof-less snapshot was not applied.");
        Require(fallback.Status == ReconciliationStatus.InvalidTerminalProof && fallback.Comparison == Host.Comparison.Mismatch &&
            fallback.RebasedLiveWorld && fallback.ReplayedTicks == 0 && !host.IsPredicting && host.StateTick == 1 &&
            host.Current == withoutProof.State && host.TerminalSequence == 0,
            "Expected authority-only fallback with no input retired or replayed.");
        var paused = Capture(host, "交付 Tick 1 权威快照 · 缺终结证明，停止预测");
        IncludeAnchor(paused, withoutProof, fallback, imports: 1);
        IncludeDelivery(paused, fallback);
        checkpoints.Add(paused);

        authority.Step([]);
        var recovery = new Host.Snapshot(102, 2, authority.Capture(), rejected.Sequence);
        host.Advance(forward);
        Require(!host.IsPredicting && host.StateTick == 1 && host.Current == fallback.Authority &&
            host.PendingSequences.SequenceEqual(new[] { rejected.Sequence, forward.Sequence }),
            "Paused world must retain the authority pose and queue input without advancing it.");
        var waiting = Capture(host, "本地 Tick 3 · 仅缓存向前输入 #2，保持权威");
        IncludeAnchor(waiting, withoutProof, fallback, imports: 1);
        checkpoints.Add(waiting);

        authority.Step([forward]);
        var authorityTick3 = authority.Capture();
        authority.Step([]);
        host.Advance();
        var resumed = host.Receive(recovery) ?? throw new InvalidOperationException("Recovery snapshot was not applied.");
        Require(resumed.Status == ReconciliationStatus.Predicted && resumed.Comparison == Host.Comparison.MissingHistory &&
            resumed.RebasedLiveWorld && resumed.Authority == recovery.State && resumed.ReplayedTicks == 2 &&
            resumed.Steps.SequenceEqual(new[] { new Host.ReplaySample(3, recovery.State, authorityTick3, 1),
                new Host.ReplaySample(4, authorityTick3, authority.Capture(), 0) }) &&
            host.IsPredicting && host.StateTick == 4 && host.Current == authority.Capture() &&
            host.TerminalSequence == rejected.Sequence && host.PendingSequences.SequenceEqual(new[] { forward.Sequence }),
            "Recovery must retire #1, replay #2 and the following empty Tick, and match independent authority.");
        var recovered = Capture(host, "交付 Tick 2 新基线 · 终结 #1，重演至 Tick 4");
        IncludeAnchor(recovered, recovery, resumed, imports: 2);
        IncludeDelivery(recovered, resumed);
        checkpoints.Add(recovered);

        host.Advance(diagonal);
        authority.Step([diagonal]);
        Require(host.IsPredicting && host.StateTick == 5 && host.Current == authority.Capture(),
            "The live world did not continue from the recovered prediction.");
        var continued = Capture(host, "追平后继续预测 Tick 5 · 接收斜向移动 #3");
        IncludeAnchor(continued, recovery, resumed, imports: 2);
        checkpoints.Add(continued);

        return CreateReplay("proof-recovery", checkpoints);
    }

    private static LocalStateSyncReplay CreateReplay(string variant, List<LocalStateSyncReplay.Checkpoint> checkpoints) => new()
    {
        CaseId = "moba-local-movement",
        Variant = variant,
        TickRate = TickRate,
        PositionLabel = "Z · 1/1000 世界单位",
        ActorModelId = PreviewModelId,
        Checkpoints = checkpoints.ToArray(),
    };

    private static LocalStateSyncReplay.Checkpoint Capture(Host host, string command) => new()
    {
        Command = command,
        Generation = 1,
        DeliveryTick = host.Tick,
        Prediction = host.IsPredicting ? Plot(host.Current) : null,
        PredictionPose = host.IsPredicting ? Pose(host.Current) : null,
        PredictionWaiting = !host.IsPredicting,
        TerminalSequence = host.TerminalSequence,
        Pending = host.PendingSequences,
        Steps = Array.Empty<LocalStateSyncReplay.ReplayStep>(),
    };

    private static void IncludeAnchor(LocalStateSyncReplay.Checkpoint point, Host.Snapshot snapshot, Host.Delivery result, int imports)
    {
        point.Sequence = snapshot.Revision;
        point.PublishedRevision = snapshot.Revision;
        point.ReconciledRevision = snapshot.Revision;
        point.AppliedTick = point.BaselineTick = snapshot.Tick;
        point.PublishedPosition = point.Authority = Plot(result.Authority);
        point.AuthorityPose = Pose(result.Authority);
        point.Imports = imports;
    }

    private static void IncludeDelivery(LocalStateSyncReplay.Checkpoint point, Host.Delivery result)
    {
        point.Admission = "AppliedFullBaseline";
        point.Reconciliation = result.Status.ToString();
        point.Steps = result.Steps.Select(step => new LocalStateSyncReplay.ReplayStep
        {
            Tick = step.Tick,
            Before = Plot(step.Before),
            After = Plot(step.After),
            AfterPose = Pose(step.After),
        }).ToArray();
        point.ReplayTicks = point.Steps.Select(step => step.Tick).ToArray();
    }

    private static int Plot(Host.State state) => checked((int)MathF.Round(state.Pose.Position.Z * 1000f));

    private static LocalStateSyncReplay.ActorPose Pose(Host.State state)
    {
        var (position, rotation) = (state.Pose.Position, state.Pose.Rotation);
        return new LocalStateSyncReplay.ActorPose
        {
            X = position.X, Y = position.Y, Z = position.Z,
            Qx = rotation.X, Qy = rotation.Y, Qz = rotation.Z, Qw = rotation.W,
        };
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
