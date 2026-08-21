using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Recording.FrameRecord;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Requests;

namespace AbilityKit.Game.Flow.Battle.Replay
{
    public interface IBattleReplayControl
    {
        bool IsReplaySession { get; }
        bool IsPlaying { get; }
        bool RenderPresentation { get; }
        int CurrentFrame { get; }
        int LastFrame { get; }
        float PlaybackSpeed { get; set; }
        string ReplayPath { get; }

        bool TryLoad(string path, bool renderPresentation, out string error);
        void Play();
        void Pause();
        bool StepForward();
        bool StepBackward();
        bool SeekToFrame(int frame);
    }

    public static class BattleReplayControlProvider
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, IBattleReplayControl> ByScope =
            new Dictionary<string, IBattleReplayControl>(StringComparer.Ordinal);

        /// <summary>仅用于 development single-active 兼容；正式调用方应按 scope 查询。</summary>
        public static IBattleReplayControl Current { get; internal set; }

        public static bool TryGet(string scope, out IBattleReplayControl control)
        {
            control = null;
            if (string.IsNullOrWhiteSpace(scope)) return false;
            lock (Gate)
            {
                return ByScope.TryGetValue(scope, out control);
            }
        }

        internal static void Publish(string scope, IBattleReplayControl control)
        {
            if (string.IsNullOrWhiteSpace(scope) || control == null) return;
            lock (Gate)
            {
                ByScope[scope] = control;
            }
        }

        internal static void Withdraw(string scope, IBattleReplayControl owner)
        {
            if (string.IsNullOrWhiteSpace(scope) || owner == null) return;
            lock (Gate)
            {
                if (ByScope.TryGetValue(scope, out var current) &&
                    ReferenceEquals(current, owner))
                {
                    ByScope.Remove(scope);
                }
            }
        }
    }

    public sealed class FrameReplayDriver
    {
        private readonly WorldId _worldId;
        private readonly List<FrameRecordInputFrame> _inputs;
        private readonly Dictionary<int, FrameRecordStateHashFrame> _expectedStateHashes;
        private readonly int _lastFrame;
        private int _cursor;
        private bool _isPlaying;
        private bool _reportedHashMismatch;
        private float _playbackSpeed = 1f;

        public FrameReplayDriver(WorldId worldId, FrameRecordFile file)
        {
            _worldId = worldId;
            _inputs = CreateOrderedInputSnapshot(file?.Inputs);
            _expectedStateHashes = new Dictionary<int, FrameRecordStateHashFrame>(file?.StateHashes?.Count ?? 0);
            if (file?.StateHashes != null)
            {
                for (int i = 0; i < file.StateHashes.Count; i++)
                {
                    var e = file.StateHashes[i];
                    if (e == null) continue;
                    _expectedStateHashes[e.Frame] = e;
                }
            }
            _lastFrame = ResolveLastFrame(file);
            _cursor = 0;
            _isPlaying = true;
            _reportedHashMismatch = false;
        }

        public bool IsPlaying => _isPlaying;
        public int LastFrame => _lastFrame;

        public float PlaybackSpeed
        {
            get => _playbackSpeed;
            set => _playbackSpeed = NormalizePlaybackSpeed(value);
        }

        public void Play() => _isPlaying = true;
        public void Pause() => _isPlaying = false;

        private static float NormalizePlaybackSpeed(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 1f;
            return Math.Max(0.1f, Math.Min(8f, value));
        }

        private static List<FrameRecordInputFrame> CreateOrderedInputSnapshot(
            IList<FrameRecordInputFrame> inputs)
        {
            var ordered = new List<OrderedInput>(inputs?.Count ?? 0);
            if (inputs == null) return new List<FrameRecordInputFrame>();

            for (var i = 0; i < inputs.Count; i++)
            {
                var input = inputs[i];
                if (input != null) ordered.Add(new OrderedInput(input, i));
            }

            ordered.Sort((left, right) =>
            {
                var frameOrder = left.Input.Frame.CompareTo(right.Input.Frame);
                return frameOrder != 0 ? frameOrder : left.SourceIndex.CompareTo(right.SourceIndex);
            });

            var snapshot = new List<FrameRecordInputFrame>(ordered.Count);
            for (var i = 0; i < ordered.Count; i++) snapshot.Add(ordered[i].Input);
            return snapshot;
        }

        private readonly struct OrderedInput
        {
            public OrderedInput(FrameRecordInputFrame input, int sourceIndex)
            {
                Input = input;
                SourceIndex = sourceIndex;
            }

            public FrameRecordInputFrame Input { get; }
            public int SourceIndex { get; }
        }

        private static int ResolveLastFrame(FrameRecordFile file)
        {
            var lastFrame = 0;
            if (file?.Inputs != null)
            {
                for (var i = 0; i < file.Inputs.Count; i++)
                {
                    var item = file.Inputs[i];
                    if (item != null) lastFrame = Math.Max(lastFrame, item.Frame);
                }
            }

            if (file?.StateHashes != null)
            {
                for (var i = 0; i < file.StateHashes.Count; i++)
                {
                    var item = file.StateHashes[i];
                    if (item != null) lastFrame = Math.Max(lastFrame, item.Frame);
                }
            }

            if (file?.Snapshots != null)
            {
                for (var i = 0; i < file.Snapshots.Count; i++)
                {
                    var item = file.Snapshots[i];
                    if (item != null) lastFrame = Math.Max(lastFrame, item.Frame);
                }
            }

            return lastFrame;
        }

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

        public bool TryGetExpectedStateHash(int frame, out FrameRecordStateHashFrame expected)
        {
            if (_expectedStateHashes == null)
            {
                expected = null;
                return false;
            }

            return _expectedStateHashes.TryGetValue(frame, out expected);
        }

        public bool TryValidateStateHashOnce(int frame, int version, uint hash, out FrameRecordStateHashFrame expected)
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
            PumpFrame(session, targetFrame);
        }

        internal void PumpFrame(BattleLogicSession session, int targetFrame)
        {
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
