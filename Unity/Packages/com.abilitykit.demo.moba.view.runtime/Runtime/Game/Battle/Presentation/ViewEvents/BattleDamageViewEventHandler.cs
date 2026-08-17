using AbilityKit.Demo.Moba;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Flow.Battle.View;
using AbilityKit.Protocol.Moba.StateSync;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    internal static class BattleDamagePresentationSourcePolicy
    {
        public static bool ShouldPresentTrigger(BattleViewEventSourceMode mode)
        {
            return mode == BattleViewEventSourceMode.TriggerOnly;
        }

        public static bool ShouldPresentSnapshot(BattleViewEventSourceMode mode)
        {
            return mode == BattleViewEventSourceMode.SnapshotOnly ||
                   mode == BattleViewEventSourceMode.Hybrid;
        }
    }

    internal sealed class BattleDamageViewEventHandler
    {
        private readonly BattleDamageFloatingTextSpawner _floatingTexts;

        public BattleDamageViewEventHandler(
            EC.IECWorld world,
            IBattleEntityQuery query,
            in EC.IEntity vfxNode,
            BattleFloatingTextSystem floatingTexts)
            : this(world, query, in vfxNode, floatingTexts, null)
        {
        }

        internal BattleDamageViewEventHandler(
            EC.IECWorld world,
            IBattleEntityQuery query,
            in EC.IEntity vfxNode,
            BattleFloatingTextSystem floatingTexts,
            BattleDamageViewEventHandlerFactory handlers)
        {
            handlers ??= new BattleDamageViewEventHandlerFactory();
            _floatingTexts = handlers.CreateFloatingTexts(world, query, in vfxNode, floatingTexts);
        }

        public void HandleDamageResult(DamageResult result)
        {
            if (result == null) return;
            _floatingTexts.Spawn(result.TargetActorId, result.Value, result.Value < 0f);
        }

        public void HandleSnapshot(MobaDamageEventSnapshotEntry[] entries)
        {
            if (entries == null || entries.Length == 0) return;
            if (!_floatingTexts.CanSpawn) return;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                _floatingTexts.Spawn(entry.TargetActorId, entry.Value, entry.Kind == (int)DamageEventKind.Heal);
            }
        }
    }

    internal sealed class BattleDamageViewEventHandlerFactory
    {
        public BattleDamageFloatingTextSpawner CreateFloatingTexts(
            EC.IECWorld world,
            IBattleEntityQuery query,
            in EC.IEntity vfxNode,
            BattleFloatingTextSystem floatingTexts)
        {
            return new BattleDamageFloatingTextSpawner(
                world,
                in vfxNode,
                floatingTexts,
                new BattleDamageFloatingTextPositionResolver(query));
        }
    }
}
