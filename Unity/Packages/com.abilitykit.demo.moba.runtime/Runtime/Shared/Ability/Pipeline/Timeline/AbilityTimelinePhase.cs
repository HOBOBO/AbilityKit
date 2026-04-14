using System;
using AbilityKit.Demo.Moba.ActionTimeline;
using AbilityKit.ActionSchema;
using AbilityKit.Pipeline;

namespace AbilityKit.Ability.Share.Impl.Pipeline.Timeline
{
    using AbilityKit.Ability;
    using AbilityKit.Ability.Share.Impl.Pipeline;
    public sealed class AbilityTimelinePhase<TCtx> : AbilityInterruptiblePhaseBase<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        private readonly SkillAssetDto _configuredAsset = null;
        private SkillAssetDto _asset;
        private MobaTimelinePlayer _player;

        public AbilityTimelinePhase(string phaseName, SkillAssetDto asset) : base(phaseName)
        {
            _configuredAsset = asset;
        }

        public AbilityTimelinePhase(string phaseName) : base(phaseName)
        {
        }

        public AbilityTimelinePhase(AbilityPipelinePhaseId phaseId, SkillAssetDto asset) : base(phaseId)
        {
            _configuredAsset = asset;
        }

        public AbilityTimelinePhase(AbilityPipelinePhaseId phaseId) : base(phaseId)
        {
        }

        protected override void OnEnter(TCtx context)
        {
            base.OnEnter(context);
            _asset = null;
            _player = null;
        }

        protected override void OnTick(TCtx context, float deltaTime)
        {
            if (_player == null)
            {
                Complete(context);
                return;
            }

            _player.Update(deltaTime);

            if (_asset != null && _player.Time >= _asset.length)
            {
                Complete(context);
            }
        }

        protected override void OnExecute(TCtx context)
        {
            _asset = context.GetData<SkillAssetDto>(AbilityPipelineSharedKeys.TimelineAssetDto);
            if (_asset == null && _configuredAsset != null)
            {
                _asset = _configuredAsset;
                context.SetData(AbilityPipelineSharedKeys.TimelineAssetDto, _asset);
            }
            if (_asset == null)
            {
                Complete(context);
                return;
            }

            Duration = _asset.length;

            var registry = context.GetData<MobaClipHandlerRegistry>(AbilityPipelineSharedKeys.TimelineRegistry);
            if (registry == null)
            {
                registry = MobaDefaultClipHandlers.CreateRegistry();
                context.SetData(AbilityPipelineSharedKeys.TimelineRegistry, registry);
            }

            var sink = context.GetData<IMobaTimelineEventSink>(AbilityPipelineSharedKeys.TimelineSink);
            if (sink == null)
            {
                var buffer = context.GetData<AbilityTimelineEventBuffer>(AbilityPipelineSharedKeys.TimelineEventBuffer);
                if (buffer == null)
                {
                    buffer = new AbilityTimelineEventBuffer();
                    context.SetData(AbilityPipelineSharedKeys.TimelineEventBuffer, buffer);
                }

                sink = buffer;
                context.SetData(AbilityPipelineSharedKeys.TimelineSink, sink);
            }

            _player = new MobaTimelinePlayer(_asset, registry, sink);
            _player.Reset(0f);
        }

        public override void Reset()
        {
            base.Reset();
            _asset = null;
            _player = null;
        }

        public override void OnInterrupt(TCtx context)
        {
            base.OnInterrupt(context);
            _asset = null;
            _player = null;
        }

        public override void HandleError(TCtx context, Exception exception)
        {
            base.HandleError(context, exception);
            _asset = null;
            _player = null;
        }
    }
}
