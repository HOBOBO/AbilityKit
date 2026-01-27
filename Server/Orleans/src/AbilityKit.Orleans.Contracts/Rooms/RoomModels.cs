using System.Collections.Generic;
using Orleans.Serialization;

namespace AbilityKit.Orleans.Contracts.Rooms;

[GenerateSerializer]
public sealed record RoomSummary(
    [property: Id(0)] string Region,
    [property: Id(1)] string ServerId,
    [property: Id(2)] string RoomId,
    [property: Id(3)] string RoomType,
    [property: Id(4)] string Title,
    [property: Id(5)] bool IsPublic,
    [property: Id(6)] int MaxPlayers,
    [property: Id(7)] int PlayerCount,
    [property: Id(8)] string OwnerAccountId,
    [property: Id(9)] long CreatedAtUnixMs,
    [property: Id(10)] Dictionary<string, string>? Tags);

[GenerateSerializer]
public sealed record CreateRoomRequest(
    [property: Id(0)] string AccountId,
    [property: Id(1)] string Region,
    [property: Id(2)] string ServerId,
    [property: Id(3)] string RoomType,
    [property: Id(4)] string Title,
    [property: Id(5)] bool IsPublic,
    [property: Id(6)] int MaxPlayers,
    [property: Id(7)] Dictionary<string, string>? Tags);

[GenerateSerializer]
public sealed record CreateRoomResponse(
    [property: Id(0)] string RoomId);

[GenerateSerializer]
public sealed record JoinRoomRequest(
    [property: Id(0)] string AccountId,
    [property: Id(1)] string Region,
    [property: Id(2)] string ServerId,
    [property: Id(3)] string RoomId);

[GenerateSerializer]
public sealed record LeaveRoomRequest(
    [property: Id(0)] string AccountId,
    [property: Id(1)] string Region,
    [property: Id(2)] string ServerId,
    [property: Id(3)] string RoomId);

[GenerateSerializer]
public sealed record RoomSnapshot(
    [property: Id(0)] RoomSummary Summary,
    [property: Id(1)] List<string> Members);

[GenerateSerializer]
public sealed record ListRoomsRequest(
    [property: Id(0)] string AccountId,
    [property: Id(1)] string Region,
    [property: Id(2)] string ServerId,
    [property: Id(3)] int Offset,
    [property: Id(4)] int Limit,
    [property: Id(5)] string? RoomType);

[GenerateSerializer]
public sealed record ListRoomsResponse(
    [property: Id(0)] List<RoomSummary> Rooms,
    [property: Id(1)] int NextOffset);
