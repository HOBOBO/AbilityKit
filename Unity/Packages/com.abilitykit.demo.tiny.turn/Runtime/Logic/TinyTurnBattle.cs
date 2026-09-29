using System;
using System.Buffers.Binary;

namespace AbilityKit.Demo.Tiny.Turn
{

public readonly struct TinyTurnState
{
    public TinyTurnState(int frame, int turn, uint currentPlayerId,
        int playerOneHp, int playerTwoHp, uint winnerId)
    {
        Frame = frame;
        Turn = turn;
        CurrentPlayerId = currentPlayerId;
        PlayerOneHp = playerOneHp;
        PlayerTwoHp = playerTwoHp;
        WinnerId = winnerId;
    }

    public int Frame { get; }
    public int Turn { get; }
    public uint CurrentPlayerId { get; }
    public int PlayerOneHp { get; }
    public int PlayerTwoHp { get; }
    public uint WinnerId { get; }
}

public sealed class TinyTurnBattle
{
    public const string AssetKey = "tiny:turn";
    public const string RulesKey = "tiny:turn.rules.v1";
    public const string RoomType = "tiny-turn";
    public const string WorldType = "tiny-turn-battle";
    public const string StateTemplate = "tiny-turn-state-authority";
    public const int TickRate = 10;
    public const int InputOpCode = 2;
    public const int SnapshotOpCode = 31002;
    public const int MaxHp = 2;
    private uint _pendingPlayerId;

    public int Frame { get; private set; }
    public int Turn { get; private set; }
    public uint CurrentPlayerId { get; private set; } = 1;
    public int PlayerOneHp { get; private set; } = MaxHp;
    public int PlayerTwoHp { get; private set; } = MaxHp;
    public uint WinnerId { get; private set; }

    public bool CanSubmit(uint playerId) => WinnerId == 0 &&
        playerId == CurrentPlayerId && _pendingPlayerId == 0;

    public bool Submit(uint playerId, ReadOnlySpan<byte> payload)
    {
        if (!CanSubmit(playerId) || payload.Length != 1 || payload[0] != 1) return false;
        _pendingPlayerId = playerId;
        return true;
    }

    public void Tick()
    {
        Frame++;
        if (_pendingPlayerId == 0) return;
        if (_pendingPlayerId == 1)
        {
            PlayerTwoHp--;
            if (PlayerTwoHp == 0) WinnerId = 1;
        }
        else
        {
            PlayerOneHp--;
            if (PlayerOneHp == 0) WinnerId = 2;
        }
        Turn++;
        CurrentPlayerId = WinnerId == 0 ? (_pendingPlayerId == 1 ? 2u : 1u) : 0;
        _pendingPlayerId = 0;
    }

    public TinyTurnState CaptureState() => new(
        Frame, Turn, CurrentPlayerId, PlayerOneHp, PlayerTwoHp, WinnerId);

    public void RestoreState(TinyTurnState state)
    {
        if (state.Frame < 0 || state.Turn < 0 || state.Turn > state.Frame ||
            state.PlayerOneHp < 0 || state.PlayerOneHp > MaxHp ||
            state.PlayerTwoHp < 0 || state.PlayerTwoHp > MaxHp ||
            state.WinnerId > 2 ||
            (state.WinnerId == 0 && state.CurrentPlayerId != 1 && state.CurrentPlayerId != 2) ||
            (state.WinnerId != 0 && state.CurrentPlayerId != 0))
            throw new ArgumentException("Invalid Tiny turn state.", nameof(state));
        Frame = state.Frame;
        Turn = state.Turn;
        CurrentPlayerId = state.CurrentPlayerId;
        PlayerOneHp = state.PlayerOneHp;
        PlayerTwoHp = state.PlayerTwoHp;
        WinnerId = state.WinnerId;
        _pendingPlayerId = 0;
    }

    public uint ComputeHash()
    {
        var state = CaptureState();
        var hash = 2166136261u;
        unchecked
        {
            foreach (var field in new uint[] { (uint)state.Frame, (uint)state.Turn,
                         state.CurrentPlayerId, (uint)state.PlayerOneHp,
                         (uint)state.PlayerTwoHp, state.WinnerId })
                hash = (hash ^ field) * 16777619u;
        }
        return hash;
    }
}

public static class TinyTurnStateCodec
{
    public static byte[] Encode(TinyTurnState state)
    {
        var bytes = new byte[24];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0), state.Frame);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), state.Turn);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), state.CurrentPlayerId);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), state.PlayerOneHp);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), state.PlayerTwoHp);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20), state.WinnerId);
        return bytes;
    }

    public static TinyTurnState Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 24) throw new ArgumentException("Invalid Tiny turn snapshot.", nameof(bytes));
        var state = new TinyTurnState(
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(0, 4)),
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(4, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(8, 4)),
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(12, 4)),
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(16, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(20, 4)));
        new TinyTurnBattle().RestoreState(state);
        return state;
    }
}
}
