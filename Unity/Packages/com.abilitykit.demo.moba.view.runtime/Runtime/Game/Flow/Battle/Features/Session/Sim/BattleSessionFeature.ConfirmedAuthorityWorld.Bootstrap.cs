using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Extensions.WorldStart;
using AbilityKit.Core.Common.Log;
using AbilityKit.Ability.Share.Impl.Moba.EntitasAdapters;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.World.Management;
using AbilityKit.Ability.World.Services;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private WorldId CreateConfirmedAuthorityWorldId()
        {
            return new WorldId((_plan.WorldId ?? "room_1") + "__confirmed");
        }

        private void CreateConfirmedAuthorityRuntimeAndWorld(out WorldId authWorldId)
        {
            var typeRegistry = new WorldTypeRegistry()
                .RegisterEntitasWorld(AbilityKit.Ability.Share.Impl.Moba.Worlds.Blueprints.MobaLobbyWorldBlueprint.Type)
                .RegisterEntitasWorld(AbilityKit.Ability.Share.Impl.Moba.Worlds.Blueprints.MobaBattleWorldBlueprint.Type);

            var blueprints = new AbilityKit.Ability.Host.WorldBlueprints.WorldBlueprintRegistry();
            AbilityKit.Ability.Share.Impl.Moba.Worlds.Blueprints.MobaWorldBlueprintsRegistration.RegisterAll(blueprints);

            var baseFactory = new RegistryWorldFactory(typeRegistry);
            var factory = new AbilityKit.Ability.Host.WorldBlueprints.WorldBlueprintWorldFactory(baseFactory, blueprints);
            _confirmedWorlds = new WorldManager(factory);

            var serverOptions = new AbilityKit.Ability.Host.Framework.HostRuntimeOptions();
            _confirmedRuntime = new AbilityKit.Ability.Host.Framework.HostRuntime(_confirmedWorlds, serverOptions);

            var fixedDelta = GetFixedDeltaSeconds();

            // Confirmed-authority world: only driven by remote inputs, up to ConfirmedFrame.
            var modules = new AbilityKit.Ability.Host.Framework.HostRuntimeModuleHost()
                .Add(new AbilityKit.Ability.Host.Extensions.FrameSync.ClientPredictionDriverModule(
                    resolveRemoteInputs: _ => _confirmedConsumable,
                    resolveLocalInputs: _ => null,
                    resolveIdealFrameLimit: _ => ResolveIdealFrameLimit(_),
                    inputDelayFrames: 0,
                    maxPredictionAheadFrames: 0,
                    minPredictionWindow: 0,
                    backlogEwmaAlpha: 0.20f,
                    enableRollback: false,
                    rollbackHistoryFrames: 0,
                    rollbackCaptureEveryNFrames: 0,
                    buildRollbackRegistry: _ => new AbilityKit.Ability.FrameSync.Rollback.RollbackRegistry(),
                    buildComputeHash: _ => null))
                .Add(new AbilityKit.Ability.Host.Extensions.Time.ServerFrameTimeModule(fixedDelta));

            modules.Add(new WorldAutoStartModule());

            modules.InstallAll(_confirmedRuntime, serverOptions);

            var builder = WorldServiceContainerFactory.CreateWithAttributes(
                AbilityKit.Ability.World.Services.Attributes.WorldServiceProfile.All,
                new[]
                {
                    typeof(WorldServiceContainerFactory).Assembly,
                    typeof(BattleLogicSession).Assembly,
                    typeof(AbilityKit.Ability.Share.Impl.Moba.Systems.MobaWorldBootstrapModule).Assembly,
                    typeof(BattleSessionFeature).Assembly
                },
                new[] { "AbilityKit" }
            );
            builder.AddModule(new MobaConfigWorldModule());
            builder.RegisterInstance(new WorldInitData(_plan.CreateWorldOpCode, _plan.CreateWorldPayload));
            builder.TryRegister<IFrameTime>(WorldLifetime.Singleton, _ => new AbilityKit.Ability.FrameSync.FrameTime());

            authWorldId = CreateConfirmedAuthorityWorldId();
            var options = new WorldCreateOptions(authWorldId, _plan.WorldType)
            {
                ServiceBuilder = builder,
            };
            options.SetEntitasContextsFactory(new MobaEntitasContextsFactory());

            _confirmedWorld = _confirmedRuntime.CreateWorld(options);
        }

        private void SetupConfirmedAuthorityInputAndBootstrap(WorldId authWorldId)
        {
            _confirmedLastTickedFrame = 0;

            var hub = FrameSyncInputHubFactory.CreateJitterBufferHub<PlayerInputCommand[]>(
                delayFrames: 0,
                missingMode: MissingFrameMode.FillDefault,
                missingFrameFactory: Array.Empty<PlayerInputCommand>,
                initialCapacity: 256);
            _confirmedInputSource = hub;
            _confirmedConsumable = hub;
            _confirmedSink = hub;

            try
            {
                if (_confirmedWorld?.Services == null)
                {
                    Log.Error("[BattleSessionFeature] ConfirmedAuthorityWorld bootstrap failed: world.Services is null");
                }
                else
                {
                    var p = new PlayerId(_plan.PlayerId);
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }
    }
}
