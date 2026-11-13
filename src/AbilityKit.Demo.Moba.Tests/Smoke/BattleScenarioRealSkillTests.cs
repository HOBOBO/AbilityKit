using AbilityKit.BattleScenario;
using AbilityKit.Demo.Moba.Acceptance;
using AbilityKit.Demo.Moba.BattleScenario;
using AbilityKit.Demo.Moba.EnvironmentModel;
using AbilityKit.Scenario;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>
/// 端到端：真实英雄（属性+技能 loadout）→ 施放真实技能 → 真实 trace（SkillCast 命中指定 configId）+ 真实状态（maxhp &gt; 0）。
/// 这是「从 smoke 到真实」的核心回归：spawn 出的 actor 必须有 SkillLoadout（否则 cast 不出指定技能）和 AttributeGroup（否则 maxhp=0）。
/// </summary>
[Trait("Gate", "BattleScenario")]
public sealed class BattleScenarioRealSkillTests
{
    [Theory]
    [InlineData(1001, 10010101)]
    [InlineData(1002, 10020101)]
    [InlineData(1003, 10030101)]
    [InlineData(1004, 10040101)]
    [InlineData(1005, 10050101)]
    [InlineData(1006, 10060101)]
    [InlineData(1007, 10070101)]
    [InlineData(3004, 30040201)]
    public void Luban_seven_heroes_spawn_and_cast_first_skill(int heroId, int skillId)
    {
        var scenario = BattleScenarioCompiler.Compile($"luban-hero-{heroId}", new BattleBlock[]
        {
            new SpawnActorBlock { Alias = "caster", HeroId = heroId, AttributeTemplateId = heroId,
                PlayerId = "player_1", Position = new TestVector3(0, 0, 0) },
            new SpawnActorBlock { Alias = "target", HeroId = 1001, AttributeTemplateId = 1001,
                TeamId = 2, Position = new TestVector3(6, 0, 0) },
            new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster", TargetAlias = "target", Slot = 1 },
            new AssertionsBlock(new MobaBattleScenarioAssertions
            {
                MustContain = { new MobaTraceAssertion { Kind = "SkillCast", ConfigId = skillId } },
            }),
        });

        var result = MobaBattleScenarioWorldRunner.Run(scenario);
        Assert.True(result.Passed, result.Summary);
    }

    [Fact]
    public void Veigar_placeholder_q_loads_its_own_flow_and_deals_fixed_damage()
    {
        var scenario = BattleScenarioCompiler.Compile("veigar-q-placeholder", new BattleBlock[]
        {
            new SpawnActorBlock { Alias = "caster", HeroId = 3004, AttributeTemplateId = 3004,
                PlayerId = "player_1", Position = new TestVector3(0, 0, 0) },
            new SpawnActorBlock { Alias = "target", HeroId = 1001, AttributeTemplateId = 1001,
                TeamId = 2, Position = new TestVector3(4, 0, 0) },
            new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster",
                TargetAlias = "target", Slot = 1 },
            new AssertionsBlock(new MobaBattleScenarioAssertions
            {
                State = { VeigarWHp("target", "lt", "50000") },
            }),
        });

        var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario);

        Assert.True(outcome.Result.Passed,
            outcome.Result.Summary + " trace=" + string.Join(",", outcome.TraceNodes.Select(node => $"{node.Kind}:{node.ConfigId}@{node.Frame}")));
        Assert.Contains(outcome.TraceNodes, node => node.Kind == "SkillCast" && node.ConfigId == 30040201);
        Assert.Contains(outcome.TraceNodes, node => node.Kind == "ProjectileLaunch" && node.ConfigId == 32040101);
        Assert.True(outcome.TraceNodes.Any(node => node.Kind == "DamageApply" && node.ConfigId == 30040201),
            string.Join(",", outcome.TraceNodes.Select(node => $"{node.Kind}:{node.ConfigId}@{node.Frame}")));
    }

    [Fact]
    public void Veigar_passive_adds_its_buff_after_q_kills_a_low_health_target()
    {
        var assertions = new MobaBattleScenarioAssertions();
        assertions.MustContain.Add(new MobaTraceAssertion { Kind = "DamageApply", ConfigId = 30040201 });
        assertions.State.Add(new MobaStateAssertion
        {
            Alias = "caster",
            Property = "hasbuff",
            ExpectedValue = "true",
        });
        var scenario = new TestScenario
        {
            CaseId = "veigar-passive-q-kill",
            Actors = new[]
            {
                new TestActor
                {
                    Alias = "caster", PlayerId = "player_1", HeroId = 3004,
                    AttributeTemplateId = 3004, TeamId = 1, Position = new TestVector3(0, 0, 0),
                },
                new TestActor
                {
                    Alias = "target", HeroId = 1001, AttributeTemplateId = 1001,
                    TeamId = 2, Position = new TestVector3(4, 0, 0),
                },
            },
            Setup = new[]
            {
                new TestSetupAction
                {
                    Action = "set_attr", ActorAlias = "target", Property = "hp", Value = 1,
                },
            },
            Timeline = new[]
            {
                new TestTimelineStep
                {
                    AtMs = 100, Action = "cast_skill", ActorAlias = "caster", TargetAlias = "target", Slot = 1,
                },
            },
            Expectations = assertions,
        };

        var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario);

        Assert.True(outcome.Result.Passed,
            outcome.Result.Summary + " trace=" + string.Join(",", outcome.TraceNodes.Select(node => $"{node.Kind}:{node.ConfigId}@{node.Frame}")));
        Assert.Contains(outcome.TraceNodes, node => node.Kind == "DamageApply" && node.ConfigId == 30040201);
    }

    [Fact]
    public void Veigar_placeholder_q_repeated_input_during_cooldown_commits_once()
    {
        var scenario = BattleScenarioCompiler.Compile("veigar-q-cooldown-placeholder", new BattleBlock[]
        {
            new ExecutionSettingsBlock
            {
                TickRate = 30,
                MaxDurationMs = 2500,
                SettleDurationMs = 0,
                EndCondition = TestEndConditionKinds.Duration,
                DurationMs = 1200,
            },
            VeigarCaster(),
            VeigarWTarget("target", 2, 4, 0),
            new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster",
                TargetAlias = "target", Slot = 1 },
            new TimelineStepBlock { AtMs = 200, Action = "cast_skill", ActorAlias = "caster",
                TargetAlias = "target", Slot = 1 },
            new AssertionsBlock(new MobaBattleScenarioAssertions
            {
                State = { VeigarWHp("target", "lt", "50000") },
            }),
        });

        var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario);

        Assert.True(outcome.Result.Passed,
            outcome.Result.Summary + " trace=" + string.Join(",", outcome.TraceNodes.Select(node => $"{node.Kind}:{node.ConfigId}@{node.Frame}")));
        Assert.Single(outcome.TraceNodes,
            node => node.Kind == "SkillCast" && node.ConfigId == 30040201);
        Assert.Contains(outcome.TraceNodes,
            node => node.Kind == "ProjectileLaunch" && node.ConfigId == 32040101);
        Assert.Single(outcome.TraceNodes,
            node => node.Kind == "DamageApply" && node.ConfigId == 30040201);
    }

    [Fact]
    public void Veigar_placeholder_q_pierces_two_aligned_enemies_with_one_hit_trigger_each()
    {
        var scenario = BattleScenarioCompiler.Compile("veigar-q-pierce-two-targets-placeholder", new BattleBlock[]
        {
            new ExecutionSettingsBlock
            {
                TickRate = 30,
                MaxDurationMs = 1800,
                SettleDurationMs = 0,
                EndCondition = TestEndConditionKinds.Duration,
                DurationMs = 1000,
            },
            VeigarCaster(),
            VeigarWTarget("front", 2, 4, 0),
            VeigarWTarget("back", 2, 5.5f, 0),
            new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster",
                TargetAlias = "front", Slot = 1 },
            new AssertionsBlock(new MobaBattleScenarioAssertions
            {
                State =
                {
                    VeigarWHp("front", "lt", "50000"),
                    VeigarWHp("back", "lt", "50000"),
                },
            }),
        });

        var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario);

        Assert.True(outcome.Result.Passed,
            outcome.Result.Summary + " trace=" + string.Join(",", outcome.TraceNodes.Select(node => $"{node.Kind}:{node.ConfigId}@{node.Frame}")));
        Assert.Equal(2, outcome.TraceNodes.Count(node => node.Kind == "DamageApply" && node.ConfigId == 30040201));
        Assert.Equal(2, outcome.TraceNodes.Count(node => node.Kind == "ProjectileHit" && node.ConfigId == 32040101));
    }

    [Fact]
    public void Veigar_placeholder_r_tracks_a_moving_target()
    {
        var scenario = BattleScenarioCompiler.Compile("veigar-r-moving-target-placeholder", new BattleBlock[]
        {
            new ExecutionSettingsBlock
            {
                TickRate = 30,
                MaxDurationMs = 2500,
                SettleDurationMs = 0,
                EndCondition = TestEndConditionKinds.Duration,
                DurationMs = 1400,
            },
            VeigarCaster(),
            // Use a clear lane; the arena's blocker around (5, 2) now correctly blocks R.
            VeigarWTarget("target", 2, 0, 5),
            new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster",
                TargetAlias = "target", Slot = 4 },
            new MoveToBlock { AtMs = 150, ActorAlias = "target", Position = new TestVector3(1, 0, 5) },
            new AssertionsBlock(new MobaBattleScenarioAssertions
            {
                State = { VeigarWHp("target", "lt", "50000") },
            }),
        });

        var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario);

        Assert.True(outcome.Result.Passed,
            outcome.Result.Summary + " trace=" + string.Join(",", outcome.TraceNodes.Select(node => $"{node.Kind}:{node.ConfigId}@{node.Frame}")));
        Assert.Contains(outcome.TraceNodes,
            node => node.Kind == "ProjectileLaunch" && node.ConfigId == 32040501);
        Assert.Single(outcome.TraceNodes,
            node => node.Kind == "DamageApply" && node.ConfigId == 30040501);
    }

    [Fact]
    public void Veigar_placeholder_w_spawns_delayed_area_and_damages_its_aim_point()
    {
        var scenario = BattleScenarioCompiler.Compile("veigar-w-placeholder", new BattleBlock[]
        {
            new ExecutionSettingsBlock
            {
                TickRate = 30,
                MaxDurationMs = 2500,
                SettleDurationMs = 0,
                EndCondition = TestEndConditionKinds.Duration,
                DurationMs = 1800,
            },
            new SpawnActorBlock { Alias = "caster", HeroId = 3004, AttributeTemplateId = 3004,
                PlayerId = "player_1", TeamId = 1, Position = new TestVector3(0, 0, 0) },
            new SpawnActorBlock { Alias = "target", HeroId = 1001, AttributeTemplateId = 1001,
                TeamId = 2, Position = new TestVector3(0, 0, 5) },
            new AimTimelineStepBlock("caster", 2, new TestVector3(0, 0, 5)),
            new AssertionsBlock(new MobaBattleScenarioAssertions
            {
                MustContain =
                {
                    new MobaTraceAssertion { Kind = "SkillCast", ConfigId = 30040301 },
                    new MobaTraceAssertion { Kind = "AreaSpawn", ConfigId = 42040301 },
                    new MobaTraceAssertion { Kind = "DamageApply", ConfigId = 30040301 },
                },
                State =
                {
                    new MobaStateAssertion { Alias = "target", Property = "hp", Comparator = "lt", ExpectedValue = "50000" },
                },
            }),
        });

        var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario);

        Assert.True(outcome.Result.Passed, outcome.Result.Summary);
        Assert.Contains(outcome.TraceNodes, node => node.Kind == "AreaSpawn" && node.ConfigId == 42040301);
        Assert.Contains(outcome.TraceNodes, node => node.Kind == "DamageApply" && node.ConfigId == 30040301);
    }

    [Theory]
    [InlineData(30)]
    public void Veigar_placeholder_w_does_not_damage_before_its_delay(int tickRate)
    {
        var scenario = BattleScenarioCompiler.Compile($"veigar-w-before-delay-{tickRate}", new BattleBlock[]
        {
            VeigarWExecution(tickRate, 400),
            VeigarCaster(),
            VeigarWTarget("target", 2, 0, 5),
            new AimTimelineStepBlock("caster", 2, new TestVector3(0, 0, 5)),
            new AssertionsBlock(new MobaBattleScenarioAssertions
            {
                MustContain = { new MobaTraceAssertion { Kind = "AreaSpawn", ConfigId = 42040301 } },
                MustNotContain =
                {
                    new MobaTraceAssertion { Kind = "DamageApply", ConfigId = 30040301 },
                    new MobaTraceAssertion { Kind = "AreaExpire", ConfigId = 42040301 },
                },
                State = { VeigarWHp("target", "eq", "50000") },
            }),
        });

        var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario);

        Assert.True(outcome.Result.Passed,
            outcome.Result.Summary + " trace=" + string.Join(",", outcome.TraceNodes.Select(node => $"{node.Kind}:{node.ConfigId}@{node.Frame}")));
    }

    [Theory]
    [InlineData(30)]
    public void Veigar_placeholder_w_hits_each_enemy_in_its_circle_once_and_expires(int tickRate)
    {
        var scenario = BattleScenarioCompiler.Compile($"veigar-w-circle-{tickRate}", new BattleBlock[]
        {
            VeigarWExecution(tickRate),
            VeigarCaster(),
            VeigarWTarget("center", 2, 0, 5),
            VeigarWTarget("inside_edge", 2, 4.4f, 5),
            VeigarWTarget("outside_edge", 2, 4.6f, 5),
            // Both axes are inside the bounding square; the point is outside the circle.
            VeigarWTarget("outside_diagonal", 2, 4, 9),
            VeigarWTarget("ally", 1, -2, 5),
            new AimTimelineStepBlock("caster", 2, new TestVector3(0, 0, 5)),
            new AssertionsBlock(new MobaBattleScenarioAssertions
            {
                State =
                {
                    VeigarWHp("center", "lt", "50000"),
                    VeigarWHp("inside_edge", "lt", "50000"),
                    VeigarWHp("outside_edge", "eq", "50000"),
                    VeigarWHp("outside_diagonal", "eq", "50000"),
                    VeigarWHp("ally", "eq", "50000"),
                },
            }),
        });

        var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario);

        Assert.True(outcome.Result.Passed, outcome.Result.Summary);
        var spawn = Assert.Single(outcome.TraceNodes,
            node => node.Kind == "AreaSpawn" && node.ConfigId == 42040301);
        var expire = Assert.Single(outcome.TraceNodes,
            node => node.Kind == "AreaExpire" && node.ConfigId == 42040301);
        var damage = outcome.TraceNodes.Where(node => node.Kind == "DamageApply" && node.ConfigId == 30040301).ToArray();
        // Both enemies lose HP and there are exactly two applications through the full lifetime.
        Assert.Equal(2, damage.Length);
        Assert.All(damage, node =>
        {
            Assert.Equal(spawn.RootId, node.RootId);
            Assert.True(node.Frame > spawn.Frame && node.Frame < expire.Frame);
        });
    }

    [Theory]
    [InlineData(30)]
    public void Veigar_placeholder_w_samples_enemies_at_impact_and_keeps_its_committed_center(int tickRate)
    {
        var scenario = BattleScenarioCompiler.Compile($"veigar-w-movement-{tickRate}", new BattleBlock[]
        {
            VeigarWExecution(tickRate),
            VeigarCaster(),
            VeigarWTarget("leaving", 2, 0, 5),
            VeigarWTarget("entering", 2, 10, 5),
            VeigarWTarget("near_moved_caster", 2, 15, 1),
            new AimTimelineStepBlock("caster", 2, new TestVector3(0, 0, 5)),
            // W is committed, but its 450ms area delay has not elapsed.
            new MoveToBlock { AtMs = 500, ActorAlias = "leaving", Position = new TestVector3(10, 0, 5) },
            new MoveToBlock { AtMs = 500, ActorAlias = "entering", Position = new TestVector3(0, 0, 5) },
            new MoveToBlock { AtMs = 500, ActorAlias = "caster", Position = new TestVector3(15, 0, 0) },
            new AssertionsBlock(new MobaBattleScenarioAssertions
            {
                State =
                {
                    VeigarWHp("leaving", "eq", "50000"),
                    VeigarWHp("entering", "lt", "50000"),
                    VeigarWHp("near_moved_caster", "eq", "50000"),
                    new MobaStateAssertion { Alias = "caster", Property = "position", ExpectedValue = "15,0,0" },
                },
            }),
        });

        var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario);

        Assert.True(outcome.Result.Passed,
            outcome.Result.Summary + " trace=" + string.Join(",", outcome.TraceNodes.Select(node => $"{node.Kind}:{node.ConfigId}@{node.Frame}")));
        Assert.Single(outcome.TraceNodes, node => node.Kind == "DamageApply" && node.ConfigId == 30040301);
        Assert.Single(outcome.TraceNodes, node => node.Kind == "AreaExpire" && node.ConfigId == 42040301);
    }

    [Theory]
    [InlineData(30)]
    public void Veigar_placeholder_w_empty_cast_expires_without_damage(int tickRate)
    {
        var scenario = BattleScenarioCompiler.Compile($"veigar-w-empty-{tickRate}", new BattleBlock[]
        {
            VeigarWExecution(tickRate),
            VeigarCaster(),
            new AimTimelineStepBlock("caster", 2, new TestVector3(0, 0, 5)),
        });

        var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario);

        Assert.True(outcome.Result.Passed, outcome.Result.Summary);
        Assert.Single(outcome.TraceNodes, node => node.Kind == "SkillCast" && node.ConfigId == 30040301);
        Assert.Single(outcome.TraceNodes, node => node.Kind == "AreaSpawn" && node.ConfigId == 42040301);
        Assert.Single(outcome.TraceNodes, node => node.Kind == "AreaExpire" && node.ConfigId == 42040301);
        Assert.DoesNotContain(outcome.TraceNodes, node => node.Kind == "DamageApply" && node.ConfigId == 30040301);
    }

    [Fact]
    public void Veigar_placeholder_w_repeated_input_during_cooldown_commits_once()
    {
        var scenario = BattleScenarioCompiler.Compile("veigar-w-cooldown-placeholder", new BattleBlock[]
        {
            VeigarWExecution(30),
            VeigarCaster(),
            VeigarWTarget("target", 2, 0, 5),
            new AimTimelineStepBlock("caster", 2, new TestVector3(0, 0, 5)),
            new TimelineStepBlock { AtMs = 200, Action = "cast_skill", ActorAlias = "caster",
                Slot = 2 },
            new AssertionsBlock(new MobaBattleScenarioAssertions
            {
                State = { VeigarWHp("target", "lt", "50000") },
            }),
        });

        var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario);

        Assert.True(outcome.Result.Passed,
            outcome.Result.Summary + " trace=" + string.Join(",", outcome.TraceNodes.Select(node => $"{node.Kind}:{node.ConfigId}@{node.Frame}")));
        Assert.Single(outcome.TraceNodes,
            node => node.Kind == "SkillCast" && node.ConfigId == 30040301);
        Assert.Single(outcome.TraceNodes,
            node => node.Kind == "AreaSpawn" && node.ConfigId == 42040301);
        Assert.Single(outcome.TraceNodes,
            node => node.Kind == "DamageApply" && node.ConfigId == 30040301);
    }

    // The live-sim bootstrap currently owns a 30Hz IFrameTime service; keep these
    // scenarios at that rate until the harness exposes an authoritative rate switch.
    private static ExecutionSettingsBlock VeigarWExecution(int tickRate, int durationMs = 1800) => new()
    {
        TickRate = tickRate,
        MaxDurationMs = 2500,
        SettleDurationMs = 0,
        EndCondition = TestEndConditionKinds.Duration,
        DurationMs = durationMs,
    };

    private static SpawnActorBlock VeigarCaster() => new()
    {
        Alias = "caster", HeroId = 3004, AttributeTemplateId = 3004,
        PlayerId = "player_1", TeamId = 1, Position = new TestVector3(0, 0, 0),
    };

    private static SpawnActorBlock VeigarWTarget(string alias, int teamId, float x, float z) => new()
    {
        Alias = alias, HeroId = 1001, AttributeTemplateId = 1001,
        TeamId = teamId, Position = new TestVector3(x, 0, z),
    };

    private static MobaStateAssertion VeigarWHp(string alias, string comparator, string value) => new()
    {
        Alias = alias, Property = "hp", Comparator = comparator, ExpectedValue = value,
    };

    [Fact]
    public void Veigar_placeholder_r_tracks_target_and_deals_fixed_damage()
    {
        var scenario = BattleScenarioCompiler.Compile("veigar-r-placeholder", new BattleBlock[]
        {
            new ExecutionSettingsBlock
            {
                TickRate = 30,
                MaxDurationMs = 3000,
                SettleDurationMs = 0,
                EndCondition = TestEndConditionKinds.Duration,
                DurationMs = 2200,
            },
            new SpawnActorBlock { Alias = "caster", HeroId = 3004, AttributeTemplateId = 3004,
                PlayerId = "player_1", TeamId = 1, Position = new TestVector3(0, 0, 0) },
            new SpawnActorBlock { Alias = "target", HeroId = 1001, AttributeTemplateId = 1001,
                TeamId = 2, Position = new TestVector3(0, 0, 6) },
            new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster",
                TargetAlias = "target", Slot = 4 },
            new AssertionsBlock(new MobaBattleScenarioAssertions
            {
                MustContain =
                {
                    new MobaTraceAssertion { Kind = "SkillCast", ConfigId = 30040501 },
                    new MobaTraceAssertion { Kind = "ProjectileLaunch", ConfigId = 32040501 },
                    new MobaTraceAssertion { Kind = "DamageApply", ConfigId = 30040501 },
                },
                State =
                {
                    new MobaStateAssertion { Alias = "target", Property = "hp", Comparator = "lt", ExpectedValue = "50000" },
                },
            }),
        });

        var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario);

        Assert.True(outcome.Result.Passed, outcome.Result.Summary);
        Assert.Contains(outcome.TraceNodes, node => node.Kind == "ProjectileLaunch" && node.ConfigId == 32040501);
        Assert.Contains(outcome.TraceNodes, node => node.Kind == "DamageApply" && node.ConfigId == 30040501);
    }

    [Fact]
    public void Veigar_placeholder_r_repeated_input_during_cooldown_commits_once()
    {
        var scenario = BattleScenarioCompiler.Compile("veigar-r-cooldown-placeholder", new BattleBlock[]
        {
            new ExecutionSettingsBlock
            {
                TickRate = 30,
                MaxDurationMs = 2400,
                SettleDurationMs = 0,
                EndCondition = TestEndConditionKinds.Duration,
                DurationMs = 1800,
            },
            new SpawnActorBlock { Alias = "caster", HeroId = 3004, AttributeTemplateId = 3004,
                PlayerId = "player_1", TeamId = 1, Position = new TestVector3(0, 0, 0) },
            new SpawnActorBlock { Alias = "target", HeroId = 1001, AttributeTemplateId = 1001,
                TeamId = 2, Position = new TestVector3(0, 0, 6) },
            new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster",
                TargetAlias = "target", Slot = 4 },
            new TimelineStepBlock { AtMs = 200, Action = "cast_skill", ActorAlias = "caster",
                TargetAlias = "target", Slot = 4 },
            new AssertionsBlock(new MobaBattleScenarioAssertions
            {
                State =
                {
                    new MobaStateAssertion { Alias = "target", Property = "hp", Comparator = "lt", ExpectedValue = "50000" },
                },
            }),
        });

        var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario);

        Assert.True(outcome.Result.Passed,
            outcome.Result.Summary + " trace=" + string.Join(",", outcome.TraceNodes.Select(node => $"{node.Kind}:{node.ConfigId}@{node.Frame}")));
        Assert.Single(outcome.TraceNodes,
            node => node.Kind == "SkillCast" && node.ConfigId == 30040501);
        Assert.Contains(outcome.TraceNodes,
            node => node.Kind == "ProjectileLaunch" && node.ConfigId == 32040501);
        Assert.Single(outcome.TraceNodes,
            node => node.Kind == "DamageApply" && node.ConfigId == 30040501);
    }

    [Fact]
    public void Veigar_placeholder_basic_attack_uses_appended_loadout_slot()
    {
        var scenario = BattleScenarioCompiler.Compile("veigar-basic-attack-placeholder", new BattleBlock[]
        {
            new ExecutionSettingsBlock
            {
                TickRate = 30,
                MaxDurationMs = 1800,
                SettleDurationMs = 0,
                EndCondition = TestEndConditionKinds.Duration,
                DurationMs = 1200,
            },
            new SpawnActorBlock { Alias = "caster", HeroId = 3004, AttributeTemplateId = 3004,
                PlayerId = "player_1", TeamId = 1, Position = new TestVector3(0, 0, 0) },
            new SpawnActorBlock { Alias = "target", HeroId = 1001, AttributeTemplateId = 1001,
                TeamId = 2, Position = new TestVector3(4, 0, 0) },
            new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster",
                TargetAlias = "target", Slot = 5 },
            new AssertionsBlock(new MobaBattleScenarioAssertions
            {
                MustContain =
                {
                    new MobaTraceAssertion { Kind = "SkillCast", ConfigId = 30040101 },
                    new MobaTraceAssertion { Kind = "DamageApply", ConfigId = 30040101 },
                },
                State =
                {
                    new MobaStateAssertion { Alias = "target", Property = "hp", Comparator = "lt", ExpectedValue = "50000" },
                },
            }),
        });

        var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario);

        Assert.True(outcome.Result.Passed, outcome.Result.Summary);
        Assert.Contains(outcome.TraceNodes, node => node.Kind == "SkillCast" && node.ConfigId == 30040101);
        Assert.Contains(outcome.TraceNodes, node => node.Kind == "DamageApply" && node.ConfigId == 30040101);
    }

    [Fact]
    public void Veigar_placeholder_e_casts_without_applying_control_or_damage()
    {
        var scenario = BattleScenarioCompiler.Compile("veigar-e-placeholder", new BattleBlock[]
        {
            new ExecutionSettingsBlock
            {
                TickRate = 30,
                MaxDurationMs = 1800,
                SettleDurationMs = 0,
                EndCondition = TestEndConditionKinds.Duration,
                DurationMs = 1200,
            },
            new SpawnActorBlock { Alias = "caster", HeroId = 3004, AttributeTemplateId = 3004,
                PlayerId = "player_1", TeamId = 1, Position = new TestVector3(0, 0, 0) },
            new SpawnActorBlock { Alias = "target", HeroId = 1001, AttributeTemplateId = 1001,
                TeamId = 2, Position = new TestVector3(0, 0, 5) },
            new AimTimelineStepBlock("caster", 3, new TestVector3(0, 0, 5)),
            new AssertionsBlock(new MobaBattleScenarioAssertions
            {
                MustContain = { new MobaTraceAssertion { Kind = "SkillCast", ConfigId = 30040401 } },
            }),
        });

        var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario);

        Assert.True(outcome.Result.Passed, outcome.Result.Summary);
        var eCast = Assert.Single(outcome.TraceNodes,
            node => node.Kind == "SkillCast" && node.ConfigId == 30040401);
        Assert.DoesNotContain(outcome.TraceNodes, node =>
            node.RootId == eCast.RootId && (node.Kind == "DamageApply" || node.Kind == "BuffApply"));
    }

    [Fact]
    public void Run_RealHeroWithLoadout_CastsRealSkillAndReadsState()
    {
        var scenario = BattleScenarioCompiler.Compile("real-skill", new BattleBlock[]
        {
            new SpawnActorBlock { Alias = "caster", HeroId = 1002, AttributeTemplateId = 1002, PlayerId = "player_1", Position = new TestVector3(0, 0, 0) },
            new SpawnActorBlock { Alias = "target", HeroId = 1002, AttributeTemplateId = 1002, TeamId = 2, Position = new TestVector3(6, 0, 0) },
            new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster", TargetAlias = "target", Slot = 1 },
            new AssertionsBlock(new MobaBattleScenarioAssertions
            {
                MustContain = { new MobaTraceAssertion { Kind = "SkillCast", ConfigId = 10020101 } },
                State = { new MobaStateAssertion { Alias = "target", Property = "maxhp", Comparator = "gt", ExpectedValue = "1" } },
            }),
        });

        var result = MobaBattleScenarioWorldRunner.Run(scenario);

        Assert.True(result.Passed, result.Summary);
        Assert.Contains("verdict=PASSED", result.Summary);
    }

    [Fact]
    public void RunDetailed_ProducesTraceTreeForRealSkill()
    {
        var scenario = BattleScenarioCompiler.Compile("trace-tree", new BattleBlock[]
        {
            new SpawnActorBlock { Alias = "caster", HeroId = 1002, AttributeTemplateId = 1002, PlayerId = "player_1" },
            new SpawnActorBlock { Alias = "target", HeroId = 1002, AttributeTemplateId = 1002, TeamId = 2 },
            new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster", TargetAlias = "target", Slot = 1 },
        });

        var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario);

        Assert.True(outcome.Result.Passed, outcome.Result.Summary);
        Assert.NotEmpty(outcome.TraceNodes);
        Assert.Contains(outcome.TraceNodes, n => n.Kind == "SkillCast" && n.ConfigId == 10020101);
        Assert.Contains(outcome.TraceNodes, n => n.ParentId == 0);
    }

    [Fact]
    public void Run_XiaoQiaoProjectile_ProducesLaunchAndDamage()
    {
        // P3 命中链：小乔一技能投射物 → ProjectileLaunch → 命中 → DamageApply。这是「预览技能表现」里最想看到的伤害断言。
        var scenario = BattleScenarioCompiler.Compile("xiaoqiao-projectile", new BattleBlock[]
        {
            new SpawnActorBlock { Alias = "caster", HeroId = 1002, AttributeTemplateId = 1002, PlayerId = "player_1", Position = new TestVector3(0, 0, 0) },
            new SpawnActorBlock { Alias = "target", HeroId = 1002, AttributeTemplateId = 1002, TeamId = 2, Position = new TestVector3(6, 0, 0) },
            new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster", TargetAlias = "target", Slot = 1 },
            new AssertionsBlock(new MobaBattleScenarioAssertions
            {
                MustContain =
                {
                    new MobaTraceAssertion { Kind = "ProjectileLaunch", ConfigId = 30020101 },
                    new MobaTraceAssertion { Kind = "DamageApply", ConfigId = 10020101 },
                },
            }),
        });

        var result = MobaBattleScenarioWorldRunner.Run(scenario);

        Assert.True(result.Passed, result.Summary);
        Assert.Contains("verdict=PASSED", result.Summary);
    }

    [Fact]
    public void Run_JinxE_DeploysTrapsAndProcessesEnterAndExpiry()
    {
        var scenario = BattleScenarioCompiler.Compile("jinx-e-production-scenario", new BattleBlock[]
        {
            new ExecutionSettingsBlock
            {
                TickRate = 30,
                MaxDurationMs = 7000,
                SettleDurationMs = 0,
                EndCondition = TestEndConditionKinds.Duration,
                DurationMs = 5500,
            },
            new SpawnActorBlock
            {
                Alias = "jinx",
                HeroId = 3002,
                AttributeTemplateId = 3002,
                PlayerId = "player_1",
                TeamId = 1,
                Position = new TestVector3(0, 0, 0),
            },
            new SpawnActorBlock
            {
                // Lian Po (1001) starts with SuperArmor; use a target that permits root.
                Alias = "first_target",
                HeroId = 1002,
                AttributeTemplateId = 1002,
                TeamId = 2,
                Position = new TestVector3(0, 0, 5),
            },
            new SpawnActorBlock
            {
                Alias = "second_target",
                HeroId = 1002,
                AttributeTemplateId = 1002,
                TeamId = 2,
                Position = new TestVector3(2, 0, 5),
            },
            new DirectionTimelineStepBlock("jinx", 3, new TestVector3(0, 0, 1)),
        });

        var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario);

        Assert.True(outcome.Result.Passed, outcome.Result.Summary);
        Assert.True(outcome.TraceNodes.Any(node => node.Kind == "SkillCast" && node.ConfigId == 30020301),
            string.Join(",", outcome.TraceNodes.Select(node => $"{node.Kind}:{node.ConfigId}")));
        Assert.Equal(3, outcome.TraceNodes.Count(node => node.Kind == "AreaSpawn" && node.ConfigId == 42020301));
        Assert.True(outcome.TraceNodes.Any(node => node.Kind == "AreaEnter" && node.ConfigId == 42020301),
            string.Join(",", outcome.TraceNodes.Select(node => $"{node.Kind}:{node.ConfigId}")));
        Assert.True(outcome.TraceNodes.Any(node => node.Kind == "BuffApply" && node.ConfigId == 32020301),
            string.Join(",", outcome.TraceNodes.Select(node => $"{node.Kind}:{node.ConfigId}")));
        Assert.True(outcome.TraceNodes.Any(node => node.Kind == "DamageApply"));
        Assert.Equal(3, outcome.TraceNodes.Count(node => node.Kind == "AreaExpire" && node.ConfigId == 42020301));
    }

    [Theory]
    [InlineData(3002, 2, true)]
    [InlineData(3002, 4, true)]
    [InlineData(1002, 1, false)]
    public void Projectile_authored_layers_pass_terrain_while_legacy_default_remains_blocked(int hero, int slot, bool shouldHit)
    {
        var scenario = BattleScenarioCompiler.Compile($"projectile-terrain-{hero}-{slot}", new BattleBlock[]
        {
            new SpawnActorBlock { Alias = "caster", HeroId = hero, AttributeTemplateId = hero,
                PlayerId = "player_1", Position = new TestVector3(0, 0, 0), TeamId = 1 },
            new SpawnActorBlock { Alias = "target", HeroId = 1001, AttributeTemplateId = 1001,
                TeamId = 2, Position = new TestVector3(6, 0, 0) },
            new PlaceObstacleBlock { Id = "blocking_wall", Shape = "box", Size = new TestVector3(1, 4, 8),
                Position = new TestVector3(3, 0, 0) },
            new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster", TargetAlias = "target", Slot = slot },
        });
        var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario);
        Assert.True(outcome.Result.Passed, outcome.Result.Summary);
        Assert.Contains(outcome.TraceNodes, n => n.Kind == "ProjectileLaunch");
        Assert.Equal(shouldHit, outcome.TraceNodes.Any(n => n.Kind == "DamageApply"));
    }

    private sealed class DirectionTimelineStepBlock : BattleAtomicBlock
    {
        private readonly string _actorAlias;
        private readonly int _slot;
        private readonly TestVector3 _direction;

        public DirectionTimelineStepBlock(string actorAlias, int slot, TestVector3 direction)
        {
            _actorAlias = actorAlias;
            _slot = slot;
            _direction = direction;
        }

        public override BattleBlockSection Section => BattleBlockSection.Timeline;

        public override void Compile(BattleScenarioBuilder builder) => builder.AddTimelineStep(new TestTimelineStep
        {
            AtMs = 100,
            Action = "cast_skill",
            ActorAlias = _actorAlias,
            Slot = _slot,
            Direction = _direction,
            Position = new TestVector3(0, 0, 5),
        });
    }

    private sealed class AimTimelineStepBlock : BattleAtomicBlock
    {
        private readonly string _actorAlias;
        private readonly int _slot;
        private readonly TestVector3 _position;

        public AimTimelineStepBlock(string actorAlias, int slot, TestVector3 position)
        {
            _actorAlias = actorAlias;
            _slot = slot;
            _position = position;
        }

        public override BattleBlockSection Section => BattleBlockSection.Timeline;

        public override void Compile(BattleScenarioBuilder builder) => builder.AddTimelineStep(new TestTimelineStep
        {
            AtMs = 100,
            Action = "cast_skill",
            ActorAlias = _actorAlias,
            Slot = _slot,
            Position = _position,
        });
    }

    [Fact]
    public void Run_WithPlaceObstacleBlock_PlacesObstacleAndStillHits()
    {
        // PlaceObstacleBlock → TestScenario.Obstacles → 放进碰撞世界。障碍物放在远处，不影响投射物命中。
        var scenario = BattleScenarioCompiler.Compile("with-obstacle", new BattleBlock[]
        {
            new SpawnActorBlock { Alias = "caster", HeroId = 1002, AttributeTemplateId = 1002, PlayerId = "player_1", Position = new TestVector3(0, 0, 0) },
            new SpawnActorBlock { Alias = "target", HeroId = 1002, AttributeTemplateId = 1002, TeamId = 2, Position = new TestVector3(6, 0, 0) },
            new PlaceObstacleBlock { Id = "wall", Shape = "box", Size = new TestVector3(2, 2, 2), Position = new TestVector3(0, 0, 10) },
            new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster", TargetAlias = "target", Slot = 1 },
            new AssertionsBlock(new MobaBattleScenarioAssertions
            {
                MustContain = { new MobaTraceAssertion { Kind = "DamageApply", ConfigId = 10020101 } },
            }),
        });

        var result = MobaBattleScenarioWorldRunner.Run(scenario);

        Assert.True(result.Passed, result.Summary);
        Assert.Contains("verdict=PASSED", result.Summary);
    }

    [Fact]
    public void Template_XiaoQiaoVsLianPo_Passes()
    {
        // 与 MobaBattleScenarioTemplates 里的「小乔一技能伤害」模板内容一致，验证模板本身能跑通（目标换成廉颇 1001）。
        var scenario = BattleScenarioCompiler.Compile("template-xiaoqiao-vs-lianpo", new BattleBlock[]
        {
            new SpawnActorBlock { Alias = "caster", HeroId = 1002, AttributeTemplateId = 1002, PlayerId = "player_1", Position = new TestVector3(0, 0, 0) },
            new SpawnActorBlock { Alias = "target", HeroId = 1001, AttributeTemplateId = 1001, TeamId = 2, Position = new TestVector3(6, 0, 0) },
            new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster", TargetAlias = "target", Slot = 1 },
            new AssertionsBlock(new MobaBattleScenarioAssertions
            {
                MustContain = { new MobaTraceAssertion { Kind = "DamageApply", ConfigId = 10020101 } },
            }),
        });

        var result = MobaBattleScenarioWorldRunner.Run(scenario);

        Assert.True(result.Passed, result.Summary);
        Assert.Contains("verdict=PASSED", result.Summary);
    }

    [Fact]
    public void Dsl_ParseFullScenario_CompilesAndRuns()
    {
        // 用 DSL 文本描述完整场景（spawn + cast + assert），解析成积木 → 编译 → 跑 → verdict，与拖积木等价。
        BattleScenarioDslParser.AssertFactory = (verb, args) => verb switch
        {
            "assert" => args.Length >= 2 ? new AssertTraceBlock { Kind = args[0], ConfigId = int.Parse(args[1]) } : null,
            _ => null,
        };

        try
        {
            var blocks = BattleScenarioDslParser.Parse(@"
spawn caster hero=1002 attr=1002 player=player_1 pos=0,0,0
spawn target hero=1001 attr=1001 team=2 pos=6,0,0
cast caster target slot=1 at=100
assert DamageApply 10020101
");

            var scenario = BattleScenarioCompiler.Compile("dsl-xiaoqiao", blocks);
            var result = MobaBattleScenarioWorldRunner.Run(scenario);

            Assert.True(result.Passed, result.Summary);
            Assert.Contains("verdict=PASSED", result.Summary);
        }
        finally
        {
            BattleScenarioDslParser.AssertFactory = null;
        }
    }

    [Fact]
    public void TraceNodes_JsonRoundTripsForEditor()
    {
        // 镜像 .NET runner 写（Program.Main）与 Unity editor 读（MobaBattleScenarioRunner.ReadTrace）的 Newtonsoft 往返。
        var nodes = new[]
        {
            new BattleScenarioTraceNode { Id = 1, ParentId = 0, RootId = 1, Kind = "SkillCast", ConfigId = 10020101, Frame = 5 },
        };

        var json = Newtonsoft.Json.JsonConvert.SerializeObject(nodes);
        var back = Newtonsoft.Json.JsonConvert.DeserializeObject<List<BattleScenarioTraceNode>>(json);

        Assert.NotNull(back);
        Assert.Single(back!);
        Assert.Equal("SkillCast", back[0].Kind);
        Assert.Equal(10020101, back[0].ConfigId);
    }

    private sealed class AssertionsBlock : BattleAtomicBlock
    {
        private readonly MobaBattleScenarioAssertions _assertions;

        public AssertionsBlock(MobaBattleScenarioAssertions assertions) => _assertions = assertions;

        public override void Compile(BattleScenarioBuilder builder) => builder.SetExpectations(_assertions);
    }
}
