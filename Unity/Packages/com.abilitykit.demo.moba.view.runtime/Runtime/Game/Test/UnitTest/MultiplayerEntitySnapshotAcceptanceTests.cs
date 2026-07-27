using System.Collections.Generic;
using System.Linq;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Flow;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Protocol.Room;
using AbilityKit.World.ECS;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Game.Test.UnitTest
{
    /// <summary>
    /// 跨客户端实体快照协议层验收测试。
    ///
    /// 验证目标：当一局战斗含 2+ 玩家时，<see cref="MobaActorSpawnSnapshotEntry"/> 的
    /// 序列化/反序列化路径能正确保持全部 actor（证明协议层支持多客户端实体同步）。
    ///
    /// 这是"演示级联机"验证金字塔的协议层基础。完整的 ECS 层测试（mock BattleContext
    /// + EntityWorld → ApplySpawn → 断言创建了 N 个实体）需要更重的装配，作为后续工作。
    /// </summary>
    [TestFixture]
    public sealed class MultiplayerEntitySnapshotAcceptanceTests
    {
        /// <summary>
        /// 两个玩家英雄的 spawn 快照经序列化→反序列化后，actor 数量和字段保持一致。
        /// 这证明服务端把两个 actor 打成一个 payload 推给客户端时，客户端能正确解码出两个。
        /// </summary>
        [Test]
        public void SerializeThenDeserialize_TwoPlayerHeroes_PreservesBothActors()
        {
            var entries = new MobaActorSpawnSnapshotEntry[]
            {
                new MobaActorSpawnSnapshotEntry(
                    netId: 1001,
                    kind: (int)SpawnEntityKind.Character,
                    code: 1001,   // hero 1001 (廉颇)
                    ownerNetId: 1,
                    x: 10f, y: 0f, z: 20f),
                new MobaActorSpawnSnapshotEntry(
                    netId: 2002,
                    kind: (int)SpawnEntityKind.Character,
                    code: 1002,   // hero 1002 (小乔)
                    ownerNetId: 2,
                    x: -10f, y: 0f, z: -20f),
            };

            var payload = MobaActorSpawnSnapshotCodec.Serialize(entries);
            Assert.NotZero(payload.Length, "Serialized payload should not be empty.");

            var decoded = MobaActorSpawnSnapshotCodec.Deserialize(payload);
            Assert.AreEqual(2, decoded.Length, "Deserialized entries should contain both player heroes.");

            // 验证第一个 actor（owner 的英雄）
            Assert.AreEqual(1001, decoded[0].NetId);
            Assert.AreEqual((int)SpawnEntityKind.Character, decoded[0].Kind);
            Assert.AreEqual(1001, decoded[0].Code);
            Assert.AreEqual(1, decoded[0].OwnerNetId);
            Assert.AreEqual(10f, decoded[0].X);
            Assert.AreEqual(20f, decoded[0].Z);

            // 验证第二个 actor（member 的英雄）
            Assert.AreEqual(2002, decoded[1].NetId);
            Assert.AreEqual((int)SpawnEntityKind.Character, decoded[1].Kind);
            Assert.AreEqual(1002, decoded[1].Code);
            Assert.AreEqual(2, decoded[1].OwnerNetId);
            Assert.AreEqual(-10f, decoded[1].X);
            Assert.AreEqual(-20f, decoded[1].Z);
        }

        /// <summary>
        /// 混合实体类型（2 玩家英雄 + 1 投射物）的 spawn 快照也能正确保持。
        /// 这证明跨客户端场景下"我看到的对方投射物"也能正确解码。
        /// </summary>
        [Test]
        public void SerializeThenDeserialize_MixedCharactersAndProjectiles_PreservesAll()
        {
            var entries = new MobaActorSpawnSnapshotEntry[]
            {
                new MobaActorSpawnSnapshotEntry(1001, (int)SpawnEntityKind.Character, 1001, 1, 10f, 0f, 20f),
                new MobaActorSpawnSnapshotEntry(2002, (int)SpawnEntityKind.Character, 1002, 2, -10f, 0f, -20f),
                new MobaActorSpawnSnapshotEntry(3003, (int)SpawnEntityKind.Projectile, 5001, 1001, 5f, 0f, 10f),
            };

            var decoded = MobaActorSpawnSnapshotCodec.Deserialize(
                MobaActorSpawnSnapshotCodec.Serialize(entries));

            Assert.AreEqual(3, decoded.Length, "All three entities (2 heroes + 1 projectile) should survive round-trip.");

            var projectile = decoded.Single(e => e.Kind == (int)SpawnEntityKind.Projectile);
            Assert.AreEqual(3003, projectile.NetId);
            Assert.AreEqual(5001, projectile.Code);
            Assert.AreEqual(1001, projectile.OwnerNetId, "Projectile should reference its owner (hero 1001).");
        }

        /// <summary>
        /// 空 payload 反序列化返回空数组而非 null——ApplySpawn 能安全处理。
        /// </summary>
        [Test]
        public void Deserialize_EmptyPayload_ReturnsEmptyArray()
        {
            var decoded = MobaActorSpawnSnapshotCodec.Deserialize(null);
            Assert.NotNull(decoded);
            Assert.Zero(decoded.Length);

            decoded = MobaActorSpawnSnapshotCodec.Deserialize(System.Array.Empty<byte>());
            Assert.NotNull(decoded);
            Assert.Zero(decoded.Length);
        }

        // ===================================================================
        // WireStateSyncSnapshotPush 往返测试（验证修复后的解码路径）
        // ===================================================================

        /// <summary>
        /// 含 2 个玩家英雄的 StateSyncPush 经 WireRoomGatewayBinary（MemoryPack）编码→解码后，
        /// 全部字段保持一致。这直接验证 GatewayRoomClient.DeserializeStateSyncSnapshotPush
        /// 修复后的解码类型（WireStateSyncSnapshotPush）在多客户端 actor 场景下正确工作。
        ///
        /// 背景：此前 GatewayRoomClient 用 MobaWorldSnapshotCodec（BinaryObjectCodec）解码，
        /// 与服务端的 MemoryPack 编码不兼容，会静默产出空快照。2026-07-20 修复后改用
        /// WireRoomGatewayBinary.Deserialize&lt;WireStateSyncSnapshotPush&gt;，本测试验证该路径。
        /// </summary>
        [Test]
        public void WireStateSyncSnapshotPush_RoundTrip_PreservesTwoPlayerActors()
        {
            var push = new WireStateSyncSnapshotPush
            {
                WorldId = 12345,
                Frame = 100,
                Timestamp = 1.5,
                IsFullSnapshot = true,
                EventWatermark = 42,
                SchemaVersion = 3,
                RemovedActorIds = new List<int> { 3003, 4004 },
                EventEpoch = "epoch-20260725",
                Actors = new List<WireStateSyncActorSnapshot>
                {
                    new WireStateSyncActorSnapshot
                    {
                        ActorId = 1001, X = 10f, Y = 0f, Z = 20f,
                        Rotation = 0f, VelocityX = 1f, VelocityZ = 0f,
                        Hp = 1000f, HpMax = 1000f, TeamId = 1
                    },
                    new WireStateSyncActorSnapshot
                    {
                        ActorId = 2002, X = -10f, Y = 0f, Z = -20f,
                        Rotation = 180f, VelocityX = -1f, VelocityZ = 0f,
                        Hp = 800f, HpMax = 1000f, TeamId = 2
                    },
                }
            };

            var wire = WireRoomGatewayBinary.Serialize(in push);
            Assert.NotZero(wire.Count, "Wire payload should not be empty.");

            var decoded = WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>(wire);

            // 快照级字段
            Assert.AreEqual(12345UL, decoded.WorldId);
            Assert.AreEqual(100, decoded.Frame);
            Assert.AreEqual(1.5, decoded.Timestamp, 0.001);
            Assert.IsTrue(decoded.IsFullSnapshot);
            Assert.AreEqual(42L, decoded.EventWatermark);
            Assert.AreEqual(3, decoded.SchemaVersion);
            Assert.AreEqual("epoch-20260725", decoded.EventEpoch);
            CollectionAssert.AreEqual(new[] { 3003, 4004 }, decoded.RemovedActorIds);

            // Actor 列表
            Assert.NotNull(decoded.Actors);
            Assert.AreEqual(2, decoded.Actors.Count, "Both player heroes should survive round-trip.");

            // 第一个 actor（owner 英雄）
            Assert.AreEqual(1001, decoded.Actors[0].ActorId);
            Assert.AreEqual(10f, decoded.Actors[0].X);
            Assert.AreEqual(20f, decoded.Actors[0].Z);
            Assert.AreEqual(1000f, decoded.Actors[0].Hp);
            Assert.AreEqual(1, decoded.Actors[0].TeamId);

            // 第二个 actor（member 英雄）
            Assert.AreEqual(2002, decoded.Actors[1].ActorId);
            Assert.AreEqual(-10f, decoded.Actors[1].X);
            Assert.AreEqual(-20f, decoded.Actors[1].Z);
            Assert.AreEqual(800f, decoded.Actors[1].Hp);
            Assert.AreEqual(2, decoded.Actors[1].TeamId);
        }

        /// <summary>
        /// Delta 快照（IsFullSnapshot=false）也能正确往返，且空 Actors 列表不 break 解码。
        /// </summary>
        [Test]
        public void WireStateSyncSnapshotPush_DeltaSnapshot_RoundTrip()
        {
            var push = new WireStateSyncSnapshotPush
            {
                WorldId = 999,
                Frame = 200,
                Timestamp = 3.0,
                IsFullSnapshot = false,
                Actors = null  // delta 快照可能不含 actor 数据
            };

            var wire = WireRoomGatewayBinary.Serialize(in push);
            var decoded = WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>(wire);

            Assert.AreEqual(999UL, decoded.WorldId);
            Assert.AreEqual(200, decoded.Frame);
            Assert.IsFalse(decoded.IsFullSnapshot);
            // Actors 为 null 是合法的（delta 快照可能只含 Payload 字段）
        }

        [Test]
        public void WireReliableBattleEventPush_RoundTrip_PreservesRecoveryCursorAndEvents()
        {
            var push = new WireReliableBattleEventPush
            {
                BattleId = "battle-1",
                Epoch = "epoch-2",
                FirstAvailableSequence = 6,
                Watermark = 7,
                RetentionGap = true,
                Events = new List<WireReliableBattleEvent>
                {
                    new WireReliableBattleEvent
                    {
                        EventId = "event-6",
                        BattleId = "battle-1",
                        Epoch = "epoch-2",
                        Sequence = 6,
                        SourceFrame = 120,
                        EventType = 9,
                        Payload = new byte[] { 1, 2, 3 }
                    }
                }
            };

            var wire = WireRoomGatewayBinary.Serialize(in push);
            var decoded = WireRoomGatewayBinary.Deserialize<WireReliableBattleEventPush>(wire);

            Assert.AreEqual("battle-1", decoded.BattleId);
            Assert.AreEqual("epoch-2", decoded.Epoch);
            Assert.AreEqual(6L, decoded.FirstAvailableSequence);
            Assert.AreEqual(7L, decoded.Watermark);
            Assert.IsTrue(decoded.RetentionGap);
            Assert.NotNull(decoded.Events);
            Assert.AreEqual(1, decoded.Events.Count);
            Assert.AreEqual("event-6", decoded.Events[0].EventId);
            Assert.AreEqual(6L, decoded.Events[0].Sequence);
            Assert.AreEqual(120, decoded.Events[0].SourceFrame);
            Assert.AreEqual(9, decoded.Events[0].EventType);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, decoded.Events[0].Payload);
        }

        [Test]
        public void WireAckReliableBattleEvents_RoundTrip_PreservesRequestAndResponse()
        {
            var request = new WireAckReliableBattleEventsReq
            {
                SessionToken = "session-1",
                BattleId = "battle-1",
                RoomId = "room-1",
                Epoch = "epoch-2",
                AckSequence = 7
            };
            var requestWire = WireRoomGatewayBinary.Serialize(in request);
            var decodedRequest =
                WireRoomGatewayBinary.Deserialize<WireAckReliableBattleEventsReq>(requestWire);

            Assert.AreEqual("session-1", decodedRequest.SessionToken);
            Assert.AreEqual("battle-1", decodedRequest.BattleId);
            Assert.AreEqual("room-1", decodedRequest.RoomId);
            Assert.AreEqual("epoch-2", decodedRequest.Epoch);
            Assert.AreEqual(7L, decodedRequest.AckSequence);

            var response = new WireAckReliableBattleEventsRes
            {
                Success = true,
                AcceptedAckSequence = 7,
                Message = "accepted"
            };
            var responseWire = WireRoomGatewayBinary.Serialize(in response);
            var decodedResponse =
                WireRoomGatewayBinary.Deserialize<WireAckReliableBattleEventsRes>(responseWire);

            Assert.IsTrue(decodedResponse.Success);
            Assert.AreEqual(7L, decodedResponse.AcceptedAckSequence);
            Assert.AreEqual("accepted", decodedResponse.Message);
        }

        // ===================================================================
        // 完整 ECS 层测试（验证 ApplySpawn → 实体创建 → Transform 写入）
        // ===================================================================

        /// <summary>
        /// 两个玩家英雄的 spawn 快照经 ApplySpawn 后，EntityWorld 创建了 2 个实体，
        /// 每个实体有正确的 BattleTransformComponent（位置与快照一致）。
        ///
        /// 这是"演示级联机"验证从协议层延伸到 ECS 层的关键测试：证明客户端收到含
        /// 两个玩家英雄的快照后，ApplySpawn 能在 EntityWorld 里创建两个独立实体——
        /// 即"我看到了对方的英雄"在数据层的闭环。
        /// </summary>
        [Test]
        public void ApplySpawn_TwoPlayerHeroes_CreatesTwoEntitiesWithCorrectTransforms()
        {
            // 最小 ECS 装配（EntityWorld 是轻量级实现，不依赖 Entitas 或 Unity 场景）
            var world = new EntityWorld(initialCapacity: 8);
            var lookup = new BattleEntityLookup();
            var factory = new BattleEntityFactory(world, lookup);

            var ctx = new BattleContext
            {
                EntityWorld = world,
                EntityLookup = lookup,
                EntityFactory = factory
            };

            var entries = new MobaActorSpawnSnapshotEntry[]
            {
                new MobaActorSpawnSnapshotEntry(
                    netId: 1001, kind: (int)SpawnEntityKind.Character, code: 1001,
                    ownerNetId: 1, x: 10f, y: 0f, z: 20f),
                new MobaActorSpawnSnapshotEntry(
                    netId: 2002, kind: (int)SpawnEntityKind.Character, code: 1002,
                    ownerNetId: 2, x: -10f, y: 0f, z: -20f),
            };

            // Act
            BattleSnapshotEntityApplier.ApplySpawn(ctx, entries);

            // Assert — 两个实体都被创建
            Assert.AreEqual(2, world.AliveCount,
                "Two player hero entities should have been created.");
            Assert.AreEqual(2, lookup.Count,
                "Lookup should have both entities bound by netId.");

            // 第一个英雄（owner）
            Assert.IsTrue(lookup.TryResolve(world, new BattleNetId(1001), out var hero1),
                "Owner hero (netId=1001) should be resolvable via lookup.");
            Assert.IsTrue(hero1.TryGetRef(out BattleTransformComponent t1));
            Assert.AreEqual(new Vector3(10f, 0f, 20f), t1.Position);

            // 第二个英雄（member）
            Assert.IsTrue(lookup.TryResolve(world, new BattleNetId(2002), out var hero2),
                "Member hero (netId=2002) should be resolvable via lookup.");
            Assert.IsTrue(hero2.TryGetRef(out BattleTransformComponent t2));
            Assert.AreEqual(new Vector3(-10f, 0f, -20f), t2.Position);
        }

        /// <summary>
        /// 对已有实体再次 ApplySpawn（updateExisting=true）时更新位置——
        /// 验证"远端英雄移动"在客户端的正确应用。
        /// </summary>
        [Test]
        public void ApplySpawn_ExistingEntity_UpdateExisting_UpdatesPosition()
        {
            var world = new EntityWorld(initialCapacity: 4);
            var lookup = new BattleEntityLookup();
            var factory = new BattleEntityFactory(world, lookup);

            var ctx = new BattleContext
            {
                EntityWorld = world,
                EntityLookup = lookup,
                EntityFactory = factory
            };

            // 先创建实体在初始位置
            BattleSnapshotEntityApplier.ApplySpawn(ctx, new MobaActorSpawnSnapshotEntry[]
            {
                new MobaActorSpawnSnapshotEntry(5001, (int)SpawnEntityKind.Character, 1001, 1, 0f, 0f, 0f),
            });
            Assert.AreEqual(1, world.AliveCount);

            // 再次 ApplySpawn 同 netId 但不同位置（模拟远端英雄移动后的新快照）
            BattleSnapshotEntityApplier.ApplySpawn(ctx, new MobaActorSpawnSnapshotEntry[]
            {
                new MobaActorSpawnSnapshotEntry(5001, (int)SpawnEntityKind.Character, 1001, 1, 50f, 0f, 50f),
            });

            // 不应该创建新实体
            Assert.AreEqual(1, world.AliveCount,
                "Applying spawn for an existing netId should not create a duplicate entity.");

            // 位置应该被更新
            Assert.IsTrue(lookup.TryResolve(world, new BattleNetId(5001), out var entity));
            Assert.IsTrue(entity.TryGetRef(out BattleTransformComponent transform));
            Assert.AreEqual(new Vector3(50f, 0f, 50f), transform.Position,
                "Position should reflect the latest snapshot (remote hero movement).");
        }

        /// <summary>
        /// ApplySpawn 过滤 NetId<=0 的无效 entry——不创建实体也不抛异常。
        /// </summary>
        [Test]
        public void ApplySpawn_InvalidNetId_FilteredSafely()
        {
            var world = new EntityWorld(initialCapacity: 4);
            var lookup = new BattleEntityLookup();
            var factory = new BattleEntityFactory(world, lookup);

            var ctx = new BattleContext
            {
                EntityWorld = world,
                EntityLookup = lookup,
                EntityFactory = factory
            };

            BattleSnapshotEntityApplier.ApplySpawn(ctx, new MobaActorSpawnSnapshotEntry[]
            {
                new MobaActorSpawnSnapshotEntry(0, (int)SpawnEntityKind.Character, 1, 1, 0f, 0f, 0f),   // NetId=0 无效
                new MobaActorSpawnSnapshotEntry(-1, (int)SpawnEntityKind.Character, 2, 2, 0f, 0f, 0f),  // NetId=-1 无效
                new MobaActorSpawnSnapshotEntry(1001, (int)SpawnEntityKind.Character, 1001, 1, 1f, 2f, 3f), // 有效
            });

            Assert.AreEqual(1, world.AliveCount,
                "Only the valid entry (NetId=1001) should create an entity.");
        }
    }
}
