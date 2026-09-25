#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Demo.Tiny.FrameSync;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Demo.Tiny.View
{
    /// <summary>Applies Room authoritative frames to the shared Tiny rollback adapter.</summary>
    internal sealed class TinyFrameReplication
    {
        private readonly TinyBattle _battle = new TinyBattle();
        private TinyFrameSyncSession? _session;
        private int _lastNetworkFrame = -1;
        private int _nextReservedInputFrame;
        private int _lastObservedServerFrame = -1;
        private bool _presentationDirty;

        public bool NeedsFullSnapshot { get; private set; }
        public bool HasBaseline => _session != null;
        public int AuthoritativeFrame => _lastNetworkFrame + 1;
        public int PredictedFrame => _session?.Frame ?? -1;
        internal uint StateHash => _session?.StateHash ?? 0;
        public int LocalPredictionCount { get; private set; }
        public int RollbackCount { get; private set; }
        public int SnapshotCorrectionCount { get; private set; }
        public int RecoveryRequestCount { get; private set; }

        public int ReserveInputFrame()
        {
            if (_session == null || NeedsFullSnapshot)
                throw new InvalidOperationException("Tiny frame baseline is pending.");
            var frame = Math.Max(Math.Max(_lastNetworkFrame + 1, _lastObservedServerFrame) +
                TinySyncSettings.SubmissionLeadFrames,
                _session.Frame);
            frame = Math.Max(frame, _nextReservedInputFrame);
            _nextReservedInputFrame = checked(frame + 1);
            return frame;
        }

        public void ObserveServerFrame(int serverFrame)
        {
            if (serverFrame >= 0)
                _lastObservedServerFrame = Math.Max(_lastObservedServerFrame, serverFrame);
        }

        public void PredictLocalInput(int frame, uint playerId, TinyInput input)
        {
            if (_session == null || NeedsFullSnapshot)
                throw new InvalidOperationException("Tiny frame baseline is pending.");
            if (frame < _session.Frame || frame - _lastNetworkFrame > 32)
            {
                RequireFullSnapshot();
                throw new InvalidOperationException("Tiny prediction exceeded its frame history.");
            }
            while (_session.Frame < frame)
                _session.Predict(Array.Empty<TinyFrameInput>());
            _session.Predict(new[] { new TinyFrameInput(playerId, input) });
            LocalPredictionCount++;
            _presentationDirty = true;
        }

        public void RequireFullSnapshot()
        {
            if (!NeedsFullSnapshot) RecoveryRequestCount++;
            NeedsFullSnapshot = true;
        }

        public void ApplyFullSnapshot(in WireStateSyncSnapshotPush snapshot)
        {
            if (!snapshot.IsFullSnapshot || snapshot.PayloadOpCode != TinyBattleStateCodec.PayloadOpCode ||
                snapshot.Payload == null || snapshot.Frame < 0)
                throw new InvalidOperationException("Tiny frame sync requires a complete Tiny state payload.");
            var state = TinyBattleStateCodec.Decode(snapshot.Payload);
            if (state.Frame != snapshot.Frame)
                throw new InvalidOperationException("Tiny snapshot frame differs from its payload.");
            if (_session != null && !NeedsFullSnapshot)
            {
                if (state.Frame <= _lastNetworkFrame + 1) return;
                if (state.Frame <= _session.Frame &&
                    _session.TryGetStateHash(state.Frame, out var predictedHash))
                {
                    var authoritative = new TinyBattle();
                    authoritative.RestoreState(state);
                    if (predictedHash == authoritative.ComputeHash())
                    {
                        _lastNetworkFrame = state.Frame - 1;
                        return;
                    }
                    SnapshotCorrectionCount++;
                }
            }
            _battle.RestoreState(state);
            _session = new TinyFrameSyncSession(_battle);
            _lastNetworkFrame = state.Frame - 1;
            _nextReservedInputFrame = 0;
            _lastObservedServerFrame = -1;
            _presentationDirty = true;
            NeedsFullSnapshot = false;
        }

        public void ApplyFrame(in WireRoomFramePush frame, ulong expectedWorldId)
        {
            if (frame.WorldId != expectedWorldId || _session == null || NeedsFullSnapshot ||
                frame.Frame <= _lastNetworkFrame) return;
            var target = checked(frame.Frame + 1);
            if (frame.Frame - _lastNetworkFrame > 64 || frame.StateHash == 0)
            {
                RequireFullSnapshot();
                return;
            }
            try
            {
                while (_session.Frame < target)
                    _session.Predict(Array.Empty<TinyFrameInput>());
            }
            catch (InvalidOperationException)
            {
                RequireFullSnapshot();
                return;
            }
            var inputs = new List<TinyFrameInput>(frame.Inputs?.Length ?? 0);
            if (frame.Inputs != null)
                foreach (var item in frame.Inputs)
                {
                    if (item.InputOpCode != TinyBattle.InputOpCode || item.Payload == null)
                    {
                        RequireFullSnapshot();
                        return;
                    }
                    try { inputs.Add(new TinyFrameInput(item.PlayerId, TinyInput.Decode(item.Payload))); }
                    catch (ArgumentException)
                    {
                        RequireFullSnapshot();
                        return;
                    }
                }
            TinyReconcileResult reconciled;
            try { reconciled = _session.ApplyAuthoritative(target, inputs, frame.StateHash); }
            catch (ArgumentException)
            {
                RequireFullSnapshot();
                return;
            }
            if (reconciled == TinyReconcileResult.NeedsFullSnapshot)
            {
                RequireFullSnapshot();
                return;
            }
            if (reconciled == TinyReconcileResult.Replayed) RollbackCount++;
            _lastNetworkFrame = frame.Frame;
            _presentationDirty = true;
        }

        public bool TryGetPresentation(ulong worldId, out WireStateSyncSnapshotPush snapshot)
        {
            if (_session == null || !_presentationDirty)
            {
                snapshot = default;
                return false;
            }
            _presentationDirty = false;
            var actors = new List<WireStateSyncActorSnapshot>();
            foreach (var actor in _battle.Actors)
                actors.Add(new WireStateSyncActorSnapshot
                {
                    ActorId = (int)actor.PlayerId,
                    X = actor.X,
                    Z = actor.Y,
                    Hp = actor.Hp,
                    HpMax = TinyBattle.MaxHp
                });
            snapshot = new WireStateSyncSnapshotPush
            {
                WorldId = worldId,
                Frame = _session.Frame,
                IsFullSnapshot = true,
                Actors = actors
            };
            return true;
        }
    }
}
