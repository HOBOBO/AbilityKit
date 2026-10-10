using System.Linq;
using System.IO;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Context;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Gameplay.Triggering;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Demo.Moba.Events.Unit;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Triggering.Runtime.Plan.Json;
using AbilityKit.Trace;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed class MobaCompleteBattleLifecycleSmokeTests
{
    [Fact]
    public void Gameplay_start_rejects_missing_trigger_binding_service()
    {
        using var bootstrapper = new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault());
        bootstrapper.Initialize();
        bootstrapper.Start();
        for (var i = 0; i < 8 && bootstrapper.Context.EcsWorld == null; i++) bootstrapper.Tick();
        bootstrapper.SetupBattle();

        var gameplay = bootstrapper.RuntimeServices!.Resolve<MobaGameplayService>();
        var gameplayId = gameplay.CurrentGameplayId;
        gameplay.Reset();
        var field = typeof(MobaGameplayService).GetField("_triggerBindings",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        field.SetValue(gameplay, null);

        Assert.False(gameplay.TryPrepareStart(gameplayId, out var error));
        Assert.Contains("binding service is missing", error);
        Assert.Equal(MobaGameplayPhase.NotStarted, gameplay.Phase);
    }

    [Fact]
    public void Character_action_snapshot_codec_preserves_actor_identity_and_frame()
    {
        var entries = new[]
        {
            new CharacterActionData(41, 2, 105, "life/alive/action/casting",
                ((long)41 << 32) | 100u, 100, 5, 77, 7001)
        };
        var payload = CharacterActionSnapshotCodec.Encode(entries);
        Assert.True(CharacterActionSnapshotCodec.TryDecode(payload, out var decoded));
        Assert.Single(decoded);
        Assert.Equal(2, decoded[0].EntityVersion);
        Assert.Equal(105, decoded[0].SnapshotFrame);
        Assert.Equal(77, decoded[0].CastInstanceId);
        Assert.False(CharacterActionSnapshotCodec.TryDecode(payload[..^1], out _));
        Assert.Throws<InvalidOperationException>(() => CharacterActionSnapshotCodec.Encode(
            new[] { entries[0], entries[0] }));
    }

    [Fact]
    public void Gameplay_snapshot_codecs_read_v1_and_reject_truncated_v2()
    {
        using var matchStream = new MemoryStream();
        using (var writer = new BinaryWriter(matchStream, System.Text.Encoding.UTF8, true))
        {
            writer.Write(1);
            writer.Write(1);
            writer.Write((int)GameplayClockPhase.Ended);
            writer.Write(3d);
            writer.Write(2d);
            writer.Write(1);
            writer.Write(90);
            writer.Write("score_limit");
        }
        Assert.True(GameplayMatchSnapshotCodec.TryDecode(matchStream.ToArray(), out var oldMatch));
        Assert.Equal(string.Empty, oldMatch.MatchId);
        Assert.Equal(0, oldMatch.SnapshotFrame);

        var current = new GameplayMatchData(1, GameplayClockPhase.Ended, 3, 2, 1, 90,
            "score_limit", "match-42", 95);
        var encoded = GameplayMatchSnapshotCodec.Encode(in current);
        Assert.True(GameplayMatchSnapshotCodec.TryDecode(encoded, out var decoded));
        Assert.Equal("match-42", decoded.MatchId);
        Assert.Equal(95, decoded.SnapshotFrame);
        Assert.False(GameplayMatchSnapshotCodec.TryDecode(encoded[..^1], out _));

        using var clockStream = new MemoryStream();
        using (var writer = new BinaryWriter(clockStream, System.Text.Encoding.UTF8, true))
        {
            writer.Write(1);
            writer.Write(1);
            writer.Write((int)GameplayClockPhase.Ended);
            writer.Write(120000);
            writer.Write(30000);
        }
        Assert.True(GameplayClockSnapshotCodec.TryDecode(clockStream.ToArray(), out var oldClock));
        Assert.Equal(string.Empty, oldClock.MatchId);
        var clock = new GameplayClockData(1, GameplayClockPhase.Ended, 120000, 30000,
            "match-42", 95);
        var encodedClock = GameplayClockSnapshotCodec.Encode(in clock);
        Assert.True(GameplayClockSnapshotCodec.TryDecode(encodedClock, out var decodedClock));
        Assert.Equal("match-42", decodedClock.MatchId);
        Assert.Equal(95, decodedClock.SnapshotFrame);
        Assert.False(GameplayClockSnapshotCodec.TryDecode(encodedClock[..^1], out _));
    }

    [Fact]
    public void Trigger_config_schedules_hero_respawn_and_publishes_remaining_time()
    {
        using var bootstrapper = new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault());
        bootstrapper.Initialize();
        bootstrapper.Start();
        for (var i = 0; i < 8 && bootstrapper.Context.EcsWorld == null; i++) bootstrapper.Tick();
        bootstrapper.SetupBattle();
        for (var i = 0; i < 10; i++) bootstrapper.Tick();

        var services = bootstrapper.RuntimeServices!;
        var actors = services.Resolve<MobaActorRegistry>();
        var heroes = actors.Entries.Where(entry => entry.Value != null && entry.Value.hasTeam &&
            entry.Value.hasUnitSubType && entry.Value.unitSubType.Value == UnitSubType.Hero &&
            entry.Value.hasAttributeGroup && entry.Value.hasResourceContainer).ToArray();
        var victim = heroes.First(entry => entry.Value.team.Value == Team.Team1);
        var killer = heroes.First(entry => entry.Value.team.Value == Team.Team2);
        var respawns = services.Resolve<MobaGameplayRespawnService>();
        var gameplay = services.Resolve<MobaGameplayService>();
        var damage = services.Resolve<DamagePipelineService>();

        ExecuteLethalDamage(damage, killer.Key, victim.Key, victim.Value.GetMobaAttrs().Hp);
        var pending = Assert.Single(respawns.CapturePending());
        Assert.Equal(victim.Key, pending.ActorId);
        Assert.InRange(pending.RemainingMilliseconds, 4999, 5000);

        var emitter = new MobaGameplayRespawnSnapshotService(respawns);
        Assert.True(emitter.TryGetSnapshot(new FrameIndex(10000), out var snapshot));
        Assert.Equal(GameplayRespawnSnapshotCodec.OpCode, snapshot.OpCode);
        Assert.True(GameplayRespawnSnapshotCodec.TryDecode(snapshot.Payload, out var decoded));
        Assert.Equal(victim.Key, Assert.Single(decoded).ActorId);

        gameplay.Tick(5f);
        respawns.Tick();
        Assert.Empty(respawns.CapturePending());
        Assert.True(victim.Value.GetMobaAttrs().Hp > 0f);
        Assert.True(emitter.TryGetSnapshot(new FrameIndex(10001), out var cleared));
        Assert.True(GameplayRespawnSnapshotCodec.TryDecode(cleared.Payload, out var empty));
        Assert.Empty(empty);
    }

    [Fact]
    public void Damage_context_lifecycle_is_independent_from_optional_trace()
    {
        using var bootstrapper = new ConsoleBattleBootstrapper(
            BattleStartConfig.CreateDefault(),
            additionalModules: new[] { new MobaTraceAdapterModule() });
        bootstrapper.Initialize();
        bootstrapper.Start();
        for (var i = 0; i < 8 && bootstrapper.Context.EcsWorld == null; i++)
        {
            bootstrapper.Tick();
        }

        bootstrapper.SetupBattle();
        for (var i = 0; i < 10; i++)
        {
            bootstrapper.Tick();
        }

        var services = bootstrapper.RuntimeServices!;
        var actors = services.Resolve<MobaActorRegistry>();
        var combatants = actors.Entries
            .Where(entry => entry.Value != null && entry.Value.hasTeam && entry.Value.hasAttributeGroup)
            .ToArray();
        var attacker = combatants.First();
        var target = combatants.First(entry => entry.Value.team.Value != attacker.Value.team.Value);
        var damage = services.Resolve<DamagePipelineService>();
        var contexts = services.Resolve<MobaExecutionContextRegistry>();
        var trace = services.Resolve<MobaTraceRegistry>();

        trace.OnDeinit(services);
        ContextLifecycleEventHandler<MobaExecutionContextNode> failingObserver =
            (in ContextLifecycleEvent<MobaExecutionContextNode> _) => throw new InvalidOperationException("observer failure");
        using var failingSubscription = contexts.Observe(failingObserver, replayExisting: false);
        var withoutTraceStart = contexts.NextContextId;
        var withoutTrace = ExecuteDamage(damage, attacker.Key, target.Key, 1f);
        failingSubscription.Dispose();

        Assert.NotNull(withoutTrace);
        AssertDamageContextChain(contexts, withoutTraceStart, expectPeriodic: true);
        Assert.False(trace.Contains(withoutTraceStart));

        trace.OnInit(services);
        trace.RegistryEvent += _ => throw new InvalidOperationException("trace observer failure");
        var withTraceStart = contexts.NextContextId;
        var withTrace = ExecuteDamage(damage, attacker.Key, target.Key, 1f);

        Assert.NotNull(withTrace);
        Assert.True(withoutTrace.Value > 0f);
        Assert.True(withTrace.Value > 0f);
        AssertDamageContextChain(contexts, withTraceStart, expectPeriodic: true);
        Assert.True(trace.Contains(withTraceStart));
        Assert.True(trace.Contains(withTraceStart + 1));
        Assert.True(trace.Contains(withTraceStart + 2));
    }

    [Fact]
    public void ConsoleWorldCompletesDeathRespawnRedeathAndSettlement()
    {
        using var bootstrapper = new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault());
        bootstrapper.Initialize();
        bootstrapper.Start();
        for (var i = 0; i < 8 && bootstrapper.Context.EcsWorld == null; i++)
        {
            bootstrapper.Tick();
        }

        bootstrapper.SetupBattle();
        for (var i = 0; i < 10; i++)
        {
            bootstrapper.Tick();
        }

        var services = bootstrapper.RuntimeServices;
        Assert.NotNull(services);
        Assert.False(services.TryResolve<MobaTraceRegistry>(out _));
        var registry = services.Resolve<MobaActorRegistry>();
        var combatants = registry.Entries
            .Where(entry => entry.Value != null && entry.Value.hasTeam && entry.Value.hasAttributeGroup && entry.Value.hasResourceContainer)
            .ToArray();
        var attacker = combatants.First();
        var target = combatants.First(entry => entry.Value.team.Value != attacker.Value.team.Value);

        var lifecycle = services.Resolve<MobaUnitLifecycleService>();
        var damage = services.Resolve<DamagePipelineService>();
        var rules = services.Resolve<MobaCombatRulesService>();
        var gameplay = services.Resolve<MobaGameplayService>();
        Assert.Equal(MobaGameplayPhase.Running, gameplay.Phase);

        var firstDeath = ExecuteLethalDamage(damage, attacker.Key, target.Key, target.Value.GetMobaAttrs().Hp);
        Assert.Equal(0f, firstDeath.TargetHp, 3);
        Assert.Equal(MobaCombatRuleFailure.Dead, rules.CanBeSearchedTarget(attacker.Key, target.Key).Failure);

        var firstRespawn = lifecycle.TryRespawn(target.Key, healthRatio: 0.5f);
        Assert.True(firstRespawn.Succeeded);
        Assert.True(rules.CanBeSearchedTarget(attacker.Key, target.Key).Passed);
        Assert.Equal(MobaUnitRespawnFailure.AlreadyAlive, lifecycle.TryRespawn(target.Key).Failure);

        var secondDeath = ExecuteLethalDamage(damage, attacker.Key, target.Key, firstRespawn.RestoredHp);
        Assert.Equal(0f, secondDeath.TargetHp, 3);
        Assert.True(lifecycle.TryRespawn(target.Key).Succeeded);

        Assert.True(gameplay.End("team_defeated", winTeamId: (int)attacker.Value.team.Value));
        Assert.Equal(MobaGameplayPhase.Ended, gameplay.Phase);
        Assert.Equal((int)attacker.Value.team.Value, gameplay.LastResult.WinTeamId);
    }

    [Fact]
    public void Fifth_enemy_hero_kill_ends_game_for_scoring_team()
    {
        using var bootstrapper = new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault());
        bootstrapper.Initialize();
        bootstrapper.Start();
        for (var i = 0; i < 8 && bootstrapper.Context.EcsWorld == null; i++) bootstrapper.Tick();
        bootstrapper.SetupBattle();
        for (var i = 0; i < 10; i++) bootstrapper.Tick();

        var services = bootstrapper.RuntimeServices!;
        var actors = services.Resolve<MobaActorRegistry>();
        var heroes = actors.Entries.Where(entry => entry.Value != null && entry.Value.hasTeam &&
            entry.Value.hasUnitSubType && entry.Value.unitSubType.Value == UnitSubType.Hero &&
            entry.Value.hasAttributeGroup && entry.Value.hasResourceContainer).ToArray();
        var victim = heroes.First(entry => entry.Value.team.Value == Team.Team1);
        var killer = heroes.First(entry => entry.Value.team.Value == Team.Team2);
        var gameplay = services.Resolve<MobaGameplayService>();
        var scores = services.Resolve<MobaGameplayVariableService>();
        var respawns = services.Resolve<MobaGameplayRespawnService>();
        var killScores = services.Resolve<TeamDeathmatchKillScoreService>();
        var lifecycle = services.Resolve<MobaUnitLifecycleService>();
        var damage = services.Resolve<DamagePipelineService>();
        Assert.Equal(0d, scores.Get(1002));
        var suicide = new UnitDieEventPayload(killer.Key, killer.Key, 0, 0, 0, 1f);
        Assert.False(killScores.TryAwardHeroKill(in suicide, 1000, 1d, out _, out _));
        var ally = heroes.First(entry => entry.Key != killer.Key && entry.Value.team.Value == Team.Team2);
        var friendlyKill = new UnitDieEventPayload(ally.Key, killer.Key, 0, 0, 0, 1f);
        Assert.False(killScores.TryAwardHeroKill(in friendlyKill, 1000, 1d, out _, out _));
        Assert.Equal(0d, scores.Get(1002));
        var deaths = 0;
        var dieEventId = TriggeringIdUtil.GetEventEid(MobaUnitTriggering.Events.Die);
        using var deathSubscription = services.Resolve<AbilityKit.Triggering.Eventing.IEventBus>()
            .Subscribe(new AbilityKit.Core.Eventing.EventKey<object>(dieEventId), _ => deaths++);

        for (var kill = 1; kill <= 5; kill++)
        {
            ExecuteLethalDamage(damage, killer.Key, victim.Key, victim.Value.GetMobaAttrs().Hp);
            if (kill < 5) Assert.Equal(kill, deaths);
            Assert.Equal(kill, scores.Get(1002));
            Assert.Equal(0d, scores.Get(1001));
            if (kill < 5)
            {
                Assert.Equal(MobaGameplayPhase.Running, gameplay.Phase);
                Assert.Equal(victim.Key, Assert.Single(respawns.CapturePending()).ActorId);
                Assert.True(lifecycle.TryRespawn(victim.Key).Succeeded);
                Assert.Empty(respawns.CapturePending());
            }
        }

        Assert.Equal(MobaGameplayPhase.Ended, gameplay.Phase);
        Assert.Empty(respawns.CapturePending());
        Assert.Equal((int)Team.Team2, gameplay.LastResult.WinTeamId);
    }

    [Fact]
    public void Configured_time_limit_ends_game_as_draw()
    {
        using var bootstrapper = new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault());
        bootstrapper.Initialize();
        bootstrapper.Start();
        for (var i = 0; i < 8 && bootstrapper.Context.EcsWorld == null; i++) bootstrapper.Tick();
        bootstrapper.SetupBattle();
        for (var i = 0; i < 10; i++) bootstrapper.Tick();

        var gameplay = bootstrapper.RuntimeServices!.Resolve<MobaGameplayService>();
        Assert.Equal(MobaGameplayPhase.Running, gameplay.Phase);
        Assert.Equal(600000, gameplay.GetClockState().DurationMilliseconds);
        Assert.Equal(600f - gameplay.ElapsedSeconds, gameplay.GetClockState().RemainingSeconds, 2);
        Assert.True(bootstrapper.RuntimeServices.Resolve<MobaSnapshotRouter>()
            .GetHealth().HasEmitter(typeof(MobaGameplayClockSnapshotService)));
        var clockEmitter = new MobaGameplayClockSnapshotService(gameplay);
        Assert.True(clockEmitter.TryGetSnapshot(new FrameIndex(10000), out var first));
        Assert.Equal(GameplayClockSnapshotCodec.OpCode, first.OpCode);
        Assert.True(GameplayClockSnapshotCodec.TryDecode(first.Payload, out var firstClock));
        Assert.Equal(gameplay.GetClockState().RemainingMilliseconds, firstClock.RemainingMilliseconds);
        Assert.False(clockEmitter.TryGetSnapshot(new FrameIndex(10001), out _));
        gameplay.Tick(gameplay.CurrentGameplay.DefaultDurationMs / 1000f);
        Assert.Equal(MobaGameplayPhase.Ended, gameplay.Phase);
        Assert.Equal(0f, gameplay.GetClockState().RemainingSeconds);
        Assert.Equal(0, gameplay.LastResult.WinTeamId);
        Assert.True(clockEmitter.TryGetSnapshot(new FrameIndex(10002), out var ended));
        Assert.True(GameplayClockSnapshotCodec.TryDecode(ended.Payload, out var endedClock));
        Assert.Equal(GameplayClockPhase.Ended, endedClock.Phase);
        Assert.Equal(0, endedClock.RemainingMilliseconds);
    }

    [Fact]
    public void Match_state_snapshot_tracks_score_result_and_reset()
    {
        using var bootstrapper = new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault());
        bootstrapper.Initialize();
        bootstrapper.Start();
        for (var i = 0; i < 8 && bootstrapper.Context.EcsWorld == null; i++) bootstrapper.Tick();
        bootstrapper.SetupBattle();
        for (var i = 0; i < 10; i++) bootstrapper.Tick();

        var services = bootstrapper.RuntimeServices!;
        var gameplay = services.Resolve<MobaGameplayService>();
        var variables = services.Resolve<MobaGameplayVariableService>();
        Assert.True(services.Resolve<MobaSnapshotRouter>().GetHealth()
            .HasEmitter(typeof(MobaGameplayMatchSnapshotService)));
        Assert.True(services.Resolve<MobaSnapshotRouter>().GetHealth()
            .HasEmitter(typeof(MobaCharacterActionSnapshotService)));
        var characterActions = new MobaCharacterActionSnapshotService(services.Resolve<MobaActorRegistry>());
        Assert.True(characterActions.TryGetSnapshot(new FrameIndex(100), out var actionSnapshot));
        Assert.Equal(CharacterActionSnapshotCodec.OpCode, actionSnapshot.OpCode);
        Assert.True(CharacterActionSnapshotCodec.TryDecode(actionSnapshot.Payload, out var actionEntries));
        Assert.NotEmpty(actionEntries);
        var emitter = new MobaGameplayMatchSnapshotService(gameplay, variables);

        Assert.True(emitter.TryGetSnapshot(new FrameIndex(100), out var initial));
        Assert.Equal(GameplayMatchSnapshotCodec.OpCode, initial.OpCode);
        Assert.True(GameplayMatchSnapshotCodec.TryDecode(initial.Payload, out var state));
        Assert.Equal(GameplayClockPhase.Running, state.Phase);
        Assert.Equal(BattleStartConfig.CreateDefault().WorldId, state.MatchId);
        Assert.Equal(100, state.SnapshotFrame);
        Assert.Equal(0d, state.Team1Score);
        Assert.False(GameplayMatchSnapshotCodec.TryDecode(initial.Payload[..^1], out _));
        Assert.False(emitter.TryGetSnapshot(new FrameIndex(101), out _));

        variables.Set(GameplayScoreKeys.Team1, 3d);
        Assert.True(emitter.TryGetSnapshot(new FrameIndex(102), out var scored));
        Assert.True(GameplayMatchSnapshotCodec.TryDecode(scored.Payload, out state));
        Assert.Equal(3d, state.Team1Score);
        Assert.Equal(102, state.SnapshotFrame);
        Assert.False(gameplay.End("invalid", -1));
        Assert.Equal(MobaGameplayPhase.Running, gameplay.Phase);
        Assert.True(gameplay.End("score_limit", 1));
        Assert.True(emitter.TryGetSnapshot(new FrameIndex(103), out var ended));
        Assert.True(GameplayMatchSnapshotCodec.TryDecode(ended.Payload, out state));
        Assert.Equal(GameplayClockPhase.Ended, state.Phase);
        Assert.Equal(1, state.WinTeamId);
        Assert.Equal("score_limit", state.EndReason);

        variables.Set(4321, 7d);
        gameplay.Start(gameplay.CurrentGameplayId);
        Assert.Equal(MobaGameplayPhase.Running, gameplay.Phase);
        Assert.False(variables.TryGet(4321, out _));
        Assert.Equal(0d, variables.Get(GameplayScoreKeys.Team1));
        Assert.True(emitter.TryGetSnapshot(new FrameIndex(104), out var restarted));
        Assert.True(GameplayMatchSnapshotCodec.TryDecode(restarted.Payload, out state));
        Assert.Equal(GameplayClockPhase.Running, state.Phase);
        Assert.Equal(0d, state.Team1Score);

        gameplay.Reset();
        Assert.Empty(variables.Capture());
        Assert.False(gameplay.End("invalid", 1));
        Assert.True(emitter.TryGetSnapshot(new FrameIndex(105), out var reset));
        Assert.True(GameplayMatchSnapshotCodec.TryDecode(reset.Payload, out state));
        Assert.Equal(GameplayClockPhase.NotStarted, state.Phase);
        Assert.Equal(0d, state.Team1Score);
        Assert.Equal(0, state.WinTeamId);
    }

    [Fact]
    public void End_notification_failure_still_commits_ended_phase()
    {
        using var bootstrapper = new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault());
        bootstrapper.Initialize();
        bootstrapper.Start();
        for (var i = 0; i < 8 && bootstrapper.Context.EcsWorld == null; i++) bootstrapper.Tick();
        bootstrapper.SetupBattle();
        for (var i = 0; i < 10; i++) bootstrapper.Tick();

        var gameplay = bootstrapper.RuntimeServices!.Resolve<MobaGameplayService>();
        Assert.Equal(MobaGameplayPhase.Running, gameplay.Phase);
        var sinkField = typeof(MobaGameplayService).GetField("_eventSink",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(sinkField);
        sinkField.SetValue(gameplay, new ThrowingGameplayEventSink());

        Assert.Throws<InvalidOperationException>(() => gameplay.End("test", 1));
        Assert.Equal(MobaGameplayPhase.Ended, gameplay.Phase);
        Assert.Equal("test", gameplay.LastResult.Reason);
        Assert.False(gameplay.End("again", 1));
    }

    [Fact]
    public void Prepared_start_notifies_only_after_explicit_publish()
    {
        using var bootstrapper = new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault());
        bootstrapper.Initialize();
        bootstrapper.Start();
        for (var i = 0; i < 8 && bootstrapper.Context.EcsWorld == null; i++) bootstrapper.Tick();
        bootstrapper.SetupBattle();
        for (var i = 0; i < 10; i++) bootstrapper.Tick();

        var gameplay = bootstrapper.RuntimeServices!.Resolve<MobaGameplayService>();
        var gameplayId = gameplay.CurrentGameplayId;
        gameplay.Reset();
        var sink = new RecordingGameplayEventSink();
        var sinkField = typeof(MobaGameplayService).GetField("_eventSink",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(sinkField);
        sinkField.SetValue(gameplay, sink);

        Assert.True(gameplay.TryPrepareStart(gameplayId, "deferred-start", out var error), error);
        Assert.True(gameplay.CommitPreparedStartDeferred());
        Assert.Equal(MobaGameplayPhase.Running, gameplay.Phase);
        Assert.Equal(0, sink.StartedCount);

        gameplay.PublishStarted();
        gameplay.PublishStarted();
        Assert.Equal(1, sink.StartedCount);
    }

    private sealed class RecordingGameplayEventSink : IMobaGameplayEventSink
    {
        public int StartedCount { get; private set; }
        public void OnGameplayStarted(int frameIndex) => StartedCount++;
        public void OnGameplayEnded(in MobaGameplayResult result) { }
    }

    private sealed class ThrowingGameplayEventSink : IMobaGameplayEventSink
    {
        public void OnGameplayStarted(int frameIndex) { }

        public void OnGameplayEnded(in MobaGameplayResult result) =>
            throw new InvalidOperationException("Intentional end notification failure.");
    }

    [Fact]
    public void Timed_gameplay_requires_one_time_expired_rule()
    {
        using var bootstrapper = new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault());
        bootstrapper.Initialize();
        bootstrapper.Start();
        for (var i = 0; i < 8 && bootstrapper.Context.EcsWorld == null; i++) bootstrapper.Tick();
        bootstrapper.SetupBattle();

        var services = bootstrapper.RuntimeServices!;
        var db = services.Resolve<TriggerPlanJsonDatabase>();
        var events = services.Resolve<MobaEventSubscriptionRegistry>();
        var missingTimeout = new GameplayMO(new GameplayDTO
        {
            Id = 1, DefaultDurationMs = 600000, TriggerIds = new[] { 900002 }
        });
        var duplicateTimeout = new GameplayMO(new GameplayDTO
        {
            Id = 1, DefaultDurationMs = 600000, TriggerIds = new[] { 900001, 900001 }
        });

        Assert.False(MobaGameplayTriggerValidator.ValidateGameplay(missingTimeout, db, events));
        Assert.False(MobaGameplayTriggerValidator.ValidateGameplay(duplicateTimeout, db, events));
    }

    [Fact]
    public void Gameplay_snapshot_restores_running_phase_score_and_time()
    {
        using var bootstrapper = new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault());
        bootstrapper.Initialize();
        bootstrapper.Start();
        for (var i = 0; i < 8 && bootstrapper.Context.EcsWorld == null; i++) bootstrapper.Tick();
        bootstrapper.SetupBattle();
        for (var i = 0; i < 10; i++) bootstrapper.Tick();

        var services = bootstrapper.RuntimeServices!;
        var gameplay = services.Resolve<MobaGameplayService>();
        var scores = services.Resolve<MobaGameplayVariableService>();
        var provider = new MobaGameplayRollbackProvider(gameplay, scores,
            services.Resolve<MobaUnitDeathSubscriber>());
        scores.Set(1002, 2d);
        gameplay.Tick(0.5f);
        var elapsedRaw = gameplay.ElapsedRaw;
        var frame = new FrameIndex(1);
        var snapshot = provider.Export(frame);

        scores.Set(1002, 4d);
        Assert.True(gameplay.End("test", (int)Team.Team1));
        provider.ValidateImport(frame, snapshot);
        provider.Import(frame, snapshot);

        Assert.Equal(MobaGameplayPhase.Running, gameplay.Phase);
        Assert.Equal(elapsedRaw, gameplay.ElapsedRaw);
        Assert.Equal(600f - gameplay.ElapsedSeconds, gameplay.GetClockState().RemainingSeconds, 2);
        Assert.Equal(2d, scores.Get(1002));
        Assert.Equal(0, gameplay.LastResult.WinTeamId);
    }

    private static DamageResult ExecuteLethalDamage(
        DamagePipelineService damage,
        int attackerActorId,
        int targetActorId,
        float targetHp)
    {
        var attack = new AttackInfo
        {
            AttackerActorId = attackerActorId,
            TargetActorId = targetActorId,
            DamageType = DamageType.Physical,
            CritType = CritType.None,
            ReasonKind = DamageReasonKind.Environment,
        };
        attack.BaseDamage.BaseValue = targetHp * 10f + 1f;
        return damage.Execute(attack);
    }

    private static DamageResult ExecuteDamage(
        DamagePipelineService damage,
        int attackerActorId,
        int targetActorId,
        float value)
    {
        var attack = new AttackInfo
        {
            AttackerActorId = attackerActorId,
            TargetActorId = targetActorId,
            DamageType = DamageType.Physical,
            CritType = CritType.None,
            ReasonKind = DamageReasonKind.Environment,
            CombatFlags = MobaCombatExecutionFlags.PeriodicDamage,
        };
        attack.BaseDamage.BaseValue = value;
        return damage.Execute(attack);
    }

    private static void AssertDamageContextChain(
        MobaExecutionContextRegistry contexts,
        long startContextId,
        bool expectPeriodic)
    {
        Assert.True(contexts.TryGet(startContextId, out var attack));
        Assert.True(contexts.TryGet(startContextId + 1, out var calc));
        Assert.True(contexts.TryGet(startContextId + 2, out var apply));
        Assert.Equal(MobaExecutionKind.DamageAttack, attack.Kind);
        Assert.Equal(MobaExecutionKind.DamageCalc, calc.Kind);
        Assert.Equal(MobaExecutionKind.DamageApply, apply.Kind);
        Assert.Equal(attack.ContextId, calc.ParentContextId);
        Assert.Equal(calc.ContextId, apply.ParentContextId);
        Assert.True(attack.IsEnded);
        Assert.True(calc.IsEnded);
        Assert.True(apply.IsEnded);
        Assert.Equal((int)MobaExecutionEndReason.Completed, attack.EndReason);
        Assert.Equal((int)MobaExecutionEndReason.Completed, calc.EndReason);
        Assert.Equal((int)MobaExecutionEndReason.Completed, apply.EndReason);
        Assert.Equal(expectPeriodic, attack.CombatFacts.IsPeriodicDamage);
        Assert.Equal(attack.CombatFacts, calc.CombatFacts);
        Assert.Equal(calc.CombatFacts, apply.CombatFacts);
    }
}
