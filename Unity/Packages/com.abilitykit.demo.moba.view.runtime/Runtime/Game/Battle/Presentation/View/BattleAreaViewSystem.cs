using AbilityKit.Protocol.Moba;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Hierarchy;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleAreaViewSystem
    {
        private readonly BattleAreaViewStore _areaViews;
        private readonly BattleViewResourceProvider _resources;
        private readonly BattleAreaViewHandleFactory _handles;

        public BattleAreaViewSystem(
            BattleViewResourceProvider resources = null,
            BattleAreaVfxPool areaVfxPool = null,
            BattleViewHierarchyManager hierarchy = null)
            : this(resources, areaVfxPool, hierarchy, null)
        {
        }

        internal BattleAreaViewSystem(
            BattleViewResourceProvider resources,
            BattleAreaVfxPool areaVfxPool,
            BattleViewHierarchyManager hierarchy,
            BattleAreaViewSystemFactory factory)
        {
            factory ??= new BattleAreaViewSystemFactory();

            _resources = BattleViewResourceProvider.OrDefault(resources);
            _areaViews = factory.CreateStore();
            _handles = factory.CreateHandles(_resources, areaVfxPool, hierarchy);
        }

        public void HandleSnapshot(
            BattleViewBinder binder,
            IBattleEntityQuery query,
            MobaAreaEventSnapshotEntry[] entries)
        {
            if (entries == null || entries.Length == 0) return;
            if (query == null) return;
            if (_resources.GetOrLoadConfigs() == null) return;

            for (var i = 0; i < entries.Length; i++)
            {
                var evt = entries[i];
                if (evt.AreaId <= 0) continue;

                if (evt.Kind == (int)AreaEventKind.Spawn)
                {
                    Spawn(binder, in evt);
                }
                else if (evt.Kind == (int)AreaEventKind.Expire)
                {
                    Expire(evt.AreaId);
                }
            }
        }

        public void Clear()
        {
            _areaViews.Clear();
        }

        private void Spawn(BattleViewBinder binder, in MobaAreaEventSnapshotEntry evt)
        {
            if (_areaViews.Contains(evt.AreaId)) return;

            var handle = _handles.TryCreate(binder, in evt);
            if (handle == null) return;

            _areaViews.Add(handle);
        }

        private void Expire(int areaId)
        {
            _areaViews.Expire(areaId);
        }
    }

    internal sealed class BattleAreaViewSystemFactory
    {
        public BattleAreaViewStore CreateStore()
        {
            return new BattleAreaViewStore();
        }

        public BattleAreaViewHandleFactory CreateHandles(
            BattleViewResourceProvider resources,
            BattleAreaVfxPool areaVfxPool,
            BattleViewHierarchyManager hierarchy = null)
        {
            return new BattleAreaViewHandleFactory(
                resources,
                areaVfxPool: areaVfxPool,
                hierarchy: hierarchy);
        }
    }
}
