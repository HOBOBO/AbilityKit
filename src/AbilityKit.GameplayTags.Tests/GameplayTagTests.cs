using AbilityKit.GameplayTags;
using Xunit;

namespace AbilityKit.GameplayTags.Tests;

public sealed class GameplayTagTests
{
    [Fact] public void None_is_default() => Assert.Equal(default(GameplayTag), GameplayTag.None);
    [Fact] public void None_value_is_zero() => Assert.Equal(0, GameplayTag.None.Value);
}
