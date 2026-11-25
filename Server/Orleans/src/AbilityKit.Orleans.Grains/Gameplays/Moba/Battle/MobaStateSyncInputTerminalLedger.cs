using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Motion;

namespace AbilityKit.Orleans.Grains.Gameplays.Moba.Battle;

/// <summary>
/// 本次权威世界运行的输入终结事实。仅接收已完成 Tick 中观察到的派发结果，
/// 不使用网关入队回执，不把最大命令序号当作连续证明。
/// </summary>
internal sealed class MobaStateSyncInputTerminalLedger
{
    private const int MaximumPendingTerminals = 64;
    private readonly Dictionary<uint, PlayerTerminals> _players = new();

    internal void ObserveCommitted(uint player, int actorId, int lifetime, Guid sourceConnectionGeneration, ulong sequence,
        MobaFrameInputDisposition disposition, Guid inputGeneration = default)
    {
        if (player == 0) return;
        if (actorId <= 0 || lifetime <= 0)
        {
            Invalidate(player);
            return;
        }
        if (!_players.TryGetValue(player, out var state))
            _players.Add(player, state = new PlayerTerminals(actorId, lifetime, sourceConnectionGeneration, inputGeneration));

        if (state.ActorId != actorId || state.Lifetime != lifetime || sourceConnectionGeneration == Guid.Empty ||
            state.SourceConnectionGeneration != sourceConnectionGeneration || state.InputGeneration != inputGeneration ||
            sequence == 0 || sequence > long.MaxValue)
        {
            state.Invalid = true;
            return;
        }

        if (state.Invalid) return;
        if (sequence <= state.Through || state.BeyondPrefix.Contains(sequence))
        {
            if (disposition != MobaFrameInputDisposition.Duplicate) state.Invalid = true;
            return;
        }
        if (disposition is not (MobaFrameInputDisposition.Executed or MobaFrameInputDisposition.Rejected))
        {
            state.Invalid = true;
            return;
        }

        if (sequence == state.Through + 1)
        {
            state.Through = sequence;
            while (state.BeyondPrefix.Remove(state.Through + 1)) state.Through++;
        }
        else if (state.BeyondPrefix.Count >= MaximumPendingTerminals)
        {
            state.Invalid = true;
        }
        else
        {
            state.BeyondPrefix.Add(sequence);
        }
    }

    internal void Invalidate(uint player)
    {
        if (player == 0) return;
        if (!_players.TryGetValue(player, out var state))
            _players.Add(player, state = new PlayerTerminals(0, 0, Guid.Empty, Guid.Empty));
        state.Invalid = true;
    }

    internal bool TryRead(uint player, int actorId, int lifetime, Guid expectedSourceConnectionGeneration,
        out long terminalThrough, Guid expectedInputGeneration = default)
    {
        terminalThrough = 0;
        if (player == 0 || actorId <= 0 || lifetime <= 0 || expectedSourceConnectionGeneration == Guid.Empty ||
            !_players.TryGetValue(player, out var state)) return false;
        if (state.Invalid || state.ActorId != actorId || state.Lifetime != lifetime ||
            state.SourceConnectionGeneration != expectedSourceConnectionGeneration ||
            state.InputGeneration != expectedInputGeneration || state.BeyondPrefix.Count != 0)
            return false;

        terminalThrough = (long)state.Through;
        return true;
    }

    private sealed class PlayerTerminals(int actorId, int lifetime, Guid sourceConnectionGeneration, Guid inputGeneration)
    {
        internal readonly int ActorId = actorId;
        internal readonly int Lifetime = lifetime;
        internal readonly Guid SourceConnectionGeneration = sourceConnectionGeneration;
        internal readonly Guid InputGeneration = inputGeneration;
        internal readonly HashSet<ulong> BeyondPrefix = new();
        internal ulong Through;
        internal bool Invalid;
    }
}

/// <summary>
/// 同一次逻辑世界提交的移动值与输入事实。它不是客户端消费的快照证明，
/// 只有绑定下发会话、流代际、修订和输入代际后才可用于裁剪历史。
/// </summary>
internal readonly record struct MobaCommittedMovementSample(string BattleId, ulong WorldId, int Tick,
    uint PlayerId, MobaAuthoritativeMovementAnchor Anchor, Guid SourceConnectionGeneration, long? TerminalThrough,
    Guid InputGeneration = default);
