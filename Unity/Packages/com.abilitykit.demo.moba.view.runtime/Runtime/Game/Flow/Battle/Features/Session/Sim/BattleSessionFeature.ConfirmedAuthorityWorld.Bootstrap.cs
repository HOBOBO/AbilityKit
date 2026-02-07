using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Impl.Moba.EntitasAdapters;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
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
                .RegisterEntitasWorld(AbilityKit.Ability.Impl.Moba.Worlds.Blueprints.MobaLobbyWorldBlueprint.Type)
                .RegisterEntitasWorld(AbilityKit.Ability.Impl.Moba.Worlds.Blueprints.MobaBattleWorldBlueprint.Type);

            var blueprints = new AbilityKit.Ability.Host.WorldBlueprints.WorldBlueprintRegistry();
            AbilityKit.Ability.Impl.Moba.Worlds.Blueprints.MobaWorldBlueprintsRegistration.RegisterAll(blueprints);

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

            modules.InstallAll(_confirmedRuntime, serverOptions);

            var builder = WorldServiceContainerFactory.CreateWithAttributes(
                AbilityKit.Ability.World.Services.Attributes.WorldServiceProfile.All,
                new[]
                {
                    typeof(WorldServiceContainerFactory).Assembly,
                    typeof(BattleLogicSession).Assembly,
                    typeof(AbilityKit.Ability.Impl.Moba.Systems.MobaWorldBootstrapModule).Assembly,
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

            var buf = new FrameJitterBuffer<PlayerInputCommand[]>(delayFrames: 0, missingMode: MissingFrameMode.FillDefault, missingFrameFactory: Array.Empty<PlayerInputCommand>, initialCapacity: 256);
            _confirmedInputSource = buf;
            _confirmedConsumable = buf;
            _confirmedSink = buf;

            try
            {
                if (_confirmedWorld?.Services == null)
                {
                    Log.Error("[BattleSessionFeature] ConfirmedAuthorityWorld bootstrap failed: world.Services is null");
                }
                else
                {
                    var p = new PlayerId(_plan.PlayerId);

                    if (_confirmedWorld.Services.TryResolve<AbilityKit.Ability.Share.Impl.Moba.Services.MobaLobbyStateService>(out var lobby) && lobby != null)
                    {
                        lobby.OnPlayerJoined(p);
                    }
                    else
                    {
                        Log.Error("[BattleSessionFeature] ConfirmedAuthorityWorld bootstrap failed: MobaLobbyStateService not found");
                    }

                    if (_confirmedWorld.Services.TryResolve<AbilityKit.Ability.Host.IWorldInputSink>(out var sink) && sink != null)
                    {
                        var frame0 = new FrameIndex(0);
                        var ready = new PlayerInputCommand(frame0, p, (int)AbilityKit.Ability.Share.Impl.Moba.Services.MobaOpCode.Ready, Array.Empty<byte>());
                        sink.Submit(frame0, new[] { ready });
                    }
                    else
                    {
                        Log.Error("[BattleSessionFeature] ConfirmedAuthorityWorld bootstrap failed: IWorldInputSink not found");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }
    }
}
