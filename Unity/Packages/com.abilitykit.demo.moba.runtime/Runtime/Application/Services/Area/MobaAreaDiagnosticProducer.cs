using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services.Area;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// Area 生命周期诊断事件草稿生成器。
    /// 从 <see cref="MobaAreaSyncSystem"/> 抽离为独立静态类，
    /// 避免诊断测试直接依赖 WorldSystemBase 所在的程序集。
    /// </summary>
    internal static class MobaAreaDiagnosticProducer
    {
        public static MobaBattleDiagnosticEventDraft CreateAreaSpawnedDraft(in MobaAreaRuntimeInfo info)
        {
            var rootContextId = info.RootContextId != 0L ? info.RootContextId : info.SourceContextId;
            var contextId = info.SourceContextId;
            var summary = $"areaId={info.AreaId}, templateId={info.TemplateId}, owner={info.OwnerActorId}";

            return new MobaBattleDiagnosticEventDraft(
                BattleDiagnosticEventKind.AreaSpawned,
                BattleDiagnosticEventChannel.TemporaryEntity,
                BattleDiagnosticEventOutcome.Succeeded,
                info.OwnerActorId,
                targetActorId: 0,
                info.TemplateId,
                rootContextId,
                contextId,
                summary: summary,
                subjectObject: BattleDiagnosticRuntimeObjectReference.Create(
                    BattleDiagnosticRuntimeObjectKind.Area,
                    info.AreaId));
        }

        public static MobaBattleDiagnosticEventDraft CreateAreaEndedDraft(in MobaAreaRuntimeInfo info)
        {
            var rootContextId = info.RootContextId != 0L ? info.RootContextId : info.SourceContextId;
            var contextId = info.SourceContextId;
            var summary = $"areaId={info.AreaId}, templateId={info.TemplateId}, owner={info.OwnerActorId}";

            return new MobaBattleDiagnosticEventDraft(
                BattleDiagnosticEventKind.AreaEnded,
                BattleDiagnosticEventChannel.TemporaryEntity,
                BattleDiagnosticEventOutcome.Succeeded,
                info.OwnerActorId,
                targetActorId: 0,
                info.TemplateId,
                rootContextId,
                contextId,
                summary: summary,
                subjectObject: BattleDiagnosticRuntimeObjectReference.Create(
                    BattleDiagnosticRuntimeObjectKind.Area,
                    info.AreaId));
        }
    }
}
