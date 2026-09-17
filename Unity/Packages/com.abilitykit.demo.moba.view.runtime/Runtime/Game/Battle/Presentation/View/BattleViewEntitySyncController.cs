using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using UnityEngine;
using AbilityKit.HFSM.Definition;
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

            SyncCharacterHfsm(in input, handle, runtimeContext);

            _attachedVfx.SyncProjectileVfx(entity, handle, input.Meta);
        }

        private static void SyncCharacterHfsm(in BattleViewEntitySyncInput input,
            BattleViewHandle handle, IBattleRuntimeContext context)
        {
            if (handle.GameObject == null || input.Meta == null ||
                input.Meta.Kind != BattleEntityKind.Character || context?.Session == null ||
                !context.Session.TryGetWorld(out var world) || world?.Services == null ||
                !world.Services.TryResolve<MobaActorRegistry>(out var registry) ||
                registry == null || !registry.TryGet(input.ActorId, out var actor) ||
                !actor.hasCharacterHfsm) return;

            if (!input.Entity.TryGetRef(out BattleCharacterHfsmComponent state) || state == null ||
                state.EntityCode != input.Meta.EntityCode)
            {
                world.Services.TryResolve<StateMachineDefinition>(out var definition);
                world.Services.TryResolve<CharacterPresentationActionCatalog>(out var catalog);
                state = new BattleCharacterHfsmComponent(definition, catalog, input.Meta.EntityCode);
                input.Entity.WithRef(state);
            }
            state.ApplySnapshot(actor.characterHfsm.Runtime.CaptureSnapshot());

            var mono = handle.GameObject.GetComponent<MonoCharacterHfsmView>();
            if (mono == null) mono = handle.GameObject.AddComponent<MonoCharacterHfsmView>();
            mono.Bind(state, context.Plan.World.TickRate, registry, input.ActorId);
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
