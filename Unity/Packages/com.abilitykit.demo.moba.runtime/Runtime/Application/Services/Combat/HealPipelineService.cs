using System;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Eventing;

namespace AbilityKit.Demo.Moba.Services
{
    public static class HealPipelineEvents
    {
        public const string BeforeApply = "heal.apply.before";
        public const string AfterApply = "heal.apply.after";
    }

    public readonly struct MobaHealRequest
    {
        public MobaHealRequest(
            int healerActorId,
            int targetActorId,
            int healType,
            float value,
            int reasonKind = 0,
            int reasonParam = 0,
            MobaGameplayOrigin origin = default,
            bool allowDeadTarget = false)
        {
            HealerActorId = healerActorId;
            TargetActorId = targetActorId;
            HealType = healType;
            Value = value;
            ReasonKind = reasonKind;
            ReasonParam = reasonParam;
            Origin = origin;
            AllowDeadTarget = allowDeadTarget;
        }

        public int HealerActorId { get; }
        public int TargetActorId { get; }
        public int HealType { get; }
        public float Value { get; }
        public int ReasonKind { get; }
        public int ReasonParam { get; }
        public MobaGameplayOrigin Origin { get; }
        public bool AllowDeadTarget { get; }
    }

    [WorldService(typeof(HealPipelineService))]
    public sealed class HealPipelineService : IService
    {
        private readonly MobaDamageService _commitPort;
        private readonly AbilityKit.Triggering.Eventing.IEventBus _eventBus;

        public HealPipelineService(
            MobaDamageService commitPort,
            AbilityKit.Triggering.Eventing.IEventBus eventBus = null)
        {
            _commitPort = commitPort ?? throw new ArgumentNullException(nameof(commitPort));
            _eventBus = eventBus;
        }

        public MobaHealthChangeResult Execute(in MobaHealRequest request)
        {
            return Execute(_commitPort, _eventBus, in request);
        }

        internal static MobaHealthChangeResult Execute(
            MobaDamageService commitPort,
            AbilityKit.Triggering.Eventing.IEventBus eventBus,
            in MobaHealRequest request)
        {
            if (commitPort == null) throw new ArgumentNullException(nameof(commitPort));
            if (!IsValid(in request)) return default;

            PublishBefore(eventBus, in request);
            var result = commitPort.CommitHealCore(
                request.HealerActorId,
                request.TargetActorId,
                request.HealType,
                request.Value,
                request.ReasonKind,
                request.ReasonParam,
                request.Origin,
                request.AllowDeadTarget);
            if (result.Succeeded) PublishAfter(eventBus, in result);
            return result;
        }

        private static bool IsValid(in MobaHealRequest request)
        {
            return request.TargetActorId > 0
                && request.Value > 0f
                && !float.IsNaN(request.Value)
                && !float.IsInfinity(request.Value);
        }

        private static void PublishBefore(AbilityKit.Triggering.Eventing.IEventBus eventBus, in MobaHealRequest request)
        {
            if (eventBus == null) return;
            var eid = TriggeringIdUtil.GetEventEid(HealPipelineEvents.BeforeApply);
            eventBus.Publish(new EventKey<MobaHealRequest>(eid), in request);
            PublishObject(eventBus, eid, request);
        }

        private static void PublishAfter(AbilityKit.Triggering.Eventing.IEventBus eventBus, in MobaHealthChangeResult result)
        {
            if (eventBus == null) return;
            var eid = TriggeringIdUtil.GetEventEid(HealPipelineEvents.AfterApply);
            eventBus.Publish(new EventKey<MobaHealthChangeResult>(eid), in result);
            PublishObject(eventBus, eid, result);
        }

        private static void PublishObject<T>(AbilityKit.Triggering.Eventing.IEventBus eventBus, int eid, T payload)
        {
            var key = new EventKey<object>(eid);
            if (!eventBus.HasSubscribers(key)) return;
            object boxed = payload;
            eventBus.Publish(key, in boxed);
        }

        public void Dispose()
        {
        }
    }
}
