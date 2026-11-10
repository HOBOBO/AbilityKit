using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Triggering;

namespace AbilityKit.Demo.Moba.Runtime.Application.Services.Triggering
{
    public readonly struct MobaTriggerExecutionStats
    {
        public readonly long DirectRequests;
        public readonly long DirectExecuted;
        public readonly long DirectInvalidRequests;
        public readonly long DirectMissingService;
        public readonly long OwnerApplyRequests;
        public readonly long OwnerApplied;
        public readonly long OwnerStopRequests;
        public readonly long OwnerStopped;
        public readonly long OwnerInvalidRequests;
        public readonly long OwnerMissingService;
        public readonly int LastTriggerId;
        public readonly long LastOwnerKey;
        public readonly int LastTriggerCount;
        public readonly string LastSource;
        public readonly string LastPayloadType;
        public readonly string LastOwnerOperation;

        private MobaTriggerExecutionStats(
            long directRequests,
            long directExecuted,
            long directInvalidRequests,
            long directMissingService,
            long ownerApplyRequests,
            long ownerApplied,
            long ownerStopRequests,
            long ownerStopped,
            long ownerInvalidRequests,
            long ownerMissingService,
            int lastTriggerId,
            long lastOwnerKey,
            int lastTriggerCount,
            string lastSource,
            string lastPayloadType,
            string lastOwnerOperation)
        {
            DirectRequests = directRequests;
            DirectExecuted = directExecuted;
            DirectInvalidRequests = directInvalidRequests;
            DirectMissingService = directMissingService;
            OwnerApplyRequests = ownerApplyRequests;
            OwnerApplied = ownerApplied;
            OwnerStopRequests = ownerStopRequests;
            OwnerStopped = ownerStopped;
            OwnerInvalidRequests = ownerInvalidRequests;
            OwnerMissingService = ownerMissingService;
            LastTriggerId = lastTriggerId;
            LastOwnerKey = lastOwnerKey;
            LastTriggerCount = lastTriggerCount;
            LastSource = lastSource;
            LastPayloadType = lastPayloadType;
            LastOwnerOperation = lastOwnerOperation;
        }

        public MobaTriggerExecutionStats WithDirectRequest(int triggerId, string source, string payloadType)
        {
            return Copy(directRequests: DirectRequests + 1, lastTriggerId: triggerId, lastSource: source, lastPayloadType: payloadType);
        }

        public MobaTriggerExecutionStats WithDirectExecuted()
        {
            return Copy(directExecuted: DirectExecuted + 1);
        }

        public MobaTriggerExecutionStats WithInvalidDirectRequest(int triggerId, string source)
        {
            return Copy(directInvalidRequests: DirectInvalidRequests + 1, lastTriggerId: triggerId, lastSource: source);
        }

        public MobaTriggerExecutionStats WithDirectMissingService()
        {
            return Copy(directMissingService: DirectMissingService + 1);
        }

        public MobaTriggerExecutionStats WithOwnerApplyRequest(long ownerKey, int triggerCount, string source)
        {
            return Copy(ownerApplyRequests: OwnerApplyRequests + 1, lastOwnerKey: ownerKey, lastTriggerCount: triggerCount, lastSource: source, lastOwnerOperation: "apply");
        }

        public MobaTriggerExecutionStats WithOwnerApplied()
        {
            return Copy(ownerApplied: OwnerApplied + 1);
        }

        public MobaTriggerExecutionStats WithOwnerStopRequest(long ownerKey, string source)
        {
            return Copy(ownerStopRequests: OwnerStopRequests + 1, lastOwnerKey: ownerKey, lastSource: source, lastOwnerOperation: "stop");
        }

        public MobaTriggerExecutionStats WithOwnerStopped()
        {
            return Copy(ownerStopped: OwnerStopped + 1);
        }

        public MobaTriggerExecutionStats WithInvalidOwnerRequest(string operation, string source)
        {
            return Copy(ownerInvalidRequests: OwnerInvalidRequests + 1, lastSource: source, lastOwnerOperation: operation);
        }

        public MobaTriggerExecutionStats WithOwnerMissingService()
        {
            return Copy(ownerMissingService: OwnerMissingService + 1);
        }

        private MobaTriggerExecutionStats Copy(
            long? directRequests = null,
            long? directExecuted = null,
            long? directInvalidRequests = null,
            long? directMissingService = null,
            long? ownerApplyRequests = null,
            long? ownerApplied = null,
            long? ownerStopRequests = null,
            long? ownerStopped = null,
            long? ownerInvalidRequests = null,
            long? ownerMissingService = null,
            int? lastTriggerId = null,
            long? lastOwnerKey = null,
            int? lastTriggerCount = null,
            string lastSource = null,
            string lastPayloadType = null,
            string lastOwnerOperation = null)
        {
            return new MobaTriggerExecutionStats(
                directRequests ?? DirectRequests,
                directExecuted ?? DirectExecuted,
                directInvalidRequests ?? DirectInvalidRequests,
                directMissingService ?? DirectMissingService,
                ownerApplyRequests ?? OwnerApplyRequests,
                ownerApplied ?? OwnerApplied,
                ownerStopRequests ?? OwnerStopRequests,
                ownerStopped ?? OwnerStopped,
                ownerInvalidRequests ?? OwnerInvalidRequests,
                ownerMissingService ?? OwnerMissingService,
                lastTriggerId ?? LastTriggerId,
                lastOwnerKey ?? LastOwnerKey,
                lastTriggerCount ?? LastTriggerCount,
                lastSource ?? LastSource,
                lastPayloadType ?? LastPayloadType,
                lastOwnerOperation ?? LastOwnerOperation);
        }
    }

    /// <summary>
    /// MOBA 触发器执行网关：统一收敛直接执行和 owner-bound 订阅两类触发器入口，便于后续做预算、追踪、诊断和策略管控。
    /// </summary>
    [WorldService(typeof(MobaTriggerExecutionGateway))]
    public sealed class MobaTriggerExecutionGateway : IWorldInitializable
    {
        [WorldInject(required: false)] private MobaEffectExecutionService _effects = null;
        [WorldInject(required: false)] private MobaTriggerPlanSubscriptionService _subscriptions = null;
        [WorldInject(required: false)] private IMobaBattleDiagnosticsService _diagnostics = null;

        public MobaTriggerExecutionStats Stats { get; private set; }

        public MobaTriggerExecutionGateway()
        {
        }

        public MobaTriggerExecutionGateway(MobaEffectExecutionService effects, MobaTriggerPlanSubscriptionService subscriptions)
            : this(effects, subscriptions, null)
        {
        }

        public MobaTriggerExecutionGateway(MobaEffectExecutionService effects, MobaTriggerPlanSubscriptionService subscriptions, IMobaBattleDiagnosticsService diagnostics)
        {
            _effects = effects;
            _subscriptions = subscriptions;
            _diagnostics = diagnostics;
        }

        public void OnInit(IWorldResolver services)
        {
            if (services == null) return;
            if (_effects == null) services.TryResolve(out _effects);
            if (_subscriptions == null) services.TryResolve(out _subscriptions);
            if (_diagnostics == null) services.TryResolve(out _diagnostics);
        }

        public void ExecuteDirectTrigger<TPayload>(in MobaTriggerExecutionRequest<TPayload> request)
        {
            if (request.TriggerId <= 0)
            {
                RecordInvalidDirectTrigger(request.TriggerId, request.Source);
                return;
            }

            Stats = Stats.WithDirectRequest(request.TriggerId, request.Source, request.PayloadTypeName);
            _diagnostics?.Counter("moba.trigger.direct.requested");
            if (_effects == null)
            {
                Stats = Stats.WithDirectMissingService();
                _diagnostics?.Counter("moba.trigger.direct.missingEffects");
                MobaRuntimeGuard.ThrowRequired(
                    nameof(MobaTriggerExecutionGateway),
                    "trigger.direct.execute",
                    nameof(MobaEffectExecutionService),
                    $"triggerId={request.TriggerId} source={request.Source} payloadType={request.PayloadTypeName}");
            }

            if (_effects.ExecuteTrigger(in request))
            {
                Stats = Stats.WithDirectExecuted();
                _diagnostics?.Counter("moba.trigger.direct.executed");
            }
        }

        public void ApplyOwnerBoundTriggers(IReadOnlyList<int> triggerIds, long ownerKey, string source = null)
        {
            if (ownerKey == 0)
            {
                RecordInvalidOwnerKey("apply", source);
                return;
            }

            var triggerCount = triggerIds != null ? triggerIds.Count : 0;
            Stats = Stats.WithOwnerApplyRequest(ownerKey, triggerCount, source);
            _diagnostics?.Counter("moba.trigger.owner.apply.requested");
            _diagnostics?.Sample("moba.trigger.owner.apply.count", triggerCount);
            if (_subscriptions == null)
            {
                Stats = Stats.WithOwnerMissingService();
                _diagnostics?.Counter("moba.trigger.owner.apply.missingSubscriptions");
                MobaRuntimeGuard.ThrowRequired(
                    nameof(MobaTriggerExecutionGateway),
                    "trigger.owner.apply",
                    nameof(MobaTriggerPlanSubscriptionService),
                    $"ownerKey={ownerKey} triggerCount={triggerCount} source={source}");
            }

            _subscriptions.ApplyTriggers(triggerIds, ownerKey);
            Stats = Stats.WithOwnerApplied();
            _diagnostics?.Counter("moba.trigger.owner.apply.executed");
        }

        public void StopOwnerBoundTriggers(long ownerKey, string source = null)
        {
            if (ownerKey == 0)
            {
                RecordInvalidOwnerKey("stop", source);
                return;
            }

            Stats = Stats.WithOwnerStopRequest(ownerKey, source);
            _diagnostics?.Counter("moba.trigger.owner.stop.requested");
            if (_subscriptions == null)
            {
                Stats = Stats.WithOwnerMissingService();
                _diagnostics?.Counter("moba.trigger.owner.stop.missingSubscriptions");
                MobaRuntimeGuard.ThrowRequired(
                    nameof(MobaTriggerExecutionGateway),
                    "trigger.owner.stop",
                    nameof(MobaTriggerPlanSubscriptionService),
                    $"ownerKey={ownerKey} source={source}");
            }

            _subscriptions.Stop(ownerKey);
            Stats = Stats.WithOwnerStopped();
            _diagnostics?.Counter("moba.trigger.owner.stop.executed");
        }

        public void CopyActiveOwnerKeys(List<long> dest)
        {
            if (dest == null) return;
            if (_subscriptions == null)
            {
                dest.Clear();
                return;
            }

            _subscriptions.CopyActiveOwnerKeys(dest);
        }

        private void RecordInvalidDirectTrigger(int triggerId, string source)
        {
            Stats = Stats.WithInvalidDirectRequest(triggerId, source);
            RecordWarning("moba.trigger.direct.invalidId", $"[MobaTriggerExecutionGateway] direct trigger ignored because triggerId is invalid. triggerId={triggerId} source={source}");
            _diagnostics?.Counter("moba.trigger.direct.invalidId");
        }

        private void RecordInvalidOwnerKey(string operation, string source)
        {
            Stats = Stats.WithInvalidOwnerRequest(operation, source);
            RecordWarning("moba.trigger.owner.invalidOwnerKey", $"[MobaTriggerExecutionGateway] owner-bound trigger operation ignored because ownerKey is invalid. operation={operation} source={source}");
            _diagnostics?.Counter("moba.trigger.owner.invalidOwnerKey");
        }

        private void RecordWarning(string key, string message)
        {
            if (_diagnostics != null)
            {
                _diagnostics.Warning(key, message);
                return;
            }

            Log.Warning(message);
        }

        public void Dispose()
        {
        }
    }
}
