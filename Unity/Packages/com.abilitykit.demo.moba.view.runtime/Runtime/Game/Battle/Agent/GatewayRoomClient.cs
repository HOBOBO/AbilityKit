using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;
using AbilityKit.Protocol.Moba.GatewayTimeSync;

namespace AbilityKit.Game.Battle.Agent
{
    public sealed class GatewayRoomClient
    {
        private readonly IConnection _connection;
        private readonly RequestClient _request;
        private readonly GatewayRoomOpCodes _opCodes;

        public GatewayRoomClient(IConnection connection, GatewayRoomOpCodes opCodes)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _opCodes = opCodes;
            _request = new RequestClient(connection);
        }

        public Task<ArraySegment<byte>> SendRawRequestAsync(uint opCode, string json, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            var bytes = Encoding.UTF8.GetBytes(json);
            return _request.SendRequestAsync(opCode, new ArraySegment<byte>(bytes), timeout, cancellationToken);
        }

        public async Task<GatewayTimeSyncResult> TimeSyncAsync(uint timeSyncOpCode, long clientSendTicks, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var req = new WireTimeSyncReq(clientSendTicks);
            var payload = WireTimeSyncBinary.Serialize(in req);
            var resp = await _request.SendRequestAsync(timeSyncOpCode, payload, timeout, cancellationToken);
            var wire = WireTimeSyncBinary.DeserializeTimeSyncRes(resp);
            return new GatewayTimeSyncResult(wire.ClientSendTicks, wire.ServerNowTicks, wire.ServerTickFrequency);
        }

        public async Task<string> GuestLoginAsync(uint guestLoginOpCode, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var payload = await SendRawRequestAsync(guestLoginOpCode, "{}", timeout, cancellationToken);
            var respJson = DecodeUtf8(payload);
            var token = TinyJson.TryGetString(respJson, "SessionToken") ?? TinyJson.TryGetString(respJson, "sessionToken") ?? string.Empty;
            return token;
        }

        public async Task<GatewayCreateRoomResult> CreateRoomAsync(
            string sessionToken,
            string region,
            string serverId,
            string roomType,
            string title,
            bool isPublic,
            int maxPlayers,
            IReadOnlyDictionary<string, string> tags,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(region)) throw new ArgumentException("region is required.", nameof(region));
            if (string.IsNullOrWhiteSpace(serverId)) throw new ArgumentException("serverId is required.", nameof(serverId));
            if (string.IsNullOrWhiteSpace(roomType)) roomType = "battle";
            if (title == null) title = string.Empty;

            var json = BuildCreateRoomJson(sessionToken, region, serverId, roomType, title, isPublic, maxPlayers, tags);
            var payload = await SendRawRequestAsync(_opCodes.CreateRoom, json, timeout, cancellationToken);

            var respJson = DecodeUtf8(payload);
            var roomId = TinyJson.TryGetString(respJson, "RoomId") ?? TinyJson.TryGetString(respJson, "roomId") ?? string.Empty;
            if (!TinyJson.TryGetUInt64(respJson, "NumericRoomId", out var numericRoomId))
            {
                TinyJson.TryGetUInt64(respJson, "numericRoomId", out numericRoomId);
            }

            return new GatewayCreateRoomResult(roomId, numericRoomId);
        }

        public async Task<GatewayJoinRoomResult> JoinRoomAsync(
            string sessionToken,
            string region,
            string serverId,
            string roomId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(region)) throw new ArgumentException("region is required.", nameof(region));
            if (string.IsNullOrWhiteSpace(serverId)) throw new ArgumentException("serverId is required.", nameof(serverId));
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));

            var json = $"{{\"SessionToken\":\"{Escape(sessionToken)}\",\"Region\":\"{Escape(region)}\",\"ServerId\":\"{Escape(serverId)}\",\"RoomId\":\"{Escape(roomId)}\"}}";
            var payload = await SendRawRequestAsync(_opCodes.JoinRoom, json, timeout, cancellationToken);

            var respJson = DecodeUtf8(payload);
            if (!TinyJson.TryGetUInt64(respJson, "NumericRoomId", out var numericRoomId))
            {
                TinyJson.TryGetUInt64(respJson, "numericRoomId", out numericRoomId);
            }

            var snapshotJson = TinyJson.TryGetObjectJson(respJson, "Snapshot") ?? TinyJson.TryGetObjectJson(respJson, "snapshot") ?? string.Empty;

            var anchorJson = TinyJson.TryGetObjectJson(respJson, "WorldStartAnchor") ?? TinyJson.TryGetObjectJson(respJson, "worldStartAnchor");
            var anchor = default(GatewayWorldStartAnchor);
            if (!string.IsNullOrEmpty(anchorJson))
            {
                if (!TinyJson.TryGetInt64(anchorJson, "StartServerTicks", out var startServerTicks))
                {
                    TinyJson.TryGetInt64(anchorJson, "startServerTicks", out startServerTicks);
                }

                if (!TinyJson.TryGetInt64(anchorJson, "ServerTickFrequency", out var serverFreq))
                {
                    TinyJson.TryGetInt64(anchorJson, "serverTickFrequency", out serverFreq);
                }

                if (!TinyJson.TryGetInt32(anchorJson, "StartFrame", out var startFrame))
                {
                    TinyJson.TryGetInt32(anchorJson, "startFrame", out startFrame);
                }

                if (!TinyJson.TryGetDouble(anchorJson, "FixedDeltaSeconds", out var fixedDeltaSeconds))
                {
                    TinyJson.TryGetDouble(anchorJson, "fixedDeltaSeconds", out fixedDeltaSeconds);
                }

                anchor = new GatewayWorldStartAnchor(startServerTicks, serverFreq, startFrame, fixedDeltaSeconds);
            }

            return new GatewayJoinRoomResult(numericRoomId, snapshotJson, in anchor);
        }

        private static string BuildCreateRoomJson(
            string sessionToken,
            string region,
            string serverId,
            string roomType,
            string title,
            bool isPublic,
            int maxPlayers,
            IReadOnlyDictionary<string, string> tags)
        {
            var sb = new StringBuilder(256);
            sb.Append('{');
            sb.Append("\"SessionToken\":\"").Append(Escape(sessionToken)).Append("\",");
            sb.Append("\"Region\":\"").Append(Escape(region)).Append("\",");
            sb.Append("\"ServerId\":\"").Append(Escape(serverId)).Append("\",");
            sb.Append("\"RoomType\":\"").Append(Escape(roomType)).Append("\",");
            sb.Append("\"Title\":\"").Append(Escape(title)).Append("\",");
            sb.Append("\"IsPublic\":").Append(isPublic ? "true" : "false").Append(',');
            sb.Append("\"MaxPlayers\":").Append(maxPlayers);

            if (tags != null && tags.Count > 0)
            {
                sb.Append(",\"Tags\":{");
                var first = true;
                foreach (var kv in tags)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append('"').Append(Escape(kv.Key)).Append("\":\"").Append(Escape(kv.Value)).Append('"');
                }
                sb.Append('}');
            }

            sb.Append('}');
            return sb.ToString();
        }

        private static string DecodeUtf8(ArraySegment<byte> seg)
        {
            if (seg.Array == null || seg.Count <= 0) return string.Empty;
            return Encoding.UTF8.GetString(seg.Array, seg.Offset, seg.Count);
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
