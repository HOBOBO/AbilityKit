using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Demo.Tiny.View;
using AbilityKit.Network.Room;
using AbilityKit.Protocol.Room;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AbilityKit.Demo.Tiny.Tests
{
    public sealed class TinyViewProjectionPlayModeTests
    {
        private const ulong WorldId = 7;

        [UnityTest]
        public IEnumerator StateSnapshotsUpdateAndRemoveActorViews()
        {
            var root = new GameObject("Tiny State Projection Test");
            var gateway = new GatewayStub();
            var session = NewSession(gateway, TinySyncMode.State);
            var module = new TinyActorViewModule();
            var context = new TinyViewModuleContext(root.transform, () => session,
                CancellationToken.None, exception => throw exception);
            try
            {
                module.OnAttach(in context);
                gateway.Push(new WireStateSyncSnapshotPush
                {
                    WorldId = WorldId, Frame = 1, IsFullSnapshot = true,
                    Actors = new List<WireStateSyncActorSnapshot>
                    {
                        new WireStateSyncActorSnapshot { ActorId = 1, X = -1, Z = 0, Hp = 100 },
                        new WireStateSyncActorSnapshot { ActorId = 2, X = 1, Z = 2, Hp = 90 }
                    }
                });
                module.Tick(in context, 0.02f);
                var owner = Actor(root, 1);
                var guest = Actor(root, 2);
                Assert.That(owner.transform.position, Is.EqualTo(new Vector3(-1, 0.5f, 0)));
                Assert.That(guest.transform.position, Is.EqualTo(new Vector3(1, 0.45f, 2)));
                Assert.That(guest.transform.localScale.y, Is.EqualTo(0.9f).Within(0.0001f));

                gateway.Push(new WireStateSyncSnapshotPush
                {
                    WorldId = WorldId, Frame = 2, IsFullSnapshot = true,
                    Actors = new List<WireStateSyncActorSnapshot>
                    {
                        new WireStateSyncActorSnapshot { ActorId = 1, X = 2, Z = -1, Hp = 40 }
                    }
                });
                module.Tick(in context, 0.02f);
                Assert.That(Actor(root, 1), Is.SameAs(owner));
                Assert.That(owner.transform.position, Is.EqualTo(new Vector3(2, 0.2f, -1)));
                Assert.That(owner.transform.localScale.y, Is.EqualTo(0.4f).Within(0.0001f));
                yield return null;
                Assert.That(root.transform.Find("Tiny Actor 2"), Is.Null);
                Assert.That(guest == null, Is.True);

                module.OnDetach(in context);
                yield return null;
                Assert.That(root.transform.childCount, Is.Zero);
            }
            finally
            {
                module.OnDetach(in context);
                session.Dispose();
                UnityEngine.Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator HybridPredictionAndSnapshotCorrectionUpdateSameActorViews()
        {
            var root = new GameObject("Tiny Hybrid Projection Test");
            var gateway = new GatewayStub();
            var session = NewSession(gateway, TinySyncMode.Hybrid);
            var module = new TinyActorViewModule();
            var context = new TinyViewModuleContext(root.transform, () => session,
                CancellationToken.None, exception => throw exception);
            try
            {
                module.OnAttach(in context);
                var authority = NewBattle();
                gateway.Push(Snapshot(authority));
                session.Tick(0.02f);
                module.Tick(in context, 0.02f);
                var owner = Actor(root, 1);
                var guest = Actor(root, 2);
                Assert.That(owner.transform.position.x, Is.EqualTo(-1));
                Assert.That(guest.transform.localScale.y, Is.EqualTo(1));

                session.SubmitInputAsync(new TinyInput(1, 0, true), CancellationToken.None)
                    .GetAwaiter().GetResult();
                module.Tick(in context, 0.02f);
                Assert.That(Actor(root, 1), Is.SameAs(owner));
                Assert.That(Actor(root, 2), Is.SameAs(guest));
                Assert.That(owner.transform.position.x, Is.EqualTo(0));
                Assert.That(guest.transform.localScale.y, Is.EqualTo(0.9f).Within(0.0001f));
                Assert.That(session.Telemetry.LocalPredictions, Is.EqualTo(1));

                while (authority.Frame < session.Telemetry.PredictedFrame) authority.Tick();
                gateway.Push(Snapshot(authority));
                session.Tick(0.02f);
                module.Tick(in context, 0.02f);
                Assert.That(session.Telemetry.SnapshotCorrections, Is.EqualTo(1));
                Assert.That(session.Telemetry.StateHash, Is.EqualTo(authority.ComputeHash()));
                Assert.That(session.NeedsFullSnapshot, Is.False);
                Assert.That(Actor(root, 1), Is.SameAs(owner));
                Assert.That(Actor(root, 2), Is.SameAs(guest));
                Assert.That(owner.transform.position.x, Is.EqualTo(-1));
                Assert.That(guest.transform.localScale.y, Is.EqualTo(1));

                module.OnDetach(in context);
                yield return null;
                Assert.That(root.transform.childCount, Is.Zero);
            }
            finally
            {
                module.OnDetach(in context);
                session.Dispose();
                UnityEngine.Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator FrameAuthorityAndRecoveryProjectOntoExistingActorViews()
        {
            var root = new GameObject("Tiny Frame Projection Test");
            var gateway = new GatewayStub();
            var session = NewSession(gateway, TinySyncMode.Frame);
            var module = new TinyActorViewModule();
            var context = new TinyViewModuleContext(root.transform, () => session,
                CancellationToken.None, exception => throw exception);
            try
            {
                module.OnAttach(in context);
                var authority = NewBattle();
                gateway.Push(Snapshot(authority));
                session.Tick(0.02f);
                module.Tick(in context, 0.02f);
                var owner = Actor(root, 1);
                var guest = Actor(root, 2);
                Assert.That(owner.transform.position.x, Is.EqualTo(-1));
                Assert.That(guest.transform.localScale.y, Is.EqualTo(1));

                var input = new TinyInput(1, 0, true);
                authority.Submit(1, input);
                authority.Tick();
                gateway.PushFrame(Frame(0, authority.ComputeHash(), input));
                session.Tick(0.02f);
                module.Tick(in context, 0.02f);
                Assert.That(session.Telemetry.LocalPredictions, Is.Zero);
                Assert.That(session.Telemetry.StateHash, Is.EqualTo(authority.ComputeHash()));
                Assert.That(Actor(root, 1), Is.SameAs(owner));
                Assert.That(Actor(root, 2), Is.SameAs(guest));
                Assert.That(owner.transform.position.x, Is.EqualTo(0));
                Assert.That(guest.transform.localScale.y, Is.EqualTo(0.9f).Within(0.0001f));

                gateway.PushFrame(Frame(1, 42));
                session.Tick(0.02f);
                module.Tick(in context, 0.02f);
                Assert.That(session.NeedsFullSnapshot, Is.True);
                Assert.That(session.CanSubmitInput, Is.False);
                Assert.That(owner.transform.position.x, Is.EqualTo(0));
                Assert.That(guest.transform.localScale.y, Is.EqualTo(0.9f).Within(0.0001f));

                authority.Submit(1, new TinyInput(-1, 0, false));
                authority.Tick();
                gateway.Push(Snapshot(authority));
                session.Tick(0.02f);
                module.Tick(in context, 0.02f);
                Assert.That(session.NeedsFullSnapshot, Is.False);
                Assert.That(session.Telemetry.StateHash, Is.EqualTo(authority.ComputeHash()));
                Assert.That(gateway.ResumeFrameStreamCalls, Is.EqualTo(2));
                Assert.That(Actor(root, 1), Is.SameAs(owner));
                Assert.That(Actor(root, 2), Is.SameAs(guest));
                Assert.That(owner.transform.position.x, Is.EqualTo(-1));
                Assert.That(guest.transform.localScale.y, Is.EqualTo(0.9f).Within(0.0001f));

                module.OnDetach(in context);
                yield return null;
                Assert.That(root.transform.childCount, Is.Zero);
            }
            finally
            {
                module.OnDetach(in context);
                session.Dispose();
                UnityEngine.Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator BattleBindingChangeClearsOldViewsBeforeNextBaseline()
        {
            var root = new GameObject("Tiny Binding Projection Test");
            var gateway = new GatewayStub();
            var session = NewSession(gateway, TinySyncMode.State);
            var module = new TinyActorViewModule();
            var context = new TinyViewModuleContext(root.transform, () => session,
                CancellationToken.None, exception => throw exception);
            try
            {
                module.OnAttach(in context);
                gateway.Push(new WireStateSyncSnapshotPush
                {
                    WorldId = WorldId, Frame = 1, IsFullSnapshot = true,
                    Actors = new List<WireStateSyncActorSnapshot>
                    {
                        new WireStateSyncActorSnapshot { ActorId = 1, X = -1, Hp = 100 }
                    }
                });
                module.Tick(in context, 0.02f);
                var oldActor = Actor(root, 1);

                SetPrivate(session, "_battleId", string.Empty);
                SetPrivate(session, "_worldId", 0UL);
                module.Tick(in context, 0.02f);
                yield return null;
                Assert.That(oldActor == null, Is.True);
                Assert.That(root.transform.Find("Tiny Arena"), Is.Not.Null);

                SetPrivate(session, "_battleId", "battle-2");
                SetPrivate(session, "_worldId", WorldId + 1);
                session.SnapshotCursor.Reset(WorldId + 1);
                module.Tick(in context, 0.02f);
                Assert.That(root.transform.Find("Tiny Actor 1"), Is.Null);
                gateway.Push(new WireStateSyncSnapshotPush
                {
                    WorldId = WorldId + 1, Frame = 0, IsFullSnapshot = true,
                    Actors = new List<WireStateSyncActorSnapshot>
                    {
                        new WireStateSyncActorSnapshot { ActorId = 2, X = 3, Hp = 80 }
                    }
                });
                module.Tick(in context, 0.02f);
                Assert.That(Actor(root, 2).transform.position, Is.EqualTo(new Vector3(3, 0.4f, 0)));
            }
            finally
            {
                module.OnDetach(in context);
                session.Dispose();
                UnityEngine.Object.Destroy(root);
            }
        }

        private static TinyBattleSession NewSession(GatewayStub gateway, TinySyncMode mode)
        {
            var launch = new DemoMultiplayerLaunchRequest("localhost", 4057, "local", "dev",
                "owner", "token", TimeSpan.FromSeconds(5));
            var session = new TinyBattleSession(launch, gateway);
            SetPrivate(session, "_battleId", "battle-1");
            SetPrivate(session, "_worldId", WorldId);
            SetPrivate(session, "_playerId", 1u);
            SetPrivate(session, "_syncMode", mode);
            session.SnapshotCursor.Reset(WorldId);
            if (mode != TinySyncMode.State)
                SetPrivate(session, "_frameReplication", new TinyFrameReplication());
            return session;
        }

        private static void SetPrivate(TinyBattleSession session, string field, object value)
        {
            // 会话的房间侧字段如今在 RoomGatewayBattleSessionBase 里，沿继承链查找。
            for (var type = typeof(TinyBattleSession); type != null; type = type.BaseType)
            {
                var member = type.GetField(field,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (member == null) continue;
                member.SetValue(session, value);
                return;
            }
            Assert.Fail("Unknown session field: " + field);
        }

        private static GameObject Actor(GameObject root, int id)
        {
            var actor = root.transform.Find("Tiny Actor " + id);
            Assert.That(actor, Is.Not.Null, "Actor " + id + " was not projected.");
            return actor.gameObject;
        }

        private static TinyBattle NewBattle()
        {
            var battle = new TinyBattle();
            battle.AddPlayer(1, -1, 0);
            battle.AddPlayer(2, 1, 0);
            return battle;
        }

        private static WireStateSyncSnapshotPush Snapshot(TinyBattle battle) =>
            new WireStateSyncSnapshotPush
            {
                WorldId = WorldId,
                Frame = battle.Frame,
                IsFullSnapshot = true,
                PayloadOpCode = TinyBattleStateCodec.PayloadOpCode,
                Payload = TinyBattleStateCodec.Encode(battle.CaptureState())
            };

        private static WireRoomFramePush Frame(int frame, uint hash, TinyInput input = default) =>
            new WireRoomFramePush
            {
                WorldId = WorldId,
                Frame = frame,
                StateHash = hash,
                Inputs = frame == 0 ? new[]
                {
                    new WireRoomFrameInput
                    {
                        PlayerId = 1,
                        InputOpCode = TinyBattle.InputOpCode,
                        Payload = input.Encode()
                    }
                } : new WireRoomFrameInput[0]
            };

        private sealed class GatewayStub : ITinyBattleGateway
        {
            private readonly Queue<WireStateSyncSnapshotPush> _snapshots =
                new Queue<WireStateSyncSnapshotPush>();
            private readonly Queue<WireRoomFramePush> _frames = new Queue<WireRoomFramePush>();

            public IRoomGatewaySessionClient Rooms { get; } = new RoomClientStub();
            public RoomGatewayConnectionState ConnectionState => RoomGatewayConnectionState.Connected;
            public long ConnectionGeneration => 0;
            public int FrameOverflowCount => 0;
            public int ResumeFrameStreamCalls { get; private set; }
            public void Push(WireStateSyncSnapshotPush snapshot) => _snapshots.Enqueue(snapshot);
            public void PushFrame(WireRoomFramePush frame) => _frames.Enqueue(frame);
            public void Tick(float deltaTime) { }
            public bool CompleteRestore(long generation) => true;
            public bool TryGetLatest(out WireStateSyncSnapshotPush snapshot)
            {
                if (_snapshots.Count > 0)
                {
                    snapshot = _snapshots.Dequeue();
                    return true;
                }
                snapshot = default;
                return false;
            }
            public bool TryDequeueFrame(out WireRoomFramePush frame)
            {
                if (_frames.Count > 0)
                {
                    frame = _frames.Dequeue();
                    return true;
                }
                frame = default;
                return false;
            }
            public bool TryGetFrameOverflow(out int minimumRecoveryFrame)
            {
                minimumRecoveryFrame = 0;
                return false;
            }
            public void PrepareFullSnapshotRecovery() { }
            public void ResumeFrameStream() => ResumeFrameStreamCalls++;
            public Task<WireRequestFullStateSyncRes> RequestFullSnapshotAsync(
                WireRequestFullStateSyncReq request, CancellationToken cancellationToken) =>
                throw new NotSupportedException();
            public Task<WireRoomSubscribeFrameSyncRes> SubscribeFrameSyncAsync(
                WireRoomSubscribeFrameSyncReq request, CancellationToken cancellationToken) =>
                throw new NotSupportedException();
            public Task<WireRoomSubmitFrameInputRes> SubmitFrameInputAsync(
                WireRoomSubmitFrameInputReq request, CancellationToken cancellationToken) =>
                Task.FromResult(new WireRoomSubmitFrameInputRes { Accepted = true });
            public Task<WireSubmitBattleInputRes> SubmitAsync(string sessionToken, string battleId,
                ulong worldId, uint playerId, TinyInput input, ulong sequence,
                CancellationToken cancellationToken) => throw new NotSupportedException();
            public void Dispose() { }
        }

        private sealed class RoomClientStub : IRoomGatewaySessionClient
        {
            public Task<RoomGatewayCreateResult> CreateRoomAsync(RoomGatewayCreateRequest request,
                TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
            public Task<RoomGatewayJoinResult> JoinRoomAsync(RoomGatewayJoinRequest request,
                TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
            public Task<RoomGatewayLeaveResult> LeaveRoomAsync(RoomGatewayLeaveRequest request,
                TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
            public Task<RoomGatewayReadyResult> SetReadyAsync(RoomGatewayReadyRequest request,
                TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
            public Task<RoomGatewayRestoreRoomResult> RestoreRoomAsync(RoomGatewayRestoreRoomRequest request,
                TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
            public Task<RoomGatewayGetSnapshotResult> GetSnapshotAsync(RoomGatewayGetSnapshotRequest request,
                TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
            public Task<RoomGatewayPickHeroResult> PickHeroAsync(RoomGatewayPickHeroRequest request,
                TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
            public Task<RoomGatewayBeginLoadingResult> BeginLoadingAsync(RoomGatewayBeginLoadingRequest request,
                TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
            public Task<RoomGatewayReportLoadingProgressResult> ReportLoadingProgressAsync(
                RoomGatewayReportLoadingProgressRequest request, TimeSpan? timeout = null,
                CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<RoomGatewayReportAssetsLoadedResult> ReportAssetsLoadedAsync(
                RoomGatewayReportAssetsLoadedRequest request, TimeSpan? timeout = null,
                CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<RoomGatewayCancelLoadingResult> CancelLoadingAsync(
                RoomGatewayCancelLoadingRequest request, TimeSpan? timeout = null,
                CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<RoomGatewayStartBattleResult> StartBattleAsync(RoomGatewayStartBattleRequest request,
                TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
            public Task<RoomGatewayStateSyncSubscriptionResult> SubscribeStateSyncAsync(
                RoomGatewayStateSyncSubscriptionRequest request, TimeSpan? timeout = null,
                CancellationToken cancellationToken = default) => throw new NotSupportedException();
        }
    }
}
