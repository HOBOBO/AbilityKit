using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using AbilityKit.Game.Flow;

namespace AbilityKit.Game.UnitTests
{
    public sealed class PresentationRuntimeContractsTests
    {
        [Test]
        public void Registry_ReusesUpdatesAndStopsLogicalInstance()
        {
            var port = new TestPort();
            var registry = new PresentationInstanceRegistry<string, int, int>(port);

            Assert.IsTrue(registry.EnsureActive("a", 1));
            Assert.IsTrue(registry.EnsureActive("a", 2));
            Assert.AreEqual(1, registry.CreatedCount);
            Assert.AreEqual(1, registry.UpdatedCount);
            Assert.IsTrue(registry.Stop("a"));
            Assert.IsFalse(registry.Stop("a"));
            Assert.AreEqual(1, registry.DestroyedCount);
        }

        [Test]
        public async Task Handle_StopIsIdempotentAndReleasesLease()
        {
            var stopped = 0;
            var released = 0;
            var handle = new PresentationHandle<int>(
                7,
                stop: _ => { stopped++; return Task.CompletedTask; },
                lease: new PresentationResourceLease(release: () => released++));

            await handle.StopAsync();
            await handle.StopAsync();
            Assert.AreEqual(1, stopped);
            Assert.AreEqual(1, released);
            Assert.AreEqual(PresentationHandleState.Stopped, handle.State);
        }

        [Test]
        public void CompositeTemplateCopiesChildren()
        {
            var source = new List<PresentationRequest>
            {
                new PresentationRequest("vfx", 1, PresentationVector3.Zero),
            };
            var template = new PresentationCompositeTemplate("combo", source);
            source.Clear();
            Assert.AreEqual(1, template.Children.Count);
            Assert.AreEqual("combo", template.Id);
        }

        [Test]
        public void EffectPolicyLedger_EnforcesUniqueIntervalAndReleases()
        {
            var ledger = new PresentationEffectPolicyLedger<string>();
            var policy = new PresentationEffectPolicy("group", unique: true, minIntervalMs: 100);
            Assert.IsTrue(ledger.TryAcquire("fx", in policy, 1000));
            Assert.IsFalse(ledger.TryAcquire("fx", in policy, 1100));
            ledger.Release("fx");
            Assert.IsFalse(ledger.TryAcquire("fx", in policy, 1050));
            Assert.IsTrue(ledger.TryAcquire("fx", in policy, 1200));
        }

        [Test]
        public void PlaybackLifecycle_CompletesRepeatedPlaysAndInterval()
        {
            var lifecycle = new PresentationPlaybackLifecycle(
                new PresentationPlaybackSpec(playCount: 2, intervalMs: 100), 0.2f);
            Assert.AreEqual(PresentationPlaybackPhase.Playing, lifecycle.Snapshot.Phase);
            Assert.IsFalse(lifecycle.Tick(0.2f));
            Assert.AreEqual(PresentationPlaybackPhase.Interval, lifecycle.Snapshot.Phase);
            Assert.IsFalse(lifecycle.Tick(0.1f));
            Assert.IsTrue(lifecycle.Tick(0.2f));
            Assert.AreEqual(PresentationPlaybackPhase.Completed, lifecycle.Snapshot.Phase);
        }

        [Test]
        public async Task Session_ReplacesLogicalKeyAndStopsOnClear()
        {
            var backend = new TestBackend();
            var session = new PresentationSession<int>(backend);
            var first = new PresentationRequest("a", 1, PresentationVector3.Zero);
            var second = new PresentationRequest("a", 2, PresentationVector3.Zero);

            Assert.IsNotNull(await session.PlayAsync(first));
            Assert.IsNotNull(await session.PlayAsync(second));
            Assert.AreEqual(1, backend.StopCount);
            Assert.AreEqual(1, session.ActiveCount);

            await session.ClearAsync();
            Assert.AreEqual(2, backend.StopCount);
            Assert.AreEqual(0, session.ActiveCount);
        }

        [Test]
        public async Task Session_ClearingWhilePlayIsPendingStopsLateHandle()
        {
            var backend = new TestBackend { BlockPlay = true };
            var session = new PresentationSession<int>(backend);
            var play = session.PlayAsync(new PresentationRequest("late", 1, PresentationVector3.Zero));
            await backend.PlayStarted.Task;

            await session.ClearAsync();
            backend.ReleasePlay();

            Assert.IsNull(await play);
            Assert.AreEqual(1, backend.StopCount);
            Assert.AreEqual(0, session.ActiveCount);
        }

        [Test]
        public async Task Session_RemovesAndStopsHandleWhenReadyFails()
        {
            var backend = new TestBackend { ReadyFailure = true };
            var session = new PresentationSession<int>(backend);
            var handle = await session.PlayAsync(new PresentationRequest("failed", 1, PresentationVector3.Zero));

            Assert.IsNotNull(handle);
            for (var i = 0; i < 10 && backend.StopCount == 0; i++) await Task.Delay(1);
            Assert.AreEqual(0, session.ActiveCount);
            Assert.AreEqual(1, backend.StopCount);
        }

        [Test]
        public void CompositePlayFailureRollsBackCreatedChildren()
        {
            var backend = new TestBackend { FailOnPlay = 2 };
            var session = new PresentationSession<int>(backend);
            var template = new PresentationCompositeTemplate("combo", new[]
            {
                new PresentationRequest("one", 1, PresentationVector3.Zero),
                new PresentationRequest("two", 2, PresentationVector3.Zero),
            });

            Assert.Throws<InvalidOperationException>(() =>
                session.PlayCompositeAsync(template).GetAwaiter().GetResult());
            Assert.AreEqual(1, backend.StopCount);
        }

        private sealed class TestPort : IPresentationCueViewPort<string, int, int>
        {
            private int _next;
            private readonly HashSet<int> _alive = new HashSet<int>();
            public bool IsAlive(int handle) => _alive.Contains(handle);
            public bool TryCreate(string key, in int payload, out int handle)
            {
                handle = ++_next;
                _alive.Add(handle);
                return true;
            }
            public void Update(int handle, in int payload) { }
            public void Destroy(int handle) => _alive.Remove(handle);
        }

        private sealed class TestBackend : IPresentationBackend<int>
        {
            private int _next;
            private TaskCompletionSource<bool> _releasePlay;
            public bool BlockPlay { get; set; }
            public bool ReadyFailure { get; set; }
            public int FailOnPlay { get; set; }
            public TaskCompletionSource<bool> PlayStarted { get; } =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public int StopCount { get; private set; }
            public bool IsAlive(int handle) => handle > 0;
            public Task<PresentationHandle<int>> PlayAsync(
                in PresentationRequest request,
                System.Threading.CancellationToken cancellationToken = default)
            {
                if (FailOnPlay > 0 && _next + 1 == FailOnPlay)
                    return Task.FromResult<PresentationHandle<int>>(null);

                if (BlockPlay)
                {
                    _releasePlay = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    PlayStarted.TrySetResult(true);
                    return WaitForPlayAsync(cancellationToken);
                }

                var id = ++_next;
                return Task.FromResult(CreateHandle(id));
            }

            private async Task<PresentationHandle<int>> WaitForPlayAsync(CancellationToken cancellationToken)
            {
                await _releasePlay.Task;
                cancellationToken.ThrowIfCancellationRequested();
                return CreateHandle(++_next);
            }

            private PresentationHandle<int> CreateHandle(int id)
            {
                var ready = ReadyFailure
                    ? Task.FromException(new InvalidOperationException("ready failed"))
                    : Task.CompletedTask;
                return new PresentationHandle<int>(id, ready: ready, stop: _ =>
                {
                    StopCount++;
                    return Task.CompletedTask;
                });
            }

            public void ReleasePlay()
            {
                _releasePlay?.TrySetResult(true);
            }
            public void Update(int handle, in PresentationRequest request) { }
            public Task StopAsync(int handle) => Task.CompletedTask;
        }
    }
}
