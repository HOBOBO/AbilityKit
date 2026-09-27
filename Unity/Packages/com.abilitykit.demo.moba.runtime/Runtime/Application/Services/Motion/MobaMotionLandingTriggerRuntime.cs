using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Runtime.Application.Services.Triggering;
using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Demo.Moba.Services.Motion
{
    public readonly struct MobaMotionLandingTriggerRuntime
    {
        public MobaMotionLandingTriggerRuntime(
            IReadOnlyList<int> triggerIds,
            int sourceActorId,
            int sourceConfigId,
            MobaCombatExecutionContext executionContext)
        {
            TriggerIds = triggerIds ?? Array.Empty<int>();
            SourceActorId = sourceActorId;
            SourceConfigId = sourceConfigId;
            ExecutionContext = executionContext;
        }

        public IReadOnlyList<int> TriggerIds { get; }
        public int SourceActorId { get; }
        public int SourceConfigId { get; }
        public MobaCombatExecutionContext ExecutionContext { get; }

        public bool IsValid => TriggerIds != null && TriggerIds.Count > 0 && SourceActorId > 0 && ExecutionContext.HasExecutionSource;
    }

    public sealed class MobaMotionLandingArgs : MobaTriggerInvocationContextBase, IMobaActorContextProvider, IMobaCombatExecutionContextProvider, IMobaTriggerExecutionSnapshotProvider
    {
        public override EffectContextKind Kind => EffectContextKind.Trigger;
        public int SourceConfigId { get; set; }
        public int Frame { get; set; }
        public int MotionActorId { get; set; }
        public Vec3 Position { get; set; }
        public MobaMotionLandingTriggerRuntime Runtime { get; set; }

        public bool TryGetSourceActorId(out int actorId)
        {
            actorId = SourceActorId > 0 ? SourceActorId : Runtime.SourceActorId;
            return actorId > 0;
        }

        public bool TryGetTargetActorId(out int actorId)
        {
            actorId = TargetActorId > 0 ? TargetActorId : MotionActorId;
            return actorId > 0;
        }

        public bool TryGetCombatExecutionContext(out MobaCombatExecutionContext context)
        {
            if (!TryGetExecutionSnapshot(out var snapshot))
            {
                context = default;
                return false;
            }

            context = Runtime.ExecutionContext.WithSnapshot(in snapshot, Frame);
            return context.HasExecutionSource;
        }

        public override bool TryGetLineageContext(out MobaTriggerLineageContext lineageContext)
        {
            if (Runtime.ExecutionContext.TryGetLineageContext(out lineageContext))
            {
                return true;
            }

            lineageContext = default;
            return false;
        }

        public override bool TryGetOrigin(out MobaGameplayOrigin origin)
        {
            if (Runtime.ExecutionContext.TryGetOrigin(out origin))
            {
                return true;
            }

            origin = default;
            return false;
        }

        public bool TryGetExecutionSnapshot(out MobaTriggerExecutionSnapshot snapshot)
        {
            if (!Runtime.ExecutionContext.TryGetExecutionSnapshot(out var sourceSnapshot))
            {
                snapshot = default;
                return false;
            }

            snapshot = sourceSnapshot.WithTrigger(TriggerId, SourceConfigId != 0 ? SourceConfigId : Runtime.SourceConfigId).WithFrame(Frame);
            return snapshot.IsValid;
        }
    }

    [WorldService(typeof(MobaMotionLandingTriggerService))]
    public sealed class MobaMotionLandingTriggerService : IService
    {
        private const string EventMotionLanding = "motion.landing";
        [WorldInject(required: false)] private MobaTriggerExecutionGateway _triggers = null;
        [WorldInject(required: false)] private MobaActorRegistry _actors = null;
        [WorldInject(required: false)] private IFrameTime _frameTime = null;

        public bool TryExecute(in MobaMotionLandingTriggerRuntime runtime)
        {
            if (!runtime.IsValid || _triggers == null)
            {
                return false;
            }

            var frame = _frameTime != null ? _frameTime.Frame.Value : runtime.ExecutionContext.Frame;
            var position = Vec3.Zero;
            if (_actors != null && _actors.TryGet(runtime.SourceActorId, out var entity) && entity != null && entity.hasTransform)
            {
                position = entity.transform.Value.Position;
            }

            for (var i = 0; i < runtime.TriggerIds.Count; i++)
            {
                var triggerId = runtime.TriggerIds[i];
                if (triggerId <= 0) continue;

                var payload = new MobaMotionLandingArgs
                {
                    TriggerId = triggerId,
                    SourceActorId = runtime.SourceActorId,
                    TargetActorId = runtime.SourceActorId,
                    SourceContextId = runtime.ExecutionContext.ParentContextId,
                    SourceConfigId = runtime.SourceConfigId,
                    Frame = frame,
                    MotionActorId = runtime.SourceActorId,
                    Position = position,
                    Runtime = runtime
                };

                var request = MobaTriggerExecutionRequest<MobaMotionLandingArgs>.Create(triggerId, payload, EventMotionLanding);
                _triggers.ExecuteDirectTrigger(in request);
            }

            return true;
        }

        public void Dispose()
        {
        }
    }
}
