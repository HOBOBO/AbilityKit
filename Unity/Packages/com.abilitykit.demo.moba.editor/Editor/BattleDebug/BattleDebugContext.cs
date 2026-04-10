using System;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.ECS;
using AbilityKit.Game.Battle;

namespace AbilityKit.Game.Editor
{
    internal readonly struct BattleDebugContext
    {
        public BattleDebugContext(IBattleDebugFacade facade, EcsEntityId selectedId, IUnitFacade selectedUnit, Action requestRepaint)
        {
            Facade = facade;
            SelectedId = selectedId;
            SelectedUnit = selectedUnit;
            RequestRepaint = requestRepaint;
        }

        public IBattleDebugFacade Facade { get; }
        public EcsEntityId SelectedId { get; }
        public IUnitFacade SelectedUnit { get; }
        public Action RequestRepaint { get; }

        public bool HasSelection => SelectedId.IsValid && SelectedUnit != null;
    }
}
