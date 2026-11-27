using AbilityKit.BattleScenario;
using AbilityKit.BattleScenario.Editor;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Acceptance.LiveSim;
using AbilityKit.Demo.Moba.BattleScenario;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Protocol.Moba;
using AbilityKit.Scenario;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace AbilityKit.Demo.Moba.BattleScenario.Runner;

/// <summary>
/// 离线验证两条正式技能输入如何围绕权威截面终结、比较和追平。
/// 仅重建固定配方的干净锚点，不提供正式技能状态导入或预测资格。
/// </summary>
internal static class AnnieSkillStateReplay
{
    private const int ConeSkillId = 30030301;
    private const int ShieldSkillId = 30030401;
    private const long TerminalSequence = 1;
    private const string BusyFailure = "skill.start.alreadyRunning";

    internal static void Export(string directory)
    {
        var recordings = new[] { Record(accepted: true), Record(accepted: false) };
        var settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
        };
        Directory.CreateDirectory(directory);
        foreach (var recording in recordings)
        {
            var path = Path.Combine(directory, $"annie-w-e-local-skill.{recording.Variant}.json");
            File.WriteAllText(path, JsonConvert.SerializeObject(recording, settings) + "\n");
            System.Console.WriteLine(path);
            var playback = ToPlayback(recording);
            var playbackPath = Path.Combine(directory, $"{playback.CaseId}.{playback.Variant}.statesync-replay.json");
            File.WriteAllText(playbackPath, playback.Serialize() + "\n");
            System.Console.WriteLine(playbackPath);
        }
    }

    internal static LocalStateSyncReplay ToPlayback(Recording recording) => new()
    {
        Version = LocalStateSyncReplay.SkillTrackVersion,
        CaseId = "annie-w-e-local-skill",
        Variant = recording.Variant,
        TickRate = recording.TickRate,
        SkillDelivery = new LocalStateSyncReplay.SkillDeliveryObservation
        {
            PrimaryLabel = "安妮 W", SecondaryLabel = "安妮 E",
            FirstInputTick = recording.FirstCastTick,
            AnchorTick = recording.AnchorTick, InputTick = recording.PendingInputTick, DeliveryTick = recording.DeliveryTick,
            TerminalThroughSequence = recording.TerminalThroughSequence,
            PendingSequence = recording.PendingSequences.Single(),
            ReplayedSequence = recording.ReplayedSequences.Length == 0 ? null : recording.ReplayedSequences.Single(),
            OldInputRejectCode = recording.OldShieldRejectCode,
            Steps = recording.ReplaySteps.Select(step => new LocalStateSyncReplay.SkillReplayStep
            {
                Tick = step.Tick, State = ToPlaybackState(step.State),
            }).ToArray(),
        },
        Checkpoints = recording.Ticks.Select(tick => new LocalStateSyncReplay.Checkpoint
        {
            Command = "skill",
            DeliveryTick = tick.Tick,
            Skill = new LocalStateSyncReplay.SkillTickObservation
            {
                Authority = ToPlaybackState(tick.Authority), OldPrediction = ToPlaybackState(tick.OldPrediction),
                Published = ToPlaybackState(tick.Published),
            },
        }).ToArray(),
    };

    private static LocalStateSyncReplay.SkillState ToPlaybackState(SkillState state) => new()
    {
        ManaRaw = state.ManaRaw, TargetHealthRaw = state.TargetHealthRaw,
        PrimaryCooldownEndMs = state.ConeCooldownEndMs, SecondaryCooldownEndMs = state.ShieldCooldownEndMs,
    };

    internal static Recording Record(bool accepted)
    {
        var fixtures = Path.Combine(AppContext.BaseDirectory, "Flows", "SkillPresentation");
        var coneCase = BattleScenarioCompiler.CompileFile(Path.Combine(fixtures, "annie-w.battlescenario"));
        var shieldCase = BattleScenarioCompiler.CompileFile(Path.Combine(fixtures, "annie-e.battlescenario"));
        var shield = shieldCase.Timeline.Single();
        var inputs = new[]
        {
            new SkillInput(1, coneCase.Timeline.Single()),
            new SkillInput(2, new TestTimelineStep
            {
                AtMs = 2400, Action = shield.Action, ActorAlias = shield.ActorAlias,
                Slot = shield.Slot, TargetAlias = shield.TargetAlias,
            }),
        };

        var old = Observe(coneCase, inputs.Select(input => input.Step), allowBusyShield: true);
        var coneTick = CastTick(old, ConeSkillId);
        var shieldTick = old.Attempts[1].Tick;
        var anchorTick = coneTick + (shieldTick - coneTick) / 2;
        var deliveryTick = shieldTick + 3;
        var terminal = inputs.Where(input => input.Sequence <= TerminalSequence).ToArray();
        var committed = accepted ? terminal : Array.Empty<SkillInput>();
        var authority = Observe(coneCase, committed.Select(input => input.Step));
        var authorityCut = authority.At(anchorTick);
        var oldAtCut = old.At(anchorTick);
        var first = old.At(old.FirstTick);
        Require(coneTick < anchorTick && anchorTick < shieldTick && oldAtCut.ConeCooldownEndMs > 0 &&
            oldAtCut.ManaRaw < first.ManaRaw && oldAtCut.TargetHealthRaw < first.TargetHealthRaw &&
            oldAtCut.SecondTargetHealthRaw < first.SecondTargetHealthRaw &&
            oldAtCut.OutsideTargetHealthRaw == first.OutsideTargetHealthRaw &&
            oldAtCut.ShieldCooldownEndMs == 0, "The cut must observe W cost, cooldown and damage before E.");
        var requiresReplay = oldAtCut != authorityCut;
        Require(requiresReplay == !accepted, "The typed authority cut must match only in the confirmed branch.");
        RequireSingleConeHitPerTarget(old, anchorTick);
        if (accepted)
            RequireSingleConeHitPerTarget(authority, anchorTick);
        else
            Require(authority.ConeHits.Length == 0, "Rejected W must be absent from the authority track.");

        var pending = inputs.Where(input => input.Sequence > TerminalSequence).ToArray();
        var replayedTicks = requiresReplay ? Enumerable.Range(anchorTick + 1, deliveryTick - anchorTick).ToArray() : Array.Empty<int>();
        var published = old;
        if (requiresReplay)
        {
            /*
             * 该配方的拒绝锚点没有已生效的技能。以相同种子重建隔离世界，
             * 只安排权威已生效及未终结输入；先验证锚点，再从候选采集重演结果。
             */
            published = Observe(coneCase, committed.Concat(pending).Select(input => input.Step), (anchorTick, authorityCut));
            Require(Count(published, ConeSkillId, "SkillCast") == 0 && published.ConeHits.Length == 0,
                "Terminal rejection must not reissue W or retain predicted damage.");
            Require(published.Attempts.Single().Tick == shieldTick && CastTick(published, ShieldSkillId) >= shieldTick &&
                Count(published, ShieldSkillId, "EffectExecution") > 0,
                "Pending E must be retried at its original input tick and execute its real effect once admitted.");
            Require(replayedTicks.All(tick => SameTargetHealth(published.At(tick), authority.At(tick))) &&
                published.At(deliveryTick).ConeCooldownEndMs == authorityCut.ConeCooldownEndMs &&
                published.At(deliveryTick).ShieldCooldownEndMs > 0 &&
                published.At(deliveryTick).ManaRaw > old.At(deliveryTick).ManaRaw,
                "The corrected track must discard W cost, cooldown and damage without discarding E.");
        }

        var ticks = Enumerable.Range(coneTick - 1, deliveryTick - coneTick + 3).Select(tick => new TickObservation(
            tick, authority.At(tick), old.At(tick), tick < deliveryTick ? old.At(tick) : published.At(tick))).ToArray();
        return new Recording(accepted ? "confirmed" : "rejected", coneCase.ResolveExecution().TickRate,
            coneTick, anchorTick, deliveryTick, shieldTick, TerminalSequence,
            pending.Select(input => input.Sequence).ToArray(), requiresReplay ? pending.Select(input => input.Sequence).ToArray() : Array.Empty<long>(),
            replayedTicks.Select(tick => new ReplayStep(tick, published.At(tick))).ToArray(), ticks,
            old.Attempts[1].RejectCode, old.ConeHits, published.ConeHits,
            published.Attempts.Count(attempt => attempt.Slot == 3 && attempt.RejectCode == null));
    }

    private static void RequireSingleConeHitPerTarget(Observation track, int anchorTick)
    {
        Require(track.ConeHits.Length == 2 && track.ConeHits.Count(hit => hit.Target == "target") == 1 &&
            track.ConeHits.Count(hit => hit.Target == "second") == 1 && track.ConeHits.All(hit => hit.Tick <= anchorTick),
            "Expected one W damage settlement per inside target before the anchor, and none elsewhere. Observed: " +
            string.Join(", ", track.ConeHits));
    }

    private static bool SameTargetHealth(SkillState left, SkillState right) =>
        left.TargetHealthRaw == right.TargetHealthRaw && left.SecondTargetHealthRaw == right.SecondTargetHealthRaw &&
        left.OutsideTargetHealthRaw == right.OutsideTargetHealthRaw;

    private static Observation Observe(TestScenario recipe, IEnumerable<TestTimelineStep> timeline,
        (int Tick, SkillState State)? expectedCut = null, bool allowBusyShield = false)
    {
        var execution = recipe.ResolveExecution();
        var schedule = timeline.Select(step => (Offset: (step.AtMs * execution.TickRate + 999) / 1000, Step: step)).ToArray();
        var scenario = new TestScenario
        {
            CaseId = recipe.CaseId, Carrier = recipe.Carrier, WorldProfileId = recipe.WorldProfileId,
            Seed = recipe.Seed, WorldParameters = recipe.WorldParameters, Actors = recipe.Actors,
            Setup = recipe.Setup, NavigationProfileId = recipe.NavigationProfileId,
            EnvironmentProfileId = recipe.EnvironmentProfileId, Obstacles = recipe.Obstacles,
            Execution = new TestExecutionSpec
            {
                TickRate = execution.TickRate, MaxDurationMs = execution.MaxDurationMs,
                EndCondition = new TestEndCondition { Kind = TestEndConditionKinds.Duration, DurationMs = 3400 },
                SettleDurationMs = 0,
            },
        };
        var states = new SortedDictionary<int, SkillState>();
        var attempts = new List<InputAttempt>(schedule.Length);
        var actors = new Dictionary<long, string>();
        int casterId = 0;
        int? initialTick = null;
        var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario, (world, tick) =>
        {
            if (initialTick == null)
            {
                Require(world.TryGetActorId("caster", out casterId), "Missing fixture caster.");
                foreach (var alias in new[] { "target", "second", "outside" })
                {
                    Require(world.TryGetActorId(alias, out var id), $"Missing fixture target: {alias}.");
                    actors.Add(id, alias);
                }
            }
            initialTick ??= tick - 1;
            foreach (var input in schedule)
            {
                if (tick - initialTick.Value != input.Offset) continue;
                var result = Dispatch(world, input.Step);
                Require(result.Success || (allowBusyShield && input.Step.Slot == 3 && result.Code == BusyFailure),
                    $"Typed input for slot {input.Step.Slot} failed: {result.Code} {result.Failure.Message ?? result.Message}.");
                attempts.Add(new InputAttempt(tick, input.Step.Slot, result.Success ? null : result.Code));
            }

            var caster = world.GetLocalMovementActor("caster");
            var target = world.GetLocalMovementActor("target");
            var secondTarget = world.GetLocalMovementActor("second");
            var outsideTarget = world.GetLocalMovementActor("outside");
            var loadout = caster.skillLoadout.ActiveSkills;
            Require(loadout[1].SkillId == ConeSkillId && loadout[2].SkillId == ShieldSkillId,
                "The fixture requires the published W and E in slots 2 and 3.");
            var state = new SkillState(caster.resourceContainer.Value.Map[ResourceType.Mana].Current.RawValue,
                target.resourceContainer.Value.Map[ResourceType.Hp].Current.RawValue,
                loadout[1].CooldownEndTimeMs, loadout[2].CooldownEndTimeMs,
                secondTarget.resourceContainer.Value.Map[ResourceType.Hp].Current.RawValue,
                outsideTarget.resourceContainer.Value.Map[ResourceType.Hp].Current.RawValue);
            states.Add(tick, state);
            if (expectedCut is { } cut && cut.Tick == tick)
                Require(state == cut.State, "The rebuilt candidate did not reproduce the authority cut.");
        });
        Require(outcome.Result.Passed && states.Count > 0 && attempts.Count == schedule.Length, outcome.Result.Summary);
        Require(expectedCut == null || states.ContainsKey(expectedCut.Value.Tick), "The candidate missed the authority cut.");
        var coneHits = outcome.TraceRecords.Where(record => record.kind == "DamageApply" && record.configId == ConeSkillId)
            .Select(record =>
            {
                Require(record.sourceActorId == casterId, $"Unexpected W source actor {record.sourceActorId}.");
                return new ConeHit(record.frame, actors.TryGetValue(record.targetActorId, out var alias)
                    ? alias : $"actor:{record.targetActorId}");
            }).ToArray();
        return new Observation(states, outcome.TraceNodes, attempts.ToArray(), coneHits);
    }

    private static MobaSkillInputHandleResult Dispatch(LiveSimSetupActionExecutor world, TestTimelineStep step)
    {
        Require(step.Action == "cast_skill" && step.ActorAlias == "caster", "Expected a local press from the fixture caster.");
        if (!world.TryGetActorId("caster", out var actorId))
            throw new InvalidOperationException("The typed skill input requires a caster.");
        var targetId = 0;
        if (step.TargetAlias != null && !world.TryGetActorId(step.TargetAlias, out targetId))
            throw new InvalidOperationException($"Unknown skill target {step.TargetAlias}.");
        var position = ToVec3(step.Position);
        var direction = ToVec3(step.Direction);
        var input = new SkillInputEvent(step.Slot, SkillInputPhase.Press, targetActorId: targetId, aimPos: in position, aimDir: in direction);
        return world.Services.Resolve<SkillCastCoordinator>().TryHandleInputResult(actorId, in input);
    }

    private static Vec3 ToVec3(TestVector3? value) => value is { } point ? new Vec3(point.X, point.Y, point.Z) : default;

    private static int CastTick(Observation observed, int skillId) => observed.Trace.Single(node =>
        node.ConfigId == skillId && node.Kind == "SkillCast").Frame;

    private static int Count(Observation observed, int skillId, string kind) => observed.Trace.Count(node =>
        node.ConfigId == skillId && node.Kind == kind);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed record SkillInput(long Sequence, TestTimelineStep Step);
    private sealed record InputAttempt(int Tick, int Slot, string? RejectCode);

    private sealed record Observation(SortedDictionary<int, SkillState> States, BattleScenarioTraceNode[] Trace,
        InputAttempt[] Attempts, ConeHit[] ConeHits)
    {
        internal int FirstTick => States.Keys.First();
        internal SkillState At(int tick) => States.TryGetValue(tick, out var state) ? state :
            throw new InvalidOperationException($"Missing fixed tick {tick} in the skill sample.");
    }

    internal readonly record struct SkillState(long ManaRaw, long TargetHealthRaw, long ConeCooldownEndMs,
        long ShieldCooldownEndMs, long SecondTargetHealthRaw, long OutsideTargetHealthRaw);
    internal sealed record ConeHit(int Tick, string Target);
    internal sealed record ReplayStep(int Tick, SkillState State);
    internal sealed record TickObservation(int Tick, SkillState Authority, SkillState OldPrediction, SkillState Published);
    internal sealed record Recording(string Variant, int TickRate, int FirstCastTick, int AnchorTick, int DeliveryTick,
        int PendingInputTick, long TerminalThroughSequence, long[] PendingSequences, long[] ReplayedSequences,
        ReplayStep[] ReplaySteps, TickObservation[] Ticks, string? OldShieldRejectCode, ConeHit[] OldConeHits, ConeHit[] PublishedConeHits,
        int PublishedShieldCastCount);
}
