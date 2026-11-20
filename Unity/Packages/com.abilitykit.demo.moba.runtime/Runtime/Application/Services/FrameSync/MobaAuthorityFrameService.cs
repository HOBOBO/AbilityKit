using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(MobaAuthorityFrameService))]
    public sealed class MobaAuthorityFrameService : IService
    {
        [WorldInject] private IFrameTime _time = null;
        [WorldInject(required: false)] private IWorldAuthorityFramesSource _authorityFrames = null;

        private WorldId _worldId;
        private IWorldRestoreFloorSource _restoreFloor;
        private bool _hasWorldId;
        private int _rollbackHistoryFrames = 600;

        public int RollbackHistoryFrames => _rollbackHistoryFrames;

        public void ConfigureRollbackHistoryFrames(int frames)
        {
            if (frames < 0) throw new ArgumentOutOfRangeException(nameof(frames));
            _rollbackHistoryFrames = frames;
        }

        public void BindWorld(WorldId worldId)
        {
            _worldId = worldId;
            _hasWorldId = true;
        }

        /// <summary>
        /// 绑定同世界帧同步驱动提供的回收门槛，不用本地帧兜底。
        /// </summary>
        public void BindRestoreFloor(WorldId worldId, IWorldRestoreFloorSource source)
        {
            BindWorld(worldId);
            _restoreFloor = source ?? throw new ArgumentNullException(nameof(source));
        }

        /// <summary>
        /// 读取已生效的最早可恢复帧；未取得权威锚点时没有回收资格。
        /// </summary>
        public bool TryGetRestoreFloor(out FrameIndex frame)
        {
            frame = default;
            var source = _restoreFloor ?? _authorityFrames as IWorldRestoreFloorSource;
            return _hasWorldId && source != null && source.TryGetRestoreFloor(_worldId, out frame);
        }

        public bool TryGetFrames(out FrameIndex confirmed, out FrameIndex predicted)
        {
            predicted = default;
            confirmed = default;

            if (_hasWorldId && _authorityFrames != null)
            {
                if (_authorityFrames.TryGetFrames(_worldId, out confirmed, out predicted))
                {
                    return true;
                }
            }

            if (_time == null) return false;

            predicted = _time.Frame;
            confirmed = predicted;
            return true;
        }

        public FrameIndex PredictedFrame
        {
            get
            {
                TryGetFrames(out _, out var p);
                return p;
            }
        }

        public FrameIndex ConfirmedFrame
        {
            get
            {
                TryGetFrames(out var c, out _);
                return c;
            }
        }

        public void Dispose()
        {
        }
    }
}
