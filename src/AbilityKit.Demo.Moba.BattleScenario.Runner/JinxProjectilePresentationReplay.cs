using AbilityKit.BattleScenario;
using AbilityKit.BattleScenario.Editor;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.BattleScenario;
using AbilityKit.Game.Flow;
using AbilityKit.Scenario;

namespace AbilityKit.Demo.Moba.BattleScenario.Runner;

internal static class JinxProjectilePresentationReplay
{
    private const int SkillId = 30020201;
    private const int ProjectileTemplateId = 32020203;
    private const int PredictionKey = 1;
    private const int DeliveryDelayTicks = 2;

    internal static void Export(string directory)
    {
        var recordings = new[] { Record(true), Record(false) };
        var json = recordings.Select(replay => replay.Serialize()).ToArray();
        Require(json[0] == Record(true).Serialize() && json[1] == Record(false).Serialize(),
            "Projectile presentation recordings are not repeatable.");
        Directory.CreateDirectory(directory);
        for (var index = 0; index < recordings.Length; index++)
            File.WriteAllText(Path.Combine(directory, $"jinx-w-local-presentation.{recordings[index].Variant}.statesync-replay.json"),
                json[index] + "\n");
    }

    internal static LocalStateSyncReplay Record(bool accepted)
    {
        var scenario = BattleScenarioCompiler.CompileFile(Path.Combine(AppContext.BaseDirectory,
            "Flows", "SkillPresentation", "jinx-w.battlescenario"));
        var visualReference = Observe(scenario, expectedCast: true);
        var authority = Observe(accepted ? scenario : WithoutCast(scenario), accepted);
        var firstTick = visualReference.Keys.Min();
        var lastTick = visualReference.Keys.Max();
        var deliveryTick = firstTick + DeliveryDelayTicks;
        Require(lastTick >= deliveryTick && !visualReference[firstTick].Equals(visualReference[deliveryTick]),
            "Projectile reference must move and survive through the delivery Tick.");
        Require(!accepted || authority.SequenceEqual(visualReference), "Independent authority trajectory did not match the reference.");

        var reconciliation = new PresentationCueReconciliationController<int, Vec3>();
        var port = new LocalVisualPort();
        var views = new PresentationCueViewRegistry<int, Vec3, int>(port);
        var checkpoints = new List<LocalStateSyncReplay.Checkpoint>();
        for (var tick = firstTick; tick <= lastTick + 1; tick++)
        {
            PresentationCueReconciliationDecision<int, Vec3> decision;
            var hasAuthority = authority.TryGetValue(tick, out var position);
            var duplicateIgnored = false;
            string phase;
            if (tick < deliveryTick)
            {
                phase = "predicted";
                var predicted = visualReference[tick];
                decision = Apply(PresentationCuePredictionOutcome.Predicted,
                    tick == firstTick ? PresentationCueSignal.Activate : PresentationCueSignal.Refresh, tick, predicted);
                Require(port.Active.Values.Single().Equals(predicted), "Predicted view did not follow the reference pose.");
            }
            else if (accepted && hasAuthority)
            {
                phase = "confirmed";
                decision = Apply(PresentationCuePredictionOutcome.Confirmed, PresentationCueSignal.Refresh, tick, position);
                if (tick == deliveryTick)
                    duplicateIgnored = Apply(PresentationCuePredictionOutcome.Confirmed, PresentationCueSignal.Refresh, tick, position).IsDuplicate;
                Require(port.Active.Values.Single().Equals(position), "Confirmed view did not follow the authority pose.");
            }
            else if (tick == deliveryTick || accepted)
            {
                phase = accepted ? "ended" : "rejected";
                decision = Apply(accepted ? PresentationCuePredictionOutcome.Confirmed : PresentationCuePredictionOutcome.Rejected,
                    PresentationCueSignal.Deactivate, tick);
                if (!accepted)
                    duplicateIgnored = Apply(PresentationCuePredictionOutcome.Rejected, PresentationCueSignal.Deactivate, tick).IsDuplicate;
            }
            else
            {
                phase = "late-prediction";
                decision = Apply(PresentationCuePredictionOutcome.Predicted, PresentationCueSignal.Refresh, tick, visualReference[firstTick]);
            }

            Require(port.Active.Count == views.ActiveCount && views.ActiveCount == reconciliation.ActiveCount,
                "Reconciliation and view counts diverged.");
            var live = port.Active.SingleOrDefault();
            checkpoints.Add(new LocalStateSyncReplay.Checkpoint
            {
                Command = "presentation",
                DeliveryTick = tick,
                Steps = Array.Empty<LocalStateSyncReplay.ReplayStep>(),
                Presentation = new LocalStateSyncReplay.PresentationObservation
                {
                    Phase = phase,
                    CueCommand = decision.Command.ToString(),
                    RejectReason = decision.RejectReason.ToString(),
                    Promoted = decision.IsPromotion,
                    DuplicateIgnored = duplicateIgnored,
                    Authority = hasAuthority ? ToPoint(position) : null,
                    View = port.Active.Count == 0 ? null : ToPoint(live.Value),
                    Handle = port.Active.Count == 0 ? null : live.Key,
                    Active = views.ActiveCount,
                    Created = views.CreatedCount,
                    Destroyed = views.DestroyedCount,
                },
            });
        }

        if (accepted)
        {
            var stale = Apply(PresentationCuePredictionOutcome.Confirmed, PresentationCueSignal.Refresh, deliveryTick, authority[deliveryTick]);
            Require(stale.RejectReason == PresentationCueReconciliationRejectReason.StaleRevision, "Old confirm revision was not rejected.");
        }
        Require(port.Active.Count == 0 && views.DestroyedCount == 1, "Expected one retired presentation handle and no live view.");
        return new LocalStateSyncReplay
        {
            CaseId = "jinx-w-local-presentation",
            Variant = accepted ? "confirmed" : "rejected",
            TickRate = scenario.ResolveExecution().TickRate,
            Checkpoints = checkpoints.ToArray(),
        };

        PresentationCueReconciliationDecision<int, Vec3> Apply(PresentationCuePredictionOutcome outcome,
            PresentationCueSignal signal, int revision, Vec3? pose = null)
        {
            var update = new PresentationCueUpdate<int, Vec3>(PredictionKey, signal, outcome, revision,
                reconciliation.CurrentGeneration, pose.HasValue, pose.GetValueOrDefault());
            var decision = reconciliation.Process(in update);
            views.Apply(in decision);
            return decision;
        }
    }

    private static LocalStateSyncReplay.PresentationPoint ToPoint(Vec3 position) => new()
    {
        X = ToMilliUnits(position.X), Y = ToMilliUnits(position.Y), Z = ToMilliUnits(position.Z),
    };

    private static int ToMilliUnits(double value) => checked((int)Math.Round(value * 1000));

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static SortedDictionary<int, Vec3> Observe(TestScenario scenario, bool expectedCast)
    {
        var poses = new SortedDictionary<int, Vec3>();
        var active = new List<ProjectileRuntimeState>();
        ProjectileId? projectile = null;
        var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario, (world, tick) =>
        {
            world.ObserveProjectiles("caster", ProjectileTemplateId, active);
            if (active.Count == 0) return;
            var sample = active.Single();
            projectile ??= sample.Id;
            Require(projectile.Value.Equals(sample.Id), "Observed more than one projectile lifetime.");
            poses.Add(tick, sample.Position);
        });
        Require(outcome.Result.Passed, outcome.Result.Summary);

        var launches = outcome.TraceNodes.Where(node => node.Kind == "ProjectileLaunch" && node.ConfigId == ProjectileTemplateId).ToArray();
        var damage = outcome.TraceNodes.Where(node => node.Kind == "DamageApply" && node.ConfigId == SkillId);
        if (expectedCast)
        {
            var launchIds = launches.Select(node => node.Id).ToHashSet();
            Require(launches.Count(node => !launchIds.Contains(node.ParentId)) == 1 && damage.Count() == 1,
                "Expected one logical launch and one damage application.");
            Require(poses.Count > 0 && poses.Keys.SequenceEqual(Enumerable.Range(poses.Keys.Min(), poses.Count)),
                "Expected continuous projectile observations.");
        }
        else
        {
            Require(launches.Length == 0 && !damage.Any() && poses.Count == 0,
                "Rejected authority world must have no launch, damage or projectile position.");
        }
        return poses;
    }

    private static TestScenario WithoutCast(TestScenario source) => new()
    {
        CaseId = source.CaseId + "-authority-rejected",
        WorldProfileId = source.WorldProfileId,
        Seed = source.Seed,
        Execution = source.ResolveExecution(),
        Actors = source.Actors,
        Setup = source.Setup,
    };

    private sealed class LocalVisualPort : IPresentationCueViewPort<int, Vec3, int>
    {
        private int _nextHandle;
        internal Dictionary<int, Vec3> Active { get; } = new();

        public bool IsAlive(int handle) => Active.ContainsKey(handle);

        public bool TryCreate(int key, in Vec3 payload, out int handle)
        {
            handle = ++_nextHandle;
            Active.Add(handle, payload);
            return true;
        }

        public void Update(int handle, in Vec3 payload) => Active[handle] = payload;
        public void Destroy(int handle) => Require(Active.Remove(handle), "View handle was retired twice.");
    }
}
