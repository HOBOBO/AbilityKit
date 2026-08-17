using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleViewEntitySyncController
    {
        private readonly BattleViewEntitySyncInputFactory _inputs;
        private readonly BattleViewModelSyncController _models;
        private readonly BattleViewAttachedVfxController _attachedVfx;

        public BattleViewEntitySyncController(
            BattleViewHandleStore handles,
            BattleViewShellController shells,
            BattleViewAttachedVfxController attachedVfx,
            BattleViewTransformController transforms,
            BattleViewResourceProvider resources = null,
            BattleViewEntitySyncInputFactory inputs = null,
            BattleViewEntitySyncControllerFactory controllers = null)
        {
            controllers ??= new BattleViewEntitySyncControllerFactory();

            _inputs = inputs ?? new BattleViewEntitySyncInputFactory();
            _models = controllers.CreateModels(handles, shells, transforms, resources);
            _attachedVfx = attachedVfx;
        }

        public void Sync(EC.IEntity entity)
        {
            Sync(entity, runtimeContext: null);
        }

        public void Sync(EC.IEntity entity, IBattleRuntimeContext runtimeContext)
        {
            if (!_inputs.TryCreate(entity, out var input)) return;
            if (!_models.Sync(in input, runtimeContext, out var handle)) return;

            _attachedVfx.SyncProjectileVfx(entity, handle, input.Meta);
        }
    }

    internal sealed class BattleViewEntitySyncControllerFactory
    {
        public BattleViewModelSyncController CreateModels(
            BattleViewHandleStore handles,
            BattleViewShellController shells,
            BattleViewTransformController transforms,
            BattleViewResourceProvider resources)
        {
            return new BattleViewModelSyncController(handles, shells, transforms, resources);
        }
    }
}
