using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Common.Record.Lockstep;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Requests;

namespace AbilityKit.Game.Flow.Battle.Replay
{
    public sealed class LockstepReplayDriver
    {
        private readonly WorldId _worldId;
        private readonly List<LockstepInputRecordFrame> _inputs;
        private readonly Dictionary<int, LockstepStateHashRecordFrame> _expectedStateHashes;
        private int _cursor;
        private bool _isPlaying;
        private bool _reportedHashMismatch;

        public LockstepReplayDriver(WorldId worldId, LockstepInputRecordFile file)
        {
            _worldId = worldId;
            _inputs = file?.Inputs ?? new List<LockstepInputRecordFrame>();
            _expectedStateHashes = new Dictionary<int, LockstepStateHashRecordFrame>(file?.StateHashes?.Count ?? 0);
            if (file?.StateHashes != null)
            {
                for (int i = 0; i < file.StateHashes.Count; i++)
                {
                    var e = file.StateHashes[i];
                    if (e == null) continue;
                    _expectedStateHashes[e.Frame] = e;
                }
            }
            _cursor = 0;
            _isPlaying = true;
            _reportedHashMismatch = false;
        }

        public bool IsPlaying => _isPlaying;

        public void Play() => _isPlaying = true;
        public void Pause() => _isPlaying = false;

        public void SeekToStart()
        {
            _cursor = 0;
            _reportedHashMismatch = false;
        }

        public void SeekToFrame(int frame)
        {
            if (frame <= 0)
            {
                _cursor = 0;
                _reportedHashMismatch = false;
                return;
            }

            var lo = 0;
            var hi = _inputs.Count;
            while (lo < hi)
            {
                var mid = lo + ((hi - lo) >> 1);
                if (_inputs[mid].Frame < frame) lo = mid + 1;
                else hi = mid;
            }

            _cursor = lo;
            _reportedHashMismatch = false;
        }

        public bool TryGetExpectedStateHash(int frame, out LockstepStateHashRecordFrame expected)
        {
            if (_expectedStateHashes == null)
            {
                expected = null;
                return false;
            }

            return _expectedStateHashes.TryGetValue(frame, out expected);
        }

        public bool TryValidateStateHashOnce(int frame, int version, uint hash, out LockstepStateHashRecordFrame expected)
        {
            if (_reportedHashMismatch)
            {
                expected = null;
                return true;
            }

            if (!TryGetExpectedStateHash(frame, out expected)) return true;

            if (expected.Version != version || expected.Hash != hash)
            {
                _reportedHashMismatch = true;
                return false;
            }

            return true;
        }

        public void Pump(BattleLogicSession session, int targetFrame)
        {
            if (!_isPlaying) return;
            if (session == null) return;

            while (_cursor < _inputs.Count)
            {
                var e = _inputs[_cursor];
                if (e.Frame > targetFrame) break;

                if (e.Frame == targetFrame)
                {
                    var payload = string.IsNullOrEmpty(e.PayloadBase64)
                        ? Array.Empty<byte>()
                        : Convert.FromBase64String(e.PayloadBase64);

                    var cmd = new PlayerInputCommand(
                        new FrameIndex(e.Frame),
                        new PlayerId(e.PlayerId),
                        e.OpCode,
                        payload);

                    session.SubmitInput(new SubmitInputRequest(_worldId, cmd));
                }

                _cursor++;
            }
        }
    }
}
