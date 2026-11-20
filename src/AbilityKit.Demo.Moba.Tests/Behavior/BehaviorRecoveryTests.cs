using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbilityKit.Ability.Behavior;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services.Behavior;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Behavior;

public sealed class BehaviorRecoveryTests
{
    [Fact]
    public void Core_restores_raw_time_pause_requests_values_and_deep_output_without_callbacks()
    {
        var runtime = Create(new BehaviorManager(), new CounterDecision());
        runtime.Tick(.037f, 7);
        runtime.State.Set("null", (string)null!);
        runtime.State.Set("entity", new BehaviorEntityId(87));
        runtime.State.Set("position", new Vec3(3, 4, 5));
        var parameters = new Dictionary<string, object> { ["damage"] = 28 };
        runtime.Output.AddEvent("pending", parameters);
        runtime.Output.AddEffect("effect", new BehaviorEntityId(4), parameters);
        runtime.Output.SetMovement(new Vec3(7, 8, 9), 5, 2.5f);
        runtime.RequestInterrupt("next tick only");
        runtime.Pause();
        var state = runtime.State; var output = runtime.Output;
        var bytes = runtime.CaptureRecoveryState();
        var callbacks = 0;
        runtime.OnComplete += _ => callbacks++;
        runtime.OnInterrupt += (_, _) => callbacks++;
        parameters["damage"] = 1000;
        runtime.State.Clear(); runtime.Output.Clear(); runtime.Resume(); runtime.Tick(.013f, 8);
        runtime.ValidateRecoveryState(bytes);
        Assert.Equal(8, runtime.CurrentFrame);
        runtime.RestoreRecoveryState(bytes);
        Assert.Same(state, runtime.State); Assert.Same(output, runtime.Output);
        Assert.Equal(BehaviorPhase.Paused, runtime.Phase);
        Assert.Equal(7, runtime.CurrentFrame);
        Assert.True(runtime.State.Has("null"));
        Assert.Null(runtime.State.Get<object>("null"));
        Assert.Equal(new BehaviorEntityId(87), runtime.State.Get<BehaviorEntityId>("entity"));
        Assert.Equal(28, runtime.Output.PendingEvents.Single().Payload["damage"]);
        Assert.Equal(28, runtime.Output.PendingEffects.Single().Params["damage"]);
        Assert.Equal(2.5f, runtime.Output.Movement!.Value.Speed);
        Assert.Equal(bytes, runtime.CaptureRecoveryState());
        Assert.Equal(0, callbacks);
    }

    [Fact]
    public void Manager_restores_registration_order_allocator_and_deterministic_continuation()
    {
        var order = new List<long>();
        var manager = new BehaviorManager();
        var first = Create(manager, new CounterDecision(order));
        var second = Create(manager, new CounterDecision(order));
        manager.Tick(.031f, 5);
        var checkpoint = manager.CaptureRecoveryState();
        manager.Tick(.041f, 6);
        var baseline = manager.CaptureRecoveryState();
        var ephemeral = Create(manager, new CounterDecision());
        manager.Interrupt(ephemeral.InstanceId, "finished future allocation");
        var creations = 0; var endings = 0;
        manager.OnBehaviorCreated += _ => creations++;
        manager.OnBehaviorEnded += (_, _) => endings++;
        manager.RestoreRecoveryState(checkpoint);
        Assert.Equal(0, creations); Assert.Equal(0, endings);
        order.Clear();
        manager.Tick(.041f, 6);
        Assert.Equal(new[] { second.InstanceId, first.InstanceId }, order);
        Assert.Equal(baseline, manager.CaptureRecoveryState());
        Assert.Equal(ephemeral.InstanceId, Create(manager, new CounterDecision()).InstanceId);
    }

    [Fact]
    public void Restored_raw_elapsed_preserves_duration_boundary_on_next_normal_tick()
    {
        var manager = new BehaviorManager();
        var runtime = manager.CreateBehavior(new BehaviorCreateConfig
        { OwnerId = 1, Decision = new CounterDecision(), DurationSeconds = .1f });
        manager.Tick(.037f, 1);
        var checkpoint = manager.CaptureRecoveryState();
        manager.Tick(.021f, 2);
        var expected = manager.CaptureRecoveryState();
        manager.RestoreRecoveryState(checkpoint);
        manager.Tick(.021f, 2);
        Assert.Equal(expected, manager.CaptureRecoveryState());
        Assert.Equal(BehaviorPhase.Running, runtime.Phase);
        var ended = 0;
        runtime.OnComplete += _ => ended++;
        manager.Tick(.043f, 3);
        Assert.Equal(BehaviorPhase.Completed, runtime.Phase);
        Assert.Equal(1, ended);
        Assert.Equal(0, manager.TotalCount);
    }

    [Fact]
    public void Reused_allocator_id_cannot_impersonate_destroyed_generation_and_generation_is_not_hashed()
    {
        var manager = new BehaviorManager();
        var empty = manager.CaptureRecoveryState();
        var original = Create(manager, new CounterDecision());
        var snapshot = manager.CaptureRecoveryState();
        var hashState = manager.CaptureRecoveryState(false);
        manager.Interrupt(original.InstanceId, "destroyed");
        manager.RestoreRecoveryState(empty);
        var replacement = Create(manager, new CounterDecision());
        Assert.Equal(original.InstanceId, replacement.InstanceId);
        Assert.Equal(hashState, manager.CaptureRecoveryState(false));
        var before = manager.CaptureRecoveryState();
        Assert.Throws<InvalidOperationException>(() => manager.RestoreRecoveryState(snapshot));
        Assert.Equal(before, manager.CaptureRecoveryState());
    }

    [Fact]
    public void Unknown_value_or_decision_is_diagnosed_only_on_optional_recovery_entry()
    {
        var manager = new BehaviorManager();
        var runtime = Create(manager, new CounterDecision());
        var serviceObject = new object();
        runtime.State.Set("customDependency", serviceObject);
        manager.Tick(.02f, 1);
        Assert.Same(serviceObject, runtime.State.Get<object>("customDependency"));
        var error = Assert.Throws<InvalidOperationException>(() => manager.CaptureRecoveryState());
        Assert.Contains("customDependency", error.Message);
        runtime.State.Remove("customDependency");
        var unsupported = Create(manager, new DelegateDecision("ordinary", (_, _) => DecisionResult.Continue()));
        manager.Tick(.02f, 2);
        Assert.Equal(BehaviorPhase.Running, unsupported.Phase);
        Assert.Contains("decision", Assert.Throws<InvalidOperationException>(() => manager.CaptureRecoveryState()).Message);
    }

    [Fact]
    public void Unknown_dependency_and_binding_require_adaptation_only_when_enabling_recovery()
    {
        var manager = new BehaviorManager();
        var runtime = manager.CreateBehavior(new BehaviorCreateConfig
        { OwnerId = 1, Decision = new CounterDecision(), World = new CustomWorld() });
        manager.Tick(.02f, 1);
        Assert.Equal(BehaviorPhase.Running, runtime.Phase);
        Assert.Contains("dependency", Assert.Throws<InvalidOperationException>(() => manager.CaptureRecoveryState()).Message);
        manager.Dispose();
        var boundManager = new BehaviorManager();
        boundManager.CreateBehavior(new BehaviorCreateConfigWithBinding
        { OwnerId = 1, Decision = new CounterDecision(), Binding = new BehaviorBinding("source", 1, 1) });
        Assert.Contains("BehaviorBinding", Assert.Throws<InvalidOperationException>(() => boundManager.CaptureRecoveryState()).Message);
    }

    [Fact]
    public void Custom_moba_executor_keeps_inheritance_but_cannot_inherit_rollback_admission_implicitly()
    {
        var manager = new BehaviorManager();
        var runtime = manager.CreateBehavior(new BehaviorCreateConfig
        { OwnerId = 1, Decision = new CounterDecision(), Executor = new CustomMobaExecutor() });
        manager.Tick(.02f, 1);
        Assert.Equal(BehaviorPhase.Running, runtime.Phase);
        Assert.Contains(nameof(CustomMobaExecutor), Assert.Throws<InvalidOperationException>(
            () => manager.CaptureRecoveryState()).Message);
        manager.Tick(.02f, 2);
        Assert.Equal(2, ((CounterDecision)runtime.Decision).Value);
    }

    [Fact]
    public void Whole_payload_preflight_rejects_bad_later_decision_before_restoring_earlier_values()
    {
        var manager = new BehaviorManager();
        var first = Create(manager, new CounterDecision());
        var secondDecision = new CounterDecision();
        Create(manager, secondDecision);
        var snapshot = manager.CaptureRecoveryState();
        manager.Tick(.02f, 1);
        secondDecision.RejectValidation = true;
        var current = first.CaptureRecoveryState();
        Assert.Throws<InvalidOperationException>(() => manager.RestoreRecoveryState(snapshot));
        Assert.Equal(current, first.CaptureRecoveryState());
        Assert.Equal(0, ((CounterDecision)first.Decision).RestoreCount);
    }

    [Fact]
    public void Actual_restore_failure_compensates_core_and_prior_decisions()
    {
        var manager = new BehaviorManager();
        var first = new CounterDecision(); var second = new CounterDecision();
        Create(manager, first); Create(manager, second);
        manager.Tick(.02f, 1);
        var snapshot = manager.CaptureRecoveryState();
        manager.Tick(.02f, 2);
        var previous = manager.CaptureRecoveryState();
        second.ThrowOnValue = 1;
        Assert.Throws<InvalidOperationException>(() => manager.RestoreRecoveryState(snapshot));
        Assert.Equal(previous, manager.CaptureRecoveryState());
        Assert.Equal(2, first.Value); Assert.Equal(2, second.Value);
    }

    [Fact]
    public void Malformed_core_and_changed_config_are_rejected_before_state_write()
    {
        var config = new Dictionary<string, object> { ["limit"] = 10 };
        var runtime = new BehaviorManager().CreateBehavior(new BehaviorCreateConfig
        { OwnerId = 1, Decision = new CounterDecision(), Config = config });
        var snapshot = runtime.CaptureRecoveryState();
        runtime.State.Set("live", 12);
        Assert.Throws<InvalidOperationException>(() => runtime.RestoreRecoveryState(snapshot.Concat(new byte[] { 0 }).ToArray()));
        Assert.Equal(12, runtime.State.Get<int>("live"));
        config["limit"] = 11;
        Assert.Throws<InvalidOperationException>(() => runtime.RestoreRecoveryState(snapshot));
        Assert.Equal(12, runtime.State.Get<int>("live"));
    }

    private static BehaviorRuntime Create(BehaviorManager manager, IBehaviorDecision decision) =>
        manager.CreateBehavior(new BehaviorCreateConfig { BehaviorKind = "test", OwnerId = 1, Decision = decision });

    private sealed class CustomWorld : DefaultWorldQuery { }
    private sealed class CustomMobaExecutor : MobaBrainExecutor { }

    private sealed class CounterDecision(List<long>? order = null) : IBehaviorDecision, IBehaviorSnapshotValidator
    {
        public string DecisionType => "counter";
        public string CurrentState => "running";
        public string SnapshotType => "counter.v1";
        public int Value;
        public int RestoreCount;
        public int? ThrowOnValue;
        public bool RejectValidation;

        public DecisionResult Decide(IBehaviorContext context, IWorldQuery world)
        {
            Value++; order?.Add(context.InstanceId);
            context.State.Set("lastFrame", context.CurrentFrame);
            return DecisionResult.Continue();
        }

        public byte[] CaptureSnapshot() => BitConverter.GetBytes(Value);
        public void ValidateSnapshot(byte[] payload)
        {
            if (RejectValidation || payload == null || payload.Length != 4)
                throw new InvalidOperationException("Invalid counter payload.");
        }
        public void RestoreSnapshot(byte[] payload)
        {
            var value = BitConverter.ToInt32(payload);
            if (value == ThrowOnValue) throw new InvalidOperationException("Injected import failure.");
            Value = value; RestoreCount++;
        }
    }
}
