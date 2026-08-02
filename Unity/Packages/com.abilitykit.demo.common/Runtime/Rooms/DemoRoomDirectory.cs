#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Demo.Common.Rooms
{
    public readonly struct DemoRoomDirectoryQuery
    {
        public DemoRoomDirectoryQuery(
            string sessionToken,
            string region,
            string serverId,
            string roomType,
            int offset = 0,
            int limit = 20)
        {
            SessionToken = sessionToken ?? string.Empty;
            Region = region ?? string.Empty;
            ServerId = serverId ?? string.Empty;
            RoomType = roomType ?? string.Empty;
            Offset = Math.Max(0, offset);
            Limit = Math.Max(1, limit);
        }

        public string SessionToken { get; }
        public string Region { get; }
        public string ServerId { get; }
        public string RoomType { get; }
        public int Offset { get; }
        public int Limit { get; }
    }

    public readonly struct DemoRoomSummary
    {
        public DemoRoomSummary(
            string region,
            string serverId,
            string roomId,
            string roomType,
            string title,
            bool isPublic,
            int maxPlayers,
            int playerCount,
            string ownerAccountId,
            long createdAtUnixMs,
            IReadOnlyDictionary<string, string>? tags = null)
        {
            Region = region ?? string.Empty;
            ServerId = serverId ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            RoomType = roomType ?? string.Empty;
            Title = title ?? string.Empty;
            IsPublic = isPublic;
            MaxPlayers = maxPlayers;
            PlayerCount = playerCount;
            OwnerAccountId = ownerAccountId ?? string.Empty;
            CreatedAtUnixMs = createdAtUnixMs;
            Tags = tags ?? EmptyTags;
        }

        private static readonly IReadOnlyDictionary<string, string> EmptyTags =
            new Dictionary<string, string>();

        public string Region { get; }
        public string ServerId { get; }
        public string RoomId { get; }
        public string RoomType { get; }
        public string Title { get; }
        public bool IsPublic { get; }
        public int MaxPlayers { get; }
        public int PlayerCount { get; }
        public string OwnerAccountId { get; }
        public long CreatedAtUnixMs { get; }
        public IReadOnlyDictionary<string, string> Tags { get; }
        public bool HasOpenSlot => MaxPlayers <= 0 || PlayerCount < MaxPlayers;
        public string DisplayName => string.IsNullOrWhiteSpace(Title) ? RoomId : Title;
    }

    public readonly struct DemoRoomDirectoryResult
    {
        public DemoRoomDirectoryResult(
            bool success,
            IReadOnlyList<DemoRoomSummary>? rooms,
            int nextOffset,
            string message)
        {
            Success = success;
            Rooms = rooms ?? Array.Empty<DemoRoomSummary>();
            NextOffset = Math.Max(0, nextOffset);
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public IReadOnlyList<DemoRoomSummary> Rooms { get; }
        public int NextOffset { get; }
        public string Message { get; }
    }

    public interface IDemoRoomDirectoryClient
    {
        Task<DemoRoomDirectoryResult> ListRoomsAsync(
            DemoRoomDirectoryQuery query,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);
    }

    public static class DemoRoomGatewayDirectoryCodec
    {
        public static ArraySegment<byte> SerializeQuery(in DemoRoomDirectoryQuery query)
        {
            if (string.IsNullOrWhiteSpace(query.SessionToken))
            {
                throw new ArgumentException("sessionToken is required.", nameof(query));
            }

            if (string.IsNullOrWhiteSpace(query.Region))
            {
                throw new ArgumentException("region is required.", nameof(query));
            }

            if (string.IsNullOrWhiteSpace(query.ServerId))
            {
                throw new ArgumentException("serverId is required.", nameof(query));
            }

            var request = new WireListRoomsReq
            {
                SessionToken = query.SessionToken,
                Region = query.Region,
                ServerId = query.ServerId,
                Offset = query.Offset,
                Limit = query.Limit,
                RoomType = query.RoomType
            };
            return WireRoomGatewayBinary.Serialize(in request);
        }

        public static DemoRoomDirectoryResult DeserializeResult(ArraySegment<byte> payload)
        {
            var response = WireRoomGatewayBinary.Deserialize<WireListRoomsRes>(payload);
            var summaries = response.Rooms;
            if (summaries == null || summaries.Count == 0)
            {
                return new DemoRoomDirectoryResult(
                    response.Success,
                    Array.Empty<DemoRoomSummary>(),
                    response.NextOffset,
                    response.Message);
            }

            var rooms = new DemoRoomSummary[summaries.Count];
            for (var i = 0; i < summaries.Count; i++)
            {
                var summary = summaries[i];
                rooms[i] = new DemoRoomSummary(
                    summary.Region,
                    summary.ServerId,
                    summary.RoomId,
                    summary.RoomType,
                    summary.Title,
                    summary.IsPublic,
                    summary.MaxPlayers,
                    summary.PlayerCount,
                    summary.OwnerAccountId,
                    summary.CreatedAtUnixMs,
                    summary.Tags);
            }

            return new DemoRoomDirectoryResult(
                response.Success,
                rooms,
                response.NextOffset,
                response.Message);
        }
    }
}
