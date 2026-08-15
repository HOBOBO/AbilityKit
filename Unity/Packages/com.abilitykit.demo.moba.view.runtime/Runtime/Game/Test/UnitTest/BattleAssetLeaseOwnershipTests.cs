using System;
using System.Collections;
using System.Collections.Generic;
using AbilityKit.Game.Battle.Presentation.Features.Loading;
using AbilityKit.Game.Battle.Shared.Assets;
using AbilityKit.Game.Flow;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class BattleAssetLeaseOwnerTests
    {
        [Test]
        public void Adopt_ReplacesAndDisposesPreviousLease()
        {
            var owner = new BattleAssetLeaseOwner();
            var previous = new TrackingLease(1);
            var replacement = new TrackingLease(2);

            owner.Adopt(previous);
            owner.Adopt(replacement);

            Assert.That(previous.DisposeCount, Is.EqualTo(1));
            Assert.That(owner.Lease, Is.SameAs(replacement));
            Assert.That(replacement.DisposeCount, Is.Zero);

            owner.Dispose();
            owner.Dispose();

            Assert.That(replacement.DisposeCount, Is.EqualTo(1));
            Assert.That(owner.Lease, Is.Null);
        }

        [Test]
        public void Adopt_InactiveCandidateDoesNotReplaceCurrentLease()
        {
            var owner = new BattleAssetLeaseOwner();
            var current = new TrackingLease(1);
            var inactive = new TrackingLease(2);
            inactive.Dispose();
            owner.Adopt(current);

            Assert.Throws<InvalidOperationException>(() => owner.Adopt(inactive));
            Assert.That(owner.Lease, Is.SameAs(current));
            Assert.That(current.DisposeCount, Is.Zero);

            owner.Dispose();
        }
    }

    public sealed class BattleLoadingScreenAssetLeaseTests
    {
        [Test]
        public void Completion_WhenSessionRejectsLease_DisposesCandidate()
        {
            var coordinator = new ControllableCoordinator();
            var session = new TrackingSessionPort { AdoptFailure = new InvalidOperationException("reject") };
            var feature = new BattleLoadingScreenFeature(coordinator);
            var context = CreateContext(session);
            var lease = new TrackingLease(1);

            feature.OnAttach(context);
            coordinator.Complete(true, lease);

            Assert.That(session.AdoptCount, Is.EqualTo(1));
            Assert.That(lease.DisposeCount, Is.EqualTo(1));
            Assert.That(feature.CurrentSnapshot.Completed, Is.True);
            Assert.That(feature.CurrentSnapshot.Success, Is.False);

            feature.OnDetach(context);
        }

        [Test]
        public void Completion_SuccessAdoptsOnceAndNotifiesOnTick()
        {
            var coordinator = new ControllableCoordinator();
            var session = new TrackingSessionPort();
            var feature = new BattleLoadingScreenFeature(coordinator);
            var context = CreateContext(session);
            var lease = new TrackingLease(1);

            feature.OnAttach(context);
            coordinator.Complete(true, lease);

            Assert.That(session.AdoptCount, Is.EqualTo(1));
            Assert.That(session.CompletedCount, Is.Zero);
            Assert.That(lease.DisposeCount, Is.Zero);

            feature.Tick(context, 0f);
            feature.Tick(context, 0f);

            Assert.That(session.CompletedCount, Is.EqualTo(1));
            feature.OnDetach(context);
        }

        [Test]
        public void CancelledCompletion_DoesNotNotifySession()
        {
            var coordinator = new ControllableCoordinator();
            var session = new TrackingSessionPort();
            var runtime = new BattleLoadingRuntime();

            runtime.Attach(session, coordinator);
            runtime.Start();
            runtime.Cancel();
            runtime.Tick();

            Assert.That(coordinator.CancelCount, Is.EqualTo(1));
            Assert.That(session.AdoptCount, Is.Zero);
            Assert.That(session.CompletedCount, Is.Zero);
            Assert.That(runtime.Snapshot.Completed, Is.True);
            Assert.That(runtime.Snapshot.Success, Is.False);
            runtime.Dispose();
        }

        [Test]
        public void Retry_LatePreviousCompletionReleasesLeaseWithoutAdopting()
        {
            var coordinator = new ControllableCoordinator();
            var session = new TrackingSessionPort();
            var runtime = new BattleLoadingRuntime();
            var staleLease = new TrackingLease(1);
            var activeLease = new TrackingLease(2);

            runtime.Attach(session, coordinator);
            runtime.Start();
            coordinator.Complete(false, null);
            runtime.Retry();

            coordinator.CompleteAt(0, true, staleLease);
            coordinator.CompleteAt(1, true, activeLease);
            runtime.Tick();

            Assert.That(staleLease.DisposeCount, Is.EqualTo(1));
            Assert.That(activeLease.DisposeCount, Is.Zero);
            Assert.That(session.AdoptCount, Is.EqualTo(1));
            Assert.That(session.CompletedCount, Is.EqualTo(1));
            runtime.Dispose();
        }

        [Test]
        public void Detach_LateCompletionReleasesLeaseWithoutAdoptingOrCompletingSession()
        {
            var coordinator = new ControllableCoordinator();
            var session = new TrackingSessionPort();
            var feature = new BattleLoadingScreenFeature(coordinator);
            var context = CreateContext(session);
            var lease = new TrackingLease(1);

            feature.OnAttach(context);
            feature.OnDetach(context);
            coordinator.Complete(true, lease);

            Assert.That(coordinator.CancelCount, Is.EqualTo(1));
            Assert.That(session.AdoptCount, Is.Zero);
            Assert.That(session.CompletedCount, Is.Zero);
            Assert.That(lease.DisposeCount, Is.EqualTo(1));
        }

        private static GamePhaseContext CreateContext(IBattleAssetLoadSessionPort session)
        {
            var features = new TestFeatureStore();
            features.Set(session);
            return new GamePhaseContext(new TestGameHost(), features, null);
        }
    }

    internal sealed class TrackingLease : IBattleAssetLease
    {
        internal TrackingLease(long launchGeneration)
        {
            LaunchGeneration = launchGeneration;
            IsActive = true;
        }

        public bool IsActive { get; private set; }
        public long LaunchGeneration { get; }
        internal int DisposeCount { get; private set; }

        public void Dispose()
        {
            if (!IsActive) return;
            IsActive = false;
            DisposeCount++;
        }
    }

    internal sealed class TrackingSessionPort : IBattleAssetLoadSessionPort
    {
        public BattleStartPlan Plan => default;
        public IBattleAssetLookup AssetLookup => null;
        internal Exception AdoptFailure { get; set; }
        internal int AdoptCount { get; private set; }
        internal int CompletedCount { get; private set; }

        public void AdoptAssetLease(IBattleAssetLease lease)
        {
            AdoptCount++;
            if (AdoptFailure != null) throw AdoptFailure;
        }

        public void NotifyAssetsLoadCompleted()
        {
            CompletedCount++;
        }
    }

    internal sealed class ControllableCoordinator : IBattleAssetLoadCoordinator
    {
        private readonly List<Action<bool>> _completions = new List<Action<bool>>();
        private IBattleAssetLease _lease;

        public bool IsLoading { get; private set; }
        public BattleAssetLoadResult LastResult { get; private set; }
        internal int CancelCount { get; private set; }

        public void StartLoading(Action<bool> onComplete)
        {
            _completions.Add(onComplete);
            IsLoading = true;
        }

        public void Cancel()
        {
            CancelCount++;
            IsLoading = false;
            if (_completions.Count > 0)
            {
                _completions[_completions.Count - 1]?.Invoke(false);
            }
        }

        public IBattleAssetLease TakeLease()
        {
            var lease = _lease;
            _lease = null;
            return lease;
        }

        public void ReleaseLease()
        {
            TakeLease()?.Dispose();
        }

        internal void Complete(bool success, IBattleAssetLease lease)
        {
            CompleteAt(_completions.Count - 1, success, lease);
        }

        internal void CompleteAt(int index, bool success, IBattleAssetLease lease)
        {
            _lease = lease;
            IsLoading = false;
            LastResult = new BattleAssetLoadResult(
                success,
                1,
                1,
                "test",
                Array.Empty<BattleAssetLoadError>(),
                lease);
            _completions[index]?.Invoke(success);
        }
    }

    internal sealed class TestFeatureStore : IGameFeatureStore
    {
        private readonly Dictionary<Type, object> _components = new Dictionary<Type, object>();

        public bool TryGet<T>(out T component) where T : class
        {
            if (_components.TryGetValue(typeof(T), out var value))
            {
                component = (T)value;
                return true;
            }

            component = null;
            return false;
        }

        public void Set<T>(T component) where T : class => _components[typeof(T)] = component;
        public void Remove<T>() where T : class => _components.Remove(typeof(T));
        public void Remove(Type componentType) => _components.Remove(componentType);
    }

    internal sealed class TestGameHost : IGameHost
    {
        private readonly IFlowCommandSink _flowCommands = new TestFlowCommandSink();

        public bool DebugEnabled => false;

        public T Get<T>() where T : class
        {
            if (_flowCommands is T result) return result;
            throw new InvalidOperationException("Unsupported test service: " + typeof(T).FullName);
        }

        public bool TryGet<T>(out T component) where T : class
        {
            component = _flowCommands as T;
            return component != null;
        }

        public void RunCoroutine(IEnumerator coroutine)
        {
        }
    }

    internal sealed class TestFlowCommandSink : IFlowCommandSink
    {
        public MobaRootState CurrentRootPhase => default;
        public MobaBattleState CurrentBattlePhase => default;
        public void RequestEnterBattle() { }
        public void RequestBattleEnd() { }
        public void RequestReturnLobby() { }
    }
}
