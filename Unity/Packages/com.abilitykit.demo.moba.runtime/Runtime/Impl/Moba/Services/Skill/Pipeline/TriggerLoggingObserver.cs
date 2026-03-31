using System;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Ability.Share.Common.Log
{
    public sealed class TriggerLoggingObserver<TCtx> : ITriggerObserver<TCtx>
    {
        private readonly ISkillLogger _logger;
        private readonly bool _enabled;

        public TriggerLoggingObserver() : this(SkillLogger.Instance, true)
        {
        }

        public TriggerLoggingObserver(ISkillLogger logger, bool enabled = true)
        {
            _logger = logger ?? SkillLogger.Instance;
            _enabled = enabled;
        }

        public void OnEvaluate<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, bool passed, in ExecCtx<TCtx> ctx)
        {
            if (!_enabled) return;

            var (casterId, skillId, instanceId, extra) = ExtractContextInfo(in args);
            _logger.LogTriggerEvaluate(casterId, skillId, instanceId, key.ToString(), passed, extra);
        }

        public void OnExecute<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, in ExecCtx<TCtx> ctx)
        {
            if (!_enabled) return;

            var (casterId, skillId, instanceId, extra) = ExtractContextInfo(in args);
            _logger.LogTriggerExecute(casterId, skillId, instanceId, key.ToString(), extra);
        }

        public void OnShortCircuit<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, ETriggerShortCircuitReason reason, in ExecCtx<TCtx> ctx)
        {
            if (!_enabled) return;

            var (casterId, skillId, instanceId, extra) = ExtractContextInfo(in args);

            using (var scope = _logger.Scope(casterId, skillId, instanceId))
            {
                scope.WithExtra("EventKey", key.ToString())
                     .WithExtra("Reason", reason.ToString())
                     .Log(SkillLogLevel.Debug, "TriggerShort", $"ShortCircuit: {reason}");
            }
        }

        public void OnConditionPassed<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int conditionId, string conditionName, in ExecCtx<TCtx> ctx)
        {
            if (!_enabled) return;
            var (casterId, skillId, instanceId, extra) = ExtractContextInfo(in args);
            using (var scope = _logger.Scope(casterId, skillId, instanceId))
            {
                scope.WithExtra("EventKey", key.ToString())
                     .WithExtra("ConditionName", conditionName)
                     .WithExtra("ConditionId", conditionId)
                     .Log(SkillLogLevel.Debug, "Condition", $"ConditionPassed: {conditionName}(Id={conditionId})");
            }
        }

        public void OnConditionFailed<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int conditionId, string conditionName, in ExecCtx<TCtx> ctx)
        {
            if (!_enabled) return;
            var (casterId, skillId, instanceId, extra) = ExtractContextInfo(in args);
            using (var scope = _logger.Scope(casterId, skillId, instanceId))
            {
                scope.WithExtra("EventKey", key.ToString())
                     .WithExtra("ConditionName", conditionName)
                     .WithExtra("ConditionId", conditionId)
                     .Log(SkillLogLevel.Debug, "Condition", $"ConditionFailed: {conditionName}(Id={conditionId})");
            }
        }

        public void OnActionExecuting<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions, in ExecCtx<TCtx> ctx)
        {
            if (!_enabled) return;
            var (casterId, skillId, instanceId, extra) = ExtractContextInfo(in args);
            using (var scope = _logger.Scope(casterId, skillId, instanceId))
            {
                scope.WithExtra("EventKey", key.ToString())
                     .WithExtra("ActionName", actionName)
                     .WithExtra("ActionId", actionId)
                     .WithExtra("ActionIndex", actionIndex)
                     .WithExtra("TotalActions", totalActions)
                     .Log(SkillLogLevel.Debug, "Action", $"ActionExecuting: [{actionIndex}/{totalActions}] {actionName}(Id={actionId})");
            }
        }

        public void OnActionExecuted<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions, bool wasInterrupted, in ExecCtx<TCtx> ctx)
        {
            if (!_enabled) return;
            var (casterId, skillId, instanceId, extra) = ExtractContextInfo(in args);
            using (var scope = _logger.Scope(casterId, skillId, instanceId))
            {
                scope.WithExtra("EventKey", key.ToString())
                     .WithExtra("ActionName", actionName)
                     .WithExtra("ActionId", actionId)
                     .WithExtra("ActionIndex", actionIndex)
                     .WithExtra("TotalActions", totalActions)
                     .WithExtra("WasInterrupted", wasInterrupted)
                     .Log(wasInterrupted ? SkillLogLevel.Warning : SkillLogLevel.Debug, "Action", $"ActionExecuted: [{actionIndex}/{totalActions}] {actionName}(Id={actionId}) Interrupted={wasInterrupted}");
            }
        }

        public void OnActionFailed<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions, string errorMessage, in ExecCtx<TCtx> ctx)
        {
            if (!_enabled) return;
            var (casterId, skillId, instanceId, extra) = ExtractContextInfo(in args);
            using (var scope = _logger.Scope(casterId, skillId, instanceId))
            {
                scope.WithExtra("EventKey", key.ToString())
                     .WithExtra("ActionName", actionName)
                     .WithExtra("ActionId", actionId)
                     .WithExtra("ActionIndex", actionIndex)
                     .WithExtra("TotalActions", totalActions)
                     .Log(SkillLogLevel.Error, "Action", $"ActionFailed: [{actionIndex}/{totalActions}] {actionName}(Id={actionId}) Error={errorMessage}");
            }
        }

        private static (int casterId, int skillId, long instanceId, string extra) ExtractContextInfo<TArgs>(in TArgs args)
        {
            int casterId = 0;
            int skillId = 0;
            long instanceId = 0;
            string extra = null;

            try
            {
                if (args is SkillCastContext scc)
                {
                    casterId = scc.CasterActorId;
                    skillId = scc.SkillId;
                    instanceId = scc.SourceContextId;
                    extra = $"Target={scc.TargetActorId} Slot={scc.SkillSlot} Level={scc.SkillLevel}";
                }
                else if (args != null)
                {
                    extra = $"Type={args.GetType().Name}";
                }
            }
            catch { }

            return (casterId, skillId, instanceId, extra);
        }
    }
}
