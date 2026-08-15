using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Recording.FrameRecord;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Flow.Battle.Replay;
using AbilityKit.Network.Battle;
using AbilityKit.Protocol.Moba.Generated.GatewayFrameSync;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class SpectatorSessionRuntimeTests
    {
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

            var startTask = runtime.StartAsync(client, 17UL, () => world);
            var request = await client.WaitForRequestAsync();

            Assert.That(request.OpCode, Is.EqualTo(OpCodes.SpectatorSubscribe));
            Assert.That(runtime.IsStarting, Is.True);
            Assert.That(runtime.Driver, Is.Null);
            Assert.That(client.PushSubscriberCount, Is.EqualTo(1));

            request.Complete(CreateMetricsResponse(worldId: 71UL, currentFrame: 0));
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

            var startTask = runtime.StartAsync(client, 18UL, () => world);
            var request = await client.WaitForRequestAsync();

            runtime.Stop();

            Assert.That(request.CancellationToken.IsCancellationRequested, Is.True);
            Assert.That(client.PushSubscriberCount, Is.Zero);
            request.Complete(CreateMetricsResponse(worldId: 72UL, currentFrame: 0));

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

            var staleTask = runtime.StartAsync(staleClient, 19UL, () => staleWorld);
            var staleRequest = await staleClient.WaitForRequestAsync();
            var activeTask = runtime.StartAsync(activeClient, 20UL, () => activeWorld);
            var activeRequest = await activeClient.WaitForRequestAsync();

            activeRequest.Complete(CreateMetricsResponse(worldId: 73UL, currentFrame: 0));
            await AwaitWithTimeoutAsync(activeTask);
            staleRequest.Complete(CreateMetricsResponse(worldId: 74UL, currentFrame: 0));

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

            var startTask = runtime.StartAsync(client, 21UL, () => world);
            var request = await client.WaitForRequestAsync();
            request.Complete(CreateMetricsResponse(worldId: 75UL, currentFrame: 0));
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

            var startTask = runtime.StartAsync(client, 22UL, () => world);
            var subscribeRequest = await client.WaitForRequestAsync();
            subscribeRequest.Complete(CreateMetricsResponse(worldId: 76UL, currentFrame: 12));
            var catchUpRequest = await client.WaitForRequestAsync();

            Assert.That(catchUpRequest.OpCode, Is.EqualTo(OpCodes.CatchUpRequest));
            Assert.That(runtime.Driver, Is.Null);
            Assert.That(world.DisposeCount, Is.Zero);

            runtime.Stop();
            catchUpRequest.Complete(Array.Empty<byte>());

            Assert.That(catchUpRequest.CancellationToken.IsCancellationRequested, Is.True);
            Assert.That(await IsCanceledAsync(startTask), Is.True);
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

            var startTask = runtime.StartAsync(client, 23UL, () => throw failure);
            var request = await client.WaitForRequestAsync();
            request.Complete(CreateMetricsResponse(worldId: 77UL, currentFrame: 0));

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

            var startTask = runtime.StartAsync(client, 24UL, () => world);
            var request = await client.WaitForRequestAsync();
            request.Complete(CreateMetricsResponse(worldId: 78UL, currentFrame: 0));
            await AwaitWithTimeoutAsync(startTask);

            runtime.Stop();
            runtime.Stop();
            runtime.Dispose();

            Assert.That(world.DisposeCount, Is.EqualTo(1));
            Assert.That(runtime.Driver, Is.Null);
            Assert.That(client.PushSubscriberCount, Is.Zero);
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

            var firstTask = first.StartAsync(firstClient, 25UL, () => firstWorld);
            var secondTask = second.StartAsync(secondClient, 26UL, () => secondWorld);
            var firstRequest = await firstClient.WaitForRequestAsync();
            var secondRequest = await secondClient.WaitForRequestAsync();
            firstRequest.Complete(CreateMetricsResponse(worldId: 79UL, currentFrame: 0));
            secondRequest.Complete(CreateMetricsResponse(worldId: 80UL, currentFrame: 0));
            await AwaitWithTimeoutAsync(Task.WhenAll(firstTask, secondTask));

            first.Stop();

            Assert.That(firstWorld.DisposeCount, Is.EqualTo(1));
            Assert.That(firstClient.PushSubscriberCount, Is.Zero);
            Assert.That(secondWorld.DisposeCount, Is.Zero);
            Assert.That(secondClient.PushSubscriberCount, Is.EqualTo(1));
            Assert.That(second.World, Is.SameAs(secondWorld));

            second.Stop();
        }

        private static byte[] CreateMetricsResponse(ulong worldId, int currentFrame)
        {
            var metrics = new WireFrameSyncMetrics(
                roomId: 1UL,
                worldId: worldId,
                battleId: "spectator-test",
                currentFrame: currentFrame,
                tickRate: 30,
                observerCount: 1,
                avgTickDeltaMs: 0d,
                lastTickDeltaMs: 0d,
                effectiveHz: 30d,
                totalFramesReceived: 0,
                catchUpHistoryFrames: 0,
                recordingFrameCount: 0,
                uptimeSeconds: 0L);
            var payload = WireCustomBinary.Serialize(metrics);
            if (payload.Array == null) return Array.Empty<byte>();

            var result = new byte[payload.Count];
            Array.Copy(payload.Array, payload.Offset, result, 0, payload.Count);
            return result;
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
            internal TrackingWorld(string id)
            {
                Id = new WorldId(id);
            }

            public WorldId Id { get; }
            public string WorldType => "spectator-test";
            public IWorldResolver Services { get; } = new EmptyWorldResolver();
            public int DisposeCount { get; private set; }
            public Exception DisposeFailure { get; set; }

            public void Initialize()
            {
            }

            public void Tick(float deltaTime)
            {
            }

            public void Dispose()
            {
                DisposeCount++;
                if (DisposeFailure != null) throw DisposeFailure;
            }
        }

        private sealed class EmptyWorldResolver : IWorldResolver
        {
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
                instance = default;
                return false;
            }
        }
    }
}
