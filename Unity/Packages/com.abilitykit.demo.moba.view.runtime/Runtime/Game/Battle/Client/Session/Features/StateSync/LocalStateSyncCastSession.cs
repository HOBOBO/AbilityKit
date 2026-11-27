using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Input;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 离线施法的会话与角色生命周期；不从移动证明、事件水位或帧同步标识推导。
    /// </summary>
    internal readonly struct LocalStateSyncCastScope
    {
        internal readonly int Generation, ActorId, EntityVersion;
        internal readonly ulong WorldId, PlayerId;
        internal readonly Guid Stream, InputGeneration;

        internal LocalStateSyncCastScope(int generation, ulong worldId, Guid stream, Guid inputGeneration,
            ulong playerId, int actorId, int entityVersion)
        {
            if (generation <= 0 || worldId == 0 || stream == Guid.Empty || inputGeneration == Guid.Empty ||
                playerId == 0 || actorId <= 0 || entityVersion <= 0)
                throw new ArgumentException("本地施法需要完整的会话和角色身份。");
            Generation = generation;
            WorldId = worldId;
            Stream = stream;
            InputGeneration = inputGeneration;
            PlayerId = playerId;
            ActorId = actorId;
            EntityVersion = entityVersion;
        }

        internal bool Matches(in LocalStateSyncCastScope other) => Generation == other.Generation &&
            WorldId == other.WorldId && Stream == other.Stream && InputGeneration == other.InputGeneration &&
            PlayerId == other.PlayerId && ActorId == other.ActorId && EntityVersion == other.EntityVersion;
    }

    /// <summary>
    /// 锚定到同一次权威提交，修订仅用于绑定，不代表输入终结。
    /// </summary>
    internal readonly struct LocalStateSyncCastStamp
    {
        internal readonly LocalStateSyncCastScope Scope;
        internal readonly int Tick;
        internal readonly long Revision;

        internal LocalStateSyncCastStamp(in LocalStateSyncCastScope scope, int tick, long revision)
        {
            Scope = scope;
            Tick = tick;
            Revision = revision;
        }

        internal bool Matches(in LocalStateSyncCastStamp other) => Tick == other.Tick &&
            Revision == other.Revision && Scope.Matches(in other.Scope);
    }

    /// <summary>
    /// 配方的类型化施法截面与权威观测；不得包含活动世界或管线引用。
    /// </summary>
    internal readonly struct LocalStateSyncCastAnchor<TAnchor, TObservation>
    {
        internal readonly LocalStateSyncCastStamp Stamp;
        internal readonly TAnchor State;
        internal readonly TObservation Observation;

        internal LocalStateSyncCastAnchor(in LocalStateSyncCastStamp stamp, TAnchor state, TObservation observation)
        {
            Stamp = stamp;
            State = state;
            Observation = observation;
        }
    }

    /// <summary>
    /// 离线权威端对已结算的连续输入前缀出具的证明，不包含逐条施法结果。
    /// </summary>
    internal readonly struct LocalStateSyncCastTerminalProof
    {
        internal readonly LocalStateSyncCastStamp Stamp;
        internal readonly long Through;

        internal LocalStateSyncCastTerminalProof(in LocalStateSyncCastStamp stamp, long through)
        {
            Stamp = stamp;
            Through = through;
        }
    }

    /// <summary>
    /// 隔离的玩法执行域；每个工厂实例独立拥有状态，返回脱离运行对象的观测。
    /// </summary>
    internal interface ILocalStateSyncCastWorld<TAnchor, TObservation> : IDisposable
    {
        void Restore(TAnchor anchor);
        bool TryStep(int tick, SkillInputEvent? input, out string rejection);
        TObservation Read();
    }

    internal enum LocalStateSyncCastResult { Ignored, AuthorityOnly, CandidateFailed, Published }

    internal interface ILocalStateSyncMovementWorld
    {
        bool TryStepMovement(int tick, in MobaActorIntent intent, out string rejection);
    }

    /// <summary>
    /// 宿主挂起候选时保留同代输入历史，不接触领域截面。
    /// </summary>
    internal interface ILocalStateSyncCastSession : IDisposable
    {
        void Suspend();
    }

    /// <summary>
    /// 每 Tick 封存一条无附载的类型化 Press、可选普通移动或显式空 Tick；候选追平后才接管。
    /// 失败只保留已采用的权威观测，不输出半程候选，也不重发游戏输入。
    /// </summary>
    internal sealed class LocalStateSyncCastSession<TAnchor, TObservation> : ILocalStateSyncCastSession
    {
        private const int HistoryLimit = 32;
        private const int PendingLimit = 64;
        private readonly Func<ILocalStateSyncCastWorld<TAnchor, TObservation>> _create;
        private readonly SortedDictionary<int, (long? Sequence, SkillInputEvent? Press, MobaActorIntent? Move)> _history = new();
        private readonly List<(int Tick, long Sequence)> _pending = new();
        private ILocalStateSyncCastWorld<TAnchor, TObservation> _published;
        private TObservation _authority;
        private TObservation _observation;
        private bool _disposed;
        private long _revision;
        private long _lastInput;

        internal LocalStateSyncCastSession(in LocalStateSyncCastScope scope,
            Func<ILocalStateSyncCastWorld<TAnchor, TObservation>> create, bool movementEnabled = false, int projectileMask = 0)
        {
            Scope = scope;
            _create = create ?? throw new ArgumentNullException(nameof(create));
            MovementEnabled = movementEnabled;
            ProjectileMask = projectileMask;
        }

        internal LocalStateSyncCastScope Scope { get; }
        internal bool MovementEnabled { get; }
        internal int ProjectileMask { get; }
        internal int AuthorityTick { get; private set; }
        internal int ClockTick { get; private set; }
        internal int PublishedTick => _published != null ? ClockTick : AuthorityTick;
        internal int PendingCount => _pending.Count;
        internal long TerminalThrough { get; private set; }
        internal bool HasPrediction => _published != null;
        internal TObservation Observation => _observation;
        internal string LastInputRejection { get; private set; }
        internal Exception LastCandidateFailure { get; private set; }
        internal IReadOnlyList<(int Tick, long? Input, TObservation State, string Rejection)> LastReplay { get; private set; } =
            Array.Empty<(int, long?, TObservation, string)>();

        internal bool Advance(int tick, SkillInputEvent? newInput)
        {
            if (newInput is { } command)
            {
                if (command.Phase != SkillInputPhase.Press || command.Slot <= 0 || command.TargetFrame != tick ||
                    command.OpCode != 0 || command.Payload != null)
                    return false;
            }
            return AdvanceCore(tick, (newInput?.InputSequence, newInput, null));
        }

        internal bool AdvanceMovement(int tick, long sequence, float dx, float dz)
        {
            var intent = MobaActorIntent.MoveDirection(dx, dz);
            return MovementEnabled && intent.IsValid() && AdvanceCore(tick, (sequence, null, intent));
        }

        private bool AdvanceCore(int tick, (long? Sequence, SkillInputEvent? Press, MobaActorIntent? Move) input)
        {
            if (_disposed || _revision == 0 || tick <= ClockTick || tick - ClockTick != 1) return false;
            if (input.Sequence is { } sequence)
            {
                if (sequence <= 0 || sequence != _lastInput + 1 || _pending.Count >= PendingLimit) return false;
                _pending.Add((tick, sequence));
                _lastInput = sequence;
            }
            _history.Add(tick, input);
            _history.Remove(tick - HistoryLimit);
            ClockTick = tick;
            LastInputRejection = null;
            if (_published == null) return true;
            try
            {
                if (TryStep(_published, tick, input.Press, input.Move, out var rejection))
                {
                    _observation = _published.Read();
                    LastInputRejection = rejection;
                    return true;
                }
                LastCandidateFailure = new InvalidOperationException($"已发布施法在 Tick {tick} 推进失败。");
            }
            catch (Exception failure) { LastCandidateFailure = failure; }
            Suspend();
            return true;
        }

        internal LocalStateSyncCastResult Adopt(in LocalStateSyncCastAnchor<TAnchor, TObservation> anchor,
            LocalStateSyncCastTerminalProof? terminal)
        {
            var stamp = anchor.Stamp;
            if (_disposed || !Scope.Matches(in stamp.Scope) || stamp.Revision <= _revision || stamp.Tick < AuthorityTick)
                return LocalStateSyncCastResult.Ignored;

            var firstCommit = _revision == 0;
            _revision = stamp.Revision;
            AuthorityTick = stamp.Tick;
            ClockTick = Math.Max(ClockTick, AuthorityTick);
            _authority = anchor.Observation;
            LastCandidateFailure = null;
            var retired = Suspend();
            if (terminal is not { } proof || !stamp.Matches(in proof.Stamp) ||
                proof.Through < TerminalThrough || proof.Through < 0 || (!firstCommit && proof.Through > _lastInput) ||
                _pending.Exists(input => input.Sequence <= proof.Through && input.Tick > stamp.Tick))
                return LocalStateSyncCastResult.AuthorityOnly;

            TerminalThrough = proof.Through;
            _lastInput = Math.Max(_lastInput, TerminalThrough);
            _pending.RemoveAll(input => input.Sequence <= TerminalThrough);
            if (!retired) return LocalStateSyncCastResult.CandidateFailed;
            var replayTicks = ClockTick - AuthorityTick;
            if (replayTicks > HistoryLimit || _pending.Exists(input => input.Tick <= stamp.Tick))
                return LocalStateSyncCastResult.AuthorityOnly;
            for (var offset = 1; offset <= replayTicks; offset++)
                if (!_history.ContainsKey(AuthorityTick + offset)) return LocalStateSyncCastResult.AuthorityOnly;

            ILocalStateSyncCastWorld<TAnchor, TObservation> candidate = null;
            try
            {
                candidate = _create();
                candidate.Restore(anchor.State);
                var steps = new (int Tick, long? Input, TObservation State, string Rejection)[replayTicks];
                for (var offset = 0; offset < replayTicks; offset++)
                {
                    var tick = AuthorityTick + offset + 1;
                    var input = _history[tick];
                    if (input.Sequence <= TerminalThrough) input = default;
                    if (!TryStep(candidate, tick, input.Press, input.Move, out var rejection))
                        throw new InvalidOperationException($"施法候选在 Tick {tick} 追平失败。");
                    steps[offset] = (tick, input.Sequence, candidate.Read(), rejection);
                }
                var observation = candidate.Read();
                _published = candidate;
                candidate = null;
                _observation = observation;
                LastReplay = steps;
                return LocalStateSyncCastResult.Published;
            }
            catch (Exception failure)
            {
                LastCandidateFailure = failure;
                return LocalStateSyncCastResult.CandidateFailed;
            }
            finally { TryRelease(candidate); }
        }

        private static bool TryStep(ILocalStateSyncCastWorld<TAnchor, TObservation> world, int tick,
            SkillInputEvent? press, MobaActorIntent? move, out string rejection)
        {
            rejection = null;
            return move is { } direction
                ? world is ILocalStateSyncMovementWorld movement && movement.TryStepMovement(tick, in direction, out rejection)
                : world.TryStep(tick, press, out rejection);
        }

        void ILocalStateSyncCastSession.Suspend() => Suspend();

        internal bool Suspend()
        {
            var previous = _published;
            _published = null;
            _observation = _authority;
            LastInputRejection = null;
            LastReplay = Array.Empty<(int, long?, TObservation, string)>();
            return TryRelease(previous);
        }

        private bool TryRelease(ILocalStateSyncCastWorld<TAnchor, TObservation> candidate)
        {
            try { candidate?.Dispose(); }
            catch (Exception failure)
            {
                LastCandidateFailure = LastCandidateFailure == null
                    ? failure
                    : new AggregateException(LastCandidateFailure, failure);
                return false;
            }
            return true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _history.Clear();
            _pending.Clear();
            _authority = default;
            if (!Suspend())
                throw new InvalidOperationException("结束施法会话时释放已发布候选失败。", LastCandidateFailure);
        }
    }
}
