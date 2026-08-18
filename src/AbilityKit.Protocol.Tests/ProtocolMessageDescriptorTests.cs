using AbilityKit.Protocol;

namespace AbilityKit.Protocol.Tests;

public sealed class ProtocolMessageDescriptorTests
{
    [Fact]
    public void OpCode_ReturnsDeclaredOpCode()
    {
        Assert.Equal(TestOpCodes.ClientRequest, ProtocolMessageDescriptor<TestClientRequest>.OpCode);
    }

    [Fact]
    public void Direction_ReturnsDeclaredDirection()
    {
        Assert.Equal(ProtocolDirection.ClientToServer, ProtocolMessageDescriptor<TestClientRequest>.Direction);
        Assert.Equal(ProtocolDirection.ServerToClient, ProtocolMessageDescriptor<TestServerPush>.Direction);
        Assert.Equal(ProtocolDirection.Bidirectional, ProtocolMessageDescriptor<TestBiEvent>.Direction);
    }

    [Fact]
    public void RequireOpCode_MatchingDirection_ReturnsOpCode()
    {
        var opCode = ProtocolMessageDescriptor<TestClientRequest>.RequireOpCode(ProtocolDirection.ClientToServer);

        Assert.Equal(TestOpCodes.ClientRequest, opCode);
    }

    [Theory]
    [InlineData(ProtocolDirection.ClientToServer)]
    [InlineData(ProtocolDirection.ServerToClient)]
    [InlineData(ProtocolDirection.Bidirectional)]
    public void RequireOpCode_BidirectionalMessage_AcceptsAnyDirection(ProtocolDirection expected)
    {
        var opCode = ProtocolMessageDescriptor<TestBiEvent>.RequireOpCode(expected);

        Assert.Equal(TestOpCodes.BiEvent, opCode);
    }

    [Fact]
    public void RequireOpCode_MismatchedDirection_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => ProtocolMessageDescriptor<TestClientRequest>.RequireOpCode(ProtocolDirection.ServerToClient));

        Assert.Contains(nameof(ProtocolDirection.ClientToServer), ex.Message);
        Assert.Contains(nameof(ProtocolDirection.ServerToClient), ex.Message);
    }

    [Fact]
    public void OpCode_TypeWithoutAttribute_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ProtocolMessageDescriptor<TestPlainStruct>.OpCode);

        Assert.Contains(nameof(ProtocolOpCodeAttribute), ex.Message);
    }
}
