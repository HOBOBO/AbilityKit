using AbilityKit.BattleFlow;
using AbilityKit.Demo.Moba.BattleFlow;
using AbilityKit.Demo.Moba.EnvironmentModel;
using AbilityKit.Scenario;
using AbilityKit.Protocol.Moba;
using AbilityKit.Game.Battle.Testing;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>验证 MOBA 世界执行核心：TestScenario → boot → 生成 actors → 施放 → 采 trace → 断言判定 → 中性结果。</summary>
public sealed class MobaBattleFlowScenarioRunnerTests
{
    [Fact]
    public void Run_CastsSkillAndCapturesTrace()
    {
        var scenario = BattleFlowCompiler.Compile("runner-smoke", new BattleBlock[]
        {
            new SetEnvironmentBlock { ProfileId = "jungle-camp" },
            new SpawnActorBlock { Alias = "caster", HeroId = 1001, PlayerId = "player_1", Position = new TestVector3(-15, 0, 0) },
            new SpawnActorBlock { Alias = "target", HeroId = 1001, TeamId = 2, Position = new TestVector3(-12, 0, 0) },
            new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster", TargetAlias = "target", Slot = 1 },
        });

        var result = MobaBattleFlowScenarioRunner.Run(scenario);

        Assert.True(result.Passed);
        Assert.Contains("actors=2", result.Summary);
        Assert.Contains("traceNodes=", result.Summary);
        Assert.Contains("env=jungle-camp(3个)", result.Summary);
    }

    [Fact]
    public void Run_HonorsExplicitTickRateAndDurationEndCondition()
    {
        var scenario = BattleFlowCompiler.Compile("runner-execution", new BattleBlock[]
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

        var result = MobaBattleFlowScenarioRunner.Run(scenario);

        Assert.True(result.Passed, result.Summary);
        Assert.Contains("tickRate=60", result.Summary);
        Assert.Contains("simulatedMs=100.", result.Summary);
    }

    [Fact]
    public void Run_FailsWhenFixedStepQuantizationExceedsMaxDuration()
    {
        var scenario = BattleFlowCompiler.Compile("runner-quantized-timeout", new BattleBlock[]
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

        var error = Assert.Throws<TimeoutException>(() => MobaBattleFlowScenarioRunner.Run(scenario));

        Assert.Contains("maxDurationMs=100", error.Message);
    }

    [Fact]
    public void Run_WithAssertion_ProducesVerdict()
    {
        // 断言一个必然不存在的 trace kind（mustNotContain），verdict 应为 PASSED
        var scenario = BattleFlowCompiler.Compile("runner-assert", new BattleBlock[]
        {
            new SpawnActorBlock { Alias = "caster", HeroId = 1001, PlayerId = "player_1", Position = new TestVector3(-15, 0, 0) },
            new SpawnActorBlock { Alias = "target", HeroId = 1001, TeamId = 2, Position = new TestVector3(-12, 0, 0) },
            new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster", TargetAlias = "target", Slot = 1 },
            new TestAssertBlock { MustNotContain = new MobaTraceAssertion { Kind = "NonExistentKind", ConfigId = 0 } },
        });

        var result = MobaBattleFlowScenarioRunner.Run(scenario);

        Assert.True(result.Passed);
        Assert.Contains("verdict=PASSED", result.Summary);
    }

    [Fact]
    public void Codec_RoundTripsAssertions()
    {
        var scenario = BattleFlowCompiler.Compile("codec-assert", new BattleBlock[]
        {
            new TestAssertBlock { MustContain = new MobaTraceAssertion { Kind = "SkillCast", ConfigId = 10010101 } },
        });

        var json = ScenarioCodec.Serialize(scenario);
        var back = ScenarioCodec.Parse(json);

        var assertions = back.Expectations as MobaBattleFlowAssertions;
        Assert.NotNull(assertions);
        Assert.Single(assertions!.MustContain);
        Assert.Equal("SkillCast", assertions.MustContain[0].Kind);
    }

    [Fact]
    public void NetworkDsl_LossPhaseDropsTimelineSkillInput()
    {
        var blocks = BattleFlowDslParser.Parse($@"
seed 77
spawn caster hero=1002 attr=1002 player=player_1 pos=0,0,0
spawn target hero=1001 attr=1001 team=2 pos=6,0,0
network phase at=0 until=1000 direction=outbound opcode={MobaOpCodes.Input.SkillInput} loss=1
cast caster target slot=1 at=100
");
        var scenario = BattleFlowCompiler.Compile("network-loss", blocks);

        var outcome = MobaBattleFlowScenarioRunner.RunDetailed(scenario);

        Assert.True(outcome.Result.Passed, outcome.Result.Summary);
        Assert.DoesNotContain(outcome.TraceNodes, node => node.Kind == "SkillCast");
        Assert.Contains(outcome.NetworkTrace, entry => entry.Contains("RandomLoss"));
        Assert.Contains("virtualNetwork=0/1", outcome.Result.Summary);
        Assert.Equal(77, scenario.Seed);
    }

    [Fact]
    public void NetworkDsl_SameSeedReplaysBattleAndNetworkFingerprint()
    {
        var blocks = BattleFlowDslParser.Parse($@"
seed 91
spawn caster hero=1002 attr=1002 player=player_1 pos=0,0,0
spawn target hero=1001 attr=1001 team=2 pos=6,0,0
network phase at=0 until=1000 direction=outbound opcode={MobaOpCodes.Input.SkillInput} latency=80 jitter=20 reorder=0.2
cast caster target slot=1 at=100
");
        var scenario = BattleFlowCompiler.Compile("network-repeat", blocks);

        var result = MobaBattleFlowScenarioRunner.VerifyDeterminism(scenario);

        Assert.True(result.Matches,
            $"{result.First.DeterminismFingerprint} != {result.Second.DeterminismFingerprint}");
        Assert.Equal(result.First.NetworkTrace, result.Second.NetworkTrace);
        Assert.NotEmpty(result.First.DeterminismFingerprint);
        Assert.Contains(result.First.TraceNodes, node => node.Kind == "SkillCast");
    }

    [Fact]
    public void NetworkDsl_ControlAtSameTimestampRunsBeforeTimelineInput()
    {
        var blocks = BattleFlowDslParser.Parse(@"
seed 47
spawn caster hero=1002 attr=1002 player=player_1 pos=0,0,0
spawn target hero=1001 attr=1001 team=2 pos=6,0,0
network disconnect at=100
cast caster target slot=1 at=100
network reconnect at=200
cast caster target slot=1 at=200
");
        var scenario = BattleFlowCompiler.Compile("network-control-order", blocks);

        var outcome = MobaBattleFlowScenarioRunner.RunDetailed(scenario);

        Assert.True(outcome.Result.Passed, outcome.Result.Summary);
        Assert.Single(outcome.TraceNodes, node => node.Kind == "SkillCast");
        Assert.Contains(outcome.NetworkTrace, entry => entry.Contains("BlockedOutbound"));
        Assert.Contains("virtualNetwork=1/2", outcome.Result.Summary);
    }

    [Fact]
    public void NetworkDsl_WaitAdvancesSimulationCursorWithoutReplayingAbsoluteGap()
    {
        var blocks = BattleFlowDslParser.Parse(@"
execution tick=10 max=2000 settle=0 end=timeline
network disconnect at=0
wait 100 at=100
wait 100 at=200
");
        var scenario = BattleFlowCompiler.Compile("network-wait-cursor", blocks);

        var outcome = MobaBattleFlowScenarioRunner.RunDetailed(scenario);

        Assert.True(outcome.Result.Passed, outcome.Result.Summary);
        Assert.Contains("simulatedMs=300.000", outcome.Result.Summary);
    }

    /// <summary>测试内的断言积木（镜像 MOBA 的 AssertTraceBlock，但直接用 .NET 可访问的 MobaBattleFlowAssertions）。</summary>
    [Fact]
    public void PredictionDsl_ReconnectHashMismatchRollsBackAndReplaysDeterministically()
    {
        var scenario = CompilePredictionScenario("eq", "6");

        var verification = MobaBattleFlowScenarioRunner.VerifyDeterminism(scenario);
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

        var outcome = MobaBattleFlowScenarioRunner.RunDetailed(scenario);

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
        var blocks = MobaBattleFlowDslParser.Parse($$"""
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
        return BattleFlowCompiler.Compile("prediction-reconcile", blocks);
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
            var blocks = MobaBattleFlowDslParser.Parse(text);
            var document = new BattleFlowDocument { CaseId = "backend", Blocks = blocks.ToList() };
            var loaded = BattleFlowCodec.Parse(BattleFlowCodec.Serialize(document));
            var scenario = BattleFlowCompiler.Compile(loaded.CaseId, loaded.Blocks);
            var restored = ScenarioCodec.Parse(ScenarioCodec.Serialize(scenario));
            var assertions = Assert.IsType<MobaBattleFlowAssertions>(restored.Expectations);
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
        => Assert.Throws<ArgumentException>(() => MobaBattleFlowDslParser.Parse(text));

    [Fact]
    public void SyncBackend_UnityRouteCannotFallBackToConsole()
    {
        var scenario = BattleFlowCompiler.Compile("unity-only",
            MobaBattleFlowDslParser.Parse("sync-backend unity-route"));
        Assert.Throws<NotSupportedException>(() => MobaBattleFlowPredictionScenarioRunner.Run(scenario));
        Assert.Throws<NotSupportedException>(() => MobaBattleFlowScenarioRunner.RunDetailed(scenario));
        Assert.Throws<InvalidOperationException>(() => MobaBattleFlowPredictionScenarioRunner.Run(
            scenario, new HeadlessMobaPredictionScenarioBackend()));
    }

    [Fact]
    public void SyncBackend_DefaultAndExplicitHeadlessHaveSameResult()
    {
        var scenario = CompilePredictionScenario("eq", "6");
        var first = MobaBattleFlowPredictionScenarioRunner.Run(scenario);
        ((MobaBattleFlowAssertions)scenario.Expectations!).PredictionBackend = "headless";
        var second = MobaBattleFlowPredictionScenarioRunner.Run(scenario);
        Assert.Equal("headless", first.BackendId);
        Assert.Equal(first.StateTrace, second.StateTrace);
        Assert.Equal(first.DeterminismFingerprint, second.DeterminismFingerprint);
    }

    [Fact]
    public void MobaParser_NestedProjectParsingDoesNotChangeGlobalFactory()
    {
        var previous = BattleFlowDslParser.AssertFactory;
        try
        {
            BattleBlock? NestedFactory(string verb, string[] args)
            {
                Assert.Single(MobaBattleFlowDslParser.Parse("assert-sync rollback eq 1"));
                return new AssertPredictionBlock { Property = "rollback", ExpectedValue = "1" };
            }
            Func<string, string[], BattleBlock?> factory = NestedFactory;
            BattleFlowDslParser.AssertFactory = factory;
            Assert.Equal(2, MobaBattleFlowDslParser.Parse(
                "sync-backend headless\nassert-custom").Count);
            Assert.Same(factory, BattleFlowDslParser.AssertFactory);
        }
        finally { BattleFlowDslParser.AssertFactory = previous; }
    }

    private sealed class TestAssertBlock : BattleAtomicBlock
    {
        public MobaTraceAssertion? MustContain { get; set; }
        public MobaTraceAssertion? MustNotContain { get; set; }

        public override void Compile(BattleFlowBuilder builder)
        {
            var assertions = builder.Expectations as MobaBattleFlowAssertions ?? new MobaBattleFlowAssertions();
            if (MustContain != null) assertions.MustContain.Add(MustContain);
            if (MustNotContain != null) assertions.MustNotContain.Add(MustNotContain);
            builder.SetExpectations(assertions);
        }
    }
}
