using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.TcpGateway;
using AbilityKit.Protocol.Room;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class GatewayRoomClientLoadingTests
    {
        private sealed class MockConnection : IConnection
        {
            public ConnectionState State => ConnectionState.Connected;
            public bool IsConnected => true;

            public event Action Connected;
            public event Action Disconnected;
            public event Action<Exception> Error;
            public event Action<uint, uint, ArraySegment<byte>> PacketReceived;
            public event Action<uint, ArraySegment<byte>> ServerPushReceived;
            public event Action<string, string> Kicked;

            public uint LastOpCode;
            public ArraySegment<byte> LastPayload;
            public uint LastSeq;

            public Func<uint, ArraySegment<byte>> Responder;

            public void Open(string host, int port) { }
            public void Close() { }
            public void Tick(float deltaTime) { }

            public void Send(uint opCode, ArraySegment<byte> payload, ushort flags = 0, uint seq = 0)
            {
                LastOpCode = opCode;
                LastPayload = payload;
                LastSeq = seq;

                if (Responder != null)
                {
                    var responsePayload = Responder(opCode);
                    var framed = FrameOk(responsePayload);
                    PacketReceived?.Invoke(opCode, seq, framed);
                }
            }

            public void Dispose() { }
        }

        private static ArraySegment<byte> FrameOk(ArraySegment<byte> payload)
        {
            // 4 字节 status (Ok=0, little-endian) + payload
            var statusBytes = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(statusBytes, (int)TcpGatewayStatusCode.Ok);

            var payloadCount = payload.Array == null ? 0 : payload.Count;
            var result = new byte[4 + payloadCount];
            Buffer.BlockCopy(statusBytes, 0, result, 0, 4);
            if (payloadCount > 0)
            {
                Buffer.BlockCopy(payload.Array, payload.Offset, result, 4, payloadCount);
            }

            return new ArraySegment<byte>(result);
        }

        private static GatewayRoomClient CreateClient(MockConnection conn)
        {
            return new GatewayRoomClient(conn, GatewayRoomOpCodes.Default);
        }

        [Test]
        public void BeginLoadingAsync_SerializesAndDeserializes()
        {
            var conn = new MockConnection();
            var client = CreateClient(conn);

            var opRes = new WireRoomOperationRes
            {
                Success = true,
                Applied = true,
                ErrorCode = 0,
                Message = "ok",
                RoomRevision = 11L,
                Snapshot = new WireRoomSnapshot
                {
                    Summary = new WireRoomSummary { RoomId = "room-1" },
                    Phase = 1,
                    RoomRevision = 11L
                }
            };
            conn.Responder = _ => WireRoomGatewayBinary.Serialize(in opRes);

            var result = client.BeginLoadingAsync("token-1", "room-1", 10L, "cmd-1").Result;

            Assert.AreEqual(RoomGatewayOpCodes.BeginLoading, conn.LastOpCode);
            var reqWire = WireRoomGatewayBinary.Deserialize<WireBeginLoadingReq>(conn.LastPayload);
            Assert.AreEqual("token-1", reqWire.SessionToken);
            Assert.AreEqual("room-1", reqWire.RoomId);
            Assert.AreEqual(10L, reqWire.ExpectedRevision);
            Assert.AreEqual("cmd-1", reqWire.CommandId);

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Applied);
            Assert.AreEqual(11L, result.RoomRevision);
            Assert.IsNotNull(result.Snapshot);
            Assert.AreEqual("room-1", result.Snapshot.RoomId);
            Assert.AreEqual(ClientRoomPhase.Loading, result.Snapshot.Phase);
        }

        [Test]
        public void ReportAssetsLoadedAsync_SerializesAndDeserializes()
        {
            var conn = new MockConnection();
            var client = CreateClient(conn);

            var opRes = new WireRoomOperationRes
            {
                Success = true,
                Applied = true,
                RoomRevision = 12L,
                Snapshot = new WireRoomSnapshot
                {
                    Summary = new WireRoomSummary { RoomId = "room-2" },
                    Phase = 1
                }
            };
            conn.Responder = _ => WireRoomGatewayBinary.Serialize(in opRes);

            var result = client.ReportAssetsLoadedAsync("token-2", "room-2", 5L, 3, "hash-x", "cmd-2").Result;

            Assert.AreEqual(RoomGatewayOpCodes.ReportAssetsLoaded, conn.LastOpCode);
            var reqWire = WireRoomGatewayBinary.Deserialize<WireReportAssetsLoadedReq>(conn.LastPayload);
            Assert.AreEqual("token-2", reqWire.SessionToken);
            Assert.AreEqual("room-2", reqWire.RoomId);
            Assert.AreEqual(5L, reqWire.LaunchGeneration);
            Assert.AreEqual(3, reqWire.ManifestVersion);
            Assert.AreEqual("hash-x", reqWire.ManifestHash);
            Assert.AreEqual("cmd-2", reqWire.CommandId);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(12L, result.RoomRevision);
        }

        [Test]
        public void GetSnapshotAsync_SerializesAndDeserializes()
        {
            var conn = new MockConnection();
            var client = CreateClient(conn);

            var snapRes = new WireRoomSnapshotRes
            {
                Success = true,
                RoomId = "room-3",
                NumericRoomId = 9001ul,
                Snapshot = new WireRoomSnapshot
                {
                    Summary = new WireRoomSummary { RoomId = "room-3" },
                    Phase = 3,
                    BattleId = "battle-3",
                    CanStart = true
                },
                Message = "ok"
            };
            conn.Responder = _ => WireRoomGatewayBinary.Serialize(in snapRes);

            var result = client.GetSnapshotAsync("token-3", "room-3").Result;

            Assert.AreEqual(RoomGatewayOpCodes.GetSnapshot, conn.LastOpCode);
            var reqWire = WireRoomGatewayBinary.Deserialize<WireGetSnapshotReq>(conn.LastPayload);
            Assert.AreEqual("token-3", reqWire.SessionToken);
            Assert.AreEqual("room-3", reqWire.RoomId);

            Assert.IsTrue(result.Success);
            Assert.AreEqual("room-3", result.RoomId);
            Assert.AreEqual(9001ul, result.NumericRoomId);
            Assert.IsNotNull(result.Snapshot);
            Assert.AreEqual(ClientRoomPhase.InBattle, result.Snapshot.Phase);
            Assert.AreEqual("battle-3", result.Snapshot.BattleId);
        }

        [Test]
        public void DeserializeRoomStateChangedPush_ParsesCorrectly()
        {
            var conn = new MockConnection();
            var client = CreateClient(conn);

            var push = new WireRoomStateChangedPush
            {
                RoomId = "room-4",
                Snapshot = new WireRoomSnapshot
                {
                    Summary = new WireRoomSummary { RoomId = "room-4" },
                    Phase = 2,
                    RoomRevision = 20L,
                    LastEventSequence = 20L
                },
                ServerNowTicks = 9999L
            };
            var payload = WireRoomGatewayBinary.Serialize(in push);

            var snapshot = client.DeserializeRoomStateChangedPush(payload);

            Assert.IsNotNull(snapshot);
            Assert.AreEqual("room-4", snapshot.RoomId);
            Assert.AreEqual(ClientRoomPhase.Starting, snapshot.Phase);
            Assert.AreEqual(20L, snapshot.RoomRevision);
        }

        [Test]
        public void SubmitBattleInputAsync_UsesAuthoritativeProtocolAndPreservesResponse()
        {
            var conn = new MockConnection();
            var client = CreateClient(conn);
            var requests = new List<WireSubmitBattleInputReq>();
            var response = new WireSubmitBattleInputRes
            {
                Success = false,
                AcceptedFrame = 42,
                CurrentFrame = 40,
                Status = "RejectedTooFarFuture",
                Message = "resync required",
                ShouldResync = true,
                ServerTicks = 123456L
            };
            conn.Responder = _ =>
            {
                requests.Add(WireRoomGatewayBinary.Deserialize<WireSubmitBattleInputReq>(conn.LastPayload));
                return WireRoomGatewayBinary.Serialize(in response);
            };

            var first = client.SubmitBattleInputAsync(
                "token-input",
                "battle-input",
                9001UL,
                42,
                7U,
                100,
                new byte[] { 1, 2, 3 }).Result;
            var second = client.SubmitBattleInputAsync(
                "token-input",
                "battle-input",
                9001UL,
                43,
                7U,
                101,
                Array.Empty<byte>()).Result;

            Assert.AreEqual(RoomGatewayOpCodes.SubmitBattleInput, conn.LastOpCode);
            Assert.AreEqual(2, requests.Count);
            Assert.AreEqual("token-input", requests[0].SessionToken);
            Assert.AreEqual("battle-input", requests[0].BattleId);
            Assert.AreEqual(9001UL, requests[0].WorldId);
            Assert.AreEqual(42, requests[0].Frame);
            Assert.AreEqual(7U, requests[0].PlayerId);
            Assert.AreEqual(100, requests[0].InputOpCode);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, requests[0].Payload);
            Assert.AreEqual(1UL, requests[0].CommandSequence);
            Assert.AreEqual(2UL, requests[1].CommandSequence);

            Assert.IsFalse(first.Success);
            Assert.AreEqual(42, first.AcceptedFrame);
            Assert.AreEqual(40, first.CurrentFrame);
            Assert.AreEqual("RejectedTooFarFuture", first.Status);
            Assert.AreEqual("resync required", first.Message);
            Assert.IsTrue(first.ShouldResync);
            Assert.AreEqual(123456L, first.ServerTicks);
            Assert.AreEqual(1UL, first.CommandSequence);
            Assert.AreEqual(2UL, second.CommandSequence);
        }

        [Test]
        public void IsRoomStateChangedPush_MatchesOpCode()
        {
            var conn = new MockConnection();
            var client = CreateClient(conn);

            Assert.IsTrue(client.IsRoomStateChangedPush(RoomGatewayOpCodes.RoomStateChanged));
            Assert.IsFalse(client.IsRoomStateChangedPush(RoomGatewayOpCodes.GetSnapshot));
        }
    }
}
