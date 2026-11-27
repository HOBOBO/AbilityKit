using AbilityKit.Ability.FrameSync;
using AbilityKit.BattleScenario;
using AbilityKit.Demo.Moba.Acceptance;
using AbilityKit.Demo.Moba.BattleScenario;
using AbilityKit.Demo.Moba.EnvironmentModel;
using AbilityKit.Scenario;
using AbilityKit.Protocol.Moba;
using AbilityKit.Game.Battle.Testing;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>验证 MOBA 世界执行核心：TestScenario → boot → 生成 actors → 施放 → 采 trace → 断言判定 → 中性结果。</summary>
public sealed class MobaBattleScenarioWorldRunnerTests
{
    [Theory]
    [InlineData("jhin-q", 3001, 30010101)]
    [InlineData("jinx-w", 3002, 30020201)]
    [InlineData("annie-q", 3003, 30030201)]
    [InlineData("veigar-q-two-targets", 3004, 30040201)]
    [InlineData("gragas-q", 3005, 30050201)]
    [InlineData("karthus-q-two-targets", 3006, 30060201)]
    [InlineData("lux-q-two-targets", 3007, 30070201)]
    public void SevenHeroes_ArchivesAndReopensRealSkillEvidence(string caseId, int heroId, int skillId)
    {
        var relativeCase = $"src/AbilityKit.Demo.Moba.BattleScenario.Runner/Flows/SkillPresentation/{caseId}.battlescenario";
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, relativeCase))) directory = directory.Parent;
        Assert.NotNull(directory);
        var scenario = BattleScenarioCompiler.CompileFile(Path.Combine(directory.FullName, relativeCase));
        string? json = null;
        var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario, null,
            snapshot => json = MobaBattleDiagnosticArtifactCodec.ExportSnapshotToString(snapshot));
        Assert.NotNull(json);
        var output = Environment.GetEnvironmentVariable("ABILITYKIT_SEVEN_HERO_DIAGNOSTIC_DIR");
        if (!string.IsNullOrEmpty(output)) File.WriteAllText(Path.Combine(output, $"{caseId}.json"), json);
        Assert.True(outcome.Result.Passed, outcome.Result.Summary);

        var restored = MobaBattleDiagnosticArtifactCodec.ImportSnapshot(json);
        using var session = new BattleDiagnosticOfflineSession(restored);
        var cast = Assert.Single(restored.Trace.Nodes.Where(node => node.Kind == "SkillCast" && node.ConfigId == skillId));
        Assert.True(cast.SourceObject.IsResolved);
        var caster = session.ResolveRelation(cast.Scope, cast.SourceObject, cast.StartFrame).CatalogEntry;
        Assert.NotNull(caster);
        Assert.Equal(heroId, caster.Value.DefinitionId);
        Assert.Equal(BattleDiagnosticActorKind.Hero, caster.Value.ActorKind);
        Assert.Contains(restored.State.Actors, actor => actor.ActorId == cast.SourceActorId &&
            actor.Generation == cast.SourceActorGeneration);
        Assert.Contains(restored.Events.Events, item => item.Kind == BattleDiagnosticEventKind.EffectStarted &&
            item.SourceActorId == cast.SourceActorId && restored.Trace.Nodes.Any(node =>
                node.ContextId == item.ContextId && node.RootContextId == item.RootContextId));
        Assert.Contains(restored.Objects.Items, item => item.ContextId > 0 &&
            (item.OwnerActorId == cast.SourceActorId || item.SourceActorId == cast.SourceActorId) &&
            (item.Kind == BattleDiagnosticRuntimeObjectKind.Projectile || item.Kind == BattleDiagnosticRuntimeObjectKind.Area));
    }

    [Theory, Trait("Gate", "BattleScenario")]
    [InlineData("veigar-q-two-targets")]
    [InlineData("veigar-r-track-displaced-target")]
    [InlineData("lux-e")]
    [InlineData("lux-e-natural-expiry")]
    [InlineData("lux-q-two-targets")]
    [InlineData("jhin-r")]
    public void SkillPresentationScenarios_PassAuthoritativeAssertions(string caseId)
    {
        var relativeCase = $"src/AbilityKit.Demo.Moba.BattleScenario.Runner/Flows/SkillPresentation/{caseId}.battlescenario";
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, relativeCase))) directory = directory.Parent;
        Assert.NotNull(directory);
        var scenario = BattleScenarioCompiler.CompileFile(Path.Combine(directory.FullName, relativeCase));
        var result = MobaBattleScenarioWorldRunner.Run(scenario);
        Assert.True(result.Passed, result.Summary);
    }

    [Fact, Trait("Gate", "MobaConsoleSmoke")]
    public void VeigarQ_Local1v1Parity_ConsumesCooldownAndHitsOnce()
    {
        const string relativeCase = "src/AbilityKit.Demo.Moba.BattleScenario.Runner/Flows/SkillPresentation/veigar-q-1v1-parity.battlescenario";
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, relativeCase))) directory = directory.Parent;
        Assert.NotNull(directory);
        var scenario = BattleScenarioCompiler.CompileFile(Path.Combine(directory.FullName, relativeCase));
        (float X, float Y, float Z)? initialTargetPosition = null;
        (float X, float Y, float Z) casterPosition = default, targetPosition = default;
        float targetHp = 0;
        long cooldownDuration = 0, remainingCooldown = 0;
        var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario, (world, _) =>
        {
            var caster = world.GetLocalMovementActor("caster");
            Assert.True(world.TryGetActorId("target", out var targetId));
            var skill = Assert.Single(caster.skillLoadout.ActiveSkills, skill => skill?.SkillId == 30040201);
            cooldownDuration = skill.CooldownDurationMs;
            remainingCooldown = skill.CooldownEndTimeMs -
                MobaSkillRuntimeAccess.GetCurrentTimeMs(world.Services.Resolve<IFrameTime>());
            var casterPose = caster.transform.Value.Position;
            casterPosition = (casterPose.X, casterPose.Y, casterPose.Z);
            var targetPose = world.GetActorPosition(targetId);
            targetPosition = (targetPose.X, targetPose.Y, targetPose.Z);
            initialTargetPosition ??= targetPosition;
            targetHp = world.GetActorHp(targetId);
        });

        Assert.True(outcome.Result.Passed, $"{outcome.Result.Summary}; hp={targetHp}, cooldown={cooldownDuration}ms");
        Assert.Equal(6000L, cooldownDuration);
        Assert.InRange(remainingCooldown, 1L, cooldownDuration);
        Assert.Equal((7f, 0f, -4f), casterPosition);
        Assert.Equal((9f, 0f, 1f), initialTargetPosition);
        Assert.Equal((7f, 0f, 1f), targetPosition);
    }

    [Fact, Trait("Gate", "BattleScenario")]
    public void JhinMagazineDoesNotOwnMovementBetweenRealAttacks()
    {
        const string relativeCase = "src/AbilityKit.Demo.Moba.BattleScenario.Runner/Flows/SkillPresentation/jhin-basic-attack-locomotion.battlescenario";
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, relativeCase))) directory = directory.Parent;
        Assert.NotNull(directory);
        var scenario = BattleScenarioCompiler.CompileFile(Path.Combine(directory.FullName, relativeCase));
        var walked = new bool[4];
        var stopped = new bool[4];
        var movePhase = -1;
        var before = 0f;
        var reloadId = 0L;
        var movedDuringReload = false;
        var result = MobaBattleScenarioWorldRunner.RunDetailed(scenario, (world, _) =>
        {
            var actor = world.GetLocalMovementActor("caster");
            if (actor == null || !actor.hasCharacterHfsm) return;
            var machine = actor.characterHfsm.Runtime;
            var action = machine.Action;
            var next = (int)machine.ResolveParameter(10);
            var current = actor.transform.Value.Position.X;
            if (action.SkillId == 30010605 && action.CastInstanceId > 0)
            {
                if (reloadId == 0)
                {
                    reloadId = action.CastInstanceId;
                    before = current;
                    actor.ReplaceMoveInput(-1f, 0f);
                }
                else
                {
                    Assert.Equal(reloadId, action.CastInstanceId);
                    if (Math.Abs(current - before) > .2f)
                    {
                        movedDuringReload = true;
                        actor.ReplaceMoveInput(0f, 0f);
                    }
                }
                return;
            }
            if (machine.FactIsCasting) return;
            var phase = next is >= 2 and <= 4 ? next - 2 : reloadId != 0 && next == 0 ? 3 : -1;
            if (phase < 0 || stopped[phase]) return;
            if (movePhase != phase)
            {
                Assert.EndsWith("/idle", action.Path);
                movePhase = phase;
                before = current;
                actor.ReplaceMoveInput(phase % 2 == 0 ? 1f : -1f, 0f);
            }
            else if (!walked[phase])
            {
                Assert.EndsWith("/moving", action.Path);
                Assert.Equal(0, action.CastInstanceId);
                if (Math.Abs(current - before) <= .2f) return;
                walked[phase] = true;
                actor.ReplaceMoveInput(0f, 0f);
            }
            else
            {
                Assert.EndsWith("/idle", action.Path);
                stopped[phase] = true;
            }
        });
        Assert.True(result.Result.Passed, result.Result.Summary);
        Assert.All(walked, value => Assert.True(value));
        Assert.All(stopped, value => Assert.True(value));
        Assert.True(movedDuringReload);
    }

    [Fact]
    public void Run_ExecutesSetupActionsBeforeAssertions()
    {
        var scenario = new TestScenario
        {
            CaseId = "runner-setup",
            Actors = new[]
            {
                new TestActor
                {
                    Alias = "target",
                    HeroId = 1001,
                    AttributeTemplateId = 1001,
                    TeamId = 2,
                    Position = new TestVector3(4, 0, 0),
                },
            },
            Setup = new[]
            {
                new TestSetupAction
                {
                    Action = "set_attr",
                    ActorAlias = "target",
                    Property = "hp",
                    Value = 1234,
                },
            },
            Expectations = new MobaBattleScenarioAssertions
            {
                State = { new MobaStateAssertion { Alias = "target", Property = "hp", ExpectedValue = "1234" } },
            },
        };

        var result = MobaBattleScenarioWorldRunner.Run(scenario);

        Assert.True(result.Passed, result.Summary);
        Assert.Contains("verdict=PASSED", result.Summary);
    }

    [Fact]
    public void Run_CastsSkillAndCapturesTrace()
    {
        var scenario = BattleScenarioCompiler.Compile("runner-smoke", new BattleBlock[]
        {
            new SetEnvironmentBlock { ProfileId = "jungle-camp" },
            new SpawnActorBlock { Alias = "caster", HeroId = 1001, PlayerId = "player_1", Position = new TestVector3(-15, 0, 0) },
            new SpawnActorBlock { Alias = "target", HeroId = 1001, TeamId = 2, Position = new TestVector3(-12, 0, 0) },
            new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster", TargetAlias = "target", Slot = 1 },
        });

        var result = MobaBattleScenarioWorldRunner.Run(scenario);

        Assert.True(result.Passed);
        Assert.Contains("actors=2", result.Summary);
        Assert.Contains("traceNodes=", result.Summary);
        Assert.Contains("env=jungle-camp(3个)", result.Summary);
    }

    [Fact]
    public void Run_HonorsExplicitTickRateAndDurationEndCondition()
    {
        var scenario = BattleScenarioCompiler.Compile("runner-execution", new BattleBlock[]
        {
            new ExecutionSettingsBlock
            {
                TickRate = 60,
                MaxDurationMs = 1_000,
                SettleDurationMs = 0,
                EndCondition = TestEndConditionKinds.Duration,
                DurationMs = 100,
            },
        });

        var result = MobaBattleScenarioWorldRunner.Run(scenario);

        Assert.True(result.Passed, result.Summary);
        Assert.Contains("tickRate=60", result.Summary);
        Assert.Contains("simulatedMs=100.", result.Summary);
    }

    [Fact]
    public void Run_FailsWhenFixedStepQuantizationExceedsMaxDuration()
    {
        var scenario = BattleScenarioCompiler.Compile("runner-quantized-timeout", new BattleBlock[]
        {
            new ExecutionSettingsBlock
            {
                TickRate = 30,
                MaxDurationMs = 100,
                SettleDurationMs = 0,
                EndCondition = TestEndConditionKinds.Duration,
                DurationMs = 100,
            },
        });

        var error = Assert.Throws<TimeoutException>(() => MobaBattleScenarioWorldRunner.Run(scenario));

        Assert.Contains("maxDurationMs=100", error.Message);
    }

    [Fact]
    public void Run_WithAssertion_ProducesVerdict()
    {
        // 断言一个必然不存在的 trace kind（mustNotContain），verdict 应为 PASSED
        var scenario = BattleScenarioCompiler.Compile("runner-assert", new BattleBlock[]
        {
            new SpawnActorBlock { Alias = "caster", HeroId = 1001, PlayerId = "player_1", Position = new TestVector3(-15, 0, 0) },
            new SpawnActorBlock { Alias = "target", HeroId = 1001, TeamId = 2, Position = new TestVector3(-12, 0, 0) },
            new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster", TargetAlias = "target", Slot = 1 },
            new TestAssertBlock { MustNotContain = new MobaTraceAssertion { Kind = "NonExistentKind", ConfigId = 0 } },
        });

        var result = MobaBattleScenarioWorldRunner.Run(scenario);

        Assert.True(result.Passed);
        Assert.Contains("verdict=PASSED", result.Summary);
    }

    [Fact]
    public void Codec_RoundTripsAssertions()
    {
        var scenario = BattleScenarioCompiler.Compile("codec-assert", new BattleBlock[]
        {
            new TestAssertBlock { MustContain = new MobaTraceAssertion { Kind = "SkillCast", ConfigId = 10010101 } },
        });

        var json = ScenarioCodec.Serialize(scenario);
        var back = ScenarioCodec.Parse(json);

        var assertions = back.Expectations as MobaBattleScenarioAssertions;
        Assert.NotNull(assertions);
        Assert.Single(assertions!.MustContain);
        Assert.Equal("SkillCast", assertions.MustContain[0].Kind);
    }

    [Fact]
    public void NetworkDsl_LossPhaseDropsTimelineSkillInput()
    {
        var blocks = BattleScenarioDslParser.Parse($@"
seed 77
spawn caster hero=1002 attr=1002 player=player_1 pos=0,0,0
spawn target hero=1001 attr=1001 team=2 pos=6,0,0
network phase at=0 until=1000 direction=outbound opcode={MobaOpCodes.Input.SkillInput} loss=1
cast caster target slot=1 at=100
");
        var scenario = BattleScenarioCompiler.Compile("network-loss", blocks);

        var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario);

        Assert.True(outcome.Result.Passed, outcome.Result.Summary);
        Assert.DoesNotContain(outcome.TraceNodes, node => node.Kind == "SkillCast");
        Assert.Contains(outcome.NetworkTrace, entry => entry.Contains("RandomLoss"));
        Assert.Contains("virtualNetwork=0/1", outcome.Result.Summary);
        Assert.Equal(77, scenario.Seed);
    }

    [Fact]
    public void NetworkDsl_SameSeedReplaysBattleAndNetworkFingerprint()
    {
        var blocks = BattleScenarioDslParser.Parse($@"
seed 91
spawn caster hero=1002 attr=1002 player=player_1 pos=0,0,0
spawn target hero=1001 attr=1001 team=2 pos=6,0,0
network phase at=0 until=1000 direction=outbound opcode={MobaOpCodes.Input.SkillInput} latency=80 jitter=20 reorder=0.2
cast caster target slot=1 at=100
");
        var scenario = BattleScenarioCompiler.Compile("network-repeat", blocks);

        var result = MobaBattleScenarioWorldRunner.VerifyDeterminism(scenario);

        Assert.True(result.Matches,
            $"{result.First.DeterminismFingerprint} != {result.Second.DeterminismFingerprint}");
        Assert.Equal(result.First.NetworkTrace, result.Second.NetworkTrace);
        Assert.NotEmpty(result.First.DeterminismFingerprint);
        Assert.Contains(result.First.TraceNodes, node => node.Kind == "SkillCast");
    }

    [Fact]
    public void NetworkDsl_ControlAtSameTimestampRunsBeforeTimelineInput()
    {
        var blocks = BattleScenarioDslParser.Parse(@"
seed 47
spawn caster hero=1002 attr=1002 player=player_1 pos=0,0,0
spawn target hero=1001 attr=1001 team=2 pos=6,0,0
network disconnect at=100
cast caster target slot=1 at=100
network reconnect at=200
cast caster target slot=1 at=200
");
        var scenario = BattleScenarioCompiler.Compile("network-control-order", blocks);

        var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario);

        Assert.True(outcome.Result.Passed, outcome.Result.Summary);
        Assert.Single(outcome.TraceNodes, node => node.Kind == "SkillCast");
        Assert.Contains(outcome.NetworkTrace, entry => entry.Contains("BlockedOutbound"));
        Assert.Contains("virtualNetwork=1/2", outcome.Result.Summary);
    }

    [Fact]
    public void NetworkDsl_WaitAdvancesSimulationCursorWithoutReplayingAbsoluteGap()
    {
        var blocks = BattleScenarioDslParser.Parse(@"
execution tick=10 max=2000 settle=0 end=timeline
network disconnect at=0
wait 100 at=100
wait 100 at=200
");
        var scenario = BattleScenarioCompiler.Compile("network-wait-cursor", blocks);

        var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario);

        Assert.True(outcome.Result.Passed, outcome.Result.Summary);
        Assert.Contains("simulatedMs=300.000", outcome.Result.Summary);
    }

    /// <summary>测试内的断言积木（镜像 MOBA 的 AssertTraceBlock，但直接用 .NET 可访问的 MobaBattleScenarioAssertions）。</summary>
    [Fact]
    public void PredictionDsl_ReconnectHashMismatchRollsBackAndReplaysDeterministically()
    {
        var scenario = CompilePredictionScenario("eq", "6");

        var verification = MobaBattleScenarioWorldRunner.VerifyDeterminism(scenario);
        var outcome = verification.First;

        Assert.True(verification.Matches);
        Assert.True(outcome.Result.Passed, outcome.Result.Summary);
        Assert.NotNull(outcome.Prediction);
        Assert.Equal(1, outcome.Prediction!.Mismatches);
        Assert.Equal(1, outcome.Prediction.Rollbacks);
        Assert.Equal(2, outcome.Prediction.MismatchFrame);
        Assert.Equal(1, outcome.Prediction.RollbackFrame);
        Assert.Equal(3, outcome.Prediction.ConfirmedFrame);
        Assert.Equal(3, outcome.Prediction.PredictedFrame);
        Assert.True(outcome.Prediction.WasReplaying);
        Assert.False(outcome.Prediction.IsReplaying);
        Assert.Equal("6", outcome.Prediction.FinalStateHash);
        Assert.NotEmpty(outcome.PredictionTrace);
        Assert.Equal(outcome.PredictionTrace, verification.Second.PredictionTrace);
        Assert.Contains("mismatch=1, rollback=1", outcome.Result.Summary);
    }

    [Fact]
    public void PredictionDsl_FailedAssertionFailsScenarioVerdict()
    {
        var scenario = CompilePredictionScenario("eq", "999");

        var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario);

        Assert.False(outcome.Result.Passed);
        Assert.Contains(outcome.PredictionFailures, failure =>
            failure.Contains("prediction.finalHash", StringComparison.Ordinal));
        Assert.Contains("syncFailures=", outcome.Result.Summary);
        Assert.Contains("verdict=FAILED", outcome.Result.Summary);
    }

    private static TestScenario CompilePredictionScenario(
        string finalHashComparator,
        string finalHash)
    {
        var blocks = MobaBattleScenarioDslParser.Parse($$"""
            seed 83
            network packet inbound opcode=5202 seq=1 frame=1 at=0
            network disconnect at=34
            network packet inbound opcode=5202 seq=2 frame=2 at=68
            network packet inbound opcode=5202 seq=3 frame=3 at=102
            network reconnect at=136
            network packet inbound opcode=5202 seq=102 frame=2 hash=999 at=136
            network packet inbound opcode=5202 seq=103 frame=3 hash=6 at=136
            network packet inbound opcode=5202 seq=104 frame=4 at=238
            assert-sync predictedHashes gte 3
            assert-sync mismatch eq 1
            assert-sync rollback eq 1
            assert-sync mismatchFrame eq 2
            assert-sync rollbackFrame eq 1
            assert-sync confirmedFrame eq 3
            assert-sync predictedFrame eq 3
            assert-sync wasReplaying eq true
            assert-sync replaying eq false
            assert-sync finalHash {{finalHashComparator}} {{finalHash}}
            """);
        return BattleScenarioCompiler.Compile("prediction-reconcile", blocks);
    }

    [Theory]
    [InlineData("headless")]
    [InlineData("unity-route")]
    public void SyncBackend_RoundTripsWithAssertionsInEitherOrder(string backend)
    {
        foreach (var text in new[]
        {
            $"sync-backend {backend}\nassert-sync rollback eq 1",
            $"assert-sync rollback eq 1\nsync-backend {backend}",
        })
        {
            var blocks = MobaBattleScenarioDslParser.Parse(text);
            var document = new BattleScenarioDocument { CaseId = "backend", Blocks = blocks.ToList() };
            var loaded = BattleScenarioCodec.Parse(BattleScenarioCodec.Serialize(document));
            var scenario = BattleScenarioCompiler.Compile(loaded.CaseId, loaded.Blocks);
            var restored = ScenarioCodec.Parse(ScenarioCodec.Serialize(scenario));
            var assertions = Assert.IsType<MobaBattleScenarioAssertions>(restored.Expectations);
            Assert.Equal(backend, assertions.PredictionBackend);
            Assert.Single(assertions.Prediction);
            Assert.Empty(restored.Commands);
        }
    }

    [Theory]
    [InlineData("sync-backend")]
    [InlineData("sync-backend socket")]
    [InlineData("sync-backend headless extra")]
    [InlineData("assert-sync rollback eq")]
    public void SyncBackend_InvalidSyntaxFails(string text)
        => Assert.Throws<ArgumentException>(() => MobaBattleScenarioDslParser.Parse(text));

    [Fact]
    public void SyncBackend_UnityRouteCannotFallBackToConsole()
    {
        var scenario = BattleScenarioCompiler.Compile("unity-only",
            MobaBattleScenarioDslParser.Parse("sync-backend unity-route"));
        Assert.Throws<NotSupportedException>(() => MobaBattleScenarioPredictionRunner.Run(scenario));
        Assert.Throws<NotSupportedException>(() => MobaBattleScenarioWorldRunner.RunDetailed(scenario));
        Assert.Throws<InvalidOperationException>(() => MobaBattleScenarioPredictionRunner.Run(
            scenario, new HeadlessMobaPredictionScenarioBackend()));
    }

    [Fact]
    public void SyncBackend_DefaultAndExplicitHeadlessHaveSameResult()
    {
        var scenario = CompilePredictionScenario("eq", "6");
        var first = MobaBattleScenarioPredictionRunner.Run(scenario);
        ((MobaBattleScenarioAssertions)scenario.Expectations!).PredictionBackend = "headless";
        var second = MobaBattleScenarioPredictionRunner.Run(scenario);
        Assert.Equal("headless", first.BackendId);
        Assert.Equal(first.StateTrace, second.StateTrace);
        Assert.Equal(first.DeterminismFingerprint, second.DeterminismFingerprint);
    }

    [Fact]
    public void MobaParser_NestedProjectParsingDoesNotChangeGlobalFactory()
    {
        var previous = BattleScenarioDslParser.AssertFactory;
        try
        {
            BattleBlock? NestedFactory(string verb, string[] args)
            {
                Assert.Single(MobaBattleScenarioDslParser.Parse("assert-sync rollback eq 1"));
                return new AssertPredictionBlock { Property = "rollback", ExpectedValue = "1" };
            }
            Func<string, string[], BattleBlock?> factory = NestedFactory;
            BattleScenarioDslParser.AssertFactory = factory;
            Assert.Equal(2, MobaBattleScenarioDslParser.Parse(
                "sync-backend headless\nassert-custom").Count);
            Assert.Same(factory, BattleScenarioDslParser.AssertFactory);
        }
        finally { BattleScenarioDslParser.AssertFactory = previous; }
    }

    private sealed class TestAssertBlock : BattleAtomicBlock
    {
        public MobaTraceAssertion? MustContain { get; set; }
        public MobaTraceAssertion? MustNotContain { get; set; }

        public override void Compile(BattleScenarioBuilder builder)
        {
            var assertions = builder.Expectations as MobaBattleScenarioAssertions ?? new MobaBattleScenarioAssertions();
            if (MustContain != null) assertions.MustContain.Add(MustContain);
            if (MustNotContain != null) assertions.MustNotContain.Add(MustNotContain);
            builder.SetExpectations(assertions);
        }
    }
}
