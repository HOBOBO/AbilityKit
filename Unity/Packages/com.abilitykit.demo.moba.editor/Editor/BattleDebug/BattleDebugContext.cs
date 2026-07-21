using System;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.ECS;
using AbilityKit.Game.Battle;

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
            Action<long, long> openTrace = null)
        {
            Facade = facade;
            SelectedId = selectedId;
            SelectedUnit = selectedUnit;
            RequestRepaint = requestRepaint;
            SelectActor = selectActor;
            OpenTrace = openTrace;
        }

        public IBattleDebugFacade Facade { get; }
        public EcsEntityId SelectedId { get; }
        public IUnitFacade SelectedUnit { get; }
        public Action RequestRepaint { get; }
        public Action<long> SelectActor { get; }
        public Action<long, long> OpenTrace { get; }

        public bool HasSelection => SelectedId.IsValid && SelectedUnit != null;
    }
}
