using AbilityKit.Battle.SearchTarget;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Services.Search;
using AbilityKit.Demo.Moba.Share.Config;
using Xunit;
using ST = AbilityKit.Battle.SearchTarget;

namespace AbilityKit.Demo.Moba.Tests.Skill;

public sealed class MobaShapeTargetRuleTests
{
    private static readonly SearchQuery EmptyQuery = default;

    [Theory]
    [InlineData(3f, 2f, true)]
    [InlineData(3.01f, 2f, false)]
    [InlineData(3f, 2.01f, false)]
    public void Rectangle_UsesRotatedFullLengthAndWidth(float x, float y, bool expected)
    {
        var rule = new MobaRectangleShapeRule(
            new ST.Vec2(1f, 1f),
            new ST.Vec2(1f, 0f),
            width: 2f,
            length: 4f);
        var context = CreateContext(new ST.Vec2(x, y));

        Assert.Equal(expected, rule.IsMatch(in EmptyQuery, context, new EntityId(1)));
    }

    [Theory]
    [InlineData(0f, 3f, true)]
    [InlineData(1f, 2f, true)]
    [InlineData(0f, 3.01f, false)]
    [InlineData(1.01f, 2f, false)]
    public void Capsule_UsesCenterLineLengthAndRoundCaps(float x, float y, bool expected)
    {
        var rule = new MobaCapsuleShapeRule(
            ST.Vec2.Zero,
            ST.Vec2.Up,
            radius: 1f,
            length: 4f);
        var context = CreateContext(new ST.Vec2(x, y));

        Assert.Equal(expected, rule.IsMatch(in EmptyQuery, context, new EntityId(1)));
    }

    [Fact]
    public void RectangleFactory_AppliesForwardAndRightOffsetsFromAimPoint()
    {
        var searchContext = CreateContext(new ST.Vec2(13f, 23f));
        var buildContext = new MobaTargetQueryBuildContext(
            actors: null,
            allActorsProvider: null,
            searchContext,
            casterActorId: 0,
            aimPosition: new AbilityKit.Core.Mathematics.Vec3(10f, 0f, 20f),
            explicitTargetActorId: 0,
            zeroScorer: null,
            topKSelector: null,
            streamingTopKSelector: null);
        var config = new SearchTargetRuleConfig(
            id: 1,
            kind: (int)SearchTargetRuleKind.RectangleShape,
            center: (int)SearchTargetPointKind.AimPosition,
            forward: (int)SearchTargetPointKind.AimPosition,
            localOffsetForward: 3f,
            localOffsetRight: 2f,
            width: 2f,
            length: 4f);

        var rule = new RectangleShapeTargetFilterFactory().Create(in buildContext, config);

        Assert.True(rule.IsMatch(in EmptyQuery, searchContext, new EntityId(1)));
    }

    private static SearchContext CreateContext(ST.Vec2 position)
    {
        return new SearchContext
        {
            PositionProvider = new FixedPositionProvider(position)
        };
    }

    private sealed class FixedPositionProvider : IPositionProvider
    {
        private readonly Vec2 _position;

        public FixedPositionProvider(Vec2 position)
        {
            _position = position;
        }

        public bool TryGetPosition(EntityId entity, out Vec2 position)
        {
            position = _position;
            return entity.IsValid;
        }
    }
}
