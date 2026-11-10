using AbilityKit.Game.Flow;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.Core.Tests;

public sealed class PresentationCueViewRegistryTests
{
    [Fact]
    public void Ensure_active_creates_then_updates_same_handle()
    {
        var port = new FakePort();
        var registry = new PresentationCueViewRegistry<string, int, int>(port);

        Assert.True(registry.EnsureActive("cast-q", 7101));
        Assert.True(registry.TryGetHandle("cast-q", out var first));
        Assert.True(registry.EnsureActive("cast-q", 7102));
        Assert.True(registry.TryGetHandle("cast-q", out var second));

        Assert.Equal(first, second);
        Assert.Equal(1, port.CreateCalls);
        Assert.Equal(1, port.UpdateCalls);
        Assert.Equal(1, registry.CreatedCount);
        Assert.Equal(1, registry.UpdatedCount);
    }

    [Fact]
    public void Stale_handle_is_removed_and_recreated()
    {
        var port = new FakePort();
        var registry = new PresentationCueViewRegistry<string, int, int>(port);
        registry.EnsureActive("cast-q", 7101);
        registry.TryGetHandle("cast-q", out var stale);
        port.Alive.Remove(stale);

        Assert.True(registry.EnsureActive("cast-q", 7102));
        Assert.True(registry.TryGetHandle("cast-q", out var replacement));

        Assert.NotEqual(stale, replacement);
        Assert.Equal(2, port.CreateCalls);
        Assert.Equal(1, registry.StaleHandleCount);
    }

    [Fact]
    public void Failed_create_is_not_registered_and_can_be_retried()
    {
        var port = new FakePort { FailNextCreate = true };
        var registry = new PresentationCueViewRegistry<string, int, int>(port);

        Assert.False(registry.EnsureActive("cast-q", 7101));
        Assert.False(registry.TryGetHandle("cast-q", out _));
        Assert.True(registry.EnsureActive("cast-q", 7101));

        Assert.Equal(2, port.CreateCalls);
        Assert.Equal(1, registry.FailedCreateCount);
        Assert.Equal(1, registry.ActiveCount);
    }

    [Fact]
    public void Apply_executes_reconciliation_commands_only_when_accepted()
    {
        var port = new FakePort();
        var registry = new PresentationCueViewRegistry<string, int, int>(port);
        var rejectedDecision = Decision(
            accepted: false,
            PresentationCueCommandKind.EnsureActive,
            "cast-q");
        var ensureDecision = Decision(
            accepted: true,
            PresentationCueCommandKind.EnsureActive,
            "cast-q");
        var stopDecision = Decision(
            accepted: true,
            PresentationCueCommandKind.Stop,
            "cast-q");

        Assert.False(registry.Apply(in rejectedDecision));
        Assert.True(registry.Apply(in ensureDecision));
        Assert.True(registry.Apply(in stopDecision));

        Assert.Equal(1, port.CreateCalls);
        Assert.Equal(1, port.DestroyCalls);
        Assert.Equal(0, registry.ActiveCount);
    }

    [Fact]
    public void Clear_attempts_every_handle_and_retries_failed_destroy()
    {
        var port = new FakePort();
        var registry = new PresentationCueViewRegistry<string, int, int>(port);
        registry.EnsureActive("cast-q", 7101);
        registry.EnsureActive("cast-w", 7102);
        registry.TryGetHandle("cast-q", out var failedHandle);
        port.DestroyFailures.Add(failedHandle);

        var failure = Assert.Throws<AggregateException>(() => registry.Clear());

        Assert.Single(failure.InnerExceptions);
        Assert.Equal(1, registry.ActiveCount);
        Assert.True(registry.TryGetHandle("cast-q", out _));
        Assert.False(registry.TryGetHandle("cast-w", out _));

        port.DestroyFailures.Clear();
        registry.Clear();

        Assert.Equal(0, registry.ActiveCount);
        Assert.Equal(2, registry.DestroyedCount);
    }

    [Fact]
    public void Clear_forces_active_instances_and_already_unregistered_drain_tails()
    {
        var port = new DrainingPort();
        var registry = new PresentationCueViewRegistry<string, int, int>(port);
        registry.EnsureActive("tail", 1);
        registry.Stop("tail");
        registry.EmitTransient("transient", 2);
        registry.EnsureActive("active", 3);
        Assert.Equal(3, port.Alive.Count);
        registry.Clear();
        Assert.Empty(port.Alive);
        Assert.Equal(0, registry.ActiveCount);
    }

    [Fact]
    public void Reconcile_forces_removed_instances_and_tails_but_preserves_desired_instance()
    {
        var port = new DrainingPort();
        var registry = new PresentationCueViewRegistry<string, int, int>(port);
        registry.EnsureActive("tail", 1);
        registry.Stop("tail");
        registry.EnsureActive("removed", 2);
        registry.EnsureActive("desired", 3);
        registry.TryGetHandle("desired", out var desired);
        registry.ReconcileActiveSet(new[] { "desired" });
        Assert.Equal(new[] { desired }, port.Alive);
        Assert.Equal(1, registry.ActiveCount);
    }

    [Fact]
    public void Failed_tail_cleanup_can_be_retried_after_registry_is_empty()
    {
        var port = new DrainingPort();
        var registry = new PresentationCueViewRegistry<string, int, int>(port);
        registry.EnsureActive("tail", 1);
        registry.TryGetHandle("tail", out var tail);
        registry.Stop("tail");
        port.FailForce = true;
        Assert.Throws<AggregateException>(() => registry.Clear());
        Assert.Equal(0, registry.ActiveCount);
        Assert.Contains(tail, port.Alive);
        port.FailForce = false;
        registry.Clear();
        Assert.Empty(port.Alive);
    }

    private sealed class DrainingPort : IPresentationCueViewPort<string, int, int>, IPresentationCueCleanupPort<int>
    {
        private int _next;
        public HashSet<int> Alive { get; } = new();
        public bool FailForce;
        public bool IsAlive(int handle) => Alive.Contains(handle);
        public bool TryCreate(string key, in int payload, out int handle)
        {
            handle = ++_next;
            return Alive.Add(handle);
        }
        public void Update(int handle, in int payload) { }
        public void Destroy(int handle) { }
        public void DestroyImmediately(int handle)
        {
            if (FailForce) throw new InvalidOperationException("force failed");
            Alive.Remove(handle);
        }
    }

    private static PresentationCueReconciliationDecision<string, int> Decision(
        bool accepted,
        PresentationCueCommandKind command,
        string key)
    {
        return new PresentationCueReconciliationDecision<string, int>(
            accepted,
            command,
            PresentationCueReconciliationRejectReason.None,
            key,
            7101,
            duplicate: false,
            promoted: false);
    }

    private sealed class FakePort : IPresentationCueViewPort<string, int, int>
    {
        private int _nextHandle;

        public HashSet<int> Alive { get; } = new HashSet<int>();

        public HashSet<int> DestroyFailures { get; } = new HashSet<int>();

        public bool FailNextCreate { get; set; }

        public int CreateCalls { get; private set; }

        public int UpdateCalls { get; private set; }

        public int DestroyCalls { get; private set; }

        public bool IsAlive(int handle) => Alive.Contains(handle);

        public bool TryCreate(string key, in int payload, out int handle)
        {
            CreateCalls++;
            if (FailNextCreate)
            {
                FailNextCreate = false;
                handle = 0;
                return false;
            }

            handle = ++_nextHandle;
            Alive.Add(handle);
            return true;
        }

        public void Update(int handle, in int payload)
        {
            UpdateCalls++;
        }

        public void Destroy(int handle)
        {
            DestroyCalls++;
            if (DestroyFailures.Contains(handle))
            {
                throw new InvalidOperationException("destroy failed");
            }

            Alive.Remove(handle);
        }
    }
}
