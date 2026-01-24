using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.Share.Common.Record.Core;

namespace AbilityKit.Ability.Share.Common.Record.Lockstep
{
    public sealed class LockstepInputRecordReplaySource : IFrameReplaySource
    {
        private readonly Dictionary<int, List<PlayerInputCommand>> _inputsByFrame;
        private readonly Dictionary<int, WorldStateSnapshot> _snapshotsByFrame;
        private readonly Dictionary<int, (WorldStateHash hash, int version)> _hashByFrame;

        public LockstepInputRecordReplaySource(LockstepInputRecordFile file)
        {
            _inputsByFrame = new Dictionary<int, List<PlayerInputCommand>>(file?.Inputs?.Count ?? 0);
            _snapshotsByFrame = new Dictionary<int, WorldStateSnapshot>(file?.Snapshots?.Count ?? 0);
            _hashByFrame = new Dictionary<int, (WorldStateHash hash, int version)>(file?.StateHashes?.Count ?? 0);

            if (file?.Inputs != null)
            {
                for (int i = 0; i < file.Inputs.Count; i++)
                {
                    var e = file.Inputs[i];
                    if (e == null) continue;

                    var payload = string.IsNullOrEmpty(e.PayloadBase64) ? Array.Empty<byte>() : Convert.FromBase64String(e.PayloadBase64);
                    var cmd = new PlayerInputCommand(new FrameIndex(e.Frame), new PlayerId(e.PlayerId), e.OpCode, payload);

                    if (!_inputsByFrame.TryGetValue(e.Frame, out var list))
                    {
                        list = new List<PlayerInputCommand>(2);
                        _inputsByFrame[e.Frame] = list;
                    }
                    list.Add(cmd);
                }
            }

            if (file?.Snapshots != null)
            {
                for (int i = 0; i < file.Snapshots.Count; i++)
                {
                    var e = file.Snapshots[i];
                    if (e == null) continue;

                    var payload = string.IsNullOrEmpty(e.PayloadBase64) ? Array.Empty<byte>() : Convert.FromBase64String(e.PayloadBase64);
                    _snapshotsByFrame[e.Frame] = new WorldStateSnapshot(e.OpCode, payload);
                }
            }

            if (file?.StateHashes != null)
            {
                for (int i = 0; i < file.StateHashes.Count; i++)
                {
                    var e = file.StateHashes[i];
                    if (e == null) continue;
                    _hashByFrame[e.Frame] = (new WorldStateHash(e.Hash), e.Version);
                }
            }
        }

        public bool TryGetInputs(FrameIndex frame, out IReadOnlyList<PlayerInputCommand> inputs)
        {
            if (_inputsByFrame.TryGetValue(frame.Value, out var list) && list != null)
            {
                inputs = list;
                return true;
            }

            inputs = Array.Empty<PlayerInputCommand>();
            return false;
        }

        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            return _snapshotsByFrame.TryGetValue(frame.Value, out snapshot);
        }

        public bool TryGetStateHash(FrameIndex frame, out WorldStateHash hash, out int version)
        {
            if (_hashByFrame.TryGetValue(frame.Value, out var it))
            {
                hash = it.hash;
                version = it.version;
                return true;
            }

            hash = default;
            version = 0;
            return false;
        }
    }
}
