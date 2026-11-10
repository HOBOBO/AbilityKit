using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.Services.StateMachine
{
    public enum MobaCharacterModeRequestKind
    {
        Enter = 0,
        Exit = 1,
        Interrupt = 2,
        Reset = 3
    }

    public enum MobaCharacterModeTransitionReason
    {
        None = 0,
        Entered = 1,
        ExitRequested = 2,
        Interrupted = 3,
        Replaced = 4,
        Death = 5,
        Controlled = 6,
        Rollback = 7
    }

    /// <summary>
    /// Shared priority vocabulary for mode arbitration. Values are deliberately
    /// sparse so new mode classes can be inserted without renumbering existing
    /// authored data.
    /// </summary>
    public static class MobaCharacterModePriority
    {
        public const int Normal = 0;
        public const int Action = 10;
        public const int ForcedMovement = 20;
        public const int SpecialMode = 30;
        public const int HardControl = 40;
        public const int Dead = 100;

        public static bool CanReplace(int requestedPriority, int activePriority) =>
            requestedPriority >= activePriority;
    }

    public readonly struct MobaCharacterModeRequest
    {
        public readonly MobaCharacterModeRequestKind Kind;
        public readonly string ModeId;
        public readonly int Priority;
        public readonly long OwnerInstanceId;
        public readonly int RequestedFrame;
        public readonly MobaCharacterModeTransitionReason Reason;

        public MobaCharacterModeRequest(MobaCharacterModeRequestKind kind, string modeId,
            int priority, long ownerInstanceId, int requestedFrame,
            MobaCharacterModeTransitionReason reason)
        {
            Kind = kind;
            ModeId = modeId ?? string.Empty;
            Priority = priority;
            OwnerInstanceId = ownerInstanceId;
            RequestedFrame = requestedFrame;
            Reason = reason;
        }
    }

    public readonly struct MobaCharacterModeState
    {
        public const string NormalModeId = "normal";

        public readonly string ModeId;
        public readonly long InstanceId;
        public readonly long OwnerInstanceId;
        public readonly int Priority;
        public readonly int StartFrame;
        public readonly int LocalFrame;
        public readonly MobaCharacterModeTransitionReason TransitionReason;

        public MobaCharacterModeState(string modeId, long instanceId, long ownerInstanceId,
            int priority, int startFrame, int localFrame,
            MobaCharacterModeTransitionReason transitionReason)
        {
            ModeId = modeId ?? string.Empty;
            InstanceId = instanceId;
            OwnerInstanceId = ownerInstanceId;
            Priority = priority;
            StartFrame = startFrame;
            LocalFrame = localFrame;
            TransitionReason = transitionReason;
        }

        public bool IsActive => !string.IsNullOrEmpty(ModeId);

        public static MobaCharacterModeState Normal(int startFrame, long instanceId = 0L) =>
            new MobaCharacterModeState(NormalModeId, instanceId, 0L, 0, startFrame, 0,
                MobaCharacterModeTransitionReason.Entered);
    }

    public sealed class MobaCharacterModeSnapshot
    {
        public MobaCharacterModeState Current;
        public int Frame;
        public long NextInstanceSequence;
        // Requests are part of the deterministic cursor. Older snapshots may
        // leave this null; that is equivalent to an empty queue.
        public MobaCharacterModeRequest[] PendingRequests;
    }

    /// <summary>
    /// Deterministic, rollback-friendly character mode domain.
    /// It is intentionally independent from the life/action HFSM machine until
    /// the shared HFSM definition supports orthogonal regions.
    /// </summary>
    public sealed class MobaCharacterModeRuntime
    {
        private readonly int _actorId;
        private readonly List<MobaCharacterModeRequest> _pending =
            new List<MobaCharacterModeRequest>();
        private MobaCharacterModeState _current;
        private int _frame;
        private long _nextInstanceSequence;

        public MobaCharacterModeRuntime(int actorId, int frame)
        {
            if (actorId <= 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (frame < 0) throw new ArgumentOutOfRangeException(nameof(frame));
            _actorId = actorId;
            _frame = frame;
            _nextInstanceSequence = 1;
            _current = MobaCharacterModeState.Normal(frame, NextInstanceId());
        }

        public int Frame => _frame;
        public MobaCharacterModeState Current => _current;

        public bool RequestEnter(string modeId, int priority, long ownerInstanceId = 0L,
            int requestedFrame = -1)
        {
            if (!IsValidModeId(modeId) || priority < 0 || ownerInstanceId < 0) return false;
            return Enqueue(new MobaCharacterModeRequest(
                MobaCharacterModeRequestKind.Enter, modeId, priority, ownerInstanceId,
                ResolveRequestedFrame(requestedFrame), MobaCharacterModeTransitionReason.Entered));
        }

        public bool RequestExit(string modeId, long ownerInstanceId = 0L,
            int requestedFrame = -1,
            MobaCharacterModeTransitionReason reason = MobaCharacterModeTransitionReason.ExitRequested)
        {
            if (!IsValidModeId(modeId) || ownerInstanceId < 0) return false;
            if (reason == MobaCharacterModeTransitionReason.None ||
                reason == MobaCharacterModeTransitionReason.Entered)
                reason = MobaCharacterModeTransitionReason.ExitRequested;
            return Enqueue(new MobaCharacterModeRequest(
                MobaCharacterModeRequestKind.Exit, modeId, int.MaxValue, ownerInstanceId,
                ResolveRequestedFrame(requestedFrame), reason));
        }

        public bool RequestInterrupt(string modeId, long ownerInstanceId = 0L,
            int requestedFrame = -1,
            MobaCharacterModeTransitionReason reason = MobaCharacterModeTransitionReason.Interrupted)
        {
            if (!IsValidModeId(modeId) || ownerInstanceId < 0) return false;
            if (reason == MobaCharacterModeTransitionReason.None ||
                reason == MobaCharacterModeTransitionReason.Entered)
                reason = MobaCharacterModeTransitionReason.Interrupted;
            return Enqueue(new MobaCharacterModeRequest(
                MobaCharacterModeRequestKind.Interrupt, modeId, int.MaxValue, ownerInstanceId,
                ResolveRequestedFrame(requestedFrame), reason));
        }

        public bool RequestResetToNormal(int requestedFrame,
            MobaCharacterModeTransitionReason reason)
        {
            if (reason == MobaCharacterModeTransitionReason.None ||
                reason == MobaCharacterModeTransitionReason.Entered)
                return false;
            return Enqueue(new MobaCharacterModeRequest(
                MobaCharacterModeRequestKind.Reset, MobaCharacterModeState.NormalModeId,
                int.MaxValue, 0L, ResolveRequestedFrame(requestedFrame), reason));
        }

        public void Advance(int frame)
        {
            if (frame < _frame) throw new ArgumentOutOfRangeException(nameof(frame));

            _pending.Sort(CompareRequests);
            for (var i = 0; i < _pending.Count; i++)
            {
                var request = _pending[i];
                if (request.RequestedFrame > frame) break;
                Apply(request, request.RequestedFrame);
            }
            _pending.RemoveAll(request => request.RequestedFrame <= frame);
            _frame = frame;
            _current = new MobaCharacterModeState(_current.ModeId, _current.InstanceId,
                _current.OwnerInstanceId, _current.Priority, _current.StartFrame,
                checked(frame - _current.StartFrame), _current.TransitionReason);
        }

        public MobaCharacterModeSnapshot CaptureSnapshot() => new MobaCharacterModeSnapshot
        {
            Current = _current,
            Frame = _frame,
            NextInstanceSequence = _nextInstanceSequence,
            PendingRequests = _pending.ToArray()
        };

        public void RestoreSnapshot(MobaCharacterModeSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            ValidateSnapshot(snapshot);
            _pending.Clear();
            if (snapshot.PendingRequests != null)
                _pending.AddRange(snapshot.PendingRequests);
            _current = snapshot.Current;
            _frame = snapshot.Frame;
            _nextInstanceSequence = snapshot.NextInstanceSequence;
        }

        private bool Enqueue(MobaCharacterModeRequest request)
        {
            if (request.RequestedFrame < _frame) return false;
            _pending.Add(request);
            return true;
        }

        private int ResolveRequestedFrame(int requestedFrame) =>
            requestedFrame < 0 ? checked(_frame + 1) : requestedFrame;

        private void Apply(MobaCharacterModeRequest request, int frame)
        {
            if (request.Kind == MobaCharacterModeRequestKind.Reset)
            {
                if (!string.Equals(_current.ModeId, MobaCharacterModeState.NormalModeId,
                        StringComparison.Ordinal))
                    TransitionTo(MobaCharacterModeState.NormalModeId,
                        MobaCharacterModePriority.Normal, 0L, frame, request.Reason);
                return;
            }

            if (request.Kind == MobaCharacterModeRequestKind.Enter)
            {
                if (string.Equals(request.ModeId, _current.ModeId, StringComparison.Ordinal) &&
                    request.OwnerInstanceId == _current.OwnerInstanceId) return;
                if (!MobaCharacterModePriority.CanReplace(request.Priority, _current.Priority)) return;
                TransitionTo(request.ModeId, request.Priority, request.OwnerInstanceId, frame,
                    _current.IsActive ? MobaCharacterModeTransitionReason.Replaced : request.Reason);
                return;
            }

            if (!string.Equals(request.ModeId, _current.ModeId, StringComparison.Ordinal) ||
                (request.OwnerInstanceId != 0L && request.OwnerInstanceId != _current.OwnerInstanceId))
                return;

            TransitionTo(MobaCharacterModeState.NormalModeId, 0, 0L, frame, request.Reason);
        }

        private void TransitionTo(string modeId, int priority, long ownerInstanceId, int frame,
            MobaCharacterModeTransitionReason reason)
        {
            _current = new MobaCharacterModeState(modeId, NextInstanceId(), ownerInstanceId,
                priority, frame, 0, reason);
        }

        private long NextInstanceId()
        {
            if (_nextInstanceSequence <= 0 || _nextInstanceSequence > uint.MaxValue)
                throw new InvalidOperationException("Character mode instance sequence exhausted.");
            var sequence = _nextInstanceSequence++;
            return ((long)_actorId << 32) | (uint)sequence;
        }

        private void ValidateSnapshot(MobaCharacterModeSnapshot snapshot)
        {
            var state = snapshot.Current;
            if (snapshot.Frame < 0 || snapshot.NextInstanceSequence <= 0 ||
                snapshot.NextInstanceSequence > uint.MaxValue || !IsValidModeId(state.ModeId) ||
                state.InstanceId <= 0 || (int)(state.InstanceId >> 32) != _actorId ||
                state.StartFrame < 0 || state.StartFrame > snapshot.Frame ||
                state.LocalFrame != snapshot.Frame - state.StartFrame || state.Priority < 0 ||
                state.OwnerInstanceId < 0)
                throw new InvalidOperationException("Character mode snapshot is invalid.");

            var sequence = (uint)state.InstanceId;
            if (snapshot.NextInstanceSequence <= sequence)
                throw new InvalidOperationException("Character mode snapshot sequence is invalid.");

            var pending = snapshot.PendingRequests;
            if (pending == null) return;
            for (var i = 0; i < pending.Length; i++)
            {
                var request = pending[i];
                if (!IsValidRequest(request) || request.RequestedFrame < snapshot.Frame)
                    throw new InvalidOperationException("Character mode snapshot contains an invalid pending request.");
            }
        }

        private static bool IsValidRequest(MobaCharacterModeRequest request) =>
            request.RequestedFrame >= 0 && IsValidModeId(request.ModeId) &&
            request.Priority >= 0 && request.OwnerInstanceId >= 0 &&
            request.Kind >= MobaCharacterModeRequestKind.Enter &&
            request.Kind <= MobaCharacterModeRequestKind.Reset;

        private static bool IsValidModeId(string modeId) =>
            !string.IsNullOrWhiteSpace(modeId) && modeId.Length <= 64;

        private static int CompareRequests(MobaCharacterModeRequest left,
            MobaCharacterModeRequest right)
        {
            var result = left.RequestedFrame.CompareTo(right.RequestedFrame);
            if (result != 0) return result;
            result = RequestOrder(left.Kind).CompareTo(RequestOrder(right.Kind));
            if (result != 0) return result;
            result = right.Priority.CompareTo(left.Priority);
            if (result != 0) return result;
            result = left.Kind.CompareTo(right.Kind);
            if (result != 0) return result;
            result = string.CompareOrdinal(left.ModeId, right.ModeId);
            if (result != 0) return result;
            return left.OwnerInstanceId.CompareTo(right.OwnerInstanceId);
        }

        private static int RequestOrder(MobaCharacterModeRequestKind kind) =>
            kind == MobaCharacterModeRequestKind.Reset ? 100 : (int)kind;
    }
}
