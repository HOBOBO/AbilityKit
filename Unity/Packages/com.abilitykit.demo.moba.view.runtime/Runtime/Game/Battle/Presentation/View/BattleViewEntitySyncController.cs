using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Demo.Moba.View.Config;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.HFSM.Definition;
using UnityEngine;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleViewEntitySyncController
    {
        private readonly BattleViewEntitySyncInputFactory _inputs;
        private readonly BattleViewModelSyncController _models;
        private readonly BattleViewAttachedVfxController _attachedVfx;
        private MobaCharacterHfsmProfileCatalog _profiles;
        private CharacterPresentationActionCatalog _catalog;

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

        private void SyncCharacterHfsm(in BattleViewEntitySyncInput input,
            BattleViewHandle handle, IBattleRuntimeContext context)
        {
            if (handle.GameObject == null || input.Meta == null ||
                !SupportsActionPresentation(input.Meta.Kind) ||
                !(context is IBattleCharacterActionReadPort actions) ||
                !actions.TryGetCharacterAction(input.ActorId, out var action)) return;

            if (!input.Entity.TryGetRef(out BattleCharacterHfsmComponent state) || state == null ||
                state.EntityCode != input.Meta.EntityCode)
            {
                if (_profiles == null)
                {
                    var loader = new ResourcesTextAssetLoader();
                    _profiles = new MobaCharacterHfsmProfileCatalog(loader);
                    _catalog = CharacterPresentationActionCatalog.Load(loader);
                }
                // Summons and other non-hero actors use the shared summon HFSM
                // definition so their generic attack slots are available to the
                // presentation bridge. Hero profile lookup remains data-driven.
                var definition = input.Meta.Kind == BattleEntityKind.Summon ||
                                 input.Meta.Kind == BattleEntityKind.Clone ||
                                 input.Meta.Kind == BattleEntityKind.Minion ||
                                 input.Meta.Kind == BattleEntityKind.Monster
                    ? MobaCharacterHfsmProfile.CreateSummonDefinition()
                    : _profiles.Resolve(input.Meta.EntityCode);
                var presentationCatalog = _catalog
                    .CreateForEntityCode(input.Meta.EntityCode)
                    .CreateForDefinition(definition);
                state = new BattleCharacterHfsmComponent(
                    definition, presentationCatalog, input.Meta.EntityCode);
                input.Entity.WithRef(state);
            }
            state.ApplyPresentation(in action);

            var mono = handle.GameObject.GetComponent<MonoCharacterHfsmView>();
            if (mono == null) mono = handle.GameObject.AddComponent<MonoCharacterHfsmView>();
            mono.Bind(state, context.Plan.World.TickRate);
        }

        private static bool SupportsActionPresentation(BattleEntityKind kind)
        {
            // CharacterAction snapshots are emitted for every actor with an
            // HFSM runtime, including summons and minions. Keep the view-side
            // binding generic so a summon can reuse the same action catalog and
            // animator bridge without introducing summon-specific branches.
            return kind == BattleEntityKind.Character ||
                   kind == BattleEntityKind.Summon ||
                   kind == BattleEntityKind.Clone ||
                   kind == BattleEntityKind.Minion ||
                   kind == BattleEntityKind.Monster;
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
