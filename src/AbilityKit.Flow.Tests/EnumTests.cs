using AbilityKit.Ability.Flow;
using Xunit;

namespace AbilityKit.Flow.Tests;

public sealed class EnumTests
{
    [Fact] public void FlowStatus_default() => Assert.Equal(FlowStatus.NotStarted, default);
    [Fact] public void FlowExecutionResult_default() => Assert.Equal(FlowStatus.NotStarted, default(FlowExecutionResult).Status);
}
