using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Search;
using AbilityKit.Demo.Moba.Share.Config;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Skill;

public sealed class NormalAttackTargetQueryTests
{
    [Fact]
    public void Create_UsesEnemyRangeAndNearestTargetContract()
    {
        var template = NormalAttackTargetQuery.Create(3f);

        Assert.Equal(1, template.MaxCount);
        Assert.Equal((int)SearchQueryExplicitTargetPolicy.PreferExplicitTarget, template.ExplicitTargetPolicy);
        Assert.Equal((int)SearchTargetProviderKind.EnemyTeam, template.Provider.Kind);
        var rule = Assert.Single(template.Rules);
        Assert.Equal((int)SearchTargetRuleKind.CircleShape, rule.Kind);
        Assert.Equal((int)SearchTargetPointKind.Caster, rule.Center);
        Assert.Equal(3f, rule.Radius);
        var scorer = Assert.Single(template.Scorers);
        Assert.Equal((int)SearchTargetScorerKind.DistanceToCaster, scorer.Kind);
        Assert.Equal((int)SearchTargetSelectorKind.TopKByScore, template.Selector.Kind);
    }

    [Fact]
    public void Template_preserves_ordered_scorers_and_each_direction()
    {
        var template = new SearchQueryTemplateMO(new SearchQueryTemplateDTO
        {
            Scorers = new[]
            {
                new SearchTargetScorerDTO
                {
                    Kind = (int)SearchTargetScorerKind.DistanceToCaster,
                    Direction = 1
                },
                new SearchTargetScorerDTO
                {
                    Kind = (int)SearchTargetScorerKind.SeededHashRandom,
                    RandomSeed = 17,
                    Direction = 0
                }
            }
        });

        Assert.Equal(2, template.Scorers.Length);
        Assert.Equal((int)SearchTargetScorerKind.DistanceToCaster, template.Scorers[0].Kind);
        Assert.Equal(1, template.Scorers[0].Direction);
        Assert.Equal((int)SearchTargetScorerKind.SeededHashRandom, template.Scorers[1].Kind);
        Assert.Equal(17, template.Scorers[1].RandomSeed);
        Assert.Equal(0, template.Scorers[1].Direction);
    }

    [Fact]
    public void Deserializer_preserves_ordered_scorers()
    {
        const string json = """
            [
              {
                "Id": 7,
                "Scorers": [
                  { "Kind": 8196, "Direction": 1 },
                  { "Kind": 8194, "RandomSeed": 23, "Direction": 0 }
                ]
              }
            ]
            """;

        var items = LubanConfigGroupDeserializer.Instance.DeserializeFromText(
            json,
            typeof(SearchQueryTemplateDTO));
        var dto = Assert.IsType<SearchQueryTemplateDTO>(Assert.Single(items));

        Assert.Equal(2, dto.Scorers.Length);
        Assert.Equal((int)SearchTargetScorerKind.DistanceToCaster, dto.Scorers[0].Kind);
        Assert.Equal(1, dto.Scorers[0].Direction);
        Assert.Equal((int)SearchTargetScorerKind.SeededHashRandom, dto.Scorers[1].Kind);
        Assert.Equal(23, dto.Scorers[1].RandomSeed);
        Assert.Equal(0, dto.Scorers[1].Direction);
    }

    [Fact]
    public void SearchWithoutExplicitTarget_SelectsNearestEnemyInRange()
    {
        var fixture = new SearchFixture();
        fixture.AddActor(1, Team.Team1, 0f);
        fixture.AddActor(2, Team.Team2, 2f);
        fixture.AddActor(3, Team.Team2, 4f);
        fixture.AddActor(4, Team.Team1, 1f);

        var results = fixture.Search(range: 3f, explicitTargetActorId: 0);

        Assert.Equal(new[] { 2 }, results);
    }

    [Fact]
    public void SearchWithExplicitTarget_RejectsTargetOutsideAttackRange()
    {
        var fixture = new SearchFixture();
        fixture.AddActor(1, Team.Team1, 0f);
        fixture.AddActor(2, Team.Team2, 4f);

        var results = fixture.Search(range: 3f, explicitTargetActorId: 2);

        Assert.Empty(results);
    }

    [Fact]
    public void SearchWithoutAvailableEnemy_ReturnsEmpty()
    {
        var fixture = new SearchFixture();
        fixture.AddActor(1, Team.Team1, 0f);
        fixture.AddActor(2, Team.Team1, 1f);
        fixture.AddActor(3, Team.Team2, 4f);

        var results = fixture.Search(range: 3f, explicitTargetActorId: 0);

        Assert.Empty(results);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void Create_WithNonPositiveRange_Throws(float range)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NormalAttackTargetQuery.Create(range));
    }

    private sealed class SearchFixture
    {
        private readonly ActorContext _context = new();
        private readonly MobaActorRegistry _actors = new();
        private readonly SearchTargetService _search;

        public SearchFixture()
        {
            _search = new SearchTargetService(_actors);
        }

        public void AddActor(int actorId, Team team, float x)
        {
            var actor = _context.CreateEntity();
            actor.AddTeam(team);
            actor.AddTransform(new Transform3(
                new Vec3(x, 0f, 0f),
                Quat.Identity,
                Vec3.One));
            _actors.Register(actorId, actor);
        }

        public List<int> Search(float range, int explicitTargetActorId)
        {
            var results = new List<int>();
            var aim = Vec3.Zero;
            _search.TrySearchActorIds(
                NormalAttackTargetQuery.Create(range),
                casterActorId: 1,
                in aim,
                explicitTargetActorId,
                results);
            return results;
        }
    }
}
