using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Impl.Moba.EntitasAdapters;
using AbilityKit.Ability.Share.Impl.Moba.Rollback;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Management;
using AbilityKit.Ability.World.Services;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private void CreateRemoteDrivenRuntimeAndWorld()
        {
            var typeRegistry = new WorldTypeRegistry()
                .RegisterEntitasWorld(AbilityKit.Ability.Impl.Moba.Worlds.Blueprints.MobaLobbyWorldBlueprint.Type)
                .RegisterEntitasWorld(AbilityKit.Ability.Impl.Moba.Worlds.Blueprints.MobaBattleWorldBlueprint.Type);

            var blueprints = new AbilityKit.Ability.Host.WorldBlueprints.WorldBlueprintRegistry();
            AbilityKit.Ability.Impl.Moba.Worlds.Blueprints.MobaWorldBlueprintsRegistration.RegisterAll(blueprints);

            var baseFactory = new RegistryWorldFactory(typeRegistry);
            var factory = new AbilityKit.Ability.Host.WorldBlueprints.WorldBlueprintWorldFactory(baseFactory, blueprints);
            _remoteDrivenWorlds = new WorldManager(factory);

            var serverOptions = new AbilityKit.Ability.Host.Framework.HostRuntimeOptions();
            _remoteDrivenRuntime = new AbilityKit.Ability.Host.Framework.HostRuntime(_remoteDrivenWorlds, serverOptions);

            var fixedDelta = GetFixedDeltaSeconds();

            var modules = new AbilityKit.Ability.Host.Framework.HostRuntimeModuleHost();
            InstallRemoteDrivenPredictionModules(modules, fixedDelta);
            modules.Add(new AbilityKit.Ability.Host.Extensions.Time.ServerFrameTimeModule(fixedDelta));
            modules.InstallAll(_remoteDrivenRuntime, serverOptions);

            BindRemoteDrivenPredictionFeaturesToBattleContext();

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

            var options = new WorldCreateOptions(new WorldId(_plan.WorldId), _plan.WorldType)
            {
                ServiceBuilder = builder,
            };
            options.SetEntitasContextsFactory(new MobaEntitasContextsFactory());

            _remoteDrivenWorld = _remoteDrivenRuntime.CreateWorld(options);

            try
            {
                if (_remoteDrivenWorld?.Services == null)
                {
                    Log.Error("[BattleSessionFeature] RemoteDrivenLocalWorld bootstrap failed: world.Services is null");
                }
                else
                {
                    var p = new PlayerId(_plan.PlayerId);

                    if (_remoteDrivenWorld.Services.TryResolve<MobaLobbyStateService>(out var lobby) && lobby != null)
                    {
                        lobby.OnPlayerJoined(p);
                    }
                    else
                    {
                        Log.Error("[BattleSessionFeature] RemoteDrivenLocalWorld bootstrap failed: MobaLobbyStateService not found");
                    }

                    if (_remoteDrivenWorld.Services.TryResolve<AbilityKit.Ability.Host.IWorldInputSink>(out var sink) && sink != null)
                    {
                        var frame0 = new FrameIndex(0);
                        var ready = new PlayerInputCommand(frame0, p, (int)MobaOpCode.Ready, Array.Empty<byte>());
                        sink.Submit(frame0, new[] { ready });
                    }
                    else
                    {
                        Log.Error("[BattleSessionFeature] RemoteDrivenLocalWorld bootstrap failed: IWorldInputSink not found");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[BattleSessionFeature] RemoteDrivenLocalWorld bootstrap threw");
            }
        }

        private void InstallRemoteDrivenPredictionModules(AbilityKit.Ability.Host.Framework.HostRuntimeModuleHost modules, float fixedDelta)
        {
            if (_plan.EnableClientPrediction)
            {
                modules.Add(new AbilityKit.Ability.Host.Extensions.FrameSync.ClientPredictionDriverModule(
                    resolveRemoteInputs: _ => _remoteDrivenConsumable,
                    resolveLocalInputs: _ => _ctx != null ? _ctx.LocalInputQueue : null,
                    resolveIdealFrameLimit: _ => ResolveIdealFrameLimit(_),
                    inputDelayFrames: _plan.InputDelayFrames < 0 ? 0 : _plan.InputDelayFrames,
                    maxPredictionAheadFrames: 30,
                    minPredictionWindow: 1,
                    backlogEwmaAlpha: 0.20f,
                    enableRollback: true,
                    rollbackHistoryFrames: 240,
                    rollbackCaptureEveryNFrames: 1,
                    buildRollbackRegistry: world =>
                    {
                        var reg = new AbilityKit.Ability.FrameSync.Rollback.RollbackRegistry();
                        if (world?.Services == null) return reg;

                        if (world.Services.TryResolve<MobaActorRegistry>(out var actorReg) && actorReg != null)
                        {
                            reg.Register(new AbilityKit.Ability.Share.Impl.Moba.Rollback.MobaActorTransformRollbackProvider(actorReg));
                        }

                        if (world.Services.TryResolve<AbilityKit.Ability.Share.Impl.Moba.Move.MobaMoveService>(out var move) && move != null)
                        {
                            reg.Register(new MobaMoveRollbackProvider(move));
                        }

                        return reg;
                    },
                    buildComputeHash: world =>
                    {
                        if (world?.Services == null) return null;

                        if (!world.Services.TryResolve<MobaLobbyStateService>(out var lobby) || lobby == null)
                        {
                            return null;
                        }

                        if (!world.Services.TryResolve<MobaActorRegistry>(out var registry) || registry == null)
                        {
                            return null;
                        }

                        return _ => new AbilityKit.Ability.FrameSync.Rollback.WorldStateHash(ComputeStateHash(lobby, registry));
                    }));
            }
            else
            {
                // Prediction disabled: still install the driver so remote inputs are consumed and the world advances.
                // Configure it to be authoritative-only (no prediction window, no rollback/reconcile, no local inputs).
                modules.Add(new AbilityKit.Ability.Host.Extensions.FrameSync.ClientPredictionDriverModule(
                    resolveRemoteInputs: _ => _remoteDrivenConsumable,
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
                    buildComputeHash: _ => null));
            }
        }

        private void BindRemoteDrivenPredictionFeaturesToBattleContext()
        {
            if (_ctx == null) return;

            // Only bind prediction stats/control from this runtime in GatewayRemote mode.
            // The confirmed-authority compare world should not override the primary session's prediction modules.
            if (!(_plan.HostMode == BattleStartConfig.BattleHostMode.GatewayRemote && _plan.UseGatewayTransport)) return;

            if (!_plan.EnableClientPrediction)
            {
                if (_remoteDrivenRuntime.Features.TryGetFeature<AbilityKit.Ability.Host.Extensions.FrameSync.IClientPredictionDriverStats>(out var stats) && stats != null)
                {
                    // Still expose stats so you can compare confirmed/predicted (predicted==confirmed in this mode).
                    _ctx.PredictionStats = stats;
                }
                else
                {
                    _ctx.PredictionStats = null;
                }
                _ctx.PredictionReconcileTarget = null;
                _ctx.PredictionReconcileControl = null;
                _ctx.PredictionTuningControl = null;
                // Prediction disabled: still create/drive remote world, but do not expose prediction interfaces.
                return;
            }

            if (_remoteDrivenRuntime.Features.TryGetFeature<AbilityKit.Ability.Host.Extensions.FrameSync.IClientPredictionDriverStats>(out var st) && st != null)
            {
                _ctx.PredictionStats = st;
            }
            else
            {
                _ctx.PredictionStats = null;
            }

            if (_remoteDrivenRuntime.Features.TryGetFeature<AbilityKit.Ability.Host.Extensions.FrameSync.IClientPredictionReconcileTarget>(out var target) && target != null)
            {
                _ctx.PredictionReconcileTarget = target;
            }
            else
            {
                _ctx.PredictionReconcileTarget = null;
            }

            if (_remoteDrivenRuntime.Features.TryGetFeature<AbilityKit.Ability.Host.Extensions.FrameSync.IClientPredictionReconcileControl>(out var control) && control != null)
            {
                _ctx.PredictionReconcileControl = control;
            }
            else
            {
                _ctx.PredictionReconcileControl = null;
            }

            if (_remoteDrivenRuntime.Features.TryGetFeature<AbilityKit.Ability.Host.Extensions.FrameSync.IClientPredictionTuningControl>(out var tuning) && tuning != null)
            {
                _ctx.PredictionTuningControl = tuning;
            }
            else
            {
                _ctx.PredictionTuningControl = null;
            }
        }

        private void SetupRemoteDrivenInputAndDebugStats()
        {
            _remoteDrivenLastTickedFrame = 0;
            _remoteDrivenLastLoggedFrame = -1;
            _remoteDrivenFirstSnapshotLogged = false;
            _remoteDrivenFirstSpawnLogged = false;

            var delay = _plan.InputDelayFrames;
            if (delay < 0) delay = 0;
            var buf = new FrameJitterBuffer<PlayerInputCommand[]>(delayFrames: delay, missingMode: MissingFrameMode.FillDefault, missingFrameFactory: Array.Empty<PlayerInputCommand>, initialCapacity: 256);
            _remoteDrivenInputSource = buf;
            _remoteDrivenConsumable = buf;
            _remoteDrivenSink = buf;

            AbilityKit.Game.Flow.BattleFlowDebugProvider.JitterBufferStats = new AbilityKit.Game.Flow.JitterBufferStatsSnapshot
            {
                DelayFrames = buf.DelayFrames,
                MissingMode = buf.MissingMode.ToString(),
                TargetFrame = buf.TargetFrame,
                MaxReceivedFrame = buf.MaxReceivedFrame,
                LastConsumedFrame = buf.LastConsumedFrame,
                BufferedCount = buf.Count,
                MinBufferedFrame = buf.MinBufferedFrame,

                AddedCount = buf.AddedCount,
                DuplicateCount = buf.DuplicateCount,
                LateCount = buf.LateCount,
                ConsumedCount = buf.ConsumedCount,
                FilledDefaultCount = buf.FilledDefaultCount,
            };
        }
    }
}
