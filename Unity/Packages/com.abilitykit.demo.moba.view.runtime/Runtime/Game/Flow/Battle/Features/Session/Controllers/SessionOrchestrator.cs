using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        internal sealed class SessionOrchestrator
        {
            private readonly BattleSessionState _state;
            private readonly BattleSessionHandles _handles;
            private readonly ISessionOrchestratorHost _host;

            public SessionOrchestrator(BattleSessionState state, BattleSessionHandles handles, ISessionOrchestratorHost host)
            {
                _state = state;
                _handles = handles;
                _host = host;
            }

            public float GetFixedDeltaSeconds()
            {
                var plan = _host.Plan;
                var tickRate = plan.TickRate;
                if (plan.HostMode == BattleStartConfig.BattleHostMode.GatewayRemote && plan.UseGatewayTransport)
                {
                    tickRate = 30;
                }
                if (tickRate <= 0) tickRate = 30;
                return 1f / tickRate;
            }

            public void StartSession()
            {
                StopSession();

                var plan = _host.Plan;

                var useRemote = plan.SyncMode == BattleSyncMode.SnapshotAuthority || (plan.HostMode == BattleStartConfig.BattleHostMode.GatewayRemote && plan.UseGatewayTransport);

                var opts = new BattleLogicSessionOptions
                {
                    Mode = useRemote ? BattleLogicMode.Remote : BattleLogicMode.Local,
                    WorldId = new WorldId(plan.WorldId),
                    WorldType = plan.WorldType,
                    ClientId = plan.ClientId,
                    PlayerId = plan.PlayerId,

                    ScanAssemblies = new[]
                    {
                        typeof(AbilityKit.Ability.World.Services.WorldServiceContainerFactory).Assembly,
                        typeof(BattleLogicSession).Assembly,
                        typeof(AbilityKit.Ability.Impl.Moba.Systems.MobaWorldBootstrapModule).Assembly,
                        typeof(BattleSessionFeature).Assembly,
                    },
                    NamespacePrefixes = new[] { "AbilityKit" },

                    AutoConnect = false,
                    AutoCreateWorld = false,
                    AutoJoin = false,
                };

                _handles.Session = _host.StartBattleLogicSession(opts);
                _handles.Session.FrameReceived += _host.FrameReceivedHandler;

                if (plan.HostMode == BattleStartConfig.BattleHostMode.GatewayRemote && plan.UseGatewayTransport)
                {
                    _host.StartRemoteDrivenLocalWorld();
                }

                if (plan.EnableConfirmedAuthorityWorld)
                {
                    _host.StartConfirmedAuthorityWorld();
                }

                _host.InvokeSessionStartingPipeline();

                _state.Tick.LastFrame = 0;
                _state.Tick.TickAcc = 0f;
                _state.Tick.FirstFrameReceived = false;

                var ctx = _host.Context;
                if (ctx != null)
                {
                    ctx.Session = _handles.Session;
                    ctx.LastFrame = _state.Tick.LastFrame;
                }

                _host.InvokeReplaySetupPipeline();
            }

            public void StopSession()
            {
                if (_handles.Session == null) return;

                try
                {
                    _handles.Session.FrameReceived -= _host.FrameReceivedHandler;

                    _host.InvokeSessionStoppingPipeline();
                    BattleLogicSessionHost.Stop();
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, "[BattleSessionFeature] StopSession failed");
                }
                finally
                {
                    try
                    {
                        var ctx = _host.Context;
                        if (ctx != null)
                        {
                            ctx.InputRecordWriter?.Dispose();
                            ctx.InputRecordWriter = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex, "[BattleSessionFeature] Dispose InputRecordWriter on StopSession failed");
                    }

                    _host.TryDestroyBattleWorlds();
                    _host.DisposeSnapshotRouting();
                    _host.DisposeConfirmedView();
                    _host.DisposeRemoteDrivenWorld();
                    _host.DisposeConfirmedWorld();
                    _host.DisposeNetworkIoDispatcher();

                    _host.ResetHandles();
                }
            }
        }
    }
}
