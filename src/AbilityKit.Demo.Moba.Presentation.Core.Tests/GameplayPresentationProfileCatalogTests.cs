using AbilityKit.Game.Flow;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.Core.Tests;

public sealed class GameplayPresentationProfileCatalogTests
{
    [Fact]
    public void CatalogRejectsInvalidAndDuplicateProfiles()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new GameplayPresentationProfileCatalog(Array.Empty<GameplayPresentationProfile>()));
        Assert.Throws<InvalidOperationException>(() =>
            new GameplayPresentationProfileCatalog(new[]
            {
                new GameplayPresentationProfile(1, "hud", "projection", "settlement"),
                new GameplayPresentationProfile(1, "other", "projection", "settlement")
            }));
        Assert.Throws<InvalidOperationException>(() =>
            new GameplayPresentationProfileCatalog(new[]
            {
                new GameplayPresentationProfile(1, "hud", "projection", "settlement", new[] { " " })
            }));
    }

    [Fact]
    public void CatalogRetainsAnImmutableAssetPathSnapshot()
    {
        var paths = new[] { "gameplay/arena/hud" };
        var catalog = new GameplayPresentationProfileCatalog(new[]
        {
            new GameplayPresentationProfile(2, "hud", "projection", "settlement", paths)
        });
        paths[0] = "changed";

        Assert.Equal("gameplay/arena/hud", catalog.Get(2).AssetPaths[0]);
        Assert.Throws<InvalidOperationException>(() => catalog.Get(1));
    }
}
