using AbilityKit.Ability.StateSync.Prediction.Reconciliation;
using AbilityKit.BattleScenario;
using AbilityKit.BattleScenario.Editor;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Acceptance.LiveSim;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Scenario;
using Host = AbilityKit.Demo.Moba.BattleScenario.Runner.LocalStateSyncMovementHost;

namespace AbilityKit.Demo.Moba.BattleScenario.Runner;

/// <summary>
/// 正式战斗世界各跑一次穿墙和绕开墙；墙、减益均结束后才交付隔离的普通移动截面。
/// </summary>
internal static class KarthusWallWalkReplay
{
    internal const string SampleName = "karthus-w-jinx-walk.settled-correction";
    private const int WallSkill = 30060301;
    private const int WallArea = 42060301;
    private const int WallDebuff = 30060391;

    internal static LocalStateSyncReplay Record()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Flows", "PredictionPrerequisites", "karthus-w-jinx-walk.battlescenario");
        var document = BattleScenarioCodec.Load(path);
        var contact = BattleScenarioCompiler.Compile(document);
        var cast = document.Sections.Timeline.OfType<TimelineStepBlock>().Single();
        var point = cast.Position ?? throw new InvalidOperationException("Expected a wall placement.");
        cast.Position = new TestVector3(point.X + 8f, point.Y, point.Z);
        var clear = BattleScenarioCompiler.Compile(document);
        var rate = contact.ResolveExecution().TickRate;
        var start = rate;
        var stop = rate * 5 / 2;
        var cut = rate * 8;
        var delivery = cut + 2;
        var authority = Observe(contact, start, stop, cut);
        var prediction = Observe(clear, start, stop, cut);
        var firstSlow = Enumerable.Range(start, stop - start).FirstOrDefault(tick => authority[tick].Slowed);
        Require(firstSlow > start && prediction.Values.All(sample => !sample.Slowed),
            "Only the crossing path should acquire the formal wall debuff.");
        var originalSpeed = prediction[start].Speed;
        Require(authority[start].Speed == originalSpeed && SamePosition(authority[start].State, prediction[start].State) &&
            authority[firstSlow].Speed < originalSpeed && authority[stop].State.Pose.Position.Z > point.Z + 0.1f &&
            prediction[stop].State.Pose.Position.Z - authority[stop].State.Pose.Position.Z > 0.25f,
            "The same forward input must pass through the wall, but reach a shorter distance in the slowed world.");
        Require(!authority[cut - 1].Slowed && authority[cut].Speed == originalSpeed &&
            SamePosition(authority[cut - 1].State, authority[cut].State) &&
            Enumerable.Range(cut, 4).All(tick => !authority[tick].Slowed && authority[tick].Speed == originalSpeed &&
                prediction[tick].Speed == originalSpeed), "Locomotion recovery requires a settled constant-speed interval.");

        var oldCut = prediction[cut];
        using var host = new Host(1f / rate, initial: prediction[cut - 1].State, moveSpeed: originalSpeed);
        host.Advance(new Host.Input(1, 0, 0));
        Require(SamePosition(host.Current, oldCut.State) && authority[cut].State.MoveZ == 0f,
            "The baseline must contain the accepted stop input.");
        host.Advance(new Host.Input(2, 0, 1));
        host.Advance();
        Require(SamePosition(host.Current, prediction[delivery].State), "The local host must reproduce the old prediction.");

        var snapshot = new Host.Snapshot(1, 1, authority[cut].State, TerminalThrough: 1);
        var result = host.Receive(in snapshot) ?? throw new InvalidOperationException("The local snapshot was not applied.");
        Require(result.Status == ReconciliationStatus.Predicted && result.Comparison == Host.Comparison.Mismatch &&
            result.RebasedLiveWorld && host.IsPredicting && host.StateTick == 3 && host.TerminalSequence == 1 &&
            host.PendingSequences.SequenceEqual(new long[] { 2 }) && result.Steps.Length == 2 &&
            result.Steps[0].InputCount == 1 && result.Steps[1].InputCount == 0 &&
            result.Steps.All(step => SamePosition(step.After, authority[cut + step.Tick - 1].State)) &&
            SamePosition(host.Current, authority[delivery].State) && !SamePosition(host.Current, prediction[delivery].State),
            "Replayed ticks must match the independent authority before publishing a different prediction.");

        var checkpoints = new List<LocalStateSyncReplay.Checkpoint>();
        for (var tick = start; tick <= stop; tick++)
        {
            if (tick != firstSlow && tick != stop && (tick - start) % 2 != 0) continue;
            var sample = authority[tick];
            var label = tick < firstSlow ? "同输入 · 两路同步向前" :
                FormattableString.Invariant($"穿墙可通行 · 权威减速 {sample.Speed:0.00} / {prediction[tick].Speed:0.00} m/s");
            var observation = Point(label, tick, prediction[tick].State);
            IncludeReference(observation, sample.State);
            checkpoints.Add(observation);
        }

        var settled = Point("跳至墙和减益均已结束 · 等待权威快照", cut, oldCut.State);
        IncludeReference(settled, authority[cut].State);
        checkpoints.Add(settled);
        var beforeDelivery = Point("快照到达前 · 旧预测继续前进", delivery, prediction[delivery].State);
        IncludeReference(beforeDelivery, authority[delivery].State);
        checkpoints.Add(beforeDelivery);

        var corrected = Point("交付已结束减速的权威快照 · 重演两 Tick 后切换", delivery, host.Current);
        IncludeCommit(corrected);
        corrected.Admission = "Applied";
        corrected.Reconciliation = result.Status.ToString();
        corrected.ComparisonTick = cut;
        corrected.Steps = result.Steps.Select(step => new LocalStateSyncReplay.ReplayStep
        {
            Tick = cut + step.Tick - 1, Before = Display(step.Before), After = Display(step.After), AfterPose = Pose(step.After),
        }).ToArray();
        corrected.ReplayTicks = corrected.Steps.Select(step => step.Tick).ToArray();
        checkpoints.Add(corrected);

        host.Advance();
        Require(host.IsPredicting && SamePosition(host.Current, authority[delivery + 1].State),
            "Prediction must continue from the published candidate with the retained input.");
        var continued = Point("追平后继续预测 · 沿用向前输入", delivery + 1, host.Current);
        IncludeCommit(continued);
        checkpoints.Add(continued);

        return new LocalStateSyncReplay
        {
            CaseId = "karthus-w-jinx-walk", Variant = "settled-correction", TickRate = rate,
            PositionLabel = "Z · 毫米", ActorModelId = authority[cut].ModelId, Checkpoints = checkpoints.ToArray(),
        };

        void IncludeCommit(LocalStateSyncReplay.Checkpoint checkpoint)
        {
            IncludeReference(checkpoint, result.Authority);
            checkpoint.Sequence = checkpoint.ReconciledRevision = snapshot.Revision;
            checkpoint.PublishedRevision = snapshot.Revision;
            checkpoint.AppliedTick = checkpoint.BaselineTick = cut;
            checkpoint.TerminalSequence = host.TerminalSequence;
            checkpoint.Imports = 1;
            checkpoint.PublishedPosition = checkpoint.Authority;
            checkpoint.Pending = host.PendingSequences;
        }
    }

    private static Dictionary<int, Sample> Observe(TestScenario scenario, int start, int stop, int cut)
    {
        var samples = new Dictionary<int, Sample>();
        var identity = (Id: 0, Lifetime: 0);
        var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario, (world, tick) =>
        {
            if (tick < start - 1 || tick > cut + 3) return;
            var actor = world.GetLocalMovementActor("runner");
            var bound = (actor.actorId.Value, world.GetActorLifetime(actor.actorId.Value));
            if (tick == start - 1)
            {
                identity = bound;
                actor.ReplaceMoveInput(0f, 1f);
                return;
            }
            var movement = tick < stop || tick > cut ? 1f : 0f;
            Require(bound == identity && identity.Lifetime > 0 && actor.hasMotion && actor.motion.Initialized &&
                actor.hasMoveInput && actor.moveInput.Dx == 0f && actor.moveInput.Dz == movement && world.CanMove(bound.Item1),
                $"The formal input or actor lifetime diverged at Tick {tick}.");
            var slowed = actor.hasBuffs && actor.buffs.Active != null &&
                actor.buffs.Active.Any(buff => buff != null && buff.BuffId == WallDebuff);
            samples.Add(tick, new Sample(new Host.State(actor.transform.Value, actor.moveInput.Dx, actor.moveInput.Dz),
                new MobaAttrs(actor).MoveSpeed, slowed, actor.modelId.Value));
            if (tick == stop - 1 || tick == cut - 1 || tick == cut)
                actor.ReplaceMoveInput(0f, tick == cut ? 1f : 0f);
        });
        Require(outcome.Result.Passed && outcome.TraceNodes.Count(node => node.ConfigId == WallSkill && node.Kind == "SkillCast") == 1 &&
            outcome.TraceNodes.Count(node => node.ConfigId == WallArea && node.Kind == "AreaSpawn") == 1 &&
            outcome.TraceNodes.Count(node => node.ConfigId == WallArea && node.Kind == "AreaExpire" && node.Frame < cut) == 1,
            "Both runs must cast the formal skill, spawn one wall, and expire it before recovery. " + outcome.Result.Summary +
            " Skill/area evidence: " + string.Join(", ", outcome.TraceNodes.Where(node => node.Kind is "SkillCast" or "AreaSpawn" or "AreaExpire")
                .Select(node => $"{node.Kind} {node.ConfigId}@{node.Frame}")));
        return samples;
    }

    private static LocalStateSyncReplay.Checkpoint Point(string command, int tick, Host.State state) => new()
    {
        Command = command, Generation = 1, DeliveryTick = tick, AppliedTick = -1, BaselineTick = -1,
        Prediction = Display(state), PredictionPose = Pose(state), Steps = Array.Empty<LocalStateSyncReplay.ReplayStep>(),
    };

    private static void IncludeReference(LocalStateSyncReplay.Checkpoint point, Host.State state)
    {
        point.Authority = Display(state);
        point.AuthorityPose = Pose(state);
    }

    private static bool SamePosition(Host.State left, Host.State right) =>
        (left.Pose.Position - right.Pose.Position).SqrMagnitude < 1e-8f;

    private static int Display(Host.State state) => checked((int)MathF.Round(state.Pose.Position.Z * 1000f));

    private static LocalStateSyncReplay.ActorPose Pose(Host.State state) => new()
    {
        X = state.Pose.Position.X, Y = state.Pose.Position.Y, Z = state.Pose.Position.Z,
        Qx = state.Pose.Rotation.X, Qy = state.Pose.Rotation.Y, Qz = state.Pose.Rotation.Z, Qw = state.Pose.Rotation.W,
    };

    private static void Require(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
    }

    private readonly record struct Sample(Host.State State, float Speed, bool Slowed, int ModelId);
}
