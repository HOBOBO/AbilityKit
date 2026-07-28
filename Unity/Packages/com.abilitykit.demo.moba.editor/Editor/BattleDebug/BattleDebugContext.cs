using System;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.ECS;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Editor.Diagnostics;

namespace AbilityKit.Game.Editor
{
    internal readonly struct BattleDebugContext
    {
        public BattleDebugContext(
            IBattleDebugFacade facade,
            EcsEntityId selectedId,
            IUnitFacade selectedUnit,
            Action requestRepaint,
            Action<long> selectActor = null,
            Action<long, long> openTrace = null,
            Action<long> openEvents = null,
            Action<BattleDebugConfigReference> openConfig = null,
            Func<int, bool> seekReplayFrame = null,
            IBattleDiagnosticReadOnlySession diagnosticSession = null,
            MobaSkillCastRuntimeService skillRuntimeService = null,
            BattleDebugDiagnosticSessionResolution diagnosticResolution = default,
            bool isOffline = false)
        {
            Facade = facade;
            SelectedId = selectedId;
            SelectedUnit = selectedUnit;
            RequestRepaint = requestRepaint;
            SelectActor = selectActor;
            OpenTrace = openTrace;
            OpenEvents = openEvents;
            OpenConfig = openConfig;
            SeekReplayFrame = seekReplayFrame;
            DiagnosticSession = diagnosticSession;
            SkillRuntimeService = skillRuntimeService;
            DiagnosticResolution = diagnosticResolution;
            IsOffline = isOffline;
        }

        public IBattleDebugFacade Facade { get; }
        public EcsEntityId SelectedId { get; }
        public IUnitFacade SelectedUnit { get; }
        public Action RequestRepaint { get; }
        public Action<long> SelectActor { get; }
        public Action<long, long> OpenTrace { get; }
        public Action<long> OpenEvents { get; }
        public Action<BattleDebugConfigReference> OpenConfig { get; }
        public Func<int, bool> SeekReplayFrame { get; }
        public IBattleDiagnosticReadOnlySession DiagnosticSession { get; }
        public MobaSkillCastRuntimeService SkillRuntimeService { get; }
        public BattleDebugDiagnosticSessionResolution DiagnosticResolution { get; }
        public bool IsOffline { get; }

        public bool HasSelection => SelectedId.IsValid;
        public bool HasRuntimeSelection => SelectedId.IsValid && SelectedUnit != null;
    }
}
