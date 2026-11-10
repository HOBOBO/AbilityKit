using System;
using AbilityKit.ActionSchema;

namespace AbilityKit.Game.Flow.Battle.Presentation.Timeline
{
    public sealed class MobaSkillPresentationCastDriver
    {
        private readonly SkillAssetDto _asset;
        private readonly IMobaSkillPresentationTimelineSink _sink;
        private readonly IMobaPresentationTemplateResolver _templateResolver;
        private readonly PresentationRateResolver _rateResolver;
        private MobaSkillPresentationTimelineRuntime _runtime;
        private long _castId;
        private float _elapsed;

        public MobaSkillPresentationCastDriver(string json, IMobaSkillPresentationTimelineSink sink,
            IMobaPresentationTemplateResolver templateResolver = null,
            PresentationRateResolver rateResolver = null)
        {
            _asset = ActionTimelineJson.LoadForRuntime(json, ActionTimelineRuntimeTypes.Presentation);
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _templateResolver = templateResolver;
            _rateResolver = rateResolver;
        }

        public void Seek(long castId, float elapsedSeconds)
        {
            SeekInternal(castId, elapsedSeconds, false);
        }

        public void SeekWithRate(long castId, float elapsedSeconds)
        {
            SeekInternal(castId, elapsedSeconds, true);
        }

        private void SeekInternal(long castId, float elapsedSeconds, bool applyRate)
        {
            if (float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds) || elapsedSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));

            if (castId <= 0 || castId != _castId || elapsedSeconds < _elapsed)
            {
                _runtime?.Stop();
                _runtime = null;
                _castId = castId;
                if (castId <= 0) return;
                _runtime = new MobaSkillPresentationTimelineRuntime(
                    castId, _asset, _sink, _templateResolver, _rateResolver);
                _runtime.StartAt(elapsedSeconds);
            }
            else
            {
                if (applyRate) _runtime.Tick(elapsedSeconds - _elapsed);
                else _runtime.TickUnscaled(elapsedSeconds - _elapsed);
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
