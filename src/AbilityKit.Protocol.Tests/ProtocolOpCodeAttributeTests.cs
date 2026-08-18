using System.Reflection;
using AbilityKit.Protocol;

namespace AbilityKit.Protocol.Tests;

public sealed class ProtocolOpCodeAttributeTests
{
    [Fact]
    public void Constructor_StoresOpCodeDirectionAndName()
    {
        var attribute = new ProtocolOpCodeAttribute(123u, ProtocolDirection.ClientToServer, "CustomName");

        Assert.Equal(123u, attribute.OpCode);
        Assert.Equal(ProtocolDirection.ClientToServer, attribute.Direction);
        Assert.Equal("CustomName", attribute.Name);
    }

    [Fact]
    public void Constructor_DefaultsDirectionToBidirectional()
    {
        var attribute = new ProtocolOpCodeAttribute(1u);

        Assert.Equal(ProtocolDirection.Bidirectional, attribute.Direction);
    }

    [Fact]
    public void Constructor_DefaultsNameToNull()
    {
        var attribute = new ProtocolOpCodeAttribute(1u);

        Assert.Null(attribute.Name);
    }

    [Fact]
    public void AttributeUsage_AllowsClassAndStructOnly()
    {
        var usage = typeof(ProtocolOpCodeAttribute).GetCustomAttribute<AttributeUsageAttribute>();

        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.Class | AttributeTargets.Struct, usage.ValidOn);
    }

    [Fact]
    public void AttributeUsage_DisallowsMultipleAndInheritance()
    {
        var usage = typeof(ProtocolOpCodeAttribute).GetCustomAttribute<AttributeUsageAttribute>();

        Assert.NotNull(usage);
        Assert.False(usage.AllowMultiple);
        Assert.False(usage.Inherited);
    }

    [Theory]
    [InlineData(ProtocolDirection.ClientToServer, 0)]
    [InlineData(ProtocolDirection.ServerToClient, 1)]
    [InlineData(ProtocolDirection.Bidirectional, 2)]
    public void ProtocolDirection_EnumValues_AreStable(ProtocolDirection direction, int numericValue)
    {
        // 枚举数值属于线格式契约的一部分：任何改变都是破坏性变更。
        Assert.Equal(numericValue, (int)direction);
    }
}
