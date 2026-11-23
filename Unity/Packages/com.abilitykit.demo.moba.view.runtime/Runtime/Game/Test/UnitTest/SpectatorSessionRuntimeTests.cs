using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Recording.FrameRecord;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Flow.Battle.Replay;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Room;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Moba.SettledFrames;
using AbilityKit.Protocol.Room;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class SpectatorSessionRuntimeTests
    {
        [UnityTest]
        public IEnumerator SettledSpectator_RequiresCheckpointAndNeverSynthesizesMissingFrames()
        {
            yield return AwaitTask(SettledSpectatorCore());
        }

        private static async Task SettledSpectatorCore()
        {
            var capabilities = CreateSettledCapabilities();
            var identity = new WireSettledIdentity
            {
                Version = SettledFrameProtocol.Version, RoomId = 17, WorldId = 71,
                BattleId = "settled-spectator", RuntimeGeneration = Guid.NewGuid().ToString("N")
            };
            var plan = new BattleStartPlan(new BattleStartPlanOptions(
                new BattleStartPlanWorldOptions("71", "moba", "spectator", "", 30, 3),
                default, default, default,
                new BattleStartPlanGatewayOptions(true, "local", 4000, 17, "token", "dev", "local",
                    false, false, "", 0, 0, identity.BattleId),
                default, default, default, default, remoteSyncCapabilities: capabilities));

            WireSettledFrame Frame(int index, params WireSettledCommand[] commands) => new WireSettledFrame
            {
                Identity = identity, Frame = index, InitialCheckpoint = index == 0,
                Commands = commands, Decisions = Array.Empty<WireSettledDecision>()
            };

            using var client = new ControllableNetworkClient();
            using var runtime = new SpectatorSessionRuntime();
            var sink = new TrackingSettledSink();
            var world = new TrackingWorld("71", sink);
            var start = runtime.StartAsync(client, plan, () => world);
            var subscribe = client.TakeRequest();
            Assert.That(subscribe.OpCode, Is.EqualTo(SettledFrameProtocol.Subscribe));
            Assert.That(SettledFrameProtocol.Decode<WireSettledSubscribeRequest>(
                new ArraySegment<byte>(subscribe.Payload)).Spectator, Is.True);
            subscribe.Complete(SettledFrameProtocol.Encode(new WireSettledSubscribeResponse
            {
                Identity = identity, LastSettledFrame = 2, TickRate = 30
            }));
            await AwaitWithTimeoutAsync(start);

            await client.SendServerPushAsync(SettledFrameProtocol.FramePushed, SettledFrameProtocol.Encode(Frame(2,
                new WireSettledCommand { Disposition = 1, PlayerId = 1, OpCode = 4, ExecutedPayload = new byte[] { 1 } },
                new WireSettledCommand { Ordinal = 1, Disposition = 2, PlayerId = 2, OpCode = 4, ExecutedPayload = Array.Empty<byte>() })));
            runtime.Tick(10);
            Assert.That(world.TickCount, Is.Zero);
            var page = client.TakeRequest();
            Assert.That(page.OpCode, Is.EqualTo(SettledFrameProtocol.CatchUp));
            var range = SettledFrameProtocol.Decode<WireSettledCatchUpRequest>(new ArraySegment<byte>(page.Payload));
            Assert.That(range.FromFrameExclusive, Is.EqualTo(-1));
            Assert.That(range.ToFrameInclusive, Is.EqualTo(2));
            page.Complete(SettledFrameProtocol.Encode(new WireSettledCatchUpResponse { Frames = new[] { Frame(0), Frame(1) } }));
            for (var attempt = 0; attempt < 100 && runtime.Driver.CurrentFrame < 2; attempt++)
            {
                runtime.Tick(10);
                await Task.Delay(5);
            }
            Assert.That(sink.Bound, Is.True);
            Assert.That(sink.ExecutedCounts, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(sink.CommittedFrames, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(world.TickCount, Is.EqualTo(2));
            Assert.That(runtime.Driver.CurrentFrame, Is.EqualTo(2));

            await client.SendServerPushAsync(SettledFrameProtocol.FramePushed, SettledFrameProtocol.Encode(Frame(4)));
            runtime.Tick(10);
            var repair = client.TakeRequest();
            Assert.That(repair.OpCode, Is.EqualTo(SettledFrameProtocol.CatchUp));
            var gap = SettledFrameProtocol.Decode<WireSettledCatchUpRequest>(new ArraySegment<byte>(repair.Payload));
            Assert.That(gap.FromFrameExclusive, Is.EqualTo(2));
            Assert.That(gap.ToFrameInclusive, Is.EqualTo(4));
            Assert.That(runtime.Driver.CurrentFrame, Is.EqualTo(2));
            runtime.Stop();
            Assert.That(client.PushSubscriberCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator StartAsync_PublishesOnlyAfterSubscribeCompletes()
        {
            yield return AwaitTask(StartAsync_PublishesOnlyAfterSubscribeCompletesCore());
        }

        private static async Task StartAsync_PublishesOnlyAfterSubscribeCompletesCore()
        {
            var client = new ControllableNetworkClient();
            var world = new TrackingWorld("spectator-a");
            var runtime = new SpectatorSessionRuntime();

            var startTask = runtime.StartAsync(client, CreateSettledPlan(17), () => world);
            var request = await client.WaitForRequestAsync();

            Assert.That(request.OpCode, Is.EqualTo(SettledFrameProtocol.Subscribe));
            Assert.That(runtime.IsStarting, Is.True);
            Assert.That(runtime.Driver, Is.Null);
            Assert.That(client.PushSubscriberCount, Is.EqualTo(1));

            request.Complete(CreateSubscriptionResponse(request, 0));
            await AwaitWithTimeoutAsync(startTask);

            Assert.That(runtime.IsStarting, Is.False);
            Assert.That(runtime.IsSpectating, Is.True);
            Assert.That(runtime.World, Is.SameAs(world));

            runtime.Stop();
            Assert.That(world.DisposeCount, Is.EqualTo(1));
            Assert.That(client.PushSubscriberCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator Stop_CancelsPendingSubscribeAndRejectsLateCompletion()
        {
            yield return AwaitTask(Stop_CancelsPendingSubscribeAndRejectsLateCompletionCore());
        }

        private static async Task Stop_CancelsPendingSubscribeAndRejectsLateCompletionCore()
        {
            var client = new ControllableNetworkClient();
            var world = new TrackingWorld("spectator-cancel");
            var runtime = new SpectatorSessionRuntime();

            var startTask = runtime.StartAsync(client, CreateSettledPlan(18), () => world);
            var request = await client.WaitForRequestAsync();

            var stopTask = runtime.StopAsync();

            Assert.That(request.CancellationToken.IsCancellationRequested, Is.True);
            await AwaitWithTimeoutAsync(stopTask);
            Assert.That(client.PushSubscriberCount, Is.Zero);
            request.Complete(CreateSubscriptionResponse(request, 0));

            Assert.That(await IsCanceledAsync(startTask), Is.True);
            Assert.That(runtime.Driver, Is.Null);
            Assert.That(world.DisposeCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator StartAsync_WhenReplaced_IgnoresStaleCompletion()
        {
            yield return AwaitTask(StartAsync_WhenReplaced_IgnoresStaleCompletionCore());
        }

        private static async Task StartAsync_WhenReplaced_IgnoresStaleCompletionCore()
        {
            var staleClient = new ControllableNetworkClient();
            var activeClient = new ControllableNetworkClient();
            var staleWorld = new TrackingWorld("spectator-stale");
            var activeWorld = new TrackingWorld("spectator-active");
            var runtime = new SpectatorSessionRuntime();

            var staleTask = runtime.StartAsync(staleClient, CreateSettledPlan(19), () => staleWorld);
            var staleRequest = await staleClient.WaitForRequestAsync();
            var activeTask = runtime.StartAsync(activeClient, CreateSettledPlan(20), () => activeWorld);
            var activeRequest = await activeClient.WaitForRequestAsync();

            activeRequest.Complete(CreateSubscriptionResponse(activeRequest, 0));
            await AwaitWithTimeoutAsync(activeTask);
            staleRequest.Complete(CreateSubscriptionResponse(staleRequest, 0));

            Assert.That(await IsCanceledAsync(staleTask), Is.True);
            Assert.That(runtime.World, Is.SameAs(activeWorld));
            Assert.That(staleWorld.DisposeCount, Is.Zero);
            Assert.That(staleClient.PushSubscriberCount, Is.Zero);
            Assert.That(activeClient.PushSubscriberCount, Is.EqualTo(1));

            runtime.Stop();
        }

        [UnityTest]
        public IEnumerator Stop_WhenWorldDisposeFails_RetainsOwnerForRetry()
        {
            yield return AwaitTask(Stop_WhenWorldDisposeFails_RetainsOwnerForRetryCore());
        }

        private static async Task Stop_WhenWorldDisposeFails_RetainsOwnerForRetryCore()
        {
            var client = new ControllableNetworkClient();
            var world = new TrackingWorld("spectator-retry")
            {
                DisposeFailure = new InvalidOperationException("world dispose failed"),
            };
            var runtime = new SpectatorSessionRuntime();

            var startTask = runtime.StartAsync(client, CreateSettledPlan(21), () => world);
            var request = await client.WaitForRequestAsync();
            request.Complete(CreateSubscriptionResponse(request, 0));
            await AwaitWithTimeoutAsync(startTask);

            Assert.Throws<InvalidOperationException>(() => runtime.Stop());
            Assert.That(runtime.World, Is.SameAs(world));
            Assert.That(world.DisposeCount, Is.EqualTo(1));
            Assert.That(client.PushSubscriberCount, Is.Zero);

            world.DisposeFailure = null;
            runtime.Stop();

            Assert.That(world.DisposeCount, Is.EqualTo(2));
            Assert.That(runtime.World, Is.Null);
        }

        [UnityTest]
        public IEnumerator Stop_CancelsPendingCatchUpAndDisposesCandidateWorld()
        {
            yield return AwaitTask(Stop_CancelsPendingCatchUpAndDisposesCandidateWorldCore());
        }

        private static async Task Stop_CancelsPendingCatchUpAndDisposesCandidateWorldCore()
        {
            var client = new ControllableNetworkClient();
            var world = new TrackingWorld("spectator-catch-up");
            var runtime = new SpectatorSessionRuntime();

            var startTask = runtime.StartAsync(client, CreateSettledPlan(22), () => world);
            var subscribeRequest = await client.WaitForRequestAsync();
            subscribeRequest.Complete(CreateSubscriptionResponse(subscribeRequest, 12));
            await AwaitWithTimeoutAsync(startTask);
            runtime.Tick(10);
            var catchUpRequest = client.TakeRequest();

            Assert.That(catchUpRequest.OpCode, Is.EqualTo(SettledFrameProtocol.CatchUp));
            Assert.That(runtime.Driver.CurrentFrame, Is.Zero);
            Assert.That(world.DisposeCount, Is.Zero);
            await AwaitWithTimeoutAsync(runtime.StopAsync());
            Assert.That(catchUpRequest.CancellationToken.IsCancellationRequested, Is.True);
            catchUpRequest.Complete(Array.Empty<byte>());
            Assert.That(world.DisposeCount, Is.EqualTo(1));
            Assert.That(runtime.World, Is.Null);
            Assert.That(client.PushSubscriberCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator StartAsync_WhenWorldFactoryFails_RollsBackSubscription()
        {
            yield return AwaitTask(StartAsync_WhenWorldFactoryFails_RollsBackSubscriptionCore());
        }

        private static async Task StartAsync_WhenWorldFactoryFails_RollsBackSubscriptionCore()
        {
            var client = new ControllableNetworkClient();
            var runtime = new SpectatorSessionRuntime();
            var failure = new InvalidOperationException("world factory failed");

            var startTask = runtime.StartAsync(client, CreateSettledPlan(23), () => throw failure);
            var request = await client.WaitForRequestAsync();
            request.Complete(CreateSubscriptionResponse(request, 0));

            Exception thrown = null;
            try
            {
                await AwaitWithTimeoutAsync(startTask);
            }
            catch (Exception ex)
            {
                thrown = ex;
            }

            Assert.That(thrown, Is.SameAs(failure));
            Assert.That(runtime.IsStarting, Is.False);
            Assert.That(runtime.Driver, Is.Null);
            Assert.That(client.PushSubscriberCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator Stop_AfterSuccessfulStart_IsIdempotent()
        {
            yield return AwaitTask(Stop_AfterSuccessfulStart_IsIdempotentCore());
        }

        private static async Task Stop_AfterSuccessfulStart_IsIdempotentCore()
        {
            var client = new ControllableNetworkClient();
            var world = new TrackingWorld("spectator-idempotent");
            var runtime = new SpectatorSessionRuntime();

            var startTask = runtime.StartAsync(client, CreateSettledPlan(24), () => world);
            var request = await client.WaitForRequestAsync();
            request.Complete(CreateSubscriptionResponse(request, 0));
            await AwaitWithTimeoutAsync(startTask);

            runtime.Stop();
            runtime.Stop();
            runtime.Dispose();
            runtime.Dispose();

            Assert.That(world.DisposeCount, Is.EqualTo(1));
            Assert.That(runtime.Driver, Is.Null);
            Assert.That(client.PushSubscriberCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator StopAsync_ConcurrentCallersSharePendingStop()
        {
            yield return AwaitTask(StopAsync_ConcurrentCallersSharePendingStopCore());
        }

        private static async Task StopAsync_ConcurrentCallersSharePendingStopCore()
        {
            var client = new ControllableNetworkClient();
            var runtime = new SpectatorSessionRuntime();
            var startTask = runtime.StartAsync(
                client,
                CreateSettledPlan(25),
                () => new TrackingWorld("spectator-concurrent-stop"));
            var request = await client.WaitForRequestAsync();

            var firstStop = runtime.StopAsync();
            var secondStop = runtime.StopAsync();

            Assert.That(secondStop, Is.SameAs(firstStop));
            Assert.That(request.CancellationToken.IsCancellationRequested, Is.True);

            await AwaitWithTimeoutAsync(firstStop);
            request.Complete(CreateSubscriptionResponse(request, 0));
            Assert.That(await IsCanceledAsync(startTask), Is.True);
            Assert.That(client.PushSubscriberCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator StopAsync_DuringReplacementStopsNewGeneration()
        {
            yield return AwaitTask(StopAsync_DuringReplacementStopsNewGenerationCore());
        }

        private static async Task StopAsync_DuringReplacementStopsNewGenerationCore()
        {
            var staleClient = new ControllableNetworkClient();
            var activeClient = new ControllableNetworkClient();
            var runtime = new SpectatorSessionRuntime();
            var staleStart = runtime.StartAsync(
                staleClient,
                CreateSettledPlan(26),
                () => new TrackingWorld("spectator-stale-stop"));
            var staleRequest = await staleClient.WaitForRequestAsync();

            var activeStart = runtime.StartAsync(
                activeClient,
                CreateSettledPlan(27),
                () => new TrackingWorld("spectator-active-stop"));
            var stopTask = runtime.StopAsync();

            await AwaitWithTimeoutAsync(stopTask);
            Assert.That(await IsCanceledAsync(staleStart), Is.True);
            Assert.That(await IsCanceledAsync(activeStart), Is.True);
            Assert.That(staleClient.PushSubscriberCount, Is.Zero);
            Assert.That(activeClient.PushSubscriberCount, Is.Zero);

            staleRequest.Complete(CreateSubscriptionResponse(staleRequest, 0));
        }

        [UnityTest]
        public IEnumerator SeparateRuntimes_OwnIndependentClientsAndWorlds()
        {
            yield return AwaitTask(SeparateRuntimes_OwnIndependentClientsAndWorldsCore());
        }

        private static async Task SeparateRuntimes_OwnIndependentClientsAndWorldsCore()
        {
            var firstClient = new ControllableNetworkClient();
            var secondClient = new ControllableNetworkClient();
            var firstWorld = new TrackingWorld("spectator-one");
            var secondWorld = new TrackingWorld("spectator-two");
            var first = new SpectatorSessionRuntime();
            var second = new SpectatorSessionRuntime();

            var firstTask = first.StartAsync(firstClient, CreateSettledPlan(25), () => firstWorld);
            var secondTask = second.StartAsync(secondClient, CreateSettledPlan(26), () => secondWorld);
            var firstRequest = await firstClient.WaitForRequestAsync();
            var secondRequest = await secondClient.WaitForRequestAsync();
            firstRequest.Complete(CreateSubscriptionResponse(firstRequest, 0));
            secondRequest.Complete(CreateSubscriptionResponse(secondRequest, 0));
            await AwaitWithTimeoutAsync(Task.WhenAll(firstTask, secondTask));

            first.Stop();

            Assert.That(firstWorld.DisposeCount, Is.EqualTo(1));
            Assert.That(firstClient.PushSubscriberCount, Is.Zero);
            Assert.That(secondWorld.DisposeCount, Is.Zero);
            Assert.That(secondClient.PushSubscriberCount, Is.EqualTo(1));
            Assert.That(second.World, Is.SameAs(secondWorld));

            second.Stop();
        }

        private static BattleStartPlan CreateSettledPlan(ulong roomId) => new BattleStartPlan(new BattleStartPlanOptions(
            new BattleStartPlanWorldOptions(roomId.ToString(), "moba", "spectator", "", 30, 0),
            default, default, default,
            new BattleStartPlanGatewayOptions(true, "local", 4000, roomId, "token", "dev", "local",
                false, false, "", 0, 0, "spectator-" + roomId),
            default, default, default, default, remoteSyncCapabilities: CreateSettledCapabilities()));

        private static RoomGatewayNetworkSyncCapabilities CreateSettledCapabilities()
        {
            var profile = NetworkSyncProfiles.Lockstep;
            return RoomGatewayNetworkSyncCapabilitiesConverter.FromWire(new WireNetworkSyncCapabilities
            {
                MetadataVersion = RoomGatewayNetworkSyncCapabilitiesConverter.CurrentMetadataVersion,
                ProfileName = SettledFrameProtocol.CapabilityProfile,
                MaximumSchemaVersion = 1,
                ClientPlayback = (int)profile.ClientPlayback,
                Input = (int)profile.Input,
                Snapshot = (int)profile.Snapshot,
                Interest = (int)profile.Interest,
                Recovery = (int)profile.Recovery,
                ServerValidation = (int)profile.ServerValidation,
                ReliableEvent = (int)profile.ReliableEvent
            });
        }

        private static byte[] CreateSubscriptionResponse(PendingRequest pending, int currentFrame)
        {
            var request = SettledFrameProtocol.Decode<WireSettledSubscribeRequest>(new ArraySegment<byte>(pending.Payload));
            return SettledFrameProtocol.Encode(new WireSettledSubscribeResponse
            {
                Identity = new WireSettledIdentity
                {
                    Version = request.Version, RoomId = request.RoomId, WorldId = request.WorldId,
                    BattleId = request.BattleId, RuntimeGeneration = Guid.NewGuid().ToString("N")
                },
                TickRate = 30, LastSettledFrame = currentFrame
            });
        }

        private static IEnumerator AwaitTask(Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                ExceptionDispatchInfo.Capture(task.Exception.GetBaseException()).Throw();
            }

            if (task.IsCanceled)
            {
                throw new OperationCanceledException();
            }
        }

        private static async Task AwaitWithTimeoutAsync(Task task)
        {
            var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.That(completed, Is.SameAs(task), "Timed out waiting for spectator operation.");
            await task;
        }

        private static async Task<bool> IsCanceledAsync(Task task)
        {
            try
            {
                await AwaitWithTimeoutAsync(task);
                return false;
            }
            catch (OperationCanceledException)
            {
                return true;
            }
        }

        private sealed class ControllableNetworkClient : INetworkClient
        {
            private readonly Queue<PendingRequest> _requests = new Queue<PendingRequest>();
            private readonly Queue<TaskCompletionSource<PendingRequest>> _waiters =
                new Queue<TaskCompletionSource<PendingRequest>>();
            private Action<uint, byte[]> _onServerPush;

            public bool IsConnected => true;
            public int PushSubscriberCount { get; private set; }

            public event Action OnConnected;
            public event Action<string> OnDisconnected;
            public event Action<Exception> OnError;

            public event Action<uint, byte[]> OnServerPush
            {
                add
                {
                    _onServerPush += value;
                    PushSubscriberCount++;
                }
                remove
                {
                    _onServerPush -= value;
                    PushSubscriberCount--;
                }
            }

            public void Connect(string host, int port)
            {
                OnConnected?.Invoke();
            }

            public void Disconnect()
            {
                OnDisconnected?.Invoke("test");
            }

            public Task<byte[]> SendRequestAsync(
                uint opCode,
                byte[] payload,
                CancellationToken cancellationToken = default)
            {
                var request = new PendingRequest(opCode, payload, cancellationToken);
                if (_waiters.Count > 0)
                {
                    _waiters.Dequeue().TrySetResult(request);
                }
                else
                {
                    _requests.Enqueue(request);
                }

                return request.Response.Task;
            }

            public Task SendServerPushAsync(
                uint opCode,
                byte[] payload,
                CancellationToken cancellationToken = default)
            {
                _onServerPush?.Invoke(opCode, payload);
                return Task.CompletedTask;
            }

            public async Task<PendingRequest> WaitForRequestAsync()
            {
                if (_requests.Count > 0) return _requests.Dequeue();

                var waiter = new TaskCompletionSource<PendingRequest>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _waiters.Enqueue(waiter);
                return await waiter.Task;
            }

            internal PendingRequest TakeRequest()
            {
                Assert.That(_requests.Count, Is.Positive, "Expected a spectator network request.");
                return _requests.Dequeue();
            }

            public void Dispose()
            {
                while (_waiters.Count > 0)
                {
                    _waiters.Dequeue().TrySetCanceled();
                }

                while (_requests.Count > 0)
                {
                    _requests.Dequeue().Complete(Array.Empty<byte>());
                }
            }
        }

        private sealed class PendingRequest
        {
            internal PendingRequest(uint opCode, byte[] payload, CancellationToken cancellationToken)
            {
                OpCode = opCode;
                Payload = payload;
                CancellationToken = cancellationToken;
                Response = new TaskCompletionSource<byte[]>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }

            internal uint OpCode { get; }
            internal byte[] Payload { get; }
            internal CancellationToken CancellationToken { get; }
            internal TaskCompletionSource<byte[]> Response { get; }

            internal void Complete(byte[] response)
            {
                Response.TrySetResult(response);
            }
        }

        private sealed class TrackingWorld : IWorld
        {
            internal TrackingWorld(string id, IWorldInputSink sink = null)
            {
                Id = new WorldId(id);
                Services = new EmptyWorldResolver(sink ?? new TrackingSettledSink());
            }

            public WorldId Id { get; }
            public string WorldType => "spectator-test";
            public IWorldResolver Services { get; }
            public int TickCount { get; private set; }
            public int DisposeCount { get; private set; }
            public Exception DisposeFailure { get; set; }

            public void Initialize()
            {
            }

            public void Tick(float deltaTime)
            {
                TickCount++;
            }

            public void Dispose()
            {
                DisposeCount++;
                if (DisposeFailure != null) throw DisposeFailure;
            }
        }

        private sealed class EmptyWorldResolver : IWorldResolver
        {
            private readonly IWorldInputSink _sink;

            internal EmptyWorldResolver(IWorldInputSink sink) => _sink = sink;

            public object Resolve(Type serviceType)
            {
                throw new KeyNotFoundException(serviceType.FullName);
            }

            public T Resolve<T>()
            {
                return (T)Resolve(typeof(T));
            }

            public bool TryResolve(Type serviceType, out object instance)
            {
                instance = null;
                return false;
            }

            public bool TryResolve<T>(out T instance)
            {
                if (_sink is T compatible)
                {
                    instance = compatible;
                    return true;
                }
                instance = default;
                return false;
            }
        }

        private sealed class TrackingSettledSink : IWorldSettledTimelineSink
        {
            internal bool Bound;
            internal readonly List<int> ExecutedCounts = new List<int>();
            internal readonly List<int> CommittedFrames = new List<int>();

            public void BindSettledTimeline() => Bound = true;
            public void Dispose() { }
            public IWorldSettledFrameUpdate PrepareSettledFrame(WorldSettledInputFrame frame) =>
                new FrameUpdate(() => CommittedFrames.Add(frame.Frame.Value));
            public void ExecuteSettledInputs(FrameIndex frame, IReadOnlyList<PlayerInputCommand> executedInputs) =>
                ExecutedCounts.Add(executedInputs.Count);
            public void Submit(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs) => throw new NotSupportedException();
            public void BindClientInput() => throw new NotSupportedException();
            public void SubmitPredicted(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs) => throw new NotSupportedException();
            public void SubmitReceived(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs) => throw new NotSupportedException();
            public PlayerInputCommand[] ExecutePredictedInputs(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs) =>
                throw new NotSupportedException();

            private sealed class FrameUpdate : IWorldSettledFrameUpdate
            {
                private readonly Action _commit;
                internal FrameUpdate(Action commit) => _commit = commit;
                public bool InvalidatesPrediction => false;
                public void Commit() => _commit();
            }
        }
    }
}
