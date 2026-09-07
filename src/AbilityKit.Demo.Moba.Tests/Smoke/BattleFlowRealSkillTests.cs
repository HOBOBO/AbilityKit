using AbilityKit.BattleFlow;
using AbilityKit.Demo.Moba.BattleFlow;
using AbilityKit.Demo.Moba.EnvironmentModel;
using AbilityKit.Scenario;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>
/// 端到端：真实英雄（属性+技能 loadout）→ 施放真实技能 → 真实 trace（SkillCast 命中指定 configId）+ 真实状态（maxhp &gt; 0）。
/// 这是「从 smoke 到真实」的核心回归：spawn 出的 actor 必须有 SkillLoadout（否则 cast 不出指定技能）和 AttributeGroup（否则 maxhp=0）。
/// </summary>
[Trait("Gate", "BattleFlow")]
public sealed class BattleFlowRealSkillTests
{
    [Fact]
    public void Run_RealHeroWithLoadout_CastsRealSkillAndReadsState()
    {
        var scenario = BattleFlowCompiler.Compile("real-skill", new BattleBlock[]
        {
            new SpawnActorBlock { Alias = "caster", HeroId = 1002, AttributeTemplateId = 1002, PlayerId = "player_1", Position = new TestVector3(0, 0, 0) },
            new SpawnActorBlock { Alias = "target", HeroId = 1002, AttributeTemplateId = 1002, TeamId = 2, Position = new TestVector3(6, 0, 0) },
            new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster", TargetAlias = "target", Slot = 1 },
            new AssertionsBlock(new MobaBattleFlowAssertions
            {
                MustContain = { new MobaTraceAssertion { Kind = "SkillCast", ConfigId = 10020101 } },
                State = { new MobaStateAssertion { Alias = "target", Property = "maxhp", Comparator = "gt", ExpectedValue = "1" } },
            }),
        });

        var result = MobaBattleFlowScenarioRunner.Run(scenario);

        Assert.True(result.Passed, result.Summary);
        Assert.Contains("verdict=PASSED", result.Summary);
    }

    [Fact]
    public void RunDetailed_ProducesTraceTreeForRealSkill()
    {
        var scenario = BattleFlowCompiler.Compile("trace-tree", new BattleBlock[]
        {
            new SpawnActorBlock { Alias = "caster", HeroId = 1002, AttributeTemplateId = 1002, PlayerId = "player_1" },
            new SpawnActorBlock { Alias = "target", HeroId = 1002, AttributeTemplateId = 1002, TeamId = 2 },
            new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster", TargetAlias = "target", Slot = 1 },
        });

        var outcome = MobaBattleFlowScenarioRunner.RunDetailed(scenario);

        Assert.True(outcome.Result.Passed, outcome.Result.Summary);
        Assert.NotEmpty(outcome.TraceNodes);
        Assert.Contains(outcome.TraceNodes, n => n.Kind == "SkillCast" && n.ConfigId == 10020101);
        Assert.Contains(outcome.TraceNodes, n => n.ParentId == 0);
    }

    [Fact]
    public void Run_XiaoQiaoProjectile_ProducesLaunchAndDamage()
    {
        // P3 命中链：小乔一技能投射物 → ProjectileLaunch → 命中 → DamageApply。这是「预览技能表现」里最想看到的伤害断言。
        var scenario = BattleFlowCompiler.Compile("xiaoqiao-projectile", new BattleBlock[]
        {
            new SpawnActorBlock { Alias = "caster", HeroId = 1002, AttributeTemplateId = 1002, PlayerId = "player_1", Position = new TestVector3(0, 0, 0) },
            new SpawnActorBlock { Alias = "target", HeroId = 1002, AttributeTemplateId = 1002, TeamId = 2, Position = new TestVector3(6, 0, 0) },
            new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster", TargetAlias = "target", Slot = 1 },
            new AssertionsBlock(new MobaBattleFlowAssertions
            {
                MustContain =
                {
                    new MobaTraceAssertion { Kind = "ProjectileLaunch", ConfigId = 30020101 },
                    new MobaTraceAssertion { Kind = "DamageApply", ConfigId = 10020101 },
                },
            }),
        });

        var result = MobaBattleFlowScenarioRunner.Run(scenario);

        Assert.True(result.Passed, result.Summary);
        Assert.Contains("verdict=PASSED", result.Summary);
    }

    [Fact]
    public void Run_WithPlaceObstacleBlock_PlacesObstacleAndStillHits()
    {
        // PlaceObstacleBlock → TestScenario.Obstacles → 放进碰撞世界。障碍物放在远处，不影响投射物命中。
        var scenario = BattleFlowCompiler.Compile("with-obstacle", new BattleBlock[]
        {
            new SpawnActorBlock { Alias = "caster", HeroId = 1002, AttributeTemplateId = 1002, PlayerId = "player_1", Position = new TestVector3(0, 0, 0) },
            new SpawnActorBlock { Alias = "target", HeroId = 1002, AttributeTemplateId = 1002, TeamId = 2, Position = new TestVector3(6, 0, 0) },
            new PlaceObstacleBlock { Id = "wall", Shape = "box", Size = new TestVector3(2, 2, 2), Position = new TestVector3(0, 0, 10) },
            new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster", TargetAlias = "target", Slot = 1 },
            new AssertionsBlock(new MobaBattleFlowAssertions
            {
                MustContain = { new MobaTraceAssertion { Kind = "DamageApply", ConfigId = 10020101 } },
            }),
        });

        var result = MobaBattleFlowScenarioRunner.Run(scenario);

        Assert.True(result.Passed, result.Summary);
        Assert.Contains("verdict=PASSED", result.Summary);
    }

    [Fact]
    public void Template_XiaoQiaoVsLianPo_Passes()
    {
        // 与 MobaBattleFlowTemplates 里的「小乔一技能伤害」模板内容一致，验证模板本身能跑通（目标换成廉颇 1001）。
        var scenario = BattleFlowCompiler.Compile("template-xiaoqiao-vs-lianpo", new BattleBlock[]
        {
            new SpawnActorBlock { Alias = "caster", HeroId = 1002, AttributeTemplateId = 1002, PlayerId = "player_1", Position = new TestVector3(0, 0, 0) },
            new SpawnActorBlock { Alias = "target", HeroId = 1001, AttributeTemplateId = 1001, TeamId = 2, Position = new TestVector3(6, 0, 0) },
            new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster", TargetAlias = "target", Slot = 1 },
            new AssertionsBlock(new MobaBattleFlowAssertions
            {
                MustContain = { new MobaTraceAssertion { Kind = "DamageApply", ConfigId = 10020101 } },
            }),
        });

        var result = MobaBattleFlowScenarioRunner.Run(scenario);

        Assert.True(result.Passed, result.Summary);
        Assert.Contains("verdict=PASSED", result.Summary);
    }

    [Fact]
    public void Dsl_ParseFullScenario_CompilesAndRuns()
    {
        // 用 DSL 文本描述完整场景（spawn + cast + assert），解析成积木 → 编译 → 跑 → verdict，与拖积木等价。
        BattleFlowDslParser.AssertFactory = (verb, args) => verb switch
        {
            "assert" => args.Length >= 2 ? new AssertTraceBlock { Kind = args[0], ConfigId = int.Parse(args[1]) } : null,
            _ => null,
        };

        try
        {
            var blocks = BattleFlowDslParser.Parse(@"
spawn caster hero=1002 attr=1002 player=player_1 pos=0,0,0
spawn target hero=1001 attr=1001 team=2 pos=6,0,0
cast caster target slot=1 at=100
assert DamageApply 10020101
");

            var scenario = BattleFlowCompiler.Compile("dsl-xiaoqiao", blocks);
            var result = MobaBattleFlowScenarioRunner.Run(scenario);

            Assert.True(result.Passed, result.Summary);
            Assert.Contains("verdict=PASSED", result.Summary);
        }
        finally
        {
            BattleFlowDslParser.AssertFactory = null;
        }
    }

    [Fact]
    public void TraceNodes_JsonRoundTripsForEditor()
    {
        // 镜像 .NET runner 写（Program.Main）与 Unity editor 读（MobaBattleFlowRunner.ReadTrace）的 Newtonsoft 往返。
        var nodes = new[]
        {
            new BattleFlowTraceNode { Id = 1, ParentId = 0, RootId = 1, Kind = "SkillCast", ConfigId = 10020101, Frame = 5 },
        };

        var json = Newtonsoft.Json.JsonConvert.SerializeObject(nodes);
        var back = Newtonsoft.Json.JsonConvert.DeserializeObject<List<BattleFlowTraceNode>>(json);

        Assert.NotNull(back);
        Assert.Single(back!);
        Assert.Equal("SkillCast", back[0].Kind);
        Assert.Equal(10020101, back[0].ConfigId);
    }

    private sealed class AssertionsBlock : BattleAtomicBlock
    {
        private readonly MobaBattleFlowAssertions _assertions;

        public AssertionsBlock(MobaBattleFlowAssertions assertions) => _assertions = assertions;

        public override void Compile(BattleFlowBuilder builder) => builder.SetExpectations(_assertions);
    }
}
