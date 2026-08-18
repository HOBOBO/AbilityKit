using AbilityKit.Protocol;

namespace AbilityKit.Protocol.Tests;

public sealed class MessageEnvelopeTests
{
    [Fact]
    public void Constructor_AssignsMessageTypeAndPayload()
    {
        var envelope = new MessageEnvelope(42, "hello");

        Assert.Equal(42, envelope.MessageType);
        Assert.Equal("hello", envelope.Payload);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void Constructor_PreservesMessageTypeBoundaryValues(int messageType)
    {
        var envelope = new MessageEnvelope(messageType, "x");

        Assert.Equal(messageType, envelope.MessageType);
    }

    [Fact]
    public void Constructor_PreservesNullPayload()
    {
        var envelope = new MessageEnvelope(1, null!);

        Assert.Null(envelope.Payload);
    }

    [Fact]
    public void Constructor_PreservesEmptyPayload()
    {
        var envelope = new MessageEnvelope(1, string.Empty);

        Assert.Equal(string.Empty, envelope.Payload);
        Assert.Empty(envelope.Payload);
    }

    [Fact]
    public void DefaultValue_HasZeroTypeAndNullPayload()
    {
        var envelope = default(MessageEnvelope);

        Assert.Equal(0, envelope.MessageType);
        Assert.Null(envelope.Payload);
    }

    [Fact]
    public void ValueEquality_EqualFields_AreEqual()
    {
        var left = new MessageEnvelope(7, "payload");
        var right = new MessageEnvelope(7, "payload");

        // MessageEnvelope 未重写 Equals，因此是 ValueType 的反射式字段比较。
        Assert.True(left.Equals(right));
        Assert.True(left.Equals((object)right));
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void ValueEquality_DifferentMessageType_NotEqual()
    {
        var left = new MessageEnvelope(1, "payload");
        var right = new MessageEnvelope(2, "payload");

        Assert.False(left.Equals(right));
    }

    [Fact]
    public void ValueEquality_DifferentPayload_NotEqual()
    {
        var left = new MessageEnvelope(1, "a");
        var right = new MessageEnvelope(1, "b");

        Assert.False(left.Equals(right));
    }

    [Fact]
    public void Copy_IsIndependentValueTypeCopy()
    {
        var original = new MessageEnvelope(5, "abc");
        var copy = original;

        Assert.Equal(original.MessageType, copy.MessageType);
        Assert.Equal(original.Payload, copy.Payload);
    }
}
