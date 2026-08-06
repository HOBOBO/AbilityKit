using AbilityKit.Coordinator;
using Xunit;

namespace AbilityKit.Coordinator.Tests;

/// <summary>
/// coordinator 包 PlayerInput 的契约测试（脱离 demo）。覆盖原始 4 参构造。
/// （原 CreateStop/OpCode 常量测试随内置 payload-codec 集群一并移除——该集群无外部使用。）
/// </summary>
public sealed class PlayerInputTests
{
    [Fact]
    public void Constructor_sets_fields()
    {
        var payload = new byte[] { 1, 2, 3 };
        var input = new PlayerInput(5, 99, 1003, payload);
        Assert.Equal(5, input.Frame);
        Assert.Equal(99, input.PlayerId);
        Assert.Equal(1003, input.OpCode);
        Assert.Same(payload, input.Payload);
    }
}
