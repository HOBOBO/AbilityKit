using System.Collections.Generic;
using Orleans.Serialization;

namespace AbilityKit.Orleans.Contracts.FrameSync;

[GenerateSerializer]
public sealed record FrameInputItem(
    [property: Id(0)] uint PlayerId,
    [property: Id(1)] int OpCode,
    [property: Id(2)] byte[] Payload);

[GenerateSerializer]
public sealed record FramePushedEvent(
    [property: Id(0)] ulong RoomId,
    [property: Id(1)] ulong WorldId,
    [property: Id(2)] int Frame,
    [property: Id(3)] List<FrameInputItem> Inputs);
