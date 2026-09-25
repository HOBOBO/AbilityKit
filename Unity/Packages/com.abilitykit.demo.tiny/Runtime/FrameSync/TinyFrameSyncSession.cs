using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;

namespace AbilityKit.Demo.Tiny.FrameSync
{

public readonly struct TinyFrameInput
{
    public TinyFrameInput(uint playerId, TinyInput input)
    {
        PlayerId = playerId;
        Input = input;
    }

    public uint PlayerId { get; }
    public TinyInput Input { get; }
}

public enum TinyReconcileResult { Matched, Replayed, NeedsFullSnapshot }

public sealed class TinyFrameSyncSession
{
    private readonly TinyBattle _battle;
    private readonly RollbackCoordinator _rollback;
    private readonly RollbackSnapshotRingBuffer _snapshots;
    private readonly InputHistoryRingBuffer _inputs;
    private readonly WorldStateHashRingBuffer _hashes;

    public TinyFrameSyncSession(TinyBattle battle, int historyCapacity = 64)
    {
        if (historyCapacity < 2) throw new ArgumentOutOfRangeException(nameof(historyCapacity));
        _battle = battle ?? throw new ArgumentNullException(nameof(battle));
        _snapshots = new RollbackSnapshotRingBuffer(historyCapacity);
        _inputs = new InputHistoryRingBuffer(historyCapacity);
        _hashes = new WorldStateHashRingBuffer(historyCapacity);
        var registry = new RollbackRegistry();
        registry.Register(new TinyBattleRollbackProvider(battle));
        _rollback = new RollbackCoordinator(registry, _snapshots);
        if (!_rollback.CaptureAndStore(new FrameIndex(battle.Frame)))
            throw new InvalidOperationException("Could not capture Tiny initial state.");
        _hashes.Store(new FrameIndex(battle.Frame), new WorldStateHash(battle.ComputeHash()));
    }

    public int Frame => _battle.Frame;
    public uint StateHash => _battle.ComputeHash();
    public bool RequiresFullSnapshot { get; private set; }

    public bool TryGetStateHash(int frame, out uint hash)
    {
        if (frame >= 0 && _hashes.TryGet(new FrameIndex(frame), out var stored))
        {
            hash = stored.Value;
            return true;
        }
        hash = 0;
        return false;
    }

    public void Predict(IReadOnlyList<TinyFrameInput> inputs)
    {
        if (RequiresFullSnapshot) throw new InvalidOperationException("Tiny requires a full authoritative snapshot.");
        var frame = new FrameIndex(checked(Frame + 1));
        var commands = Normalize(frame, inputs);
        Tick(commands);
        _inputs.Store(frame, commands);
        Capture(frame);
    }

    public TinyReconcileResult ApplyAuthoritative(int frame, IReadOnlyList<TinyFrameInput> inputs, uint authoritativeHash)
    {
        if (RequiresFullSnapshot) return TinyReconcileResult.NeedsFullSnapshot;
        if (frame <= 0 || frame > Frame) throw new ArgumentOutOfRangeException(nameof(frame));
        var index = new FrameIndex(frame);
        var commands = Normalize(index, inputs);
        if (!_inputs.TryGet(index, out var predicted) || !_snapshots.TryGet(new FrameIndex(frame - 1), out _))
            return RequireRecovery();
        for (var replayFrame = frame + 1; replayFrame <= Frame; replayFrame++)
            if (!_inputs.TryGet(new FrameIndex(replayFrame), out _)) return RequireRecovery();

        if (SameInputs(predicted, commands))
        {
            return _hashes.TryGet(index, out var hash) && hash.Value == authoritativeHash
                ? TinyReconcileResult.Matched : RequireRecovery();
        }

        var end = Frame;
        if (!_rollback.TryRestore(new FrameIndex(frame - 1))) return RequireRecovery();
        _inputs.Store(index, commands);
        for (var current = frame; current <= end; current++)
        {
            var currentIndex = new FrameIndex(current);
            if (!_inputs.TryGet(currentIndex, out var replay))
                throw new InvalidOperationException("Preflighted Tiny input history was lost.");
            Tick(replay);
            Capture(currentIndex);
        }
        return _hashes.TryGet(index, out var corrected) && corrected.Value == authoritativeHash
            ? TinyReconcileResult.Replayed : RequireRecovery();
    }

    public void RestoreAuthoritativeFullState(TinyBattleState state)
    {
        _battle.RestoreState(state);
        _inputs.Clear();
        _hashes.Clear();
        _rollback.ClearHistory();
        Capture(new FrameIndex(state.Frame));
        RequiresFullSnapshot = false;
    }

    private TinyReconcileResult RequireRecovery()
    {
        RequiresFullSnapshot = true;
        return TinyReconcileResult.NeedsFullSnapshot;
    }

    private PlayerInputCommand[] Normalize(FrameIndex frame, IReadOnlyList<TinyFrameInput> inputs)
    {
        if (inputs == null) throw new ArgumentNullException(nameof(inputs));
        if (inputs.Count > 2) throw new ArgumentException("Tiny accepts at most two inputs per frame.", nameof(inputs));
        var ordered = inputs.OrderBy(item => item.PlayerId).ToArray();
        if (ordered.Any(item => item.PlayerId == 0 || !item.Input.IsValid || !_battle.ContainsPlayer(item.PlayerId)) ||
            ordered.Select(item => item.PlayerId).Distinct().Count() != ordered.Length)
            throw new ArgumentException("Invalid or duplicate Tiny frame input.", nameof(inputs));
        return ordered.Select(item => new PlayerInputCommand(frame,
            new PlayerId(item.PlayerId.ToString(CultureInfo.InvariantCulture)), TinyBattle.InputOpCode,
            item.Input.Encode())).ToArray();
    }

    private void Tick(PlayerInputCommand[] commands)
    {
        foreach (var command in commands)
            _battle.Submit(uint.Parse(command.Player.Value, CultureInfo.InvariantCulture), TinyInput.Decode(command.Payload));
        _battle.Tick();
    }

    private void Capture(FrameIndex frame)
    {
        if (!_rollback.CaptureAndStore(frame)) throw new InvalidOperationException("Could not capture Tiny frame.");
        _hashes.Store(frame, new WorldStateHash(_battle.ComputeHash()));
    }

    private static bool SameInputs(PlayerInputCommand[] a, PlayerInputCommand[] b)
    {
        if (a.Length != b.Length) return false;
        for (var i = 0; i < a.Length; i++)
            if (a[i].Player.Value != b[i].Player.Value ||
                !a[i].Payload.AsSpan().SequenceEqual(b[i].Payload))
                return false;
        return true;
    }
}
}
