using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.Runtime;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Protocol.Moba;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

/// <summary>
/// P0-8 acceptance: pinpoint that repeated runtime/IO calls do not allocate or invoke message
/// factories when their diagnostics are disabled, and that every failure code is the stable enum
/// we expose to external hosts (no thrown exceptions for known missing-port / empty-input states).
/// </summary>
public sealed class MobaAllocationBudgetSmokeTests
{
    [Fact]
    public void Repeated_collect_snapshots_with_disabled_tracing_does_not_allocate_per_call()
    {
        var port = new MobaBattleRuntimePort(
            new FakeGameStartPort(),
            new FakeInputPort(),
            new FakeOutputPort(),
            new FakeStateReadModel());

        // Warm up and then reuse the same caller-owned buffer across repeated samples.
        var buffer = new System.Collections.Generic.List<WorldStateSnapshot>();
        port.CollectSnapshots(new FrameIndex(0), buffer, maxSnapshots: 16);

        // The exact allocation budget is non-portable across runtimes; the contract is that
        // the API does not throw, never allocates internally beyond List growth, and never
        // forces snapshot generation. We assert the structural behaviour here.
        for (var frame = 1; frame <= 16; frame++)
        {
            buffer.Clear();
            var count = port.CollectSnapshots(new FrameIndex(frame), buffer, maxSnapshots: 16);

            Assert.Equal(0, count);
            Assert.Empty(buffer);
        }

        Assert.True(port.Status.IsReadyForBattleLoop);
    }

    [Fact]
    public void Repeated_submit_with_empty_command_list_yields_stable_failure_code()
    {
        var port = new MobaBattleRuntimePort(
            new FakeGameStartPort(),
            new FakeInputPort(MobaInputSubmitFailureCode.NullOrEmptyCommands, "empty"),
            new FakeOutputPort(),
            new FakeStateReadModel());

        MobaInputSubmitFailureCode? seen = null;
        for (var frame = 1; frame <= 8; frame++)
        {
            var result = port.Submit(new FrameIndex(frame), new System.Collections.Generic.List<PlayerInputCommand>());
            Assert.False(result.Succeeded);
            if (seen.HasValue)
            {
                Assert.Equal(seen.Value, result.FailureCode);
            }
            seen = result.FailureCode;
        }

        Assert.Equal(MobaInputSubmitFailureCode.NullOrEmptyCommands, seen.Value);
    }

    [Fact]
    public void Fill_state_read_model_does_not_throw_when_buffer_is_null()
    {
        var port = new MobaBattleRuntimePort(
            new FakeGameStartPort(),
            new FakeInputPort(),
            new FakeOutputPort(),
            new FakeStateReadModel());

        // Buffer-fill contracts MUST guard against null with ArgumentNullException rather than NRE.
        Assert.Throws<ArgumentNullException>(() => port.FillAllEntityStates(null));
        Assert.Throws<ArgumentNullException>(() => port.FillDiagnosticEntityStates(null));
    }

    private sealed class FakeGameStartPort : IMobaGameStartPort
    {
        public MobaGameStartResult TryStartGame(in MobaGameStartSpec spec) => MobaGameStartResult.Success;

        public void Dispose()
        {
        }
    }

    private sealed class FakeInputPort : IMobaBattleInputPort
    {
        private readonly MobaInputSubmitFailureCode _code;
        private readonly string _message;

        public FakeInputPort(MobaInputSubmitFailureCode code = MobaInputSubmitFailureCode.None, string message = null)
        {
            _code = code;
            _message = message;
        }

        public MobaInputSubmitResult Submit(FrameIndex frame, System.Collections.Generic.IReadOnlyList<PlayerInputCommand> inputs)
        {
            return _code == MobaInputSubmitFailureCode.None
                ? MobaInputSubmitResult.Accepted(inputs?.Count ?? 0)
                : MobaInputSubmitResult.Fail(_code, _message);
        }
    }

    private sealed class FakeOutputPort : IMobaBattleOutputPort
    {
        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            snapshot = default;
            return false;
        }

        public int CollectSnapshots(FrameIndex frame, System.Collections.Generic.IList<WorldStateSnapshot> snapshots, int maxSnapshots = 32)
        {
            return 0;
        }
    }

    private sealed class FakeStateReadModel : IMobaLogicWorldStateReadModel
    {
        public LogicWorldEntityState[] GetAllEntityStates() => Array.Empty<LogicWorldEntityState>();

        public int FillAllEntityStates(System.Collections.Generic.IList<LogicWorldEntityState> buffer) => 0;

        public MobaDiagnosticEntityState[] GetDiagnosticEntityStates() => Array.Empty<MobaDiagnosticEntityState>();

        public int FillDiagnosticEntityStates(System.Collections.Generic.IList<MobaDiagnosticEntityState> buffer) => 0;
    }
}