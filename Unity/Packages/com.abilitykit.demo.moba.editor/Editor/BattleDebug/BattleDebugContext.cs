using System;
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
            BattleDebugEntityId selectedId,
            IUnitFacade selectedUnit,
            Action requestRepaint,
            Action<long> selectActor = null,
            Action<long, long> openTrace = null,
            Action<long> openEvents = null,
            Action<BattleDiagnosticEvent> openEvent = null,
            Action openRecentFailures = null,
            Action<BattleDebugConfigReference> openConfig = null,
            Func<int, bool> seekReplayFrame = null,
            IBattleDiagnosticReadOnlySession diagnosticSession = null,
            MobaSkillCastRuntimeService skillRuntimeService = null,
            BattleDebugDiagnosticSessionResolution diagnosticResolution = default,
            bool isOffline = false,
            BattleDiagnosticWorkspaceState workspaceState = null)
        {
            Facade = facade;
            SelectedId = selectedId;
            SelectedUnit = selectedUnit;
            RequestRepaint = requestRepaint;
            SelectActor = selectActor;
            OpenTrace = openTrace;
            OpenEvents = openEvents;
            OpenEvent = openEvent;
            OpenRecentFailures = openRecentFailures;
            OpenConfig = openConfig;
            SeekReplayFrame = seekReplayFrame;
            DiagnosticSession = diagnosticSession;
            SkillRuntimeService = skillRuntimeService;
            DiagnosticResolution = diagnosticResolution;
            IsOffline = isOffline;
            WorkspaceState = workspaceState;
        }

        public IBattleDebugFacade Facade { get; }
        public BattleDebugEntityId SelectedId { get; }
        public IUnitFacade SelectedUnit { get; }
        public Action RequestRepaint { get; }
        public Action<long> SelectActor { get; }
        public Action<long, long> OpenTrace { get; }
        public Action<long> OpenEvents { get; }
        public Action<BattleDiagnosticEvent> OpenEvent { get; }
        public Action OpenRecentFailures { get; }
        public Action<BattleDebugConfigReference> OpenConfig { get; }
        public Func<int, bool> SeekReplayFrame { get; }
        public IBattleDiagnosticReadOnlySession DiagnosticSession { get; }
        public MobaSkillCastRuntimeService SkillRuntimeService { get; }
        public BattleDebugDiagnosticSessionResolution DiagnosticResolution { get; }
        public bool IsOffline { get; }
        public BattleDiagnosticWorkspaceState WorkspaceState { get; }

        public bool HasSelection => SelectedId.IsValid;
        public bool HasRuntimeSelection => SelectedId.IsValid && SelectedUnit != null;
    }
}
