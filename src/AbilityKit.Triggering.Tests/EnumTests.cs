using AbilityKit.Triggering.Runtime.Config;
using Xunit;

namespace AbilityKit.Triggering.Tests;

public sealed class EnumTests
{
    [Fact] public void ECueKind_default_is_none() => Assert.Equal(ECueKind.None, default);
    [Fact] public void ECueLevel_default_is_none() => Assert.Equal(ECueLevel.None, default);
    [Fact] public void ECueLifecycleStage_default_is_none() => Assert.Equal(ECueLifecycleStage.None, default);
}
