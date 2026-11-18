using System;
using System.Linq;
using System.Text;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Deterministic;
using AbilityKit.HFSM.Definition;
using AbilityKit.Modifiers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

[Collection("Moba runtime inspection")]
public sealed class MobaCharacterHfsmRollbackPreflightTests
{
    [Theory]
    [InlineData("frame")]
    [InlineData("definition")]
    [InlineData("bindings")]
    [InlineData("action")]
    [InlineData("facts")]
    [InlineData("aim")]
    [InlineData("mode")]
    [InlineData("duplicate")]
    [InlineData("null-machine")]
    public void Invalid_second_actor_aborts_coordinator_before_any_import(string defect)
    {
        using var fixture = new Fixture();
        var checkpoint = fixture.Coordinator.Capture(Fixture.Frame);
        var data = JArray.Parse(Encoding.UTF8.GetString(fixture.Provider.Export(Fixture.Frame)));
        var second = data[1]!;
        switch (defect)
        {
            case "frame": second["Machine"]!["Frame"] = 99; break;
            case "definition": second["Machine"]!["DefinitionHash"] = 0; break;
            case "bindings": second["BindingsFingerprint"] = "invalid"; break;
            case "action": second["LocalFrame"] = 99; break;
            case "facts": second["Facts"] = null; break;
            case "aim": second["NextAimScopeSequence"] = 0; break;
            case "mode": second["Mode"]!["Frame"] = 99; break;
            case "duplicate": data.Add(data[0]!.DeepClone()); break;
            case "null-machine": second["Machine"] = null; break;
        }
        ReplacePayload(checkpoint, fixture.Provider.Key, Encoding.UTF8.GetBytes(data.ToString()));
        fixture.Advance();
        // A failed validation must not register a new profile or clear contributions,
        // including when an earlier actor in the same payload is valid.
        fixture.InstallOtherProfile();
        var live = fixture.Provider.Export(new FrameIndex(2));
        var parameters = fixture.ParameterProvider.Export(new FrameIndex(2));
        fixture.Probe.Value = 2;

        Assert.False(fixture.Coordinator.TryRestore(checkpoint, out var result));
        Assert.Equal(RollbackOperationStatus.ProviderFailed, result.Status);
        Assert.Equal(fixture.Provider.Key, result.ProviderKey);
        Assert.Equal(0, fixture.Probe.ImportCount);
        Assert.Equal(2, fixture.Probe.Value);
        Assert.Equal(live, fixture.Provider.Export(new FrameIndex(2)));
        Assert.Equal(parameters, fixture.ParameterProvider.Export(new FrameIndex(2)));
        Assert.True(fixture.Parameters.UsesProfile(17, "preflight.other"));
        Assert.True(fixture.Parameters.IsDeclared(17, 99));
        Assert.Equal(7f, fixture.Parameters.Resolve(17, 99));
        Assert.Equal(0, fixture.LogicCalls);
    }

    [Fact]
    public void Valid_preflight_then_later_failure_leaves_profiles_and_live_machines_untouched()
    {
        using var fixture = new Fixture(rejectLater: true);
        var checkpoint = fixture.Coordinator.Capture(Fixture.Frame);
        fixture.Advance();
        fixture.InstallOtherProfile();
        fixture.Probe.Value = 2;
        var live = fixture.Provider.Export(new FrameIndex(2));
        var parameters = fixture.ParameterProvider.Export(new FrameIndex(2));
        Assert.False(fixture.Coordinator.TryRestore(checkpoint, out var result));
        Assert.Equal(20000, result.ProviderKey);
        Assert.Equal(0, fixture.Probe.ImportCount);
        Assert.Equal(2, fixture.Probe.Value);
        Assert.Equal(live, fixture.Provider.Export(new FrameIndex(2)));
        Assert.Equal(parameters, fixture.ParameterProvider.Export(new FrameIndex(2)));
        Assert.True(fixture.Parameters.UsesProfile(17, "preflight.other"));
        Assert.Equal(0, fixture.LogicCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Valid_hero_and_summon_restore_existing_or_missing_machine(bool removeMachines)
    {
        using var fixture = new Fixture(summon: true);
        var checkpoint = fixture.Coordinator.Capture(Fixture.Frame);
        var expected = fixture.Provider.Export(Fixture.Frame);
        fixture.Advance();
        if (removeMachines)
            foreach (var id in fixture.Actors.CopyActorIdsInOrder())
                if (fixture.Actors.TryGet(id, out var actor)) actor.RemoveAndDisposeCharacterHfsm();
        fixture.LogicCalls = 0;
        fixture.Probe.Value = 2;

        Assert.True(fixture.Coordinator.TryRestore(checkpoint, out var result), result.ToString());
        Assert.Equal(1, fixture.Probe.ImportCount);
        Assert.Equal(1, fixture.Probe.Value);
        Assert.Equal(expected, fixture.Provider.Export(Fixture.Frame));
        Assert.Equal(0, fixture.LogicCalls);
        Assert.True(fixture.Parameters.UsesProfile(17, fixture.Definition.DefinitionId));
        Assert.True(fixture.Parameters.UsesProfile(18, MobaCharacterHfsmProfile.CreateSummonDefinition().DefinitionId));
    }

    [Fact]
    public void Direct_invalid_recovery_keeps_parameters_and_machine_unchanged()
    {
        using var fixture = new Fixture();
        var entries = JArray.Parse(Encoding.UTF8.GetString(fixture.Provider.Export(Fixture.Frame)));
        entries[1]!["LocalFrame"] = 99;
        fixture.Advance();
        fixture.InstallOtherProfile();
        var live = fixture.Provider.Export(new FrameIndex(2));
        var parameters = fixture.ParameterProvider.Export(new FrameIndex(2));

        Assert.Throws<InvalidOperationException>(() => fixture.Provider.ImportState(Fixture.Frame,
            Encoding.UTF8.GetBytes(entries.ToString())));
        Assert.Equal(live, fixture.Provider.Export(new FrameIndex(2)));
        Assert.Equal(parameters, fixture.ParameterProvider.Export(new FrameIndex(2)));
        Assert.True(fixture.Parameters.UsesProfile(17, "preflight.other"));
        Assert.Equal(0, fixture.LogicCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Incompatible_existing_parameter_contribution_is_checked_before_applying_any_actor(bool direct)
    {
        using var fixture = new Fixture();
        var checkpoint = fixture.Coordinator.Capture(Fixture.Frame);
        var payload = fixture.Provider.Export(Fixture.Frame);
        fixture.Advance();
        // Same definition id with stale declaration metadata: registration used to
        // reject this before applying actors, and must retain that validation.
        var stale = MobaCharacterHfsmProfile.CreateGenericDefinition();
        stale.Parameters.Add(new StateMachineParameterDefinition
        {
            Id = 99, Name = "StaleValue", SupportedOperations = new() { (int)ModifierOp.Override }
        });
        fixture.Parameters.RegisterActorProfile(18, stale);
        var modifier = ModifierData.Override(ModifierKey.FromPacked(99), 9f);
        fixture.Parameters.Set(18, 902, 99, in modifier);
        var live = fixture.Provider.Export(new FrameIndex(2));
        var parameters = fixture.ParameterProvider.Export(new FrameIndex(2));
        if (direct)
            Assert.Throws<InvalidOperationException>(() => fixture.Provider.ImportState(Fixture.Frame, payload));
        else
        {
            Assert.False(fixture.Coordinator.TryRestore(checkpoint, out var result));
            Assert.Equal(fixture.Provider.Key, result.ProviderKey);
            Assert.Equal(0, fixture.Probe.ImportCount);
        }
        Assert.Equal(live, fixture.Provider.Export(new FrameIndex(2)));
        Assert.Equal(parameters, fixture.ParameterProvider.Export(new FrameIndex(2)));
        Assert.True(fixture.Parameters.IsDeclared(18, 99));
        Assert.Equal(9f, fixture.Parameters.Resolve(18, 99));
    }

    [Fact]
    public void Empty_full_baseline_removes_machines_and_profiles_but_empty_payload_is_noop()
    {
        using var fixture = new Fixture();
        var before = fixture.Provider.Export(Fixture.Frame);
        fixture.Provider.ValidateImport(Fixture.Frame, null!);
        fixture.Provider.Import(Fixture.Frame, Array.Empty<byte>());
        Assert.Equal(before, fixture.Provider.Export(Fixture.Frame));
        fixture.Provider.ValidateImport(Fixture.Frame, Encoding.UTF8.GetBytes("[]"));
        Assert.Equal(before, fixture.Provider.Export(Fixture.Frame));

        fixture.Provider.Import(Fixture.Frame, Encoding.UTF8.GetBytes("[]"));
        Assert.Equal("[]", Encoding.UTF8.GetString(fixture.Provider.Export(Fixture.Frame)));
        Assert.False(fixture.Parameters.IsDeclared(17, 1));
        Assert.False(fixture.Parameters.IsDeclared(18, 1));
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("owner")]
    [InlineData("parameter")]
    [InlineData("unregistered-actor")]
    [InlineData("undeclared-parameter")]
    [InlineData("operation")]
    [InlineData("unsupported-operation")]
    [InlineData("null-entry")]
    public void Invalid_later_parameter_contribution_never_clears_or_partially_restores(string defect)
    {
        foreach (var path in new[] { "coordinator", "direct-provider", "direct-service" })
        {
            using var fixture = new Fixture();
            var entries = new[] { Contribution(17, 101, 2f), Contribution(18, 102, 3f) };
            switch (defect)
            {
                case "actor": entries[1].ActorId = 0; break;
                case "owner": entries[1].OwnerId = 0; break;
                case "parameter": entries[1].ParameterId = 0; break;
                case "unregistered-actor": entries[1].ActorId = 999; break;
                case "undeclared-parameter": entries[1].ParameterId = 99; break;
                case "operation": entries[1].Modifier.Op = (ModifierOp)255; break;
                case "unsupported-operation": entries[1].Modifier.Op = ModifierOp.Add; break;
                case "null-entry": entries[1] = null!; break;
            }
            var checkpoint = fixture.Coordinator.Capture(Fixture.Frame);
            var payload = Encoding.UTF8.GetBytes(JArray.FromObject(entries).ToString());
            ReplacePayload(checkpoint, fixture.ParameterProvider.Key, payload);
            fixture.Advance();
            fixture.InstallOtherProfile();
            var live = fixture.Provider.Export(new FrameIndex(2));
            var parameters = fixture.ParameterProvider.Export(new FrameIndex(2));

            if (path == "coordinator")
            {
                Assert.False(fixture.Coordinator.TryRestore(checkpoint, out var result));
                Assert.Equal(fixture.ParameterProvider.Key, result.ProviderKey);
                Assert.Equal(0, result.ProviderCount);
                Assert.Equal(0, fixture.Probe.ImportCount);
            }
            else if (path == "direct-provider")
                Assert.ThrowsAny<Exception>(() => fixture.ParameterProvider.Import(Fixture.Frame, payload));
            else
                Assert.ThrowsAny<Exception>(() => fixture.Parameters.RestoreSnapshot(entries));

            Assert.Equal(live, fixture.Provider.Export(new FrameIndex(2)));
            Assert.Equal(parameters, fixture.ParameterProvider.Export(new FrameIndex(2)));
            Assert.True(fixture.Parameters.UsesProfile(17, "preflight.other"));
            Assert.Equal(7f, fixture.Parameters.Resolve(17, 99));
            Assert.Equal(0, fixture.Parameters.Resolve(18, 1));
            Assert.Equal(0, fixture.LogicCalls);
        }
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("{}")]
    [InlineData("[null]")]
    public void Malformed_parameter_payload_aborts_before_any_import(string json)
    {
        using var fixture = new Fixture();
        var checkpoint = fixture.Coordinator.Capture(Fixture.Frame);
        ReplacePayload(checkpoint, fixture.ParameterProvider.Key, Encoding.UTF8.GetBytes(json));
        fixture.InstallOtherProfile();
        var before = fixture.ParameterProvider.Export(Fixture.Frame);
        Assert.False(fixture.Coordinator.TryRestore(checkpoint, out var result));
        Assert.Equal(fixture.ParameterProvider.Key, result.ProviderKey);
        Assert.Equal(0, result.ProviderCount);
        Assert.Equal(0, fixture.Probe.ImportCount);
        Assert.Equal(before, fixture.ParameterProvider.Export(Fixture.Frame));
        Assert.ThrowsAny<Exception>(() => fixture.ParameterProvider.Import(Fixture.Frame, Encoding.UTF8.GetBytes(json)));
        Assert.Equal(before, fixture.ParameterProvider.Export(Fixture.Frame));
    }

    [Fact]
    public void Target_profile_accepts_contributions_absent_from_current_profile_and_replays_twice()
    {
        using var fixture = new Fixture();
        const long firstOwner = 4_294_967_297L;
        const long secondOwner = 8_589_934_593L; // same truncated 32-bit identity
        fixture.Parameters.RestoreSnapshot(new[]
        {
            Contribution(17, firstOwner, 2f), Contribution(17, secondOwner, 3f)
        });
        var checkpoint = fixture.Coordinator.Capture(Fixture.Frame);
        var expected = fixture.ParameterProvider.Export(Fixture.Frame);
        var machine = fixture.Provider.Export(Fixture.Frame);
        for (var replay = 0; replay < 2; replay++)
        {
            fixture.Advance();
            fixture.InstallOtherProfile(excludeControlled: true);
            fixture.LogicCalls = 0; // Forward simulation may execute a state exit; restore must not.
            Assert.False(fixture.Parameters.IsDeclared(17, 1));
            Assert.True(fixture.Coordinator.TryRestore(checkpoint, out var result), result.ToString());
            Assert.Equal(expected, fixture.ParameterProvider.Export(Fixture.Frame));
            Assert.Equal(machine, fixture.Provider.Export(Fixture.Frame));
            fixture.Parameters.ClearSource(17, firstOwner);
            Assert.Equal(3f, fixture.Parameters.Resolve(17, 1));
            fixture.Parameters.ClearSource(17, secondOwner);
            Assert.Equal(0f, fixture.Parameters.Resolve(17, 1));
            Assert.Equal(0, fixture.LogicCalls);
        }
    }

    [Theory]
    [InlineData("target-profile")]
    [InlineData("omitted-actor")]
    [InlineData("empty-baseline")]
    public void Parameters_must_match_profiles_after_hfsm_import(string defect)
    {
        using var fixture = new Fixture();
        var checkpoint = fixture.Coordinator.Capture(Fixture.Frame);
        fixture.InstallOtherProfile();
        // This payload is valid in the current profile, but not after target HFSM import.
        var payload = fixture.ParameterProvider.Export(Fixture.Frame);
        fixture.ParameterProvider.ValidateImport(Fixture.Frame, payload);
        ReplacePayload(checkpoint, fixture.ParameterProvider.Key, payload);
        if (defect != "target-profile")
        {
            var hfsm = JArray.Parse(Encoding.UTF8.GetString(fixture.Provider.Export(Fixture.Frame)));
            if (defect == "omitted-actor") hfsm.RemoveAt(0);
            else hfsm.Clear();
            ReplacePayload(checkpoint, fixture.Provider.Key, Encoding.UTF8.GetBytes(hfsm.ToString()));
        }
        Assert.False(fixture.Coordinator.TryRestore(checkpoint, out var result));
        Assert.Equal(fixture.ParameterProvider.Key, result.ProviderKey);
        Assert.Equal(0, fixture.Probe.ImportCount);
        Assert.True(fixture.Parameters.UsesProfile(17, "preflight.other"));
        Assert.Equal(payload, fixture.ParameterProvider.Export(Fixture.Frame));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[]")]
    public void Empty_parameter_payload_keeps_existing_clear_semantics_without_clearing_profiles(string? json)
    {
        using var fixture = new Fixture();
        fixture.InstallOtherProfile();
        var payload = json == null ? null! : Encoding.UTF8.GetBytes(json);
        fixture.ParameterProvider.ValidateImport(Fixture.Frame, payload);
        Assert.Equal(7f, fixture.Parameters.Resolve(17, 99));
        fixture.ParameterProvider.Import(Fixture.Frame, payload);
        Assert.Empty(fixture.Parameters.CaptureSnapshot());
        Assert.True(fixture.Parameters.UsesProfile(17, "preflight.other"));
    }

    [Fact]
    public void Hfsm_noop_and_parameter_only_registry_validate_against_live_profiles()
    {
        using var fixture = new Fixture();
        fixture.InstallOtherProfile();
        var checkpoint = fixture.Coordinator.Capture(Fixture.Frame);
        ReplacePayload(checkpoint, fixture.Provider.Key, Array.Empty<byte>());
        var expected = fixture.ParameterProvider.Export(Fixture.Frame);
        Assert.True(fixture.Coordinator.TryRestore(checkpoint, out var result), result.ToString());
        Assert.Equal(expected, fixture.ParameterProvider.Export(Fixture.Frame));
        Assert.True(fixture.Parameters.UsesProfile(17, "preflight.other"));

        var registry = new RollbackRegistry();
        registry.Register(fixture.Probe);
        registry.Register(fixture.ParameterProvider);
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(1));
        checkpoint = coordinator.Capture(Fixture.Frame);
        ReplacePayload(checkpoint, fixture.ParameterProvider.Key,
            Encoding.UTF8.GetBytes(JArray.FromObject(new[] { Contribution(18, 0, 2f) }).ToString()));
        var imported = fixture.Probe.ImportCount;
        Assert.False(coordinator.TryRestore(checkpoint, out result));
        Assert.Equal(fixture.ParameterProvider.Key, result.ProviderKey);
        Assert.Equal(imported, fixture.Probe.ImportCount);
        Assert.Equal(expected, fixture.ParameterProvider.Export(Fixture.Frame));
    }

    [Fact]
    public void Empty_combined_baseline_clears_contributions_and_profiles()
    {
        using var fixture = new Fixture();
        var checkpoint = fixture.Coordinator.Capture(Fixture.Frame);
        ReplacePayload(checkpoint, fixture.Provider.Key, Encoding.UTF8.GetBytes("[]"));
        fixture.InstallOtherProfile();
        Assert.True(fixture.Coordinator.TryRestore(checkpoint, out var result), result.ToString());
        Assert.Empty(fixture.Parameters.CaptureSnapshot());
        Assert.False(fixture.Parameters.IsDeclared(17, 99));
        Assert.False(fixture.Parameters.IsDeclared(18, 1));
        Assert.Equal("[]", Encoding.UTF8.GetString(fixture.Provider.Export(Fixture.Frame)));
    }

    [Fact]
    public void Hfsm_without_parameter_service_does_not_predict_a_profile_change()
    {
        using var fixture = new Fixture();
        fixture.InstallOtherProfile();
        var registry = new RollbackRegistry();
        registry.Register(new MobaCharacterHfsmRollbackProvider(fixture.Actors, fixture.Definition));
        registry.Register(fixture.ParameterProvider);
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(1));
        var checkpoint = coordinator.Capture(Fixture.Frame);
        var expected = fixture.ParameterProvider.Export(Fixture.Frame);
        Assert.True(coordinator.TryRestore(checkpoint, out var result), result.ToString());
        Assert.Equal(expected, fixture.ParameterProvider.Export(Fixture.Frame));
        Assert.True(fixture.Parameters.UsesProfile(17, "preflight.other"));
    }

    [Fact]
    public void Repeated_owner_entries_keep_last_write_semantics_and_allow_later_preflight_rejection()
    {
        using var fixture = new Fixture(rejectLater: true);
        fixture.Parameters.RestoreSnapshot(new[] { Contribution(17, 901, 2f), Contribution(17, 901, 3f) });
        Assert.Single(fixture.Parameters.CaptureSnapshot());
        Assert.Equal(3f, fixture.Parameters.Resolve(17, 1));
        var checkpoint = fixture.Coordinator.Capture(Fixture.Frame);
        fixture.InstallOtherProfile(excludeControlled: true);
        var before = fixture.ParameterProvider.Export(Fixture.Frame);
        Assert.False(fixture.Coordinator.TryRestore(checkpoint, out var result));
        Assert.Equal(20000, result.ProviderKey);
        Assert.Equal(0, fixture.Probe.ImportCount);
        Assert.Equal(before, fixture.ParameterProvider.Export(Fixture.Frame));
        Assert.True(fixture.Parameters.UsesProfile(17, "preflight.other"));
    }

    private static MobaCharacterHfsmParameterService.SnapshotEntry Contribution(int actor, long owner, float value)
    {
        var modifier = ModifierData.Override(ModifierKey.FromPacked(1), value, sourceId: 101);
        return new(actor, owner, 1, in modifier);
    }

    private static void ReplacePayload(WorldRollbackSnapshot snapshot, int key, byte[] payload)
    {
        for (var i = 0; i < snapshot.Entries.Length; i++)
            if (snapshot.Entries[i].Key == key)
                snapshot.Entries[i] = new WorldRollbackSnapshotEntry(key, payload);
    }

    private sealed class Probe : IRollbackStateProvider
    {
        public int Key => 1;
        public int Value = 1;
        public int ImportCount;
        public byte[] Export(FrameIndex frame) => BitConverter.GetBytes(Value);
        public void Import(FrameIndex frame, byte[] payload)
        { ImportCount++; Value = BitConverter.ToInt32(payload); }
    }

    private sealed class RejectingProvider : IRollbackStateProvider, IRollbackStatePreflightProvider
    {
        public int Key => 20000;
        public byte[] Export(FrameIndex frame) => new byte[] { 1 };
        public void ValidateImport(FrameIndex frame, byte[] payload) => throw new InvalidOperationException("later failure");
        public void Import(FrameIndex frame, byte[] payload) => throw new Exception("must not import");
    }

    private sealed class Fixture : IDisposable
    {
        public static readonly FrameIndex Frame = new(1);
        private readonly ActorContext _context = new();
        public readonly MobaActorRegistry Actors = new();
        public readonly MobaCharacterHfsmParameterService Parameters = new();
        public readonly StateMachineDefinition Definition = MobaCharacterHfsmProfile.CreateGenericDefinition();
        public readonly Probe Probe = new();
        public readonly RollbackRegistry Registry = new();
        public readonly MobaCharacterHfsmRollbackProvider Provider;
        public readonly MobaCharacterHfsmParameterRollbackProvider ParameterProvider;
        public readonly RollbackCoordinator Coordinator;
        public int LogicCalls;

        public Fixture(bool summon = false, bool rejectLater = false)
        {
            // Detached validation must not invoke entry/exit actions in this definition.
            var idle = Definition.Machines.Single(m => m.Id == "action").States.Single(s => s.Id == "idle");
            idle.LogicEntryActionKeys.Add("trigger.enter.123");
            idle.LogicExitActionKeys.Add("trigger.exit.124");
            for (var id = 17; id <= 18; id++)
            {
                var actor = _context.CreateEntity();
                actor.AddActorId(id);
                var definition = id == 18 && summon ? MobaCharacterHfsmProfile.CreateSummonDefinition() : Definition;
                if (id == 18 && summon)
                {
                    actor.AddSkillLoadout(7654321, Array.Empty<ActiveSkillRuntime>(), Array.Empty<PassiveSkillRuntime>());
                    actor.AddSummonMeta(9876, true);
                }
                Actors.Register(id, actor);
                Parameters.RegisterActorProfile(id, definition);
                actor.AddCharacterHfsm(new MobaCharacterHfsmRuntime(id, definition, 0, Fixed64.Zero,
                    registerForInspection: false, parameters: Parameters,
                    executeLogicTrigger: (_, _, _, _) => LogicCalls++));
                actor.characterHfsm.Runtime.Tick(1, Fixed64.FromDouble(.1), true, false, false, 0, 0);
            }
            LogicCalls = 0;
            Provider = new MobaCharacterHfsmRollbackProvider(Actors, Definition, Parameters);
            ParameterProvider = new MobaCharacterHfsmParameterRollbackProvider(Parameters);
            Registry.Register(Probe);
            Registry.Register(Provider);
            Registry.Register(ParameterProvider);
            if (rejectLater) Registry.Register(new RejectingProvider());
            Coordinator = new RollbackCoordinator(Registry, new RollbackSnapshotRingBuffer(2));
        }

        public void Advance()
        {
            foreach (var id in Actors.CopyActorIdsInOrder())
                if (Actors.TryGet(id, out var actor))
                    actor.characterHfsm.Runtime.Tick(2, Fixed64.FromDouble(.2), true, false, false, 0, 0);
        }

        public void InstallOtherProfile(bool excludeControlled = false)
        {
            var other = MobaCharacterHfsmProfile.CreateGenericDefinition();
            other.DefinitionId = "preflight.other";
            if (excludeControlled) other.Parameters.RemoveAll(p => p.Id == 1);
            other.Parameters.Add(new StateMachineParameterDefinition
            {
                Id = 99, Name = "TestValue", SupportedOperations = new() { (int)ModifierOp.Override }
            });
            Parameters.RegisterActorProfile(17, other);
            var modifier = ModifierData.Override(ModifierKey.FromPacked(99), 7f);
            Parameters.Set(17, 901, 99, in modifier);
        }

        public void Dispose()
        {
            foreach (var actor in _context.GetEntities())
                if (actor.hasCharacterHfsm) actor.RemoveAndDisposeCharacterHfsm();
            _context.DestroyAllEntities();
            Actors.Dispose();
            Parameters.Dispose();
        }
    }
}
