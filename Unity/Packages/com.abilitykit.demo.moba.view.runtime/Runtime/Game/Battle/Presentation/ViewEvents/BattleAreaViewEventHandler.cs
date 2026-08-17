using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Flow.Battle.View;
using AbilityKit.Protocol.Moba.StateSync;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    internal sealed class BattleAreaViewEventHandler
    {
        private readonly EC.IECWorld _world;
        private readonly IBattleEntityQuery _query;
        private readonly BattleViewBinder _binder;
        private readonly BattleAreaViewSystem _areaViews;

        public BattleAreaViewEventHandler(
            EC.IECWorld world,
            IBattleEntityQuery query,
            BattleViewBinder binder,
            BattleAreaViewSystem areaViews)
        {
            _world = world;
            _query = query;
            _binder = binder;
            _areaViews = areaViews;
        }

        public void HandleSnapshot(MobaAreaEventSnapshotEntry[] entries)
        {
            if (entries == null || entries.Length == 0) return;
            if (_world == null) return;
            if (_query == null) return;

            _areaViews?.HandleSnapshot(_binder, _query, entries);
        }
    }
}
