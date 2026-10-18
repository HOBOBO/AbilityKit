using System;
using AbilityKit.ActionSchema;

namespace AbilityKit.Game.Flow.Battle.Presentation.Timeline
{
    public sealed class MobaSkillPresentationCastDriver
    {
        private readonly SkillAssetDto _asset;
        private readonly IMobaSkillPresentationTimelineSink _sink;
        private MobaSkillPresentationTimelineRuntime _runtime;
        private long _castId;
        private float _elapsed;

        public MobaSkillPresentationCastDriver(string json, IMobaSkillPresentationTimelineSink sink)
        {
            _asset = ActionTimelineJson.LoadForRuntime(json, ActionTimelineRuntimeTypes.Presentation);
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        public void Seek(long castId, float elapsedSeconds)
        {
            if (float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds) || elapsedSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));

            if (castId <= 0 || castId != _castId || elapsedSeconds < _elapsed)
            {
                _runtime?.Stop();
                _runtime = null;
                _castId = castId;
                if (castId <= 0) return;
                _runtime = new MobaSkillPresentationTimelineRuntime(castId, _asset, _sink);
                _runtime.StartAt(elapsedSeconds);
            }
            else
            {
                _runtime.Tick(elapsedSeconds - _elapsed);
            }
            _elapsed = elapsedSeconds;
        }

        public void Stop()
        {
            _runtime?.Stop();
            _runtime = null;
            _castId = 0;
            _elapsed = 0;
        }
    }
}
