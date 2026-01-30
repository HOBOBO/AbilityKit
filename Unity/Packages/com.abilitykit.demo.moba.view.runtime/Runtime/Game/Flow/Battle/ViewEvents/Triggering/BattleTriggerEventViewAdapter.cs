using System;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle;

namespace AbilityKit.Game.Flow.Battle.ViewEvents.Triggering
{
    public sealed class BattleTriggerEventViewAdapter : IDisposable
    {
        private BattleTriggerEventViewBridge _bridge;

        public BattleTriggerEventViewAdapter(BattleLogicSession session, IBattleViewEventSink sink)
        {
            if (session == null) return;
            if (sink == null) return;

            if (session.TryGetWorld(out var world) && TryGetEventBus(world, out var bus))
            {
                _bridge = new BattleTriggerEventViewBridge(bus, sink);
            }
        }

        public void Dispose()
        {
            _bridge?.Dispose();
            _bridge = null;
        }

        private static bool TryGetEventBus(IWorld world, out IEventBus bus)
        {
            bus = null;
            if (world?.Services == null) return false;
            return world.Services.TryResolve(out bus) && bus != null;
        }
    }
}
