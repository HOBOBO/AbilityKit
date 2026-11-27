using System.Diagnostics;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.CreateWorld;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Mathematics;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Console.Bootstrap;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Events.Summon;
using AbilityKit.Demo.Moba.Input;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Services.StateImport;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Demo.Moba.Worlds.Blueprints;
using AbilityKit.Deterministic;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Network.Battle.Projection;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using Xunit;
using Xunit.Abstractions;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

[Collection("Moba lifecycle preparation")]
public sealed class MobaStateSyncBattleWorldCandidateTests(ITestOutputHelper output)
{
    private const float Delta = 1f / 32;
    private const ulong AuthorityWorldId = 5;

    [Fact]
    public void Authority_projects_continuous_follow_and_stable_idle_at_the_owner_boundary()
    {
        using var world = BattleWorld.Build("summon-follow-authority");
        var services = world.Services;
        var actors = services.Resolve<MobaActorRegistry>();
        var ownerId = Assert.Single(actors.CopyActorIdsInOrder());
        Assert.True(actors.TryGet(ownerId, out var owner));
        var frame = Assert.IsType<FrameTime>(services.Resolve<IFrameTime>());
        var casts = services.Resolve<SkillCastCoordinator>();
        var projection = services.Resolve<IActorProjectionProducer>();
        var ownerStart = new Vec3(7f, 0f, -4f);
        owner.ReplaceTransform(new Transform3(ownerStart, Quat.Identity, owner.transform.Value.Scale));
        var spawn = ownerStart + new Vec3(0f, 0f, 5f);
        Assert.True(services.Resolve<MobaSummonService>().TrySummon(ownerId, 30030502,
            in spawn, Vec3.Forward, default(SummonSourceContext), out var petId));

        Step(0f);
        Assert.Equal(spawn.Z, projection.ExtractFull(petId).PosZ);
        for (var sample = 0; sample < 28; sample++)
        {
            var previous = projection.ExtractFull(petId).PosZ;
            Step(-1f);
            var petZ = projection.ExtractFull(petId).PosZ;
            var ownerZ = projection.ExtractFull(ownerId).PosZ;
            if (sample >= 2) Assert.InRange(previous - petZ, 0.04f, 0.15f);
            Assert.InRange(petZ - ownerZ, 4.8f, 5.25f);
        }

        Step(0f);
        Step(0f);
        var settledZ = projection.ExtractFull(petId).PosZ;
        for (var sample = 0; sample < 6; sample++)
        {
            Step(0f);
            Assert.Equal(settledZ, projection.ExtractFull(petId).PosZ);
        }
        for (var sample = 0; sample < 10; sample++)
        {
            Step(1f);
            Assert.Equal(settledZ, projection.ExtractFull(petId).PosZ);
        }
        for (var sample = 0; sample < 24; sample++) Step(-1f);
        Assert.True(projection.ExtractFull(petId).PosZ < settledZ - 0.5f);

        void Step(float direction)
        {
            frame.StepTo(new FrameIndex(frame.Frame.Value + 1), Delta);
            var intent = MobaActorIntent.MoveDirection(0f, direction);
            Assert.True(MobaMoveInputCommandHandler.TryApplyDirection(owner, ownerId, casts, in intent));
            world.Tick(Delta);
        }
    }
    private const int FirstSlot = 2, SecondSlot = 3;
    private const int AnchorTick = 100, PendingTick = 102;
    private const int FlightMask = 1 << 24;

    [Fact]
    public void Respawn_baseline_projects_the_new_pose_without_death_cues()
    {
        var loadout = new BattleLoadout(3006, 30060101, [30060201, 30060301, 30060401, 30060501], 1);
        using var world = BattleWorld.Build("respawn-authority", loadout: loadout);
        var services = world.Services;
        var actorId = Assert.Single(services.Resolve<MobaActorRegistry>().CopyActorIdsInOrder());
        var frame = Assert.IsType<FrameTime>(services.Resolve<IFrameTime>());
        var projection = services.Resolve<IActorProjectionProducer>();
        var cues = services.Resolve<MobaPresentationCueSnapshotService>();
        var attack = new AttackInfo { AttackerActorId = actorId, TargetActorId = actorId, DamageType = DamageType.True };
        attack.BaseDamage.FixedBaseValue = Fixed64.FromInt32(100000);
        Assert.NotNull(services.Resolve<MobaCombatEffectService>().DealDamage(attack));
        frame.StepTo(new FrameIndex(frame.Frame.Value + 1), Delta);
        world.Tick(Delta);

        var dead = projection.ExtractFull(actorId);
        Assert.Equal(0f, dead.Hp);
        Assert.Contains(ReadBaseline(), entry => entry.TargetActorId == actorId && entry.TemplateId == 90036003);
        Assert.Contains(ReadBaseline(), entry => entry.TargetActorId == actorId && entry.TemplateId == 90036005);

        var spawn = new Vec3(dead.PosX + 4, dead.PosY, dead.PosZ);
        Assert.True(services.Resolve<MobaUnitLifecycleService>().TryRespawn(actorId, in spawn).Succeeded);
        var alive = projection.ExtractFull(actorId);
        Assert.True(alive.Hp > 0);
        Assert.Equal(spawn.X, alive.PosX);
        Assert.DoesNotContain(ReadBaseline(), entry => entry.TargetActorId == actorId &&
            entry.TemplateId is 90036003 or 90036005);

        MobaPresentationCueSnapshotEntry[] ReadBaseline()
        {
            Assert.True(cues.TryCaptureStateSyncBaseline(frame.Frame, out var snapshot));
            return MobaPresentationCueSnapshotCodec.Deserialize(snapshot.Payload);
        }
    }

    [Fact]
    public void Authority_cask_ignores_an_enemy_projectile_and_projects_damage_to_the_unit()
    {
        var loadout = new BattleLoadout(3005, 30050101, [30050201, 30050301, 30050401, 30050501], 4);
        using var world = BattleWorld.Build("cask-authority", loadout: loadout, includeOpponent: true);
        var services = world.Services;
        var actors = services.Resolve<MobaActorRegistry>();
        var caster = Assert.Single(actors.Entries, entry => entry.Value.team.Value == Team.Team1).Key;
        var target = Assert.Single(actors.Entries, entry => entry.Value.team.Value == Team.Team2).Key;
        var frame = Assert.IsType<FrameTime>(services.Resolve<IFrameTime>());
        var projection = services.Resolve<IActorProjectionProducer>();
        var aim = new Vec3(100, 0, 106);
        var missilePosition = new Vec3(100, 0, 107.5f);
        Step();
        var before = projection.ExtractFull(target);
        Assert.True(services.Resolve<MobaProjectileService>().Shoot(target, ProjectileEmitterType.Linear,
            32040101, 0.1f, 120, 4f, missilePosition, new Vec3(1, 0, 0)));
        Step();
        var missile = Assert.Single(actors.Entries, entry => entry.Value.hasEntityMainType &&
            entry.Value.entityMainType.Value == EntityMainType.Projectile);
        Assert.False(missile.Value.hasResourceContainer);

        var attack = new AttackInfo { AttackerActorId = caster, TargetActorId = missile.Key, DamageType = DamageType.True };
        attack.BaseDamage.FixedBaseValue = Fixed64.One;
        Assert.Null(services.Resolve<MobaCombatEffectService>().DealDamage(attack));
        var cast = services.Resolve<SkillCastCoordinator>().TryCastBySlot(caster, 4, aim, Vec3.Forward, target);
        Assert.True(cast.Success, cast.FailReason);

        for (var step = 0; step < 64; step++) Step();
        var after = projection.ExtractFull(target);
        Assert.True(after.Hp < before.Hp);
        Assert.True(after.PosZ > before.PosZ + 1f);
        Assert.True(actors.TryGet(missile.Key, out var survivingMissile));
        Assert.Same(missile.Value, survivingMissile);
        Assert.False(survivingMissile.hasResourceContainer);

        void Step()
        {
            frame.StepTo(new FrameIndex(frame.Frame.Value + 1), Delta);
            world.Tick(Delta);
        }
    }

    [Theory]
    [InlineData(2, true, 11)]
    [InlineData(2, true, 23)]
    [InlineData(1, true, 11)]
    [InlineData(2, false, 11)]
    public void Flight_reconciliation_uses_the_production_world(int anchorTick, bool authoritativeFire, int speed)
    {
        const int fireTick = 2, deliveryTick = 4;
        using var authority = new BattleWorld("flight-authority", flightSpeed: speed);
        var scope = authority.CreateScope();
        var worlds = new List<EntitasWorld>();
        using var session = new LocalStateSyncCastSession<LocalStateSyncBattleCastCut, LocalStateSyncBattleCastObservation>(
            in scope, () => new LocalStateSyncBattleCastWorld(in scope, Delta, () =>
            {
                var world = BattleWorld.Build($"flight-candidate-{worlds.Count}", flightSpeed: speed);
                worlds.Add(world);
                return world;
            }, projectileMask: FlightMask), projectileMask: FlightMask);
        Assert.Equal(LocalStateSyncCastResult.Published, Deliver(session, authority.CaptureFlight(scope, 1), 1, 0));
        var cut = default(LocalStateSyncBattleCastCut);
        var oracle = new Dictionary<int, LocalStateSyncFlightObservation?>();
        var input = new SkillInputEvent(FirstSlot, SkillInputPhase.Press, targetFrame: fireTick, inputSequence: 1);
        for (var tick = 1; tick <= deliveryTick; tick++)
        {
            authority.Step(authoritativeFire && tick == fireTick ? input : null);
            oracle[tick] = authority.ReadFlightObservation(scope.ActorId);
            Assert.True(session.Advance(tick, tick == fireTick ? input : null));
            Assert.True(session.HasPrediction, session.LastCandidateFailure?.ToString());
            if (tick == fireTick) Assert.NotNull(BattleWorld.FlightObservation(worlds[0], scope.ActorId).Id);
            if (tick == anchorTick) cut = authority.CaptureFlight(scope, 2);
        }

        var terminal = anchorTick >= fireTick ? 1L : 0L;
        var adoption = Deliver(session, cut, 2, terminal);
        Assert.True(adoption == LocalStateSyncCastResult.Published, session.LastCandidateFailure?.ToString() ?? adoption.ToString());
        Assert.Equal(terminal, session.TerminalThrough);
        Assert.Equal(1 - terminal, session.PendingCount);
        Assert.Equal(terminal == 0 ? new long?[] { 1 } : Array.Empty<long?>(),
            session.LastReplay.Where(step => step.Input.HasValue).Select(step => step.Input));
        Assert.Equal(Enumerable.Range(anchorTick + 1, deliveryTick - anchorTick), session.LastReplay.Select(step => step.Tick));
        foreach (var step in session.LastReplay)
            Assert.Equal(oracle[step.Tick], step.State.Flight);
        Assert.Equal(2, worlds.Count);
        Assert.Equal(authority.FlightObservation(scope.ActorId), BattleWorld.FlightObservation(worlds[1], scope.ActorId));
        Assert.Equal(LocalStateSyncCastResult.Ignored, Deliver(session, cut, 2, terminal));
        var exits = 0;
        var wasFlying = authoritativeFire;
        for (var tick = deliveryTick + 1; tick <= 16; tick++)
        {
            authority.Step();
            Assert.True(session.Advance(tick, null));
            Assert.True(session.HasPrediction, session.LastCandidateFailure?.ToString());
            var actual = BattleWorld.FlightObservation(worlds[1], scope.ActorId);
            Assert.Equal(authority.FlightObservation(scope.ActorId), actual);
            if (wasFlying && !actual.Id.HasValue) exits++;
            wasFlying = actual.Id.HasValue;
        }
        Assert.Equal(authoritativeFire ? 1 : 0, exits);
        Assert.Equal(2, worlds.Count);
        Assert.Equal(0, BattleWorld.FlightObservation(worlds[1], scope.ActorId).Retains);
    }

    [Theory]
    [InlineData(3002, 30020201, 2, 4, 6, 9, 500)]
    [InlineData(3007, 30070201, 1, 3, 5, 7, 250)]
    public void Published_skill_rejection_before_release_replays_only_pending_movement_in_the_formal_world(
        int hero, int skillId, int slot, int anchorTick, int pendingTick, int deliveryTick, int releaseAtMs)
    {
        var loadout = hero == 3007 ? new BattleLoadout(hero, 30070101, [skillId, 30070301, 30070401, 30070501], slot) :
            new BattleLoadout(hero, 30020001, [30020101, skillId, 30020301, 30020401], slot);
        using var authority = new BattleWorld("published-cast-rejected-authority", loadout: loadout, movement: true);
        var scope = authority.CreateScope();
        var created = new List<EntitasWorld>();
        using var session = new LocalStateSyncCastSession<LocalStateSyncBattleCastCut, LocalStateSyncBattleCastObservation>(
            in scope, () => new LocalStateSyncBattleCastWorld(in scope, Delta, () =>
            {
                var candidate = BattleWorld.Build($"published-cast-candidate-{created.Count}", loadout: loadout, movement: true);
                created.Add(candidate);
                return candidate;
            }, observeCastProgress: true), movementEnabled: true);
        var initial = authority.CaptureSettledMovement(scope, revision: 1);
        Assert.Equal(skillId, initial.Economy.First.SkillId);
        Assert.Equal(LocalStateSyncCastResult.Published, Deliver(session, initial, 1, 0));

        var cast = new SkillInputEvent(slot, SkillInputPhase.Press,
            aimPos: new Vec3(0, 0, 5), aimDir: new Vec3(0, 0, 1), targetFrame: 1, inputSequence: 1);
        Assert.True(session.Advance(1, cast));
        Assert.Null(session.LastInputRejection);
        var original = Assert.Single(created);
        var running = ReadRunning(original.Services.Resolve<SkillCastCoordinator>(), scope.ActorId);
        Assert.True(running.HasValue);
        Assert.Equal(skillId, running.Value.SkillId);
        Assert.True(session.Observation.Economy.ManaRaw < initial.Economy.ManaRaw);
        Assert.True(session.Observation.Economy.First.EndMs > 0);

        var oracle = new Dictionary<int, LocalStateSyncBattleCastObservation>();
        var anchor = default(LocalStateSyncBattleCastCut);
        for (var tick = 1; tick <= deliveryTick; tick++)
        {
            authority.Step(moveForward: tick == pendingTick);
            oracle[tick] = authority.CaptureObservation();
            if (tick == 1) continue;
            Assert.True(tick == pendingTick ? session.AdvanceMovement(tick, 2, 0f, 1f) : session.Advance(tick, null),
                $"The production recipe did not advance to Tick {tick}.");
            Assert.True(session.HasPrediction, session.LastCandidateFailure?.ToString());
            Assert.Single(original.Services.Resolve<MobaActorRegistry>().CopyActorIdsInOrder());
            if (tick == anchorTick)
            {
                anchor = authority.CaptureSettledMovement(scope, revision: 2);
                Assert.Equal(oracle[tick], anchor.Observe());
                Assert.NotNull(session.Observation.CastElapsedMs);
                Assert.NotEqual(oracle[tick], session.Observation);
            }
        }
        Assert.InRange(session.Observation.CastElapsedMs.GetValueOrDefault(-1), 0, releaseAtMs - 1);
        Assert.Equal(0, session.AuthorityTick);
        Assert.Single(created);

        Assert.Equal(LocalStateSyncCastResult.Published, Deliver(session, anchor, 2, 1));
        Assert.Equal(anchorTick, session.AuthorityTick);
        Assert.Equal(deliveryTick, session.PublishedTick);
        Assert.Equal(1L, session.TerminalThrough);
        Assert.Equal(1, session.PendingCount);
        Assert.Equal(2, created.Count);
        Assert.Equal(Enumerable.Range(anchorTick + 1, deliveryTick - anchorTick), session.LastReplay.Select(step => step.Tick));
        var replayed = Assert.Single(session.LastReplay, step => step.Input.HasValue);
        Assert.Equal(pendingTick, replayed.Tick);
        Assert.Equal(2L, replayed.Input);
        foreach (var step in session.LastReplay)
        {
            Assert.Null(step.Rejection);
            Assert.Equal(oracle[step.Tick], step.State);
        }
        Assert.Equal(0, session.Observation.Economy.First.EndMs);
        Assert.Null(session.Observation.CastElapsedMs);
        Assert.True(session.Observation.Position.Z > oracle[anchorTick].Position.Z);
        authority.Step();
        Assert.True(session.Advance(deliveryTick + 1, null));
        Assert.Equal(authority.CaptureObservation(), session.Observation);
        Assert.Equal(2, created.Count);
    }

    [Fact]
    public void Committed_windup_is_restored_without_dispatching_its_terminal_press_again()
    {
        const int anchorTick = 3, deliveryTick = 9;
        using var authority = new BattleWorld("windup-authority", windup: true);
        var initial = authority.Read();
        var scope = authority.CreateScope();
        var domains = new List<TrackedWorld>();
        Action? checkUnpublished = null;
        using var session = new LocalStateSyncCastSession<LocalStateSyncBattleCastCut, LocalStateSyncBattleCastObservation>(
            in scope, () => CreateCandidate(scope, domains, windup: true, beforeStep: checkUnpublished));
        Assert.Equal(LocalStateSyncCastResult.Published, Deliver(session, authority.CaptureSettled(), 1, 0));
        authority.Step();
        authority.Step(Press(2));
        authority.Step();
        var stamp = new LocalStateSyncCastStamp(in scope, anchorTick, 2);
        var cut = authority.CaptureWindup(stamp);
        var atCut = authority.Read();
        Assert.Equal(initial.Mana - Fixed64.FromInt32(10).RawValue, atCut.Mana);
        Assert.True(atCut.FirstEnd > 0);
        Assert.Equal(1, authority.TransactionCount);

        for (var tick = 1; tick <= deliveryTick; tick++)
            Assert.True(session.Advance(tick, tick == 1 ? Press(tick) : null));
        Assert.NotEqual(atCut.FirstEnd, session.Observation.Economy.First.EndMs);
        checkUnpublished = () =>
        {
            Assert.False(session.HasPrediction);
            Assert.Equal(anchorTick, session.PublishedTick);
            Assert.Empty(session.LastReplay);
        };
        Assert.Equal(LocalStateSyncCastResult.Published, Deliver(session, cut, 2, 1));
        domains[1].BeforeStep = null;
        Assert.Equal(atCut, authority.Read());
        Assert.Equal(anchorTick, session.AuthorityTick);
        Assert.Equal(deliveryTick, session.PublishedTick);
        Assert.Equal(0, session.PendingCount);
        Assert.Equal(1, domains[1].RestoredTransactions);
        Assert.Equal(authority.ReadRunning(), domains[1].RestoredRunning);
        Assert.All(domains[1].Steps, step => Assert.Null(step.Input));
        Assert.Equal(Enumerable.Range(anchorTick + 1, deliveryTick - anchorTick), session.LastReplay.Select(step => step.Tick));
        for (var offset = 0; offset < session.LastReplay.Count; offset++)
        {
            authority.Step();
            Assert.Equal(authority.CaptureObservation(), session.LastReplay[offset].State);
            Assert.Equal(authority.ReadRunning(), domains[1].RunningSteps[offset]);
        }
        Assert.True(session.Advance(deliveryTick + 1, null));
        authority.Step();
        Assert.Null(authority.ReadRunning());
        Assert.Equal(0, authority.TransactionCount);
        Assert.Null(domains[1].RunningSteps[^1]);
        Assert.Equal(authority.CaptureObservation(), session.Observation);

        static SkillInputEvent Press(int tick) => new(FirstSlot, SkillInputPhase.Press, targetFrame: tick, inputSequence: 1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Settled_authority_cut_resumes_in_the_formal_battle_world(bool firstInputAccepted) =>
        VerifySettledAuthorityCut(firstInputAccepted, replayTicks: 6);

    [Theory]
    [InlineData(6)]
    [InlineData(32)]
    public void Settled_formal_recipe_reports_candidate_timings(int replayTicks)
    {
        const int sampleCount = 6;
        VerifySettledAuthorityCut(firstInputAccepted: true, replayTicks);
        var samples = new CandidateTiming[sampleCount];
        for (var sample = 0; sample < sampleCount; sample++)
        {
            var timing = new CandidateTiming();
            VerifySettledAuthorityCut(firstInputAccepted: true, replayTicks, timing);
            Assert.Equal(replayTicks + 1, timing.Ticks.Count);
            samples[sample] = timing;
        }

        output.WriteLine($"Formal settled recipe: {replayTicks} replay ticks, {sampleCount} runs after one warm-up; milliseconds, p50 / max.");
        Report("world build", samples.Select(sample => sample.Build));
        Report("restore excluding build", samples.Select(sample => sample.Restore - sample.Build));
        Report("single input tick", samples.SelectMany(sample => sample.Ticks.Where(step => step.Tick == PendingTick).Select(step => step.Elapsed)));
        Report("single empty tick", samples.SelectMany(sample => sample.Ticks.Where(step => step.Tick != PendingTick &&
            step.Tick <= AnchorTick + replayTicks).Select(step => step.Elapsed)));
        Report("session adoption to publication", samples.Select(sample => sample.Adopt));

        void Report(string phase, IEnumerable<long> values)
        {
            var sorted = values.OrderBy(value => value).ToArray();
            var median = (sorted[(sorted.Length - 1) / 2] + sorted[sorted.Length / 2]) / 2d;
            FormattableString line = $"{phase} (n={sorted.Length}): {median * 1000 / Stopwatch.Frequency:F3} / {sorted[^1] * 1000d / Stopwatch.Frequency:F3}";
            output.WriteLine(FormattableString.Invariant(line));
        }
    }

    private static void VerifySettledAuthorityCut(bool firstInputAccepted, int replayTicks, CandidateTiming? timing = null)
    {
        var deliveryTick = AnchorTick + replayTicks;
        using var authority = new BattleWorld("cast-authority");
        var start = authority.Read();
        var scope = authority.CreateScope();
        var domains = new List<TrackedWorld>();
        using var session = new LocalStateSyncCastSession<LocalStateSyncBattleCastCut, LocalStateSyncBattleCastObservation>(
            in scope, () => CreateCandidate(scope, domains, timing: domains.Count == 1 ? timing : null));
        Assert.Equal(LocalStateSyncCastResult.Published, Deliver(session, authority.CaptureSettled(), 1, 0));
        var first = new SkillInputEvent(FirstSlot, SkillInputPhase.Press,
            aimPos: new Vec3(0, 0, 5), aimDir: new Vec3(0, 0, 1), targetFrame: 1, inputSequence: 1);
        authority.Step(firstInputAccepted ? first : null);
        Assert.True(session.Advance(1, first));
        while (authority.Tick < AnchorTick)
        {
            authority.Step();
            Assert.True(session.Advance(authority.Tick, null));
        }
        var cut = authority.CaptureSettled();
        var atCut = authority.Read();
        if (firstInputAccepted)
        {
            Assert.True(atCut.FirstEnd > 0);
            Assert.True(atCut.Mana < start.Mana);
        }
        else
        {
            Assert.Equal(0, atCut.FirstEnd);
            Assert.Equal(start.Mana, atCut.Mana);
        }

        var admission = new AuthoritativeSnapshotAdmission();
        admission.Reset(AuthorityWorldId);
        Assert.True(admission.Check(AuthorityWorldId, cut.Tick, isFullSnapshot: true).Accepted);
        var pending = new SkillInputEvent(SecondSlot, SkillInputPhase.Press, targetActorId: cut.Actor.ActorId,
            aimPos: new Vec3(1, 0, 1), aimDir: new Vec3(0, 0, 1), targetFrame: PendingTick, inputSequence: 2);
        for (var tick = cut.Tick + 1; tick <= deliveryTick; tick++)
            Assert.True(session.Advance(tick, tick == PendingTick ? pending : null));
        Assert.True(admission.Admit(AuthorityWorldId, cut.Tick, isFullSnapshot: true).Accepted);
        var adoptStart = timing == null ? 0 : Stopwatch.GetTimestamp();
        var adoption = Deliver(session, cut, 2, 1);
        if (timing != null) timing.Adopt = Stopwatch.GetTimestamp() - adoptStart;
        Assert.Equal(LocalStateSyncCastResult.Published, adoption);
        Assert.Equal(atCut, authority.Read());
        Assert.Equal(AnchorTick, session.AuthorityTick);
        Assert.Equal(deliveryTick, session.PublishedTick);
        Assert.Equal(1, session.PendingCount);
        Assert.Equal(1L, session.TerminalThrough);
        Assert.True(domains[0].Disposed);
        Assert.False(domains[1].Disposed);
        Assert.Equal(Enumerable.Range(AnchorTick + 1, replayTicks), domains[1].Steps.Select(step => step.Tick));
        Assert.Equal(pending, Assert.Single(domains[1].Steps, step => step.Input.HasValue).Input!.Value);
        Assert.Equal(2L, Assert.Single(session.LastReplay, step => step.Input.HasValue).Input);
        Assert.All(session.LastReplay, step => Assert.Equal(atCut.FirstEnd, step.State.Economy.First.EndMs));
        Assert.Equal(0, session.LastReplay[0].State.Economy.Second.EndMs);
        Assert.True(session.Observation.Economy.Second.EndMs > 0);
        Assert.True(session.Observation.Economy.ManaRaw < atCut.Mana);

        foreach (var expected in session.LastReplay)
        {
            authority.Step(expected.Tick == PendingTick ? pending : null);
            Assert.Equal(authority.CaptureObservation(), expected.State);
        }
        Assert.Equal(LocalStateSyncCastResult.Ignored, Deliver(session, cut, 2, 1));
        Assert.Equal(2, domains.Count);
        Assert.True(session.Advance(deliveryTick + 1, null));
        authority.Step();
        Assert.Equal(authority.CaptureObservation(), session.Observation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Incomplete_recovery_keeps_authority_until_a_newer_baseline_can_catch_up(bool failCandidate)
    {
        using var authority = new BattleWorld("isolated-authority");
        var scope = authority.CreateScope();
        var domains = new List<TrackedWorld>();
        using var session = new LocalStateSyncCastSession<LocalStateSyncBattleCastCut, LocalStateSyncBattleCastObservation>(
            in scope, () => CreateCandidate(scope, domains, domains.Count == 1 && failCandidate ? 5 : 0));
        Assert.Equal(LocalStateSyncCastResult.Published, Deliver(session, authority.CaptureSettled(), 1, 0));
        authority.Step();
        Assert.True(session.Advance(1, new SkillInputEvent(FirstSlot, SkillInputPhase.Press, targetFrame: 1, inputSequence: 1)));
        authority.Step();
        var cut = authority.CaptureSettled();
        var pending = new SkillInputEvent(SecondSlot, SkillInputPhase.Press, targetFrame: 4, inputSequence: 2);
        Assert.True(session.Advance(2, null));
        Assert.True(session.Advance(3, null));
        Assert.True(session.Advance(4, pending));
        Assert.True(session.Advance(5, null));
        Assert.Equal(failCandidate ? LocalStateSyncCastResult.CandidateFailed : LocalStateSyncCastResult.AuthorityOnly,
            Deliver(session, cut, 2, failCandidate ? 1 : null));
        Assert.False(session.HasPrediction);
        Assert.Equal(cut.Tick, session.PublishedTick);
        Assert.Equal(cut.Observe(), session.Observation);
        Assert.Empty(session.LastReplay);
        Assert.Equal(failCandidate ? 1 : 2, session.PendingCount);
        Assert.True(domains[0].Disposed);
        if (failCandidate)
        {
            Assert.True(domains[1].Disposed);
            Assert.Equal(new[] { 3, 4, 5 }, domains[1].Steps.Select(step => step.Tick));
        }
        else Assert.Single(domains);

        Assert.True(session.Advance(6, null));
        Assert.Equal(cut.Observe(), session.Observation);
        Assert.Equal(6, session.ClockTick);
        authority.Step();
        var newer = authority.CaptureSettled();
        Assert.Equal(LocalStateSyncCastResult.Published, Deliver(session, newer, 3, 1));
        Assert.True(session.HasPrediction);
        Assert.Equal(3, session.AuthorityTick);
        Assert.Equal(6, session.PublishedTick);
        Assert.Equal(1L, session.TerminalThrough);
        Assert.Equal(2L, Assert.Single(session.LastReplay, step => step.Input.HasValue).Input);
        Assert.Equal(new[] { 4, 5, 6 }, session.LastReplay.Select(step => step.Tick));
        Assert.Equal(pending, Assert.Single(domains[^1].Steps, step => step.Input.HasValue).Input!.Value);
        Assert.Equal(1, session.PendingCount);
        foreach (var step in session.LastReplay)
        {
            authority.Step(step.Tick == 4 ? pending : null);
            Assert.Equal(authority.CaptureObservation(), step.State);
        }
        Assert.Equal(authority.CaptureObservation(), session.Observation);
        Assert.True(session.Advance(7, null));
        authority.Step();
        Assert.Equal(authority.CaptureObservation(), session.Observation);
    }

    [Fact]
    public void Stale_generation_or_missing_tick_history_cannot_build_a_candidate()
    {
        using var authority = new BattleWorld("history-authority");
        var scope = authority.CreateScope();
        var domains = new List<TrackedWorld>();
        using var session = new LocalStateSyncCastSession<LocalStateSyncBattleCastCut, LocalStateSyncBattleCastObservation>(
            in scope, () => CreateCandidate(scope, domains));
        var cut = authority.CaptureSettled();
        Assert.Equal(LocalStateSyncCastResult.Published, Deliver(session, cut, 1, 0));
        Assert.False(session.Advance(1, new SkillInputEvent(FirstSlot, SkillInputPhase.Hold, targetFrame: 1, inputSequence: 1)));
        Assert.Equal(0, session.ClockTick);
        Assert.True(session.Advance(1, null));
        Assert.False(session.Advance(1, new SkillInputEvent(FirstSlot, SkillInputPhase.Press, targetFrame: 1, inputSequence: 1)));
        var pending = new SkillInputEvent(SecondSlot, SkillInputPhase.Press, targetActorId: cut.Actor.ActorId,
            aimPos: new Vec3(1, 0, 1), aimDir: new Vec3(0, 0, 1), targetFrame: 32, inputSequence: 1);
        for (var tick = 2; tick <= 33; tick++) Assert.True(session.Advance(tick, tick == 32 ? pending : null));

        var staleScope = new LocalStateSyncCastScope(scope.Generation + 1, scope.WorldId, scope.Stream,
            scope.InputGeneration, scope.PlayerId, scope.ActorId, scope.EntityVersion);
        var staleStamp = new LocalStateSyncCastStamp(in staleScope, cut.Tick, 2);
        var staleAnchor = new LocalStateSyncCastAnchor<LocalStateSyncBattleCastCut, LocalStateSyncBattleCastObservation>(
            in staleStamp, cut, cut.Observe());
        Assert.Equal(LocalStateSyncCastResult.Ignored, session.Adopt(in staleAnchor, null));
        Assert.True(session.HasPrediction);
        Assert.Equal(LocalStateSyncCastResult.AuthorityOnly, Deliver(session, cut, 2, 0));
        Assert.False(session.HasPrediction);
        Assert.Equal(cut.Observe(), session.Observation);
        Assert.Equal(1, session.PendingCount);
        Assert.Empty(session.LastReplay);
        Assert.True(Assert.Single(domains).Disposed);

        authority.Step();
        Assert.True(session.Advance(34, null));
        Assert.Equal(LocalStateSyncCastResult.AuthorityOnly, Deliver(session, authority.CaptureSettled(), 3, 0));
        Assert.Single(domains);
        authority.Step();
        Assert.Equal(LocalStateSyncCastResult.Published, Deliver(session, authority.CaptureSettled(), 4, 0));
        Assert.Equal(34, session.PublishedTick);
        Assert.Equal(1, session.PendingCount);
        Assert.Equal(Enumerable.Range(3, 32), session.LastReplay.Select(step => step.Tick));
        Assert.Equal(32, Assert.Single(session.LastReplay, step => step.Input == 1).Tick);
        foreach (var step in session.LastReplay)
        {
            authority.Step(step.Tick == 32 ? pending : null);
            Assert.Equal(authority.CaptureObservation(), step.State);
        }
        Assert.True(session.Advance(35, null));
        authority.Step();
        Assert.Equal(authority.CaptureObservation(), session.Observation);
    }

    private static LocalStateSyncCastResult Deliver(
        LocalStateSyncCastSession<LocalStateSyncBattleCastCut, LocalStateSyncBattleCastObservation> session,
        LocalStateSyncBattleCastCut cut, long revision, long? through)
    {
        var stamp = new LocalStateSyncCastStamp(session.Scope, cut.Tick, revision);
        var anchor = new LocalStateSyncCastAnchor<LocalStateSyncBattleCastCut, LocalStateSyncBattleCastObservation>(
            in stamp, cut, cut.Observe());
        return session.Adopt(in anchor, through is long prefix ? new LocalStateSyncCastTerminalProof(in stamp, prefix) : null);
    }

    private static TrackedWorld CreateCandidate(LocalStateSyncCastScope scope, List<TrackedWorld> domains, int failTick = 0,
        bool windup = false, Action? beforeStep = null, CandidateTiming? timing = null)
    {
        EntitasWorld battle = null!;
        var domain = new TrackedWorld(new LocalStateSyncBattleCastWorld(in scope, Delta, Build), failTick,
            windup ? () => battle : null, scope.ActorId) { BeforeStep = beforeStep, Timing = timing };
        domains.Add(domain);
        return domain;

        EntitasWorld Build()
        {
            var start = timing == null ? 0 : Stopwatch.GetTimestamp();
            battle = BattleWorld.Build($"cast-candidate-{domains.Count}", windup);
            if (timing != null) timing.Build = Stopwatch.GetTimestamp() - start;
            return battle;
        }
    }

    private sealed class CandidateTiming
    {
        internal long Build, Restore, Adopt;
        internal List<(int Tick, long Elapsed)> Ticks { get; } = [];
    }

    private sealed class TrackedWorld(LocalStateSyncBattleCastWorld world, int failTick, Func<EntitasWorld>? battle, int actorId) :
        ILocalStateSyncCastWorld<LocalStateSyncBattleCastCut, LocalStateSyncBattleCastObservation>
    {
        internal bool Disposed { get; private set; }
        internal List<(int Tick, SkillInputEvent? Input)> Steps { get; } = [];
        internal SkillPipelineRunner.RunningSnapshot? RestoredRunning { get; private set; }
        internal int RestoredTransactions { get; private set; }
        internal List<SkillPipelineRunner.RunningSnapshot?> RunningSteps { get; } = [];
        internal Action? BeforeStep { get; set; }
        internal CandidateTiming? Timing { get; init; }

        public void Restore(LocalStateSyncBattleCastCut anchor)
        {
            var start = Timing == null ? 0 : Stopwatch.GetTimestamp();
            world.Restore(anchor);
            if (Timing != null) Timing.Restore = Stopwatch.GetTimestamp() - start;
            if (battle == null) return;
            var services = battle().Services;
            RestoredRunning = ReadRunning(services.Resolve<SkillCastCoordinator>(), actorId);
            RestoredTransactions = services.Resolve<MobaSkillEconomyService>().PendingTransactionCount;
        }
        public LocalStateSyncBattleCastObservation Read() => world.Read();
        public bool TryStep(int tick, SkillInputEvent? input, out string rejection)
        {
            BeforeStep?.Invoke();
            Steps.Add((tick, input));
            var start = Timing == null ? 0 : Stopwatch.GetTimestamp();
            var result = world.TryStep(tick, input, out rejection) && tick != failTick;
            if (Timing != null) Timing.Ticks.Add((tick, Stopwatch.GetTimestamp() - start));
            if (battle != null) RunningSteps.Add(ReadRunning(battle().Services.Resolve<SkillCastCoordinator>(), actorId));
            return result;
        }

        public void Dispose()
        {
            Disposed = true;
            world.Dispose();
        }
    }

    private static SkillPipelineRunner.RunningSnapshot? ReadRunning(SkillCastCoordinator cast, int actorId) =>
        cast.TryGetLatestRunning(actorId, out var running) ? running : null;

    private readonly record struct OwnerState(int Tick, long Mana, int FirstSkill, long FirstEnd, int FirstDuration,
        int SecondSkill, long SecondEnd, int SecondDuration, Vec3 Position);

    private sealed record BattleLoadout(int HeroId, int BasicAttack, int[] Skills, int PrimarySlot);

    private sealed class BattleWorld : IDisposable
    {
        private readonly EntitasWorld _world;
        private readonly FrameTime _frame;
        private readonly MobaActorRegistry _actors;
        private readonly SkillCastCoordinator _cast;
        private readonly MobaSkillEconomyService _economy;
        private readonly ActorEntity _actor;
        private readonly int _actorId;
        private readonly int _heroId;
        private readonly int _firstSlot;
        private readonly int _secondSlot;
        private readonly bool _movement;
        internal int Tick => _frame.Frame.Value;

        internal BattleWorld(string id, bool windup = false, BattleLoadout? loadout = null, bool movement = false,
            int flightSpeed = 0)
        {
            _world = Build(id, windup, loadout, movement, flightSpeed);
            _heroId = loadout?.HeroId ?? 3003;
            _firstSlot = loadout?.PrimarySlot ?? FirstSlot;
            _secondSlot = _firstSlot + 1;
            _movement = movement;
            try
            {
                var services = _world.Services;
                _frame = Assert.IsType<FrameTime>(services.Resolve<IFrameTime>());
                _actors = services.Resolve<MobaActorRegistry>();
                _cast = services.Resolve<SkillCastCoordinator>();
                _economy = services.Resolve<MobaSkillEconomyService>();
                _actorId = Assert.Single(_actors.CopyActorIdsInOrder());
                Assert.True(_actors.TryGet(_actorId, out _actor));
            }
            catch
            {
                _world.Dispose();
                throw;
            }
        }

        private const int Owner = 1;

        internal LocalStateSyncCastScope CreateScope() => new(1, AuthorityWorldId, Guid.NewGuid(), Guid.NewGuid(),
            Owner, _actorId, _actors.GetEntityVersion(_actorId));

        internal static EntitasWorld Build(string id, bool windup = false, BattleLoadout? loadout = null, bool movement = false,
            int flightSpeed = 0, bool includeOpponent = false)
        {
            var config = BattleStartConfig.CreateDefault();
            config.TickRate = 32;
            config.SyncMode = AbilityKit.Demo.Moba.Share.SyncMode.SnapshotAuthority;
            var player = config.Players[0];
            if (includeOpponent)
            {
                var opponent = config.Players[1];
                config.Players = [player, opponent];
                player.TeamId = 1;
                player.PositionX = player.PositionZ = 100;
                opponent.TeamId = 2;
                opponent.PositionX = 100;
                opponent.PositionZ = 106;
                opponent.EnableBrainOnSpawn = false;
            }
            else config.Players = [player];
            if (movement) player.PlayerId = config.PlayerId = "1";
            player.HeroId = player.AttributeTemplateId = loadout?.HeroId ?? 3003;
            player.BasicAttackSkillId = loadout?.BasicAttack ?? 30030101;
            player.SkillIds = loadout is null ? [30030201, 30030301, 30030401] : [.. loadout.Skills];
            player.EnableBrainOnSpawn = false;

            var builder = WorldServiceContainerFactory.CreateWithAttributes(WorldServiceProfile.All,
                [typeof(WorldServiceContainerFactory).Assembly, typeof(MobaWorldBootstrapModule).Assembly], ["AbilityKit"]);
            builder.AddModule(new ConsoleConfigModule());
            builder.RegisterInstance(config.BuildLaunchSpec().ToWorldInitData(MobaWorldBootstrapModule.InitOpCode));
            builder.TryRegister<IFrameTime>(WorldLifetime.Singleton, _ => new FrameTime());
            var options = new WorldCreateOptions(new WorldId(id), MobaBattleWorldBlueprint.Type) { ServiceBuilder = builder };
            new MobaBattleWorldBlueprint().Configure(options);
            var world = new EntitasWorld(options);
            try
            {
                world.Initialize();
                if (windup || movement || flightSpeed != 0)
                {
                    var registry = world.Services.Resolve<MobaActorRegistry>();
                    Assert.True(registry.TryGet(Assert.Single(registry.CopyActorIdsInOrder()), out var actor));
                    if (windup)
                        InstallWindupFixture(world.Services.Resolve<MobaConfigDatabase>(),
                            actor.skillLoadout.ActiveSkills[FirstSlot - 1].SkillId);
                    if (movement) LocalStateSyncBattleCastWorld.ConfigureIsolatedLocomotion(actor);
                    if (flightSpeed != 0) InstallFlightFixture(world.Services, actor.skillLoadout.ActiveSkills[FirstSlot - 1].SkillId, flightSpeed);
                }
                return world;
            }
            catch
            {
                world.Dispose();
                throw;
            }
        }

        internal LocalStateSyncBattleCastCut CaptureFlight(LocalStateSyncCastScope scope, long revision)
        {
            var value = CaptureSettled();
            var stamp = new LocalStateSyncCastStamp(in scope, Tick, revision);
            return new LocalStateSyncBattleCastCut(Tick, value.Actor, value.Economy,
                projectile: LocalStateSyncProjectileCut.Capture(_world, in stamp, FlightMask));
        }

        internal (int? Id, int Actor, int Version, Vec3 Position, int Lifetime, long Distance, int Retains, int NextActor)
            FlightObservation(int ownerId) => FlightObservation(_world, ownerId);

        internal LocalStateSyncFlightObservation? ReadFlightObservation(int ownerId)
        {
            var flight = LocalStateSyncProjectileCut.ReadFlight(_world, ownerId, FlightMask, _actor, out var projectile);
            if (projectile == null) return null;
            var actorId = projectile.actorId.Value;
            return new LocalStateSyncFlightObservation(flight, actorId, _actors.GetEntityVersion(actorId), projectile.transform.Value);
        }

        internal static (int? Id, int Actor, int Version, Vec3 Position, int Lifetime, long Distance, int Retains, int NextActor)
            FlightObservation(EntitasWorld world, int ownerId)
        {
            var actors = world.Services.Resolve<MobaActorRegistry>();
            Assert.True(actors.TryGet(ownerId, out var owner));
            var flight = LocalStateSyncProjectileCut.ReadFlight(world, ownerId, FlightMask, owner, out var projectile);
            var actorId = projectile?.actorId.Value ?? 0;
            return (flight.ActiveId?.Value, actorId, actors.GetEntityVersion(actorId),
                projectile?.transform.Value.Position ?? Vec3.Zero, flight.LifetimeFramesLeft, flight.DistanceLeftRaw,
                world.Services.Resolve<MobaSkillCastRuntimeService>().CaptureRollbackSnapshot().Retains.Length,
                world.Services.Resolve<ActorIdAllocator>().NextId);
        }

        private static void InstallFlightFixture(IWorldResolver services, int skillId, int speed)
        {
            const int projectileId = 982103, flowId = 982101, triggerId = 982102, actionId = 982104;
            var configs = services.Resolve<MobaConfigDatabase>();
            Assert.True(configs.TryGetDto<SkillDTO>(skillId, out var skill));
            skill.PreCastFlowId = 0;
            skill.CastFlowId = flowId;
            skill.LevelTableId = 0;
            skill.RequiredTargetQueryId = 0;
            skill.AllowCastWithoutTarget = true;
            var reload = configs.ReloadFromDtoArrays(new Dictionary<Type, Array>
            {
                [typeof(SkillDTO)] = new[] { skill },
                [typeof(ProjectileDTO)] = new[] { new ProjectileDTO { Id = projectileId, Speed = speed,
                    LifetimeMs = 250, MaxDistance = 40, CollisionLayerMask = FlightMask,
                    HitPolicyKind = (int)ProjectileHitPolicyKind.ExitOnHit } },
                [typeof(SkillFlowDTO)] = new[] { new SkillFlowDTO { Id = flowId, Phases =
                    [new SkillPhaseDTO { Type = (int)SkillPhaseType.RulePlan, PhaseId = "flight",
                        RulePlan = new SkillRulePlanPhaseDTO { TriggerIds = [triggerId], AbortOnFailure = true } }] } },
            }, strict: false);
            Assert.True(reload.Succeeded);
            services.Resolve<TriggerPlanJsonDatabase>().LoadFromJson("""
                {"format":"runtime","formatVersion":1,"triggers":[
                  {"triggerId":982102,"actions":[{"actionId":982104,"arity":0}]}]}
                """);
            services.Resolve<ActionRegistry>().Register<NamedAction0<object, object, IWorldResolver>>(new ActionId(actionId),
                (args, _, action) =>
                {
                    var cast = Assert.IsType<SkillPipelineContext>(args);
                    Assert.True(cast.TryGetOrigin(out var origin));
                    var source = new ProjectileSourceContext(cast.CasterActorId, 0, projectileId,
                        origin.ImmediateContextId, origin.EffectiveRootContextId, origin.OwnerContextId, cast.RuntimeHandle, in origin);
                    if (!action.Context.Resolve<MobaProjectileService>().Shoot(cast.CasterActorId,
                        ProjectileEmitterType.Linear, projectileId, speed, 8, 40, cast.AimPos, cast.AimDir, in source))
                        action.Control.RejectAction("Could not fire local straight-flight recipe.");
                }, isDeterministic: true);
        }

        internal void Step(SkillInputEvent? input = null, bool moveForward = false)
        {
            _frame.StepTo(new FrameIndex(Tick + 1), Delta);
            if (moveForward)
            {
                var intent = MobaActorIntent.MoveDirection(0f, 1f);
                Assert.True(MobaMoveInputCommandHandler.TryApplyDirection(_actor, _actorId, _cast, in intent));
            }
            if (input is { } command)
            {
                Assert.Equal(Tick, command.TargetFrame);
                var result = _cast.TryHandleInputResult(_actorId, in command);
                Assert.True(result.Success, result.Code + ": " + result.Message);
            }
            _world.Tick(Delta);
        }

        internal LocalStateSyncBattleCastCut CaptureSettled()
        {
            Assert.False(_cast.TryGetLatestRunning(_actorId, out _));
            Assert.Equal(0, _economy.PendingTransactionCount);
            return CaptureCut();
        }

        internal int TransactionCount => _economy.PendingTransactionCount;
        internal SkillPipelineRunner.RunningSnapshot? ReadRunning() =>
            MobaStateSyncBattleWorldCandidateTests.ReadRunning(_cast, _actorId);

        internal LocalStateSyncBattleCastCut CaptureWindup(LocalStateSyncCastStamp stamp) =>
            CaptureCut(LocalStateSyncWindupCut.Capture(_world, in stamp, FirstSlot, SecondSlot));

        internal LocalStateSyncBattleCastCut CaptureSettledMovement(LocalStateSyncCastScope scope, long revision)
        {
            Assert.Null(ReadRunning());
            Assert.Equal(0, _economy.PendingTransactionCount);
            var stamp = new LocalStateSyncCastStamp(in scope, Tick, revision);
            return CaptureCut(movement: LocalStateSyncMovementCut.Capture(_world, in stamp));
        }

        private LocalStateSyncBattleCastCut CaptureCut(LocalStateSyncWindupCut? windup = null, LocalStateSyncMovementCut? movement = null)
        {
            var owner = Read();
            var hp = _actor.resourceContainer.Value.Map[ResourceType.Hp];
            return new LocalStateSyncBattleCastCut(Tick, new MobaActorStateImport(_actorId, owner.Position.X, owner.Position.Y,
                owner.Position.Z, 0f, hp.Current.ToSingle(), hp.LastMax.ToSingle(), (int)_actor.team.Value, 1, _heroId, Owner),
                windup?.Economy ?? CaptureObservation().Economy, windup, movement);
        }

        private static void InstallWindupFixture(MobaConfigDatabase configs, int skillId)
        {
            const int windupFlow = 982001, settleFlow = 982002;
            Assert.True(configs.TryGetDto<SkillDTO>(skillId, out var skill));
            skill.PreCastFlowId = windupFlow;
            skill.CastFlowId = settleFlow;
            skill.LevelTableId = 0;
            skill.RequiredTargetQueryId = 0;
            skill.AllowCastWithoutTarget = true;
            var reload = configs.ReloadFromDtoArrays(new Dictionary<Type, Array>
            {
                [typeof(SkillDTO)] = new[] { skill },
                [typeof(SkillFlowDTO)] = new[]
                {
                    new SkillFlowDTO { Id = windupFlow, Phases =
                    [
                        new SkillPhaseDTO { Type = (int)SkillPhaseType.Economy, PhaseId = "reserve",
                            Economy = new SkillEconomyPhaseDTO { Operation = (int)SkillEconomyOperation.ReserveCast,
                                ResourceType = (int)ResourceType.Mana, ResourceAmount = 10,
                                UseResolvedResourceCost = false, ChargeCost = 0, MaxCharges = 0,
                                RequireExplicitCommit = true, StartSkillCooldown = true, SkillCooldownMs = 500,
                                UseResolvedSkillCooldown = false } },
                        new SkillPhaseDTO { Type = (int)SkillPhaseType.CommitPoint, PhaseId = "commit",
                            CommitPoint = new SkillCommitPointPhaseDTO { RequireEconomyReservation = true } },
                        new SkillPhaseDTO { Type = (int)SkillPhaseType.Delay, PhaseId = "wait",
                            Delay = new SkillDelayPhaseDTO { DelayMs = 250 } },
                    ] },
                    new SkillFlowDTO { Id = settleFlow, Phases =
                    [
                        new SkillPhaseDTO { Type = (int)SkillPhaseType.Delay, PhaseId = "finish",
                            Delay = new SkillDelayPhaseDTO { DelayMs = 1 } },
                    ] },
                },
            }, strict: false);
            Assert.True(reload.Succeeded);
        }

        internal LocalStateSyncBattleCastObservation CaptureObservation()
        {
            var owner = Read();
            return new LocalStateSyncBattleCastObservation(Tick, new LocalStateSyncCastEconomy(owner.Mana,
                new LocalStateSyncCastSlot(_firstSlot, owner.FirstSkill, Slot(_firstSlot).Level, owner.FirstEnd, owner.FirstDuration),
                new LocalStateSyncCastSlot(_secondSlot, owner.SecondSkill, Slot(_secondSlot).Level, owner.SecondEnd, owner.SecondDuration)),
                owner.Position, rotation: _movement ? _actor.transform.Value.Rotation : null);
        }

        internal OwnerState Read() => new(Tick, _actor.resourceContainer.Value.Map[ResourceType.Mana].Current.RawValue,
            Slot(_firstSlot).SkillId, Slot(_firstSlot).CooldownEndTimeMs, Slot(_firstSlot).CooldownDurationMs,
            Slot(_secondSlot).SkillId, Slot(_secondSlot).CooldownEndTimeMs, Slot(_secondSlot).CooldownDurationMs,
            _actor.transform.Value.Position);

        private ActiveSkillRuntime Slot(int slot) => _actor.skillLoadout.ActiveSkills[slot - 1];

        public void Dispose() => _world.Dispose();
    }
}
