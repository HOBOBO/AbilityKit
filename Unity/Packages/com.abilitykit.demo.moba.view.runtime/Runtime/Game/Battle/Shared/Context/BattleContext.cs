using AbilityKit.Ability.Host;
using AbilityKit.Core.Pooling;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleContext : IPoolable, IBattleHudInputSink, IBattleRuntimeContext, IBattleEntityContext, IBattleInputContext, IBattleSnapshotRoutingContext
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
            Reset(disposeOwnedResources: true, destroyCollections: false);
        }

        void IPoolable.OnPoolDestroy()
        {
            Reset(disposeOwnedResources: true, destroyCollections: true);
        }

        private void Reset(bool disposeOwnedResources, bool destroyCollections)
        {
            Session = null;
            RuntimeWorld = null;
            Plan = default;
            LastFrame = 0;
            LogicTimeSeconds = 0d;

            LocalActorId = 0;
            LocalControlPlayerId = null;
            CanSubmitGameplayInput = true;
            ClearRuntimePlayerLoadouts();

            Hooks = null;

            ClearSnapshotRouting();

            if (disposeOwnedResources)
            {
                InputRecordWriter?.Dispose();
                LocalInputQueue?.Dispose();
            }

            InputRecordWriter = null;
            LocalInputQueue = null;

            PredictionStats = null;
            PredictionReconcileTarget = null;
            PredictionReconcileControl = null;
            PredictionTuningControl = null;

            RuntimeWorldId = default;
            HasRuntimeWorldId = false;
            EnableRemoteInterpolation = false;

            EntityNode = default;
            EntityWorld = null;
            EntityLookup = null;
            EntityFactory = null;
            EntityQuery = null;
            ViewVfxManager = null;
            ViewVfxNode = default;

            if (destroyCollections)
            {
                DirtyEntities = null;
            }
            else
            {
                DirtyEntities?.Clear();
            }

            ResetHudInput();
        }
    }
}
