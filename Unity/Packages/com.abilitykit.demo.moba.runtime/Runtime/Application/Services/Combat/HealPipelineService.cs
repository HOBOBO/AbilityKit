using System;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Eventing;
using AbilityKit.Demo.Moba.Services.Combat.Transactions;

namespace AbilityKit.Demo.Moba.Services
{
    public static class HealPipelineEvents
    {
        public const string BeforeApply = "heal.apply.before";
        public const string AfterApply = "heal.apply.after";
    }

    public readonly struct MobaHealRequest : IMobaActorContextProvider, IMobaOriginContextProvider
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

        public bool TryGetSourceActorId(out int actorId)
        {
            actorId = HealerActorId;
            return actorId > 0;
        }

        public bool TryGetTargetActorId(out int actorId)
        {
            actorId = TargetActorId;
            return actorId > 0;
        }

        public bool TryGetOrigin(out MobaGameplayOrigin origin)
        {
            origin = Origin;
            return origin.IsValid;
        }
    }

    public sealed class MobaHealTransaction : MobaCombatTransactionBase, IMobaActorContextProvider, IMobaOriginContextProvider
    {
        public MobaHealTransaction(in MobaHealRequest request)
            : base(request.Origin.EffectiveRootContextId)
        {
            HealerActorId = request.HealerActorId;
            TargetActorId = request.TargetActorId;
            HealType = request.HealType;
            Value = request.Value;
            ReasonKind = request.ReasonKind;
            ReasonParam = request.ReasonParam;
            Origin = request.Origin;
            AllowDeadTarget = request.AllowDeadTarget;
        }

        public int HealerActorId { get; private set; }
        public int TargetActorId { get; private set; }
        public int HealType { get; private set; }
        public float Value { get; private set; }
        public int ReasonKind { get; private set; }
        public int ReasonParam { get; private set; }
        public MobaGameplayOrigin Origin { get; }
        public bool AllowDeadTarget { get; private set; }

        public void SetValue(float value) => Value = value;
        public void Redirect(int targetActorId) => TargetActorId = targetActorId;
        public void SetHealType(int healType) => HealType = healType;
        public void SetAllowDeadTarget(bool allow) => AllowDeadTarget = allow;
        public bool TryGetSourceActorId(out int actorId) { actorId = HealerActorId; return actorId > 0; }
        public bool TryGetTargetActorId(out int actorId) { actorId = TargetActorId; return actorId > 0; }
        public bool TryGetOrigin(out MobaGameplayOrigin origin) { origin = Origin; return origin.IsValid; }
    }

    [WorldService(typeof(HealPipelineService))]
    public sealed class HealPipelineService : IService
    {
        private readonly MobaDamageService _commitPort;
        private readonly AbilityKit.Triggering.Eventing.IEventBus _eventBus;
        private readonly MobaCombatTransactionPipeline _transactions;

        public HealPipelineService(
            MobaDamageService commitPort,
            AbilityKit.Triggering.Eventing.IEventBus eventBus = null,
            MobaCombatTransactionPipeline transactions = null)
        {
            _commitPort = commitPort ?? throw new ArgumentNullException(nameof(commitPort));
            _eventBus = eventBus;
            _transactions = transactions;
        }

        public MobaHealthChangeResult Execute(in MobaHealRequest request)
        {
            return Execute(_commitPort, _eventBus, _transactions, in request);
        }

        internal static MobaHealthChangeResult Execute(
            MobaDamageService commitPort,
            AbilityKit.Triggering.Eventing.IEventBus eventBus,
            MobaCombatTransactionPipeline transactions,
            in MobaHealRequest request,
            Action onCommitted = null)
        {
            if (commitPort == null) throw new ArgumentNullException(nameof(commitPort));
            if (!IsValid(in request)) return default;
            var contexts = commitPort.HealExecutionContexts;
            if (contexts == null)
                throw new InvalidOperationException("Heal execution requires MobaExecutionContextRegistry.");
            var transaction = new MobaHealTransaction(in request);
            PublishBefore(eventBus, in request, transaction);
            MobaHealthChangeResult result = default;
            var nodeId = 0L;
            var createdFrame = 0;
            var hpCommitted = false;
            try
            {
                var committed = transactions == null
                    ? IsValid(transaction) && Commit(transaction)
                    : transactions.TryExecute(transaction, IsValid, Commit);
                if (!committed) return default;
                if (result.Succeeded) PublishAfter(eventBus, in result);
                return result;
            }
            finally
            {
                if (nodeId != 0L)
                    contexts.End(nodeId, (int)(hpCommitted ? MobaExecutionEndReason.Completed : MobaExecutionEndReason.Failed),
                        commitPort.HealExecutionFrame != 0 ? commitPort.HealExecutionFrame : createdFrame);
            }

            bool Commit(MobaHealTransaction current)
            {
                var parent = current.Origin;
                createdFrame = commitPort.HealExecutionFrame;
                var node = contexts.Create(new MobaExecutionContextCreateRequest(
                    MobaExecutionKind.HealApply,
                    current.ReasonParam != 0 ? current.ReasonParam : parent.ImmediateConfigId,
                    current.HealerActorId,
                    current.TargetActorId,
                    parentContextId: parent.EffectiveParentContextId,
                    rootContextId: parent.EffectiveRootContextId,
                    ownerContextId: parent.OwnerContextId,
                    frame: createdFrame,
                    originKind: parent.ImmediateKind,
                    originConfigId: parent.ImmediateConfigId));
                nodeId = node.ContextId;
                var origin = MobaGameplayOriginBuilder.Create()
                    .FromOrigin(in parent)
                    .WithActors(current.HealerActorId, current.TargetActorId)
                    .WithRootContext(node.RootContextId)
                    .WithOwnerContext(node.OwnerContextId)
                    .WithLifecycleNode(MobaExecutionKind.HealApply, node.ConfigId, node.ContextId)
                    .Build();
                result = commitPort.CommitHealCore(
                    current.HealerActorId,
                    current.TargetActorId,
                    current.HealType,
                    current.Value,
                    current.ReasonKind,
                    current.ReasonParam,
                    origin,
                    current.AllowDeadTarget,
                    () =>
                    {
                        hpCommitted = true;
                        onCommitted?.Invoke();
                    });
                return result.Succeeded;
            }
        }

        private static bool IsValid(in MobaHealRequest request)
        {
            return request.TargetActorId > 0
                && request.Value > 0f
                && !float.IsNaN(request.Value)
                && !float.IsInfinity(request.Value);
        }

        private static bool IsValid(MobaHealTransaction request)
        {
            return request != null && !request.IsCancelled && request.TargetActorId > 0
                && request.Value > 0f && !float.IsNaN(request.Value) && !float.IsInfinity(request.Value);
        }

        private static void PublishBefore(AbilityKit.Triggering.Eventing.IEventBus eventBus, in MobaHealRequest request, MobaHealTransaction transaction)
        {
            if (eventBus == null) return;
            var eid = TriggeringIdUtil.GetEventEid(HealPipelineEvents.BeforeApply);
            eventBus.Publish(new EventKey<MobaHealRequest>(eid), in request);
            PublishObject(eventBus, eid, transaction);
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
