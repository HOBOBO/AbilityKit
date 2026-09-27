using AbilityKit.Demo.Moba.Share;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class BattleEnterGameContractBoundaryTests
{
    [Fact]
    public void ContractIsOwnedByPlatformNeutralAssembly()
    {
        var contractsAssembly = typeof(StateHashData).Assembly;

        Assert.Same(contractsAssembly, typeof(BattleEnterGameSnapshot).Assembly);
        Assert.Same(contractsAssembly, typeof(BattleEnterGamePlayer).Assembly);
        Assert.Same(contractsAssembly, typeof(BattlePlayerLoadout).Assembly);
        Assert.Equal("AbilityKit.Demo.Moba.Presentation.Contracts", contractsAssembly.GetName().Name);
    }
}
