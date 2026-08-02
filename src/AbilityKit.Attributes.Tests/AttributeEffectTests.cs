using AbilityKit.Attributes;
using Xunit;

namespace AbilityKit.Attributes.Tests;

public sealed class AttributeEffectTests
{
    [Fact] public void Constructor_empty_returns_non_null_entries()
        => Assert.NotNull(new AttributeEffect().Entries);

    [Fact] public void Entry_default_is_struct()
        => Assert.True(default(AttributeEffect.Entry).GetType().IsValueType);
}
