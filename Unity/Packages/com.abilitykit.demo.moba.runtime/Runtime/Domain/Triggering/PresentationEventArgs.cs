using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config;

namespace AbilityKit.Demo.Moba.Triggering
{
    public sealed class PresentationEventArgs : IMobaActorContextProvider, IMobaOriginContextProvider, IMobaTriggerLineageContextProvider, IMobaContextSourceProvider, ITriggerCueDataProvider
    {
        public string EventId;

        public int TemplateId;
        public string RequestKey;
        public int DurationMsOverride;

        public int[] Targets;
        public Vec3[] Positions;

        public int SourceActorId;
        public int TargetActorId;
        public long SourceContextId;
        public long RootContextId;
        public long OwnerContextId;
        public MobaExecutionKind ExecutionKind;

        public object Scale;
        public object Radius;
        public object Color;

        public bool TryGetSourceActorId(out int actorId)
        {
            actorId = SourceActorId;
            return actorId > 0;
        }

        public bool TryGetTargetActorId(out int actorId)
        {
            actorId = TargetActorId;
            return actorId > 0;
        }

        public bool TryGetOrigin(out MobaGameplayOrigin origin)
        {
            var executionKind = ExecutionKind != MobaExecutionKind.None ? ExecutionKind : MobaExecutionKind.PresentationPlay;
            origin = MobaGameplayOriginBuilder.Create()
                .WithActors(SourceActorId, TargetActorId)
                .WithImmediate(executionKind, TemplateId, SourceContextId)
                .WithRootContext(RootContextId)
                .WithOwnerContext(OwnerContextId)
                .Build();
            return origin.IsValid;
        }

        public bool TryGetLineageContext(out MobaTriggerLineageContext lineageContext)
        {
            if (TryGetOrigin(out var origin) && origin.IsValid)
            {
                lineageContext = new MobaTriggerLineageContext(
                    EffectContextKind.Trigger,
                    origin.ImmediateKind,
                    origin.SourceActorId,
                    origin.TargetActorId,
                    origin.EffectiveParentContextId,
                    RootContextId != 0 ? RootContextId : origin.EffectiveRootContextId,
                    OwnerContextId,
                    origin.ImmediateConfigId);
                return true;
            }

            var executionKind = ExecutionKind != MobaExecutionKind.None ? ExecutionKind : MobaExecutionKind.PresentationPlay;
            lineageContext = new MobaTriggerLineageContext(EffectContextKind.Trigger, executionKind, SourceActorId, TargetActorId, SourceContextId, RootContextId, OwnerContextId, TemplateId);
            return SourceActorId > 0 || TargetActorId > 0 || SourceContextId != 0 || TemplateId > 0;
        }

        public bool TryGetContextSource(out MobaContextSourceView source)
        {
            if (TryGetLineageContext(out var lineageContext))
            {
                source = MobaContextSourceView.FromLineage(
                    in lineageContext,
                    MobaContextSourceResolveKind.DirectProvider,
                    MobaContextSourceBoundary.Snapshot,
                    runtimeKind: MobaRuntimeKindNames.Presentation,
                    runtimeConfigId: TemplateId);
                return source.IsValid;
            }

            source = default;
            return false;
        }
        public bool TryGetCueData(
            ECueLevel cueLevel,
            ECueLifecycleStage cueStage,
            int actionIndex,
            in TriggerCueDescriptor cueDescriptor,
            out object cueData,
            out string cuePayload)
        {
            cueData = this;
            cuePayload = RequestKey;
            return true;
        }
    }
}
