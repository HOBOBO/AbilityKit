using AbilityKit.Ability.StateSync.Prediction.Reconciliation;
using AbilityKit.BattleScenario;
using AbilityKit.BattleScenario.Editor;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Acceptance.LiveSim;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.BattleScenario;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Scenario;
using Newtonsoft.Json;

namespace AbilityKit.Demo.Moba.BattleScenario.Runner;

/// <summary>
/// 从同一次本地验收的正式技能观测生成回放，仅在 E 完全结束后的空旷直线区间对账。
/// </summary>
internal static class GragasStateSyncReplay
{
    private const string CaseId = "gragas-e-local-statesync";
    private const float Tolerance = 0.0001f;
    private static readonly WalkInput Stop = new(1, 0f);
    private static readonly WalkInput Back = new(2, -1f);

    internal static void Export(string directory)
    {
        var fixtures = Path.Combine(AppContext.BaseDirectory, "Flows", "PredictionPrerequisites");
        var contact = BattleScenarioCompiler.CompileFile(Path.Combine(fixtures, "gragas-e-q-contact.battlescenario"));
        var clear = BattleScenarioCompiler.CompileFile(Path.Combine(fixtures, "gragas-e-q-clear.battlescenario"));
        var first = Run(contact, clear);
        var second = Run(contact, clear);
        Require(first.SequenceEqual(second), "Two local runs produced different replay or authority evidence.");

        Directory.CreateDirectory(directory);
        foreach (var artifact in first)
        {
            var path = Path.Combine(directory, artifact.Name);
            File.WriteAllText(path, artifact.Contents);
            System.Console.WriteLine(path);
        }
    }

    private static Artifact[] Run(TestScenario contact, TestScenario clear)
    {
        var authority = Observe(contact);
        var oldPrediction = Observe(clear);
        Require(authority.AnchorTick == oldPrediction.AnchorTick && authority.TickRate == oldPrediction.TickRate,
            "Both participants must observe the same Tick interval.");
        Require(Math.Abs(authority.Cut.Position.Z - oldPrediction.Cut.Position.Z) > 0.05f,
            "The formal E hit and miss must land at different positions.");

        var artifacts = new List<Artifact>
        {
            Record("confirmed", authority, Observe(contact), ReconciliationStatus.AuthoritativeOnly),
            Record("corrected", authority, oldPrediction, ReconciliationStatus.Predicted),
            Record("without-proof", authority, oldPrediction, ReconciliationStatus.InvalidTerminalProof),
            Record("history-gap", authority, oldPrediction, ReconciliationStatus.MissingTickCoverage),
            new(CaseId + ".authority.trace.json", JsonConvert.SerializeObject(authority.Outcome.TraceNodes, Formatting.Indented)),
            new(CaseId + ".evidence.json", JsonConvert.SerializeObject(new
            {
                authority.TickRate,
                authority.AnchorTick,
                Authority = Evidence(authority),
                Prediction = Evidence(oldPrediction),
            }, Formatting.Indented)),
        };
        return artifacts.ToArray();
    }

    private static object Evidence(Observation observation) => new
    {
        observation.FirstSkillMotionTick,
        observation.LastSkillMotionTick,
        Cut = EvidenceSample(observation.Cut),
        Moved = EvidenceSample(observation.Moved),
        Idle = EvidenceSample(observation.Idle),
        observation.Outcome.DeterminismFingerprint,
    };

    private static object EvidenceSample(MovementSample sample) => new
    {
        Position = new { sample.Position.X, sample.Position.Y, sample.Position.Z },
        Rotation = new { sample.Rotation.X, sample.Rotation.Y, sample.Rotation.Z, sample.Rotation.W },
        sample.ModelId,
        sample.Speed,
        sample.Delta,
        sample.Consumed,
    };

    private static Observation Observe(TestScenario scenario)
    {
        var tickRate = scenario.ResolveExecution().TickRate;
        var anchorTick = tickRate * 2;
        var firstSkillMotion = -1;
        var lastSkillMotion = -1;
        var boundActor = (Id: 0, Lifetime: 0);
        var samples = new List<MovementSample>(4);
        var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario, (world, tick) =>
        {
            if (tick > anchorTick + 2) return;
            var actor = world.GetLocalMovementActor("caster");
            if (!actor.hasMotion || !actor.motion.Initialized || !actor.hasTransform)
            {
                Require(tick < anchorTick - 1, "The movement projection requires initialized formal motion.");
                return;
            }

            var sources = actor.motion.Pipeline.CaptureSourceOrder();
            var activeSkillMotion = sources.Any(source => source.IsActive && source is not LocomotionMotionSource);
            if (activeSkillMotion)
            {
                if (firstSkillMotion < 0) firstSkillMotion = tick;
                lastSkillMotion = tick;
            }

            if (tick < anchorTick - 1) return;
            var actorId = actor.actorId.Value;
            var lifetime = world.GetActorLifetime(actorId);
            Require(actorId > 0 && lifetime > 0 && world.TryGetPlayerId("caster", out var controller) && controller.Value == "player_1",
                "The fixture requires its locally controlled actor and a stable lifetime.");
            if (tick == anchorTick - 1) boundActor = (actorId, lifetime);
            Require(boundActor == (actorId, lifetime) && !activeSkillMotion && world.CanMove(actorId),
                "Only settled, uncontrolled locomotion in the same actor lifetime is supported.");

            var speed = new MobaAttrs(actor).MoveSpeed;
            var position = actor.transform.Value.Position;
            Require(float.IsFinite(speed) && speed > 0 && float.IsFinite(position.X) && float.IsFinite(position.Y) && float.IsFinite(position.Z),
                "The movement projection requires finite position and positive movement speed.");

            var consumed = tick == anchorTick ? Stop : tick == anchorTick + 1 ? Back : (WalkInput?)null;
            if (tick >= anchorTick)
            {
                var locomotion = sources.OfType<LocomotionMotionSource>().Single();
                Require(actor.hasMoveInput && actor.moveInput.Dx == 0f && actor.moveInput.Dz == (consumed?.Z ?? 0f)
                    && locomotion.Space == MotionInputSpace.World && locomotion.Speed == speed
                    && locomotion.Input.Equals(new Vec3(0f, 0f, consumed?.Z ?? 0f)), "The formal motion system did not consume the scheduled local input.");
            }

            Require(actor.hasModelId && actor.modelId.Value > 0, "The formal actor is missing its model binding.");
            samples.Add(new MovementSample(position, actor.transform.Value.Rotation, actor.modelId.Value,
                speed, world.FixedDelta, consumed));
            if (tick < anchorTick + 2)
                actor.ReplaceMoveInput(0f, tick == anchorTick ? Back.Z : 0f);
        });

        Require(outcome.Result.Passed, "Formal skill assertions failed: " + outcome.Result.Summary);
        Require(samples.Count == 4 && firstSkillMotion >= 0 && lastSkillMotion < anchorTick - 1,
            "Expected to observe a live skill motion and its completion before the cut.");
        var before = samples[0];
        var cut = samples[1];
        var moved = samples[2];
        var idle = samples[3];
        Require(Near(before.Position, cut.Position) && Near(moved.Position, idle.Position)
            && before.ModelId == cut.ModelId && cut.ModelId == moved.ModelId && moved.ModelId == idle.ModelId
            && cut.Rotation.Equals(moved.Rotation) && moved.Rotation.Equals(idle.Rotation)
            && cut.Speed == moved.Speed && cut.Speed == idle.Speed && cut.Delta == moved.Delta && cut.Delta == idle.Delta,
            "The cut must be stationary with constant locomotion parameters and explicit neutral input on the empty Tick.");
        Require(!Near(cut.Position, moved.Position) && Near(Step(cut.Projection, Back).Position, moved.Position),
            "The isolated free-space movement step disagrees with the formal world oracle.");
        return new Observation(tickRate, anchorTick, boundActor, firstSkillMotion, lastSkillMotion, cut, moved, idle, outcome);
    }

    private static Artifact Record(string variant, Observation authority, Observation old, ReconciliationStatus expected)
    {
        var isOnTime = expected == ReconciliationStatus.AuthoritativeOnly;
        var tick = authority.AnchorTick;
        var deliveryTick = isOnTime ? tick : tick + 2;
        var oldPosition = isOnTime ? old.Cut.Position : old.Idle.Position;
        var oldProjection = isOnTime ? old.Cut.Projection : old.Idle.Projection;
        Require(authority.BoundActor == old.BoundActor && old.AnchorTick == tick && old.TickRate == authority.TickRate,
            "Observed local inputs and the authority baseline must address the same fixture participant.");
        Require(authority.Cut.ModelId == old.Cut.ModelId, "Both projections must use the observed model binding.");
        var scope = new ReconciliationScope(1, 1, new Guid("b5781238-8a8c-4241-a64e-efde0020c917"),
            authority.BoundActor.Id, authority.BoundActor.Lifetime, 1, 1);
        var steps = new List<LocalStateSyncReplay.ReplayStep>();
        var replayed = new List<long>();
        var reconciler = new LocalMovementReconciler<MovementCut, WalkInput>(scope, Replay);

        Register(tick, old.Cut);
        if (!isOnTime)
        {
            Register(tick + 1, old.Moved);
            if (expected != ReconciliationStatus.MissingTickCoverage)
                Register(tick + 2, old.Idle);
        }

        MovementCut? imported = null;
        var imports = 0;
        var pipeline = new ClientSnapshotSyncPipeline<LocalSnapshot>(new ClientSnapshotSyncOptions<LocalSnapshot>(1, 1,
            (in LocalSnapshot value) => value.Envelope, (in LocalSnapshot value) =>
            {
                imported = value.Cut;
                imports++;
            }));
        var snapshot = new LocalSnapshot(new SnapshotStreamEnvelope(1, 1, 1, tick, SnapshotStreamSnapshotKind.FullBaseline,
            tick, 1, 1), authority.Cut.Projection);
        var admission = pipeline.Apply(in snapshot);
        Require(admission.Applied && imported.HasValue && imports == 1, "The typed full baseline was not applied exactly once.");
        var stream = pipeline.State;
        var movement = new CommittedMovement<MovementCut>(scope, stream.LastAppliedSequence, stream.LastAppliedFrame,
            imported.GetValueOrDefault(), isFullBaseline: true);

        InputTerminalProof? proof = null;
        if (expected != ReconciliationStatus.InvalidTerminalProof)
        {
            var terminal = authority.Cut.Consumed ?? throw new InvalidOperationException("No authority input receipt at the cut.");
            Require(terminal == Stop && authority.Moved.Consumed == Back, "The authority cut must contain only the terminal stop command.");
            proof = new InputTerminalProof(scope, movement.Revision, movement.Tick, terminal.Sequence);
        }

        var reconciliation = reconciler.Reconcile(in movement, proof, deliveryTick);
        Require(reconciliation.Status == expected, $"Expected {expected}, got {reconciliation.Status}: {reconciliation.StepException}");
        Require(reconciler.TryGetAuthoritative(out var anchor) && anchor == authority.Cut.Projection,
            "Reconciliation must preserve the committed authority position.");
        var predicted = reconciler.TryGetPrediction(out var corrected);
        if (expected == ReconciliationStatus.Predicted)
        {
            Require(predicted && replayed.SequenceEqual(new[] { Back.Sequence }) && steps.Count == 2,
                "Only the pending move may replay; the following covered empty Tick must not repeat it.");
            Require(Near(corrected.Position, authority.Idle.Position) && !Near(corrected.Position, oldPosition),
                "Correction must match the independent authority movement oracle and differ from the old prediction.");
        }
        else
        {
            Require(!predicted && steps.Count == 0 && replayed.Count == 0, "No local step may run in an authority-only branch.");
            if (isOnTime) Require(Near(oldPosition, anchor.Position), "On-time confirmation must agree with the old prediction.");
        }

        var artifact = new LocalStateSyncReplay
        {
            CaseId = CaseId,
            Variant = variant,
            TickRate = authority.TickRate,
            PositionLabel = "MOBA 世界 Z 轴（每 1000 格 = 1 世界单位）",
            ActorModelId = authority.Cut.ModelId,
            Checkpoints = new[]
            {
                new LocalStateSyncReplay.Checkpoint
                {
                    Command = "旧预测（正式世界）", Generation = 1, DeliveryTick = deliveryTick, AppliedTick = -1, BaselineTick = -1,
                    Prediction = Display(oldPosition), Pending = isOnTime ? new[] { Stop.Sequence } : new[] { Stop.Sequence, Back.Sequence },
                    PredictionPose = Pose(oldProjection),
                    Steps = Array.Empty<LocalStateSyncReplay.ReplayStep>(),
                },
                new LocalStateSyncReplay.Checkpoint
                {
                    Command = "交付 E 结束后的权威落点", Generation = 1, DeliveryTick = deliveryTick,
                    Admission = admission.Status.ToString(), Validation = admission.Validation.Status.ToString(), Reconciliation = expected.ToString(),
                    Sequence = stream.LastAppliedSequence, AppliedTick = stream.LastAppliedFrame, BaselineTick = stream.LastBaselineFrame,
                    PublishedRevision = movement.Revision, ReconciledRevision = reconciler.AcceptedRevision, TerminalSequence = reconciler.TerminalSequence,
                    Imports = imports, PublishedPosition = Display(anchor.Position), Authority = Display(anchor.Position),
                    Prediction = predicted ? Display(corrected.Position) : null,
                    AuthorityPose = Pose(anchor), PredictionPose = predicted ? Pose(corrected) : null,
                    Pending = Enumerable.Range(0, reconciler.PendingCount).Select(reconciler.GetPendingSequence).ToArray(),
                    ReplayTicks = steps.Select(step => step.Tick).ToArray(), Steps = steps.ToArray(),
                },
            },
        };
        var json = artifact.Serialize();
        var frames = LocalStateSyncReplay.Parse(json).BuildTimeline().Frames;
        Require(frames.Count == (predicted ? 5 : 2) && frames[0].Point.Prediction == Display(oldPosition),
            "The editor timeline must start at the observed old prediction.");
        return new Artifact($"{CaseId}.{variant}.statesync-replay.json", json);

        void Register(int inputTick, MovementSample observed)
        {
            if (observed.Consumed is { } input)
                Require(reconciler.TryRecordInput(scope, input.Sequence, inputTick, input), "Unable to retain observed local input.");
            Require(reconciler.SealTick(scope, inputTick), "Unable to seal local input Tick.");
        }

        bool Replay(int replayTick, ReadOnlySpan<WalkInput> inputs, ref MovementCut candidate)
        {
            if (inputs.Length > 1) return false;
            var before = candidate.Position;
            if (!inputs.IsEmpty)
            {
                replayed.Add(inputs[0].Sequence);
                candidate = Step(candidate, inputs[0]);
            }
            steps.Add(new LocalStateSyncReplay.ReplayStep
            {
                Tick = replayTick, Before = Display(before), After = Display(candidate.Position), AfterPose = Pose(candidate),
            });
            return true;
        }
    }

    private static MovementCut Step(MovementCut cut, WalkInput input)
    {
        var source = new LocomotionMotionSource(cut.Speed, MotionInputSpace.World);
        source.SetInput(0f, input.Z);
        var state = new MotionState(cut.Position);
        var delta = Vec3.Zero;
        source.Tick(0, ref state, cut.Delta, ref delta);
        return cut with { Position = cut.Position + delta };
    }

    private static bool Near(Vec3 left, Vec3 right) => (left - right).SqrMagnitude <= Tolerance * Tolerance;
    private static int Display(Vec3 position) => checked((int)MathF.Round(position.Z * 1000f));
    private static LocalStateSyncReplay.ActorPose Pose(MovementCut cut) => new()
    {
        X = cut.Position.X, Y = cut.Position.Y, Z = cut.Position.Z,
        Qx = cut.Rotation.X, Qy = cut.Rotation.Y, Qz = cut.Rotation.Z, Qw = cut.Rotation.W,
    };

    private static void Require(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
    }

    private readonly record struct WalkInput(long Sequence, float Z);
    private readonly record struct MovementCut(Vec3 Position, Quat Rotation, float Speed, float Delta);
    private readonly record struct MovementSample(Vec3 Position, Quat Rotation, int ModelId, float Speed, float Delta, WalkInput? Consumed)
    {
        internal MovementCut Projection => new(Position, Rotation, Speed, Delta);
    }
    private readonly record struct LocalSnapshot(SnapshotStreamEnvelope Envelope, MovementCut Cut);
    private sealed record Observation(int TickRate, int AnchorTick, (int Id, int Lifetime) BoundActor, int FirstSkillMotionTick, int LastSkillMotionTick,
        MovementSample Cut, MovementSample Moved, MovementSample Idle, MobaBattleScenarioRunOutcome Outcome);
    private sealed record Artifact(string Name, string Contents);
}
