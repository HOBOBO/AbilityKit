using AbilityKit.Coordinator;
using Xunit;

namespace AbilityKit.Coordinator.Tests;

/// <summary>
/// coordinator 包 PlayerInput 的直接契约测试（脱离 demo）。
/// 覆盖构造/创建/操作码（序列化依赖 MemoryPack resolver，单元测仅验构造逻辑）。
/// </summary>
public sealed class PlayerInputTests
{
    [Fact]
    public void Constructor_sets_fields()
    {
        var payload = new byte[] { 1, 2, 3 };
        var input = new PlayerInput(5, 99, InputOpCodes.Stop, payload);
        Assert.Equal(5, input.Frame);
        Assert.Equal(99, input.PlayerId);
        Assert.Equal(InputOpCodes.Stop, input.OpCode);
        Assert.Same(payload, input.Payload);
    }

    [Fact]
    public void CreateStop_sets_opcode_and_empty_payload()
    {
        var input = PlayerInput.CreateStop(10, 1);
        Assert.Equal(10, input.Frame);
        Assert.Equal(1, input.PlayerId);
        Assert.Equal(InputOpCodes.Stop, input.OpCode);
        Assert.NotNull(input.Payload);
        Assert.Empty(input.Payload);
    }

    [Fact]
    public void OpCode_constants_are_distinct()
    {
        Assert.NotEqual(InputOpCodes.Move, InputOpCodes.Skill);
        Assert.NotEqual(InputOpCodes.Skill, InputOpCodes.Stop);
        Assert.NotEqual(InputOpCodes.Stop, InputOpCodes.Move);
    }
}
