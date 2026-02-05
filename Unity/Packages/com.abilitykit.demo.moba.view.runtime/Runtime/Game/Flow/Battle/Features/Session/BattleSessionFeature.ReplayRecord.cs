using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Common.Record.Lockstep;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Flow.Battle.Replay;
using AbilityKit.Game.Battle.Component;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private const int StateHashRecordIntervalFrames = 10;

        private void BuildReplayOrRecord(BattleStartConfig.BattleRunMode runMode)
        {
            if (runMode == BattleStartConfig.BattleRunMode.Replay)
            {
                var file = LockstepJsonInputRecordReader.Load(_plan.InputReplayPath);
                _replay = new LockstepReplayDriver(new WorldId(_plan.WorldId), file);
            }

            if (_ctx != null)
            {
                if (runMode == BattleStartConfig.BattleRunMode.Record)
                {
                    _ctx.InputRecordWriter?.Dispose();
                    _ctx.InputRecordWriter = new LockstepJsonInputRecordWriter(
                        _plan.InputRecordOutputPath,
                        new LockstepInputRecordMeta
                        {
                            WorldId = _plan.WorldId,
                            WorldType = _plan.WorldType,
                            TickRate = 30,
                            RandomSeed = 0,
                            PlayerId = _plan.PlayerId,
                            StartedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        });
                }
            }
        }

        private void OnFrameReplayAndRecording(FramePacket packet)
        {
            if (_ctx == null) return;

            if (_replay != null)
            {
                if (_ctx.EntityNode.IsValid && _ctx.EntityNode.TryGetComponent(out BattleStateHashSnapshotComponent hs) && hs != null)
                {
                    if (!_replay.TryValidateStateHashOnce(hs.Frame, hs.Version, hs.Hash, out var expected))
                    {
                        Log.Error($"[BattleReplay] State hash mismatch at frame={hs.Frame}, expected(version={expected.Version}, hash={expected.Hash}), actual(version={hs.Version}, hash={hs.Hash})");
                        _replay.Pause();
                    }
                }
            }

            if (_plan.EnableInputRecording && _ctx.InputRecordWriter != null)
            {
                if (packet.Snapshot.HasValue)
                {
                    var s = packet.Snapshot.Value;
                    _ctx.InputRecordWriter.AppendSnapshot(_lastFrame, s.OpCode, s.Payload);
                }

                var interval = StateHashRecordIntervalFrames;
                if (interval <= 0) interval = 10;

                if ((_lastFrame % interval) == 0)
                {
                    if (_ctx.EntityNode.IsValid && _ctx.EntityNode.TryGetComponent(out BattleStateHashSnapshotComponent h) && h != null)
                    {
                        _ctx.InputRecordWriter.AppendStateHash(h.Frame, h.Version, h.Hash);
                    }
                }
            }
        }
    }
}
