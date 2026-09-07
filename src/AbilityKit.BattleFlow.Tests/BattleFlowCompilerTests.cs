using System.Collections.Generic;
using AbilityKit.BattleFlow;
using AbilityKit.Scenario;
using Xunit;

namespace AbilityKit.BattleFlow.Tests;

/// <summary>验证积木模型：原子积木编译到 IR、复合积木展平、积木库注册（粒度项目可选）。</summary>
public sealed class BattleFlowCompilerTests
{
    [Fact]
    public void AtomicBlocks_CompileToNeutralScenario()
    {
        var scenario = BattleFlowCompiler.Compile("case-1", new BattleBlock[]
        {
            new SetEnvironmentBlock { Id = "env", ProfileId = "jungle-camp" },
            new SpawnActorBlock { Id = "caster", Alias = "caster", HeroId = 1001, Position = new TestVector3(-15, 0, 0) },
            new SpawnActorBlock { Id = "target", Alias = "target", TeamId = 2, Position = new TestVector3(-12, 0, 0) },
            new TimelineStepBlock { Id = "cast", AtMs = 100, Action = "cast_skill", ActorAlias = "caster", TargetAlias = "target", Slot = 1 },
        });

        Assert.Equal("case-1", scenario.CaseId);
        Assert.Equal("jungle-camp", scenario.EnvironmentProfileId);
        Assert.Equal(2, scenario.Actors.Count);
        Assert.Single(scenario.Timeline);
        Assert.Equal("cast_skill", scenario.Timeline[0].Action);
        Assert.Empty(TestScenarioValidator.Validate(scenario));
    }

    [Fact]
    public void CompositeBlock_FlattensChildren()
    {
        // 项目定义一个「标准野怪测试」复合积木：环境 + 目标 + 施放
        var standardJungleTest = new BattleCompositeBlock
        {
            Id = "standard-jungle-test",
            DisplayName = "标准野怪测试",
            Children = new BattleBlock[]
            {
                new SetEnvironmentBlock { ProfileId = "jungle-camp" },
                new SpawnActorBlock { Alias = "target", TeamId = 2 },
                new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster", TargetAlias = "target" },
            },
        };

        var scenario = BattleFlowCompiler.Compile("case-2", new BattleBlock[]
        {
            new SpawnActorBlock { Alias = "caster", HeroId = 1001 },
            standardJungleTest,
        });

        Assert.Equal("jungle-camp", scenario.EnvironmentProfileId);
        Assert.Equal(2, scenario.Actors.Count);          // caster + target
        Assert.Single(scenario.Timeline);
    }

    [Fact]
    public void Library_RegistersAndResolvesBlocks()
    {
        var library = new BattleBlockLibrary()
            .Add(new BattleCompositeBlock { Id = "standard-jungle-test", DisplayName = "标准野怪测试" });

        Assert.True(library.TryGet("standard-jungle-test", out var block));
        Assert.Equal("标准野怪测试", block.DisplayName);
    }

    [Fact]
    public void Library_RejectsDuplicateId()
    {
        var library = new BattleBlockLibrary().Add(new BattleCompositeBlock { Id = "dup" });
        Assert.Throws<ArgumentException>(() => library.Add(new BattleCompositeBlock { Id = "dup" }));
    }

    [Fact]
    public void AssertionBlock_SetsOpaqueExpectations()
    {
        var payload = new object();
        var scenario = BattleFlowCompiler.Compile("case-assert", new BattleBlock[]
        {
            new ObjectAssertBlock { Id = "assert", Payload = payload },
        });

        Assert.Same(payload, scenario.Expectations);
    }

    [Fact]
    public void Codec_RoundTripsBlockTree()
    {
        var doc = new BattleFlowDocument
        {
            CaseId = "case-flow",
            Blocks = new List<BattleBlock>
            {
                new SetEnvironmentBlock { ProfileId = "jungle-camp" },
                new SpawnActorBlock { Alias = "caster", HeroId = 1001 },
                new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster" },
                new BattleCompositeBlock { Id = "macro", Children = new List<BattleBlock> { new WaitBlock { AtMs = 200 } } },
            },
        };

        var json = BattleFlowCodec.Serialize(doc);
        var back = BattleFlowCodec.Parse(json);

        Assert.Equal("case-flow", back.CaseId);
        Assert.Equal(4, back.Blocks.Count);
        Assert.IsType<SetEnvironmentBlock>(back.Blocks[0]);
        Assert.IsType<SpawnActorBlock>(back.Blocks[1]);
        Assert.IsType<TimelineStepBlock>(back.Blocks[2]);
        Assert.IsType<BattleCompositeBlock>(back.Blocks[3]);
    }

    /// <summary>模拟项目自定义的断言积木：把 opaque 断言对象塞进 Expectations。</summary>
    private sealed class ObjectAssertBlock : BattleAtomicBlock
    {
        public object? Payload { get; set; }

        public override void Compile(BattleFlowBuilder builder) => builder.SetExpectations(Payload);
    }
}
