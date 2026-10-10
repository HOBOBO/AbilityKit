using AbilityKit.Ability.Host;
using AbilityKit.Core.Pooling;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleContext : IPoolable, IBattleHudInputSink, IBattleRuntimeContext, IBattleEntityContext, IBattleInputContext, IBattleSnapshotRoutingContext, IBattleInputSessionIdentityPort, IBattleCharacterActionReadPort, IBattleGameplayPresentationSource, IBattleHudPresentationPort
    {
        private static readonly ObjectPool<BattleContext> Pool = Pools.GetPool(
            key: "BattleContext",
            createFunc: () => new BattleContext(),
            defaultCapacity: 1,
            maxSize: 8);

        public static BattleContext Rent()
        {
            return Pool.Get();
        }

        public static void Return(BattleContext ctx)
        {
            if (ctx == null) return;
            Pool.Release(ctx);
        }

        void IPoolable.OnPoolGet()
        {
        }

        void IPoolable.OnPoolRelease()
        {
            Reset(destroyCollections: false);
        }

        void IPoolable.OnPoolDestroy()
        {
            Reset(destroyCollections: true);
        }

        private void Reset(bool destroyCollections)
        {
            ClearRuntimeSession();
            Plan = default;
            LastFrame = 0;
            LogicTimeSeconds = 0d;

            LocalActorId = 0;
            _snapshotLocalActorId = 0;
            LocalControlPlayerId = null;
            _inputBlockReasons = BattleGameplayInputBlockReason.None;
            ClearRuntimePlayerLoadouts();
            _characterActions.Clear();

            ClearSnapshotRouting();
            ResetInputRuntime();
            ResetPredictionRuntime();
            StateHashSnapshotReceived = null;

            _entities.Reset(destroyCollections);
            _presentation.Reset();
        }
    }
}
