/*
 * 只为下一主版本前保留的 Core 兼容面建立行为基线，不作为新增生产使用方。
 */
#pragma warning disable CS0618
using AbilityKit.Core.Continuous;
using Xunit;

namespace AbilityKit.Core.Tests;

public sealed class ContinuousCompatibilityContractTests
{
    [Fact]
    public void Enum_values_remain_compatible()
    {
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, Enum.GetValues<ContinuousState>().Select(x => (int)x));
        Assert.Equal(new[] { 0, 1, 2, 3 }, Enum.GetValues<ContinuousEndReason>().Select(x => (int)x));
    }

    [Fact]
    public void Registration_preserves_order_and_does_not_deduplicate_config_ids()
    {
        var calls = 0;
        var manager = new DefaultContinuousManager(lifecycleBinders: new[] { new Binder { Registered = (_, _) => calls++ } });
        var first = Item("same");
        var second = Item("same");
        Assert.True(manager.Register(first));
        Assert.True(manager.Register(second));
        Assert.True(manager.Register(first));
        Assert.Equal(2, calls);
        Assert.Equal(new[] { first, second }, manager.GetAllContinuous());
        Assert.Equal(new[] { first, second }, manager.GetOwnerContinuous(1));
        manager.Unregister(first);
        manager.Register(first);
        Assert.Equal(new[] { second, first }, manager.GetAllContinuous());
    }

    [Fact]
    public void Registration_uses_default_equality_instead_of_reference_identity()
    {
        var manager = new DefaultContinuousManager();
        var first = Item("a");
        var equivalent = Item("b");
        first.EqualityKey = equivalent.EqualityKey = "equal";
        Assert.True(manager.Register(first));
        Assert.True(manager.Register(equivalent));
        Assert.Same(first, Assert.Single(manager.GetAllContinuous()));
        Assert.Equal(1, manager.TotalCount);
        manager.Unregister(equivalent);
        Assert.Empty(manager.GetAllContinuous());
    }

    [Fact]
    public void Invalid_registration_and_activation_set_their_distinct_reasons()
    {
        var manager = new DefaultContinuousManager();
        Assert.False(manager.Register(null!));
        Assert.Equal("Continuous or config is null", manager.LastRejectReason);
        Assert.False(manager.Register(new Continuous(null!)));
        Assert.False(manager.TryActivate(null!));
        Assert.Equal("Continuous is null", manager.LastRejectReason);
        Assert.True(manager.Register(Item("valid")));
        Assert.Null(manager.LastRejectReason);
    }

    [Fact]
    public void Admission_short_circuits_and_duplicate_registration_skips_policies()
    {
        var calls = new List<string>();
        var first = new Policy { RegisterCheck = () => { calls.Add("first"); return false; }, Reason = "rejected" };
        var second = new Policy { RegisterCheck = () => { calls.Add("second"); return true; } };
        var manager = new DefaultContinuousManager(new[] { first, second });
        var item = Item("a");
        Assert.False(manager.Register(item));
        Assert.Equal("rejected", manager.LastRejectReason);
        Assert.Equal(new[] { "first" }, calls);
        first.RegisterCheck = () => true;
        Assert.True(manager.Register(item));
        calls.Clear();
        first.RegisterCheck = () => throw new InvalidOperationException();
        Assert.True(manager.Register(item));
        Assert.Empty(calls);
        Assert.Null(manager.LastRejectReason);
    }

    [Fact]
    public void Activation_registers_before_rejecting_terminated_objects()
    {
        var manager = new DefaultContinuousManager();
        var item = Item("ended");
        item.End(ContinuousEndReason.Completed);
        Assert.False(manager.TryActivate(item));
        Assert.Equal(1, manager.TotalCount);
        Assert.Equal(0, item.ActivateCalls);
        Assert.Equal("Continuous is null or terminated", manager.LastRejectReason);
    }

    [Fact]
    public void Successful_resume_keeps_previous_reject_reason_but_activation_clears_it()
    {
        var policy = new Policy { ActivateCheck = () => false, Reason = "blocked" };
        var manager = new DefaultContinuousManager(new[] { policy });
        var item = Item("a");
        item.Activate();
        item.Pause();
        Assert.False(manager.TryResume(item));
        Assert.Equal("blocked", manager.LastRejectReason);
        policy.ActivateCheck = () => true;
        Assert.True(manager.TryResume(item));
        Assert.Equal("blocked", manager.LastRejectReason);
        Assert.True(manager.TryActivate(item));
        Assert.Null(manager.LastRejectReason);
    }

    [Fact]
    public void Registration_binders_use_a_snapshot_while_later_nodes_use_current_list()
    {
        var calls = new List<string>();
        var manager = new DefaultContinuousManager();
        var second = new Binder { Registered = (_, _) => calls.Add("second-register"), Activated = (_, _) => calls.Add("second-active") };
        var added = new Binder { Registered = (_, _) => calls.Add("added-register"), Activated = (_, _) => calls.Add("added-active") };
        manager.AddLifecycleBinder(new Binder
        {
            Registered = (_, _) => { calls.Add("first-register"); manager.RemoveLifecycleBinder(second); manager.AddLifecycleBinder(added); }
        });
        manager.AddLifecycleBinder(second);
        Assert.True(manager.TryActivate(Item("a")));
        Assert.Equal(new[] { "first-register", "second-register", "added-active" }, calls);
    }

    [Fact]
    public void Non_registration_callbacks_iterate_live_binder_list_without_a_snapshot()
    {
        var calls = new List<string>();
        var manager = new DefaultContinuousManager();
        Binder? first = null;
        first = new Binder { Activated = (_, _) => { calls.Add("first"); manager.RemoveLifecycleBinder(first!); } };
        manager.AddLifecycleBinder(first);
        manager.AddLifecycleBinder(new Binder { Activated = (_, _) => calls.Add("second") });
        Assert.True(manager.TryActivate(Item("a")));
        Assert.Equal(new[] { "first" }, calls);
    }

    [Fact]
    public void Reentrant_unregistration_during_registration_has_no_transaction_barrier()
    {
        var calls = new List<string>();
        var manager = new DefaultContinuousManager(lifecycleBinders: new[]
        {
            new Binder { Registered = (c, m) => { calls.Add("binder"); m.Unregister(c); } }
        });
        manager.OnRegistered += _ => calls.Add("registered-event");
        manager.OnUnregistered += (_, _) => calls.Add("unregistered-event");
        Assert.True(manager.Register(Item("a")));
        Assert.Equal(0, manager.TotalCount);
        Assert.Equal(new[] { "binder", "unregistered-event", "registered-event" }, calls);
    }

    [Fact]
    public void Registration_failure_compensates_attempted_binders_in_reverse_and_preserves_original_exception()
    {
        var calls = new List<string>();
        var original = new InvalidOperationException("register");
        var manager = new DefaultContinuousManager();
        manager.AddLifecycleBinder(new Binder
        {
            Registered = (_, _) => calls.Add("first-register"),
            Unregistered = (_, reason, m) => { Assert.Equal(ContinuousEndReason.CleanedUp, reason); Assert.Equal(0, m.TotalCount); calls.Add("first-undo"); }
        });
        manager.AddLifecycleBinder(new Binder
        {
            Registered = (_, _) => { calls.Add("second-register"); throw original; },
            Unregistered = (_, _, _) => { calls.Add("second-undo"); throw new Exception("compensation"); }
        });
        manager.AddLifecycleBinder(new Binder { Registered = (_, _) => calls.Add("unreached"), Unregistered = (_, _, _) => calls.Add("unexpected") });
        var item = Item("a");
        Assert.Same(original, Assert.Throws<InvalidOperationException>(() => manager.Register(item)));
        Assert.Equal(new[] { "first-register", "second-register", "second-undo", "first-undo" }, calls);
        Assert.Empty(manager.GetAllContinuous());
        Assert.Empty(manager.GetOwnerContinuous(1));
        item.End(ContinuousEndReason.Completed);
        Assert.Equal(4, calls.Count);
    }

    [Fact]
    public void Public_registration_event_failure_does_not_roll_back_registration()
    {
        var manager = new DefaultContinuousManager();
        var original = new InvalidOperationException("event");
        manager.OnRegistered += _ => throw original;
        var item = Item("a");
        Assert.Same(original, Assert.Throws<InvalidOperationException>(() => manager.Register(item)));
        Assert.Same(item, Assert.Single(manager.GetAllContinuous()));
        Assert.True(manager.Register(item));
        item.End(ContinuousEndReason.SourceEnded);
        Assert.Equal(0, manager.TotalCount);
    }

    [Fact]
    public void Constructor_keeps_duplicates_but_add_methods_deduplicate_and_remove_first()
    {
        var registrations = 0;
        var checks = 0;
        var binder = new Binder { Registered = (_, _) => registrations++ };
        var policy = new Policy { RegisterCheck = () => { checks++; return true; } };
        var manager = new DefaultContinuousManager(new[] { policy, policy }, new[] { binder, binder });
        manager.AddLifecycleBinder(binder);
        manager.AddAdmissionPolicy(policy);
        manager.Register(Item("a"));
        Assert.Equal(2, registrations);
        Assert.Equal(2, checks);
        Assert.True(manager.RemoveLifecycleBinder(binder));
        Assert.True(manager.RemoveAdmissionPolicy(policy));
        manager.Register(Item("b"));
        Assert.Equal(3, registrations);
        Assert.Equal(3, checks);
        Assert.False(manager.RemoveLifecycleBinder(null!));
        Assert.False(manager.RemoveAdmissionPolicy(null!));
    }

    [Fact]
    public void Activation_binder_failure_keeps_legacy_registration_and_active_state()
    {
        var original = new InvalidOperationException("activate");
        var manager = new DefaultContinuousManager(lifecycleBinders: new[] { new Binder { Activated = (_, _) => throw original } });
        var publicEvent = false;
        manager.OnActivated += _ => publicEvent = true;
        var item = Item("a");
        Assert.Same(original, Assert.Throws<InvalidOperationException>(() => manager.TryActivate(item)));
        Assert.True(item.IsActive);
        Assert.False(item.IsTerminated);
        Assert.Equal(1, manager.TotalCount);
        Assert.Equal(1, manager.ActiveCount);
        Assert.False(publicEvent);
    }

    [Fact]
    public void Repeated_activation_calls_object_again_without_repeating_activation_notifications()
    {
        var calls = new List<string>();
        var manager = new DefaultContinuousManager(lifecycleBinders: new[] { new Binder { Activated = (_, _) => calls.Add("binder") } });
        manager.OnActivated += _ => calls.Add("event");
        var item = Item("a");
        Assert.True(manager.TryActivate(item));
        Assert.True(manager.TryActivate(item));
        Assert.Equal(2, item.ActivateCalls);
        Assert.Equal(new[] { "binder", "event" }, calls);
    }

    [Fact]
    public void Activation_that_does_not_enter_active_state_retains_only_registration()
    {
        var manager = new DefaultContinuousManager();
        var item = Item("a");
        item.AcceptActivate = false;
        Assert.False(manager.TryActivate(item));
        Assert.Equal(1, manager.TotalCount);
        Assert.Equal(0, manager.ActiveCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Pause_and_resume_require_the_object_to_commit_its_new_state(bool pause)
    {
        var notifications = 0;
        var manager = new DefaultContinuousManager(lifecycleBinders: new[] { new Binder { Paused = (_, _) => notifications++, Resumed = (_, _) => notifications++ } });
        var item = Item("a");
        manager.TryActivate(item);
        if (pause)
        {
            item.AcceptPause = false;
            Assert.False(manager.TryPause(item));
            Assert.Equal(1, manager.ActiveCount);
        }
        else
        {
            manager.TryPause(item);
            notifications = 0;
            item.AcceptResume = false;
            Assert.False(manager.TryResume(item));
            Assert.Equal(0, manager.ActiveCount);
        }
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void Unregistered_pause_and_resume_can_create_activity_without_registration()
    {
        var manager = new DefaultContinuousManager();
        var item = Item("a");
        item.Activate();
        Assert.True(manager.TryPause(item));
        Assert.True(manager.TryResume(item));
        Assert.Equal(0, manager.TotalCount);
        Assert.Equal(1, manager.ActiveCount);
        Assert.Empty(manager.GetAllActiveContinuous());
        Assert.Empty(manager.GetOwnerActiveContinuous(1));
        manager.Unregister(item);
        Assert.Equal(1, manager.ActiveCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Pause_and_resume_binder_failures_keep_committed_state(bool pause)
    {
        var original = new InvalidOperationException("callback");
        var manager = new DefaultContinuousManager();
        var item = Item("a");
        manager.TryActivate(item);
        if (!pause) manager.TryPause(item);
        manager.AddLifecycleBinder(new Binder { Paused = (_, _) => throw original, Resumed = (_, _) => throw original });
        Assert.Same(original, Assert.Throws<InvalidOperationException>(() => pause ? manager.TryPause(item) : manager.TryResume(item)));
        Assert.Equal(pause, item.IsPaused);
        Assert.Equal(pause ? 0 : 1, manager.ActiveCount);
        Assert.Equal(1, manager.TotalCount);
    }

    [Fact]
    public void Missing_end_event_leaves_registration_and_activity_indexes_untouched()
    {
        var manager = new DefaultContinuousManager();
        var item = Item("a");
        item.EmitEndEvent = false;
        manager.TryActivate(item);
        Assert.True(manager.TryEnd(item));
        Assert.Equal(1, manager.TotalCount);
        Assert.Equal(1, manager.ActiveCount);
        Assert.Empty(manager.GetOwnerActiveContinuous(1));
        Assert.False(manager.TryEnd(item));
    }

    [Fact]
    public void End_forwards_event_reason_and_finally_unregisters_even_if_ended_binder_throws()
    {
        var original = new InvalidOperationException("ended");
        var calls = new List<string>();
        var manager = new HookManager();
        manager.AddLifecycleBinder(new Binder
        {
            Ended = (_, reason, m) =>
            {
                Assert.Equal(ContinuousEndReason.SourceEnded, reason);
                Assert.Equal(1, m.TotalCount);
                Assert.Equal(0, m.ActiveCount);
                calls.Add("ended");
                throw original;
            },
            Unregistered = (_, reason, m) => { Assert.Equal(ContinuousEndReason.SourceEnded, reason); Assert.Equal(0, m.TotalCount); calls.Add("unregistered"); }
        });
        manager.OnUnregistered += (_, reason) => { Assert.Equal(ContinuousEndReason.SourceEnded, reason); calls.Add("event"); };
        var item = Item("a");
        manager.TryActivate(item);
        Assert.Same(original, Assert.Throws<InvalidOperationException>(() => manager.TryEnd(item, ContinuousEndReason.SourceEnded)));
        Assert.Equal(new[] { "ended", "unregistered", "event" }, calls);
        Assert.Equal(0, manager.TotalCount);
        Assert.False(manager.HookCalled);
    }

    [Fact]
    public void Unregistration_failure_in_finally_replaces_earlier_ended_failure()
    {
        var earlier = new InvalidOperationException("ended");
        var later = new ArgumentException("unregistered");
        var manager = new DefaultContinuousManager(lifecycleBinders: new[] { new Binder { Ended = (_, _, _) => throw earlier, Unregistered = (_, _, _) => throw later } });
        var item = Item("a");
        manager.TryActivate(item);
        Assert.Same(later, Assert.Throws<ArgumentException>(() => manager.TryEnd(item)));
        Assert.Equal(0, manager.TotalCount);
        Assert.Equal(0, manager.ActiveCount);
    }

    [Fact]
    public void Unregistration_failure_keeps_removal_and_stops_later_notifications()
    {
        var original = new InvalidOperationException("unregister");
        var laterCalls = 0;
        var manager = new DefaultContinuousManager(lifecycleBinders: new[]
        {
            new Binder { Unregistered = (_, _, _) => throw original },
            new Binder { Unregistered = (_, _, _) => laterCalls++ }
        });
        manager.OnUnregistered += (_, _) => laterCalls++;
        var item = Item("a");
        manager.TryActivate(item);
        Assert.Same(original, Assert.Throws<InvalidOperationException>(() => manager.Unregister(item)));
        Assert.True(item.IsActive);
        Assert.Equal(0, manager.TotalCount);
        Assert.Equal(0, laterCalls);
        item.End(ContinuousEndReason.Completed);
        Assert.Equal(0, laterCalls);
    }

    [Fact]
    public void Unregister_only_detaches_and_does_not_end_object()
    {
        var manager = new DefaultContinuousManager();
        var item = Item("a");
        var ends = 0;
        item.OnEnded += (_, _) => ends++;
        manager.TryActivate(item);
        manager.Unregister(item);
        manager.Unregister(item);
        Assert.True(item.IsActive);
        Assert.False(item.IsTerminated);
        Assert.Equal(0, ends);
        Assert.Empty(manager.GetOwnerContinuous(1));
    }

    [Fact]
    public void Owner_view_tracks_current_list_but_snapshots_and_recreated_owner_lists_do_not()
    {
        var manager = new DefaultContinuousManager();
        var absent = manager.GetOwnerContinuous(1);
        var first = Item("first");
        var second = Item("second");
        manager.TryActivate(first);
        var view = manager.GetOwnerContinuous(1);
        var allSnapshot = manager.GetAllContinuous();
        var activeSnapshot = manager.GetOwnerActiveContinuous(1);
        manager.TryActivate(second);
        Assert.Equal(2, view.Count);
        Assert.Single(allSnapshot);
        Assert.Single(activeSnapshot);
        Assert.Empty(absent);
        manager.Unregister(first);
        manager.Unregister(second);
        Assert.Empty(view);
        manager.Register(Item("new-list"));
        Assert.Empty(view);
        Assert.Single(manager.GetOwnerContinuous(1));
    }

    [Fact]
    public void Direct_state_changes_expose_difference_between_live_owner_queries_and_activity_index()
    {
        var manager = new DefaultContinuousManager();
        var first = Item("first");
        manager.TryActivate(first);
        first.Pause();
        Assert.Equal(1, manager.ActiveCount);
        Assert.Same(first, Assert.Single(manager.GetAllActiveContinuous()));
        Assert.Empty(manager.GetOwnerActiveContinuous(1));
        var second = Item("second");
        manager.Register(second);
        second.Activate();
        Assert.Same(second, Assert.Single(manager.GetOwnerActiveContinuous(1)));
        Assert.Same(first, Assert.Single(manager.GetAllActiveContinuous()));
    }

    [Fact]
    public void Pause_batch_skips_new_registration_but_still_operates_removed_snapshot_item()
    {
        var manager = new DefaultContinuousManager();
        var first = Item("first");
        var removed = Item("removed");
        var added = Item("added");
        manager.TryActivate(first);
        manager.TryActivate(removed);
        manager.AddLifecycleBinder(new Binder { Paused = (c, _) => { if (ReferenceEquals(c, first)) { manager.Unregister(removed); manager.TryActivate(added); } } });
        manager.PauseAll(1);
        Assert.True(first.IsPaused);
        Assert.True(removed.IsPaused);
        Assert.True(added.IsActive);
        Assert.Equal(new[] { first, added }, manager.GetAllContinuous());
    }

    [Fact]
    public void Clear_uses_registration_snapshot_and_preserves_callback_additions()
    {
        var manager = new DefaultContinuousManager();
        var old = Item("old");
        var added = Item("added");
        manager.TryActivate(old);
        manager.OnUnregistered += (_, _) => manager.Register(added);
        manager.Clear();
        Assert.Same(added, Assert.Single(manager.GetAllContinuous()));
        Assert.True(old.IsActive);
    }

    [Fact]
    public void Batch_callback_failure_stops_later_items_without_rolling_back_progress()
    {
        var original = new InvalidOperationException("pause");
        var manager = new DefaultContinuousManager(lifecycleBinders: new[] { new Binder { Paused = (_, _) => throw original } });
        var first = Item("first");
        var second = Item("second");
        manager.TryActivate(first);
        manager.TryActivate(second);
        Assert.Same(original, Assert.Throws<InvalidOperationException>(() => manager.PauseAll(1)));
        Assert.True(first.IsPaused);
        Assert.True(second.IsActive);
        Assert.Equal(1, manager.ActiveCount);
    }

    [Fact]
    public void Interrupt_uses_permission_and_abort_reason_without_requiring_registration()
    {
        var manager = new DefaultContinuousManager();
        var denied = new Continuous(new Config("denied", CanBeInterrupted: false));
        var allowed = Item("allowed");
        Assert.False(manager.TryInterrupt(denied, "cancel"));
        Assert.False(denied.IsTerminated);
        Assert.True(manager.TryInterrupt(allowed, "cancel"));
        Assert.Equal("cancel", allowed.AbortReason);
        Assert.Equal(ContinuousState.Aborted, allowed.State);
        Assert.Equal(0, manager.TotalCount);
    }

    [Fact]
    public void Tag_policy_only_blocks_other_active_objects_of_same_owner()
    {
        var blocked = new Tags("silenced");
        var policy = new BlockByOwnerActiveTagsPolicy(blocked);
        var manager = new DefaultContinuousManager();
        var self = new Continuous(new Config("self") { Tags = new Tags("silenced") });
        manager.TryActivate(self);
        Assert.True(policy.CanActivate(self, manager, out _));
        var otherOwner = new Continuous(new Config("other-owner", 2) { Tags = new Tags("silenced") });
        manager.TryActivate(otherOwner);
        Assert.True(policy.CanActivate(self, manager, out _));
        var peer = new Continuous(new Config("peer") { Tags = new Tags("silenced") });
        manager.TryActivate(peer);
        Assert.False(policy.CanActivate(self, manager, out var reason));
        Assert.Equal("Blocked by active continuous tags", reason);
        manager.TryPause(peer);
        Assert.True(policy.CanActivate(self, manager, out _));
        Assert.False(self.IsPaused);
    }

    [Fact]
    public void Tag_policy_keeps_container_reference_and_allows_empty_inputs()
    {
        var blocked = new Tags();
        var policy = new BlockByOwnerActiveTagsPolicy(blocked);
        var manager = new DefaultContinuousManager();
        manager.TryActivate(new Continuous(new Config("peer") { Tags = new Tags("silenced") }));
        var target = Item("target");
        Assert.True(policy.CanActivate(target, manager, out _));
        blocked.Values.Add("silenced");
        Assert.False(policy.CanActivate(target, manager, out _));
        Assert.True(policy.CanActivate(null!, manager, out _));
        Assert.True(policy.CanActivate(target, null!, out _));
        Assert.True(new BlockByOwnerActiveTagsPolicy(null!).CanActivate(target, manager, out _));
        Assert.True(policy.CanRegister(null!, null!, out var reason));
        Assert.Null(reason);
    }

    [Fact]
    public void Optional_configs_do_not_automatically_schedule_mutex_stack_or_cascade()
    {
        var manager = new DefaultContinuousManager();
        var parent = new Continuous(new Config("parent") { Tags = new Tags("blocked") });
        var childConfig = new Config("child") { ParentId = "parent", BlockByTags = new Tags("blocked"), PauseByTags = new Tags("blocked") };
        var child = new Continuous(childConfig);
        Assert.True(manager.TryActivate(parent));
        Assert.True(manager.TryActivate(child));
        Assert.True(parent.IsActive);
        Assert.True(child.IsActive);
        Assert.Equal(0f, child.ElapsedSeconds);
        Assert.Equal(10, childConfig.Stack);
        manager.TryEnd(parent);
        Assert.True(child.IsActive);
        Assert.Same(child, Assert.Single(manager.GetAllContinuous()));
    }

    [Fact]
    public void Allow_all_policy_still_checks_validity_and_never_sets_reason()
    {
        var policy = AllowAllContinuousAdmissionPolicy.Instance;
        Assert.False(policy.CanRegister(null!, null!, out var reason));
        Assert.Null(reason);
        var noConfig = new Continuous(null!);
        Assert.False(policy.CanRegister(noConfig, null!, out reason));
        Assert.Null(reason);
        Assert.True(policy.CanActivate(noConfig, null!, out reason));
        noConfig.End(ContinuousEndReason.Completed);
        Assert.False(policy.CanActivate(noConfig, null!, out reason));
        Assert.Null(reason);
    }

    [Fact]
    public void Null_binder_accepts_all_nodes_without_accessing_arguments()
    {
        var binder = NullContinuousLifecycleBinder.Instance;
        binder.OnRegistered(null!, null!);
        binder.OnActivated(null!, null!);
        binder.OnPaused(null!, null!);
        binder.OnResumed(null!, null!);
        binder.OnEnded(null!, ContinuousEndReason.Completed, null!);
        binder.OnUnregistered(null!, ContinuousEndReason.CleanedUp, null!);
        Assert.Same(binder, NullContinuousLifecycleBinder.Instance);
    }

    private static Continuous Item(string id) => new(new Config(id));

    private sealed record Config(string Id, long OwnerId = 1, bool CanBeInterrupted = true) :
        IContinuousConfig, ITagConfig, IMutexConfig, IDurationConfig, IHierarchyConfig, IStackConfig
    {
        public ITagContainer Tags { get; init; } = new Tags();
        public ITagContainer PauseByTags { get; init; } = new Tags();
        public ITagContainer BlockByTags { get; init; } = new Tags();
        public string MutexGroup => "shared";
        public int Priority => 1;
        public float? DurationSeconds => 0f;
        public string ParentId { get; init; } = "";
        public bool CascadeOnExpire => true;
        public int Stack { get; set; } = 10;
        public int MaxStack => 1;
    }

    private sealed class Tags(params string[] tags) : ITagContainer
    {
        public HashSet<string> Values { get; } = new(tags);
        public bool HasTag(string tag) => Values.Contains(tag);
        public bool HasAny(ITagContainer other) => Values.Any(other.HasTag);
        public int Count => Values.Count;
    }

    private sealed class Continuous(IContinuousConfig config) : IContinuous
    {
        public IContinuousConfig Config { get; } = config;
        public ContinuousState State { get; private set; }
        public bool IsActive => State == ContinuousState.Active;
        public bool IsTerminated => State is ContinuousState.Expired or ContinuousState.Aborted;
        public bool IsPaused => State == ContinuousState.Paused;
        public float ElapsedSeconds => 0f;
        public string? EqualityKey { get; set; }
        public int ActivateCalls { get; private set; }
        public bool AcceptActivate { get; set; } = true;
        public bool AcceptPause { get; set; } = true;
        public bool AcceptResume { get; set; } = true;
        public bool EmitEndEvent { get; set; } = true;
        public string? AbortReason { get; private set; }
        public event Action<IContinuous, ContinuousEndReason>? OnEnded;
        public void Activate() { ActivateCalls++; if (AcceptActivate) State = ContinuousState.Active; }
        public void Pause() { if (AcceptPause) State = ContinuousState.Paused; }
        public void Resume() { if (AcceptResume) State = ContinuousState.Active; }
        public void End(ContinuousEndReason reason) { State = ContinuousState.Expired; if (EmitEndEvent) OnEnded?.Invoke(this, reason); }
        public void Abort(string reason) { AbortReason = reason; State = ContinuousState.Aborted; if (EmitEndEvent) OnEnded?.Invoke(this, ContinuousEndReason.Interrupted); }
        public override bool Equals(object? obj) => ReferenceEquals(this, obj) || EqualityKey != null && obj is Continuous other && EqualityKey == other.EqualityKey;
        public override int GetHashCode() => EqualityKey?.GetHashCode() ?? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }

    private sealed class Policy : IContinuousAdmissionPolicy
    {
        public Func<bool> RegisterCheck { get; set; } = () => true;
        public Func<bool> ActivateCheck { get; set; } = () => true;
        public string? Reason { get; init; }
        public bool CanRegister(IContinuous continuous, IContinuousManager manager, out string? reason) { reason = Reason; return RegisterCheck(); }
        public bool CanActivate(IContinuous continuous, IContinuousManager manager, out string? reason) { reason = Reason; return ActivateCheck(); }
    }

    private sealed class Binder : IContinuousLifecycleBinder
    {
        public Action<IContinuous, IContinuousManager>? Registered { get; init; }
        public Action<IContinuous, IContinuousManager>? Activated { get; init; }
        public Action<IContinuous, IContinuousManager>? Paused { get; init; }
        public Action<IContinuous, IContinuousManager>? Resumed { get; init; }
        public Action<IContinuous, ContinuousEndReason, IContinuousManager>? Ended { get; init; }
        public Action<IContinuous, ContinuousEndReason, IContinuousManager>? Unregistered { get; init; }
        public void OnRegistered(IContinuous continuous, IContinuousManager manager) => Registered?.Invoke(continuous, manager);
        public void OnActivated(IContinuous continuous, IContinuousManager manager) => Activated?.Invoke(continuous, manager);
        public void OnPaused(IContinuous continuous, IContinuousManager manager) => Paused?.Invoke(continuous, manager);
        public void OnResumed(IContinuous continuous, IContinuousManager manager) => Resumed?.Invoke(continuous, manager);
        public void OnEnded(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager) => Ended?.Invoke(continuous, reason, manager);
        public void OnUnregistered(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager) => Unregistered?.Invoke(continuous, reason, manager);
    }

    private sealed class HookManager : DefaultContinuousManager
    {
        public bool HookCalled { get; private set; }
        protected override ContinuousEndReason ResolveEndReason(IContinuous continuous) { HookCalled = true; return ContinuousEndReason.Interrupted; }
    }
}
#pragma warning restore CS0618
