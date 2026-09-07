using AbilityKit.BattleFlow;
using Xunit;

namespace AbilityKit.BattleFlow.Tests;

/// <summary>DSL 解析：文本语句 → 积木。验证中性动词 + 断言动词委托。</summary>
public sealed class BattleFlowDslParserTests
{
    [Fact]
    public void Parse_NeutralVerbs_ProducesBlocks()
    {
        var blocks = BattleFlowDslParser.Parse(@"
# 小乔打廉颇
spawn caster hero=1002 attr=1002 player=player_1 pos=0,0,0
spawn target hero=1001 attr=1001 team=2 pos=6,0,0
cast caster target slot=1 at=100
wait 500 at=600
");

        Assert.Equal(4, blocks.Count);

        var caster = Assert.IsType<SpawnActorBlock>(blocks[0]);
        Assert.Equal("caster", caster.Alias);
        Assert.Equal(1002, caster.HeroId);
        Assert.Equal(1002, caster.AttributeTemplateId);
        Assert.Equal("player_1", caster.PlayerId);

        var target = Assert.IsType<SpawnActorBlock>(blocks[1]);
        Assert.Equal(2, target.TeamId);

        var cast = Assert.IsType<TimelineStepBlock>(blocks[2]);
        Assert.Equal("cast_skill", cast.Action);
        Assert.Equal("caster", cast.ActorAlias);
        Assert.Equal("target", cast.TargetAlias);
        Assert.Equal(1, cast.Slot);
        Assert.Equal(100, cast.AtMs);

        var wait = Assert.IsType<WaitBlock>(blocks[3]);
        Assert.Equal(500, wait.DurationMs);
        Assert.Equal(600, wait.AtMs);
    }

    [Fact]
    public void Parse_AssertVerb_DelegatesToFactory()
    {
        BattleFlowDslParser.AssertFactory = (verb, args) => verb switch
        {
            "assert" => new TestAssertBlock { Kind = args[0], ConfigId = int.Parse(args[1]) },
            _ => null,
        };

        try
        {
            var blocks = BattleFlowDslParser.Parse("assert DamageApply 10020101");

            var assert = Assert.IsType<TestAssertBlock>(blocks[0]);
            Assert.Equal("DamageApply", assert.Kind);
            Assert.Equal(10020101, assert.ConfigId);
        }
        finally
        {
            BattleFlowDslParser.AssertFactory = null;
        }
    }

    private sealed class TestAssertBlock : BattleAtomicBlock
    {
        public string Kind { get; set; } = string.Empty;
        public int ConfigId { get; set; }

        public override void Compile(BattleFlowBuilder builder)
        {
        }
    }
}
