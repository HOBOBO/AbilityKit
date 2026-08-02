using AbilityKit.Battle.SearchTarget;
using Xunit;

namespace AbilityKit.Combat.Targeting.Tests;

public sealed class SearchHitTests
{
    [Fact]
    public void Construct_and_read_fields()
    {
        var hit = new SearchHit(null!, 0.85f, 42UL);
        Assert.Null(hit.Id);
        Assert.Equal(0.85f, hit.Score);
        Assert.Equal(42UL, hit.Key);
    }

    [Fact]
    public void SearchQuery_construct_and_read()
    {
        var q = new SearchQuery(default!, null, null!, null!, 10, 0);
        Assert.Equal(10, q.MaxCount);
        Assert.Equal(0, q.Flags);
        Assert.Null(q.Rules);
    }
}
