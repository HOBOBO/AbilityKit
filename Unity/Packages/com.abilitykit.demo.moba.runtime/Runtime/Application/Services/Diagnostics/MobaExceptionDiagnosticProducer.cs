using System;
using AbilityKit.Demo.Moba.Diagnostics;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 警告/异常诊断事件草稿生成器。
    /// 从 <see cref="MobaBattleDiagnosticsService"/> 抽离映射逻辑，便于独立测试。
    /// </summary>
    internal static class MobaExceptionDiagnosticProducer
    {
        public static MobaBattleDiagnosticEventDraft CreateWarningDraft(
            in MobaBattleDiagnosticContext context,
            string message)
        {
            return CreateDraft(
                BattleDiagnosticEventKind.Warning,
                BattleDiagnosticEventOutcome.None,
                in context,
                message);
        }

        public static MobaBattleDiagnosticEventDraft CreateExceptionDraft(
            in MobaBattleDiagnosticContext context,
            Exception exception,
            string message)
        {
            var summary = message ?? string.Empty;
            if (exception != null)
            {
                summary += ", type=" + exception.GetType().Name;
            }

            return CreateDraft(
                BattleDiagnosticEventKind.Exception,
                BattleDiagnosticEventOutcome.Failed,
                in context,
                summary);
        }

        private static MobaBattleDiagnosticEventDraft CreateDraft(
            BattleDiagnosticEventKind kind,
            BattleDiagnosticEventOutcome outcome,
            in MobaBattleDiagnosticContext context,
            string summary)
        {
            var handle = context.RuntimeHandle;
            var runtime = handle.IsValid
                ? new BattleDiagnosticRuntimeHandle(handle.RuntimeId, handle.Generation)
                : default;
            var rootContextId = context.RootContextId != 0L
                ? context.RootContextId
                : (handle.IsValid ? handle.RootContextId : 0L);

            return new MobaBattleDiagnosticEventDraft(
                kind,
                BattleDiagnosticEventChannel.WarningAndException,
                outcome,
                context.ActorId,
                targetActorId: 0,
                context.SkillId,
                rootContextId,
                context.SourceContextId,
                runtime,
                summary: summary);
        }
    }
}
