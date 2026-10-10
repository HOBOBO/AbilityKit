using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Demo.Moba.Systems
{
    public readonly struct MobaWorldSystemServices
    {
        public readonly IMobaBattleDiagnosticsService Diagnostics;
        public readonly IMobaBattleExceptionPolicy Exceptions;

        public MobaWorldSystemServices(IMobaBattleDiagnosticsService diagnostics, IMobaBattleExceptionPolicy exceptions)
        {
            Diagnostics = diagnostics;
            Exceptions = exceptions;
        }

        public long StartTimestamp => Diagnostics != null ? Diagnostics.GetTimestamp() : 0L;
    }

    public static class MobaWorldSystemExecution
    {
        public static MobaWorldSystemServices Resolve(IWorldResolver services)
        {
            IMobaBattleDiagnosticsService diagnostics = null;
            IMobaBattleExceptionPolicy exceptions = null;

            services?.TryResolve(out diagnostics);
            services?.TryResolve(out exceptions);

            return new MobaWorldSystemServices(diagnostics, exceptions);
        }

        public static void Warn(in MobaWorldSystemServices services, string key, string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            if (services.Diagnostics != null)
            {
                services.Diagnostics.Warning(key, message);
                return;
            }

            AbilityKit.Core.Logging.Log.Warning(message);
        }

        public static void Require(
            bool condition,
            IWorldResolver resolver,
            string owner,
            string operation,
            string requirement,
            string detail = null,
            MobaBattleExceptionDomain domain = MobaBattleExceptionDomain.WorldSystem,
            MobaBattleExceptionSeverity severity = MobaBattleExceptionSeverity.Critical)
        {
            if (condition) return;

            MobaRuntimeGuard.ThrowRequired(
                resolver,
                owner,
                operation,
                requirement,
                domain,
                severity,
                detail);
        }

        public static void HandleException(
            in MobaWorldSystemServices services,
            Exception exception,
            string owner,
            string operation,
            MobaBattleExceptionSeverity severity = MobaBattleExceptionSeverity.Fatal,
            int actorId = 0,
            int skillId = 0,
            long runtimeId = 0L,
            string detail = null)
        {
            if (exception == null) return;

            if (services.Exceptions != null)
            {
                services.Exceptions.Handle(
                    exception,
                    new MobaBattleExceptionContext(
                        MobaBattleExceptionDomain.WorldSystem,
                        operation,
                        actorId: actorId,
                        skillId: skillId,
                        runtimeId: runtimeId,
                        detail: detail),
                    severity);
                if (severity >= MobaBattleExceptionSeverity.Critical)
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
                return;
            }

            var message = $"[{owner}] {severity} failure. operation={operation}";
            if (actorId != 0) message += $" actor={actorId}";
            if (skillId != 0) message += $" skill={skillId}";
            if (runtimeId != 0L) message += $" runtime={runtimeId}";
            if (!string.IsNullOrEmpty(detail)) message += " " + detail;

            if (services.Diagnostics != null)
            {
                services.Diagnostics.Exception(operation, exception, message);
            }
            else
                AbilityKit.Core.Logging.Log.Exception(exception, message);
            if (severity >= MobaBattleExceptionSeverity.Critical)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
        }

        public static void Sample(in MobaWorldSystemServices services, string metricName, double value)
        {
            services.Diagnostics?.Sample(metricName, value);
        }

        public static void RecordDuration(
            in MobaWorldSystemServices services,
            string metricName,
            long startTimestamp,
            double warnThresholdMs,
            string context = null)
        {
            services.Diagnostics?.RecordDuration(metricName, startTimestamp, warnThresholdMs, context);
        }
    }
}
