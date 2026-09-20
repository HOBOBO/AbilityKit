using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Game.Flow.Battle.ViewEvents.Triggering
{
    internal sealed class BattleTriggerEventBusResolver
    {
        public bool TryGetEventBus(IWorld world, out IEventBus bus)
        {
            bus = null;
            if (world?.Services == null) return false;
            return world.Services.TryResolve(out bus) && bus != null;
        }
    }

    public sealed class BattleTriggerEventViewAdapter : IDisposable
    {
        private readonly BattleTriggerEventBusResolver _eventBusResolver;
        private BattleTriggerEventViewBridge _bridge;

        public BattleTriggerEventViewAdapter(BattleLogicSession session, IBattleViewEventSink sink)
            : this(session, sink, new BattleTriggerEventBusResolver())
        {
        }

        internal BattleTriggerEventViewAdapter(
            BattleLogicSession session,
            IBattleViewEventSink sink,
            BattleTriggerEventBusResolver eventBusResolver)
        {
            _eventBusResolver = eventBusResolver ?? new BattleTriggerEventBusResolver();
            if (session == null) return;
            if (sink == null) return;

            if (session.TryGetWorld(out var world) && _eventBusResolver.TryGetEventBus(world, out var bus))
            {
                _bridge = new BattleTriggerEventViewBridge(bus, sink);
            }
        }

        public void Dispose()
        {
            _bridge?.Dispose();
            _bridge = null;
        }
    }
}
