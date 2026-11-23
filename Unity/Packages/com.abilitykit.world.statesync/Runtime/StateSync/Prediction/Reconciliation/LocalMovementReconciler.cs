using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.StateSync.Prediction.Reconciliation
{
    /// <summary>
    /// 进程内局部移动对账原型。调用方在单一执行线上提供已提交权威值、输入和覆盖证明；不接线会话或帧同步。
    /// </summary>
    internal sealed class LocalMovementReconciler<TState, TInput> where TState : unmanaged where TInput : unmanaged
    {
        internal const int DefaultReplayTickLimit = 32;
        internal const int DefaultPendingInputLimit = 64;

        private readonly TryMovementStep<TState, TInput> _step;
        private readonly List<PendingInput> _pending;
        private readonly long[] _coverage;
        private readonly TInput[] _stepInputs;
        private ReconciliationScope _scope;
        private CommittedMovement<TState>? _anchor;
        private TState? _prediction;
        private long _latestInput;
        private long _terminalSequence;
        private long _uncoveredSequence;
        private int _uncoveredTick = -1;
        private int _latestSealedTick = -1;
        private bool _waitingForFull;
        private bool _stepping;

        internal LocalMovementReconciler(ReconciliationScope scope, TryMovementStep<TState, TInput> step,
            int replayTickLimit = DefaultReplayTickLimit, int pendingInputLimit = DefaultPendingInputLimit)
        {
            if (replayTickLimit <= 0) throw new ArgumentOutOfRangeException(nameof(replayTickLimit));
            if (pendingInputLimit <= 0) throw new ArgumentOutOfRangeException(nameof(pendingInputLimit));

            _scope = scope;
            _step = step ?? throw new ArgumentNullException(nameof(step));
            _pending = new List<PendingInput>(pendingInputLimit);
            _coverage = new long[replayTickLimit];
            _stepInputs = new TInput[pendingInputLimit];
        }

        internal int PendingCount => _pending.Count;
        internal long TerminalSequence => _terminalSequence;
        internal long AcceptedRevision => _anchor?.Revision ?? 0;
        internal bool WaitingForFullBaseline => _waitingForFull;

        internal long GetPendingSequence(int index) => _pending[index].Sequence;

        internal bool TryGetAuthoritative(out TState state)
        {
            state = _anchor?.State ?? default;
            if (!_waitingForFull && _anchor.HasValue) return true;
            state = default;
            return false;
        }

        internal bool TryGetPrediction(out TState state)
        {
            state = _prediction.GetValueOrDefault();
            return _prediction.HasValue;
        }

        internal void SwitchScope(ReconciliationScope scope)
        {
            EnsureNotStepping();
            if (_scope.Matches(scope)) return;

            _scope = scope;
            _anchor = null;
            _prediction = null;
            _pending.Clear();
            Array.Clear(_coverage, 0, _coverage.Length);
            _latestInput = 0;
            _terminalSequence = 0;
            _uncoveredSequence = 0;
            _uncoveredTick = -1;
            _latestSealedTick = -1;
            _waitingForFull = false;
        }

        /// <summary>
        /// 由宿主报告当前权威基线不可信；不发送请求，也不废止同一输入代际的待终结记录。
        /// </summary>
        internal void InvalidateBaseline(ReconciliationScope scope)
        {
            EnsureNotStepping();
            if (!_scope.Matches(scope)) return;

            _waitingForFull = true;
            _prediction = null;
        }

        /// <summary>
        /// 记录正序号与计划 Tick。重复记录不覆盖已存值；无法安全记录的新输入使预测退出，不能伪装为空 Tick。
        /// </summary>
        internal bool TryRecordInput(ReconciliationScope scope, long sequence, int tick, TInput input)
        {
            EnsureNotStepping();
            if (!_scope.Matches(scope) || sequence <= _terminalSequence || tick < 0)
                return false;

            if (sequence <= _latestInput)
            {
                foreach (var pending in _pending)
                    if (pending.Sequence == sequence && pending.Tick == tick) return false;

                return RecordCoverageLoss(sequence, tick);
            }

            _latestInput = sequence;
            if (_pending.Count == _stepInputs.Length || IsSealed(tick) || IsOutsideCoverageWindow(tick))
                return RecordCoverageLoss(sequence, tick);

            _pending.Add(new PendingInput(sequence, tick, input));
            return true;
        }

        /// <summary>
        /// 宿主确认该 Tick 的本地输入采集已结束，空 Tick 也须显式封存；不会推断相邻 Tick 已完整。
        /// </summary>
        internal bool SealTick(ReconciliationScope scope, int tick)
        {
            EnsureNotStepping();
            if (!_scope.Matches(scope) || tick < 0 || IsOutsideCoverageWindow(tick)) return false;

            _latestSealedTick = Math.Max(tick, _latestSealedTick);
            _coverage[tick % _coverage.Length] = (long)tick + 1;
            return true;
        }

        /// <summary>
        /// 接收已提交的权威截面，再尝试重演到目标 Tick；预测失败不撤销权威锚点或可信的输入终结。
        /// </summary>
        internal ReconciliationResult Reconcile(in CommittedMovement<TState> sample, InputTerminalProof? terminalProof, int targetTick)
        {
            EnsureNotStepping();
            if (!_scope.Matches(sample.Scope) || sample.Revision <= AcceptedRevision || sample.Tick < 0 ||
                (_anchor.HasValue && sample.Tick < _anchor.Value.Tick))
                return new ReconciliationResult(ReconciliationStatus.Ignored);

            if (_waitingForFull && !sample.IsFullBaseline)
                return new ReconciliationResult(ReconciliationStatus.WaitingForFullBaseline);

            _waitingForFull = false;
            _anchor = sample;
            _prediction = null;

            if (!terminalProof.HasValue || !terminalProof.Value.Matches(_scope, sample.Revision, sample.Tick) ||
                terminalProof.Value.ThroughSequence < _terminalSequence)
                return new ReconciliationResult(ReconciliationStatus.InvalidTerminalProof);

            _terminalSequence = terminalProof.Value.ThroughSequence;
            TrimTerminalInputs();
            if (_uncoveredSequence > _terminalSequence || _uncoveredTick > sample.Tick)
                return new ReconciliationResult(ReconciliationStatus.UncoveredInput);

            _uncoveredSequence = 0;
            _uncoveredTick = -1;
            foreach (var input in _pending)
                if (input.Tick <= sample.Tick)
                    return new ReconciliationResult(ReconciliationStatus.PendingInputBeforeAnchor);

            var ticks = (long)targetTick - sample.Tick;
            if (ticks <= 0) return new ReconciliationResult(ReconciliationStatus.AuthoritativeOnly);
            if (ticks > _coverage.Length) return new ReconciliationResult(ReconciliationStatus.ReplayLimitExceeded);

            for (var offset = 1; offset <= ticks; offset++)
                if (!IsSealed(sample.Tick + offset))
                    return new ReconciliationResult(ReconciliationStatus.MissingTickCoverage);

            return Replay(sample.State, sample.Tick, (int)ticks);
        }

        private ReconciliationResult Replay(TState candidate, int anchorTick, int ticks)
        {
            var attempts = 0;
            _stepping = true;
            try
            {
                while (attempts < ticks)
                {
                    var tick = anchorTick + ++attempts;
                    var count = 0;
                    foreach (var input in _pending)
                        if (input.Tick == tick)
                            _stepInputs[count++] = input.Value;

                    if (!_step(tick, new ReadOnlySpan<TInput>(_stepInputs, 0, count), ref candidate))
                        return new ReconciliationResult(ReconciliationStatus.StepFailed, attempts);
                }
            }
            catch (Exception exception)
            {
                return new ReconciliationResult(ReconciliationStatus.StepFailed, attempts, exception);
            }
            finally
            {
                _stepping = false;
            }

            _prediction = candidate;
            return new ReconciliationResult(ReconciliationStatus.Predicted, attempts);
        }

        private void TrimTerminalInputs()
        {
            var count = 0;
            while (count < _pending.Count && _pending[count].Sequence <= _terminalSequence) count++;
            if (count > 0) _pending.RemoveRange(0, count);
        }

        private bool RecordCoverageLoss(long sequence, int tick)
        {
            _uncoveredSequence = Math.Max(_uncoveredSequence, sequence);
            _uncoveredTick = Math.Max(_uncoveredTick, tick);
            _prediction = null;
            return false;
        }

        private bool IsSealed(int tick) => !IsOutsideCoverageWindow(tick) &&
            _coverage[tick % _coverage.Length] == (long)tick + 1;

        private bool IsOutsideCoverageWindow(int tick) => _latestSealedTick - tick >= _coverage.Length;

        private void EnsureNotStepping()
        {
            if (_stepping) throw new InvalidOperationException("A movement step cannot mutate reconciliation state.");
        }

        /// <summary>
        /// 按输入序号追加的本地值命令；计划 Tick 不会在对账时改写。
        /// </summary>
        private readonly struct PendingInput
        {
            internal readonly long Sequence;
            internal readonly int Tick;
            internal readonly TInput Value;

            internal PendingInput(long sequence, int tick, TInput value)
            {
                Sequence = sequence;
                Tick = tick;
                Value = value;
            }
        }
    }
}
