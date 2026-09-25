using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;

namespace AbilityKit.Demo.Tiny.FrameSync
{

public sealed class TinyBattleRollbackProvider : IRollbackStateProvider, IRollbackStatePreflightProvider
{
    private readonly TinyBattle _battle;

    public TinyBattleRollbackProvider(TinyBattle battle) => _battle = battle;
    public int Key => 1;

    public byte[] Export(FrameIndex frame)
    {
        var state = _battle.CaptureState();
        if (state.Frame != frame.Value) throw new InvalidOperationException("Tiny frame and rollback frame differ.");
        return TinyBattleStateCodec.Encode(state);
    }

    public void ValidateImport(FrameIndex frame, byte[] payload) => Decode(frame, payload);

    public void Import(FrameIndex frame, byte[] payload) => _battle.RestoreState(Decode(frame, payload));

    private static TinyBattleState Decode(FrameIndex frame, byte[] payload)
    {
        var state = TinyBattleStateCodec.Decode(payload);
        if (state.Frame != frame.Value)
            throw new ArgumentException("Tiny rollback frame does not match the payload.", nameof(payload));
        return state;
    }
}
}
