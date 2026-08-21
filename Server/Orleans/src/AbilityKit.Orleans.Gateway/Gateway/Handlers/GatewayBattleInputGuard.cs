using AbilityKit.Orleans.Contracts.Battle;

namespace AbilityKit.Orleans.Gateway.Handlers;

public sealed class GatewayBattleInputGuard
{
    internal const int MaxPayloadBytes = 4096;
    internal const int MaxOpCode = 65535;
    internal const int ReplayWindowSize = 128;
    internal const int InputsPerSecond = 60;
    internal const int BurstInputs = 90;
    internal const int MaxTrackedKeys = 4096;
    internal static readonly long IdleStateTtlTicks = TimeSpan.FromMinutes(5).Ticks;

    private readonly BattleInputSecurityOptions _options;
    private readonly long _idleStateTtlTicks;
    private readonly Dictionary<InputKey, InputState> _states = new();
    private readonly object _sync = new();

    public GatewayBattleInputGuard(BattleInputSecurityOptions? options = null)
    {
        _options = (options ?? new BattleInputSecurityOptions()).Snapshot();
        var failures = BattleInputSecurityOptions.GetValidationFailures(_options);
        if (failures.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", failures), nameof(options));
        }

        _idleStateTtlTicks = TimeSpan.FromSeconds(_options.GatewayIdleStateTtlSeconds).Ticks;
    }

    internal BattleInputSecurityOptions Options => _options.Snapshot();

    internal int TrackedKeyCount
    {
        get
        {
            lock (_sync) return _states.Count;
        }
    }

    internal GatewayBattleInputGuardResult Check(
        string sessionToken,
        string battleId,
        uint playerId,
        ulong sequence,
        long nowTicks,
        GatewayBattleInputLane lane = GatewayBattleInputLane.BattleCommand)
    {
        lock (_sync)
        {
            var key = new InputKey(sessionToken, battleId, playerId, lane);
            if (!_states.TryGetValue(key, out var state))
            {
                if (_states.Count >= _options.MaxGatewayTrackedKeys)
                {
                    RemoveExpiredStates(nowTicks);
                    if (_states.Count >= _options.MaxGatewayTrackedKeys)
                    {
                        return GatewayBattleInputGuardResult.RateLimited;
                    }
                }

                state = new InputState(_options.BurstInputs, nowTicks);
                _states.Add(key, state);
            }

            state.LastSeenTicks = Math.Max(state.LastSeenTicks, nowTicks);
            if (sequence != 0)
            {
                if (state.Sequences.Contains(sequence))
                {
                    return GatewayBattleInputGuardResult.Duplicate;
                }

                if (state.HighestSequence > 0
                    && sequence < state.HighestSequence
                    && state.HighestSequence - sequence >= (ulong)_options.ReplayWindowSize)
                {
                    return GatewayBattleInputGuardResult.TooOld;
                }
            }

            if (!TryConsumeToken(state, nowTicks))
            {
                return GatewayBattleInputGuardResult.RateLimited;
            }

            return sequence == 0
                ? GatewayBattleInputGuardResult.AcceptedLegacy
                : GatewayBattleInputGuardResult.Accepted;
        }
    }

    internal void RecordAccepted(string sessionToken, string battleId, uint playerId, ulong sequence)
    {
        if (sequence == 0) return;

        lock (_sync)
        {
            if (!_states.TryGetValue(
                    new InputKey(sessionToken, battleId, playerId, GatewayBattleInputLane.BattleCommand),
                    out var state)) return;

            state.Sequences.Add(sequence);
            if (sequence > state.HighestSequence)
            {
                state.HighestSequence = sequence;
            }

            var minimum = state.HighestSequence >= (ulong)_options.ReplayWindowSize
                ? state.HighestSequence - (ulong)_options.ReplayWindowSize + 1
                : 1;
            state.Sequences.RemoveWhere(value => value < minimum);
        }
    }

    internal int GetTrackedSequenceCount(string sessionToken, string battleId, uint playerId)
    {
        lock (_sync)
        {
            return _states.TryGetValue(
                    new InputKey(sessionToken, battleId, playerId, GatewayBattleInputLane.BattleCommand),
                    out var state)
                ? state.Sequences.Count
                : 0;
        }
    }

    private void RemoveExpiredStates(long nowTicks)
    {
        var minimumLastSeenTicks = nowTicks - _idleStateTtlTicks;
        foreach (var key in _states
                     .Where(entry => entry.Value.LastSeenTicks < minimumLastSeenTicks)
                     .Select(entry => entry.Key)
                     .ToArray())
        {
            _states.Remove(key);
        }
    }

    private bool TryConsumeToken(InputState state, long nowTicks)
    {
        var elapsedTicks = Math.Max(0, nowTicks - state.LastRefillTicks);
        state.LastRefillTicks = nowTicks;
        state.Tokens = Math.Min(
            _options.BurstInputs,
            state.Tokens + elapsedTicks * (double)_options.InputsPerSecond / TimeSpan.TicksPerSecond);
        if (state.Tokens < 1d)
        {
            return false;
        }

        state.Tokens -= 1d;
        return true;
    }

    private readonly record struct InputKey(
        string SessionToken,
        string BattleId,
        uint PlayerId,
        GatewayBattleInputLane Lane);

    private sealed class InputState
    {
        public InputState(double tokens, long nowTicks)
        {
            Tokens = tokens;
            LastRefillTicks = nowTicks;
            LastSeenTicks = nowTicks;
        }

        public HashSet<ulong> Sequences { get; } = new();
        public ulong HighestSequence { get; set; }
        public double Tokens { get; set; }
        public long LastRefillTicks { get; set; }
        public long LastSeenTicks { get; set; }
    }
}

internal enum GatewayBattleInputLane
{
    BattleCommand,
    FrameInput
}

internal enum GatewayBattleInputGuardResult
{
    Accepted,
    AcceptedLegacy,
    Duplicate,
    TooOld,
    RateLimited
}
