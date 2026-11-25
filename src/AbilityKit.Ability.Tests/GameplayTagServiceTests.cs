using AbilityKit.Ability.Tags;
using AbilityKit.GameplayTags;
using Xunit;

namespace AbilityKit.Ability.Tests;

public sealed class GameplayTagServiceTests
{
    [Fact]
    public void RemoveTag_NotifiesOnlyAfterLastSourceReferenceIsRemoved()
    {
        var service = new GameplayTagService();
        var tag = GameplayTagManager.Instance.RequestTag("Ability.TagService.LastSource");
        var first = new GameplayTagSource(101);
        var second = new GameplayTagSource(102);
        var changes = new List<GameplayTagDelta>();
        service.TagsChanged += (_, delta, _) => changes.Add(delta);

        Assert.True(service.AddTag(1, tag, first));
        Assert.True(service.AddTag(1, tag, first));
        Assert.True(service.AddTag(1, tag, second));
        Assert.True(Assert.Single(changes).Added.HasTagExact(tag));

        Assert.True(service.RemoveTag(1, tag, first));
        Assert.True(service.RemoveTag(1, tag, second));
        Assert.True(service.HasTag(1, tag));
        Assert.Single(changes);

        Assert.True(service.RemoveTag(1, tag, first));
        Assert.False(service.HasTag(1, tag));
        Assert.Equal(2, changes.Count);
        Assert.True(changes[1].Removed.HasTagExact(tag));
    }

    [Fact]
    public void ClearOwner_ClearsOnlyThatOwnersReferencesAndPublishesOneSystemDelta()
    {
        var service = new GameplayTagService();
        var first = GameplayTagManager.Instance.RequestTag("Ability.TagService.ClearOwner.First");
        var second = GameplayTagManager.Instance.RequestTag("Ability.TagService.ClearOwner.Second");
        var source = new GameplayTagSource(101);
        Assert.True(service.AddTag(1, first, source));
        Assert.True(service.AddTag(1, second, source));
        Assert.True(service.AddTag(2, first, source));
        var changes = new List<(int Owner, GameplayTagDelta Delta, GameplayTagSource Source)>();
        service.TagsChanged += (owner, delta, origin) => changes.Add((owner, delta, origin));

        service.ClearOwner(1);
        service.ClearOwner(1);

        var change = Assert.Single(changes);
        Assert.Equal(1, change.Owner);
        Assert.Equal(GameplayTagSource.System.Value, change.Source.Value);
        Assert.Equal(2, change.Delta.Removed.Count);
        Assert.True(change.Delta.Removed.HasTagExact(first));
        Assert.True(change.Delta.Removed.HasTagExact(second));
        Assert.False(service.HasTag(1, first));
        Assert.False(service.RemoveTag(1, first, source));
        Assert.True(service.HasTag(2, first));
        Assert.True(service.AddTag(1, first, source));
        Assert.True(service.RemoveTag(1, first, source));
        Assert.False(service.HasTag(1, first));
    }
}
