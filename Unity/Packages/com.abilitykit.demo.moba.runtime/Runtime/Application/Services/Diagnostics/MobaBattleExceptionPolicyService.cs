using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Demo.Moba.Services
{
    public enum MobaBattleExceptionSeverity
    {
        Trace = 0,
        Warning = 1,
        Recoverable = 2,
        Critical = 3,
        Fatal = 4,
    }

    public enum MobaBattleExceptionDomain
    {
        Unknown = 0,
        Bootstrap = 1,
        WorldSystem = 2,
        Service = 3,
        Skill = 4,
        Buff = 5,
        Projectile = 6,
        Damage = 7,
        Triggering = 8,
        Cleanup = 9,
        Snapshot = 10,
        Input = 11,
        Summon = 12,
    }

    public readonly struct MobaBattleExceptionContext
    {
        public readonly MobaBattleExceptionDomain Domain;
        public readonly string Operation;
        public readonly int ActorId;
        public readonly int SkillId;
        public readonly long RuntimeId;
        public readonly long RootContextId;
        public readonly long SourceContextId;
        public readonly MobaSkillCastRuntimeHandle RuntimeHandle;
        public readonly string Detail;

        public MobaBattleExceptionContext(
            MobaBattleExceptionDomain domain,
            string operation,
            int actorId = 0,
            int skillId = 0,
            long runtimeId = 0L,
            string detail = null,
            long rootContextId = 0L,
            long sourceContextId = 0L,
            MobaSkillCastRuntimeHandle runtimeHandle = default)
        {
            Domain = domain;
            Operation = operation;
            ActorId = actorId;
            SkillId = skillId;
            RuntimeId = runtimeId != 0L ? runtimeId : runtimeHandle.RuntimeId;
            RootContextId = rootContextId != 0L ? rootContextId : runtimeHandle.RootContextId;
            SourceContextId = sourceContextId;
            RuntimeHandle = runtimeHandle;
            Detail = detail;
        }

        public string BuildKey(MobaBattleExceptionSeverity severity)
        {
            var operation = string.IsNullOrEmpty(Operation) ? "unknown" : Operation;
            return "exception:" + Domain + ":" + severity + ":" + operation;
        }

        public string BuildMessage(MobaBattleExceptionSeverity severity)
        {
            var operation = string.IsNullOrEmpty(Operation) ? "unknown" : Operation;
            var message = $"Battle exception. severity={severity} domain={Domain} operation={operation}";
            if (ActorId != 0) message += $" actor={ActorId}";
            if (SkillId != 0) message += $" skill={SkillId}";
            if (RuntimeId != 0L) message += $" runtime={RuntimeId}";
            if (RootContextId != 0L) message += $" rootContextId={RootContextId}";
            if (SourceContextId != 0L) message += $" sourceContextId={SourceContextId}";
            if (RuntimeHandle.IsValid) message += $" runtimeHandle={RuntimeHandle}";
            if (!string.IsNullOrEmpty(Detail)) message += " " + Detail;
            return message;
        }

        public MobaBattleDiagnosticContext ToDiagnosticContext()
        {
            return new MobaBattleDiagnosticContext(RootContextId, SourceContextId, RuntimeHandle, ActorId, SkillId, Detail);
        }
    }

    public interface IMobaBattleExceptionPolicy
    {
        void Handle(Exception exception, in MobaBattleExceptionContext context, MobaBattleExceptionSeverity severity);
        bool TryHandle(Exception exception, in MobaBattleExceptionContext context, MobaBattleExceptionSeverity severity);
    }

    [WorldService(typeof(IMobaBattleExceptionPolicy), WorldLifetime.Scoped)]
    [WorldService(typeof(MobaBattleExceptionPolicyService), WorldLifetime.Scoped)]
    public sealed class MobaBattleExceptionPolicyService : IMobaBattleExceptionPolicy, IService
    {
        [WorldInject(required: false)] private IMobaBattleDiagnosticsService _diagnostics = null;

        public void Handle(Exception exception, in MobaBattleExceptionContext context, MobaBattleExceptionSeverity severity)
        {
            if (!TryHandle(exception, in context, severity) && IsFatal(severity))
            {
                throw exception;
            }
        }

        public bool TryHandle(Exception exception, in MobaBattleExceptionContext context, MobaBattleExceptionSeverity severity)
        {
            if (exception == null) return false;

            var key = context.BuildKey(severity);
            var message = context.BuildMessage(severity);
            var maxCount = GetMaxCount(severity);

            if (_diagnostics != null)
            {
                var diagnosticContext = context.ToDiagnosticContext();
                _diagnostics.Exception(key, exception, message, in diagnosticContext, maxCount);
                _diagnostics.Counter(MobaBattleDiagnosticMetric.ExceptionPrefix + context.Domain);
                _diagnostics.Counter(MobaBattleDiagnosticMetric.ExceptionPrefix + severity);
                return true;
            }

            AbilityKit.Core.Logging.Log.Exception(exception, "[MobaExceptionPolicy] " + message);
            return true;
        }

        public void Dispose()
        {
        }

        private static int GetMaxCount(MobaBattleExceptionSeverity severity)
        {
            switch (severity)
            {
                case MobaBattleExceptionSeverity.Trace:
                    return 1;
                case MobaBattleExceptionSeverity.Warning:
                    return MobaBattleDiagnosticsDefaults.DefaultWarningLimit;
                case MobaBattleExceptionSeverity.Recoverable:
                    return MobaBattleDiagnosticsDefaults.DefaultExceptionLimit;
                case MobaBattleExceptionSeverity.Critical:
                case MobaBattleExceptionSeverity.Fatal:
                    return 0;
                default:
                    return MobaBattleDiagnosticsDefaults.DefaultExceptionLimit;
            }
        }

        private static bool IsFatal(MobaBattleExceptionSeverity severity)
        {
            return severity == MobaBattleExceptionSeverity.Fatal;
        }
    }
}
