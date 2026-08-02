using AbilityKit.Context;
using Xunit;

namespace AbilityKit.Context.Tests;

public sealed class EnumTests
{
    [Fact] public void ContextEventType_default() => Assert.Equal(ContextEventType.Created, default);
    [Fact] public void ContextValueSource_default() => Assert.Equal(ContextValueSource.None, default);
}
