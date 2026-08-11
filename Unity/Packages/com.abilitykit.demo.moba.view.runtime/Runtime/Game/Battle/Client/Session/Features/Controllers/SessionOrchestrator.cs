using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Logging;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Flow
{
    internal sealed class SessionOrchestrator
    {
        private readonly BattleSessionState _state;
        private readonly BattleSessionHandles _handles;
        private readonly ISessionOrchestratorHost _host;
        private CleanupStep _completedCleanupSteps;
        private bool _cleanupRequired;
        private bool _sessionStartingPipelineEntered;

        [Flags]
        private enum CleanupStep
        {
            None = 0,
            RecordWriter = 1 << 0,
            BattleContext = 1 << 1,
            StoppingPipeline = 1 << 2,
            SnapshotRouting = 1 << 3,
            ConfirmedView = 1 << 4,
            BattleWorlds = 1 << 5,
            ConfirmedWorld = 1 << 6,
            RemoteDrivenWorld = 1 << 7,
            RemoteInterpolation = 1 << 8,
            FrameSubscription = 1 << 9,
            LogicSession = 1 << 10,
            SessionHandles = 1 << 11,
        }

        public SessionOrchestrator(BattleSessionState state, BattleSessionHandles handles, ISessionOrchestratorHost host)
        {
            _state = state;
            _handles = handles;
            _host = host;
        }

        public float GetFixedDeltaSeconds()
        {
            var plan = _host.Plan;
            return 1f / ResolveTickRate(plan);
        }

        public void StartSession()
        {
            StopSession();
            _state.BeginStart();
            _cleanupRequired = true;
            _completedCleanupSteps = CleanupStep.None;
            _sessionStartingPipelineEntered = false;

            try
            {
                var plan = _host.Plan;
                StartLogicSession(plan);
                StartAuxiliaryWorlds(plan);
                _sessionStartingPipelineEntered = true;
                _host.InvokeSessionStartingPipeline();
                ResetTickState();
                BindBattleContext();
                _host.InvokeReplaySetupPipeline();
                _state.CompleteStart();
            }
            catch (Exception ex)
            {
                try
                {
                    DisposeSessionResources();
                }
                catch (Exception cleanupEx)
                {
                    var combined = new AggregateException(ex, cleanupEx);
                    var failure = new InvalidOperationException(
                        "Battle session startup failed and cleanup also reported failures.",
                        combined);
                    _state.Fault(failure);
                    throw failure;
                }

                _state.Fault(ex);
                throw;
            }
        }

        public void StopSession()
        {
            if (_state.Lifecycle == BattleSessionLifecycleState.Stopping) return;
            if (!_cleanupRequired &&
                (_state.Lifecycle == BattleSessionLifecycleState.Created ||
                 _state.Lifecycle == BattleSessionLifecycleState.Stopped))
            {
                if (_state.Lifecycle == BattleSessionLifecycleState.Created)
                {
                    _state.BeginStop();
                    _state.CompleteStop();
                }
                return;
            }

            _state.BeginStop();
            try
            {
                DisposeSessionResources();
                _state.CompleteStop();
            }
            catch (Exception ex)
            {
                _state.Fault(ex);
                throw;
            }
        }

        private void StartLogicSession(BattleStartPlan plan)
        {
            _host.StartBattleLogicSession(BuildSessionOptions(plan));
            _host.SubscribeFrameReceived();
        }

        private static BattleLogicSessionOptions BuildSessionOptions(BattleStartPlan plan)
        {
            var world = plan.World;

            return new BattleLogicSessionOptions
            {
                Mode = ResolveLogicMode(plan),
                WorldId = new WorldId(world.WorldId),
                WorldType = world.WorldType,
                ClientId = world.ClientId,
                PlayerId = world.PlayerId,

                ScanAssemblies = new[]
                {
                    typeof(AbilityKit.Ability.World.Services.WorldServiceContainerFactory).Assembly,
                    typeof(BattleLogicSession).Assembly,
                    typeof(AbilityKit.Demo.Moba.Systems.MobaWorldBootstrapModule).Assembly,
                    typeof(BattleSessionFeature).Assembly,
                },
                NamespacePrefixes = new[] { "AbilityKit" },

                AutoConnect = false,
                AutoCreateWorld = false,
                AutoJoin = false,
            };
        }

        private static BattleLogicMode ResolveLogicMode(BattleStartPlan plan)
        {
            return ShouldUseRemoteLogic(plan)
                ? BattleLogicMode.Remote
                : BattleLogicMode.Local;
        }

        private static bool ShouldUseRemoteLogic(BattleStartPlan plan)
        {
            return plan.Sync.SyncMode == BattleSyncMode.SnapshotAuthority || IsGatewayRemoteTransport(plan);
        }

        private void StartAuxiliaryWorlds(BattleStartPlan plan)
        {
            if (IsGatewayRemoteTransport(plan))
            {
                _host.StartRemoteDrivenLocalWorld();
            }

            if (plan.Authority.EnableConfirmedAuthorityWorld)
            {
                _host.StartConfirmedAuthorityWorld();
            }
        }

        private void ResetTickState()
        {
            _state.Tick.Reset();
        }

        private void BindBattleContext()
        {
            SessionContextBinder.BindRuntimeSession(_host.Context, _state, _handles);
        }

        private void DisposeSessionResources()
        {
            if (!_cleanupRequired) return;

            var failures = new List<Exception>();

            void DisposeStep(CleanupStep step, Action action, string name)
            {
                if ((_completedCleanupSteps & step) != 0) return;

                try
                {
                    action?.Invoke();
                    _completedCleanupSteps |= step;
                }
                catch (Exception ex)
                {
                    failures.Add(new InvalidOperationException(
                        $"Battle session cleanup step failed: {name}.",
                        ex));
                    Log.Exception(ex, $"[BattleSessionFeature] Session resource cleanup failed: {name}");
                }
            }

            // Reverse startup order. Each successful step is remembered so a later Stop can
            // resume only the failed work without repeating already-completed teardown.
            DisposeStep(CleanupStep.RecordWriter, DisposeContextRecordWriter, "input record writer");
            DisposeStep(CleanupStep.BattleContext, ClearBattleContext, "battle context");

            if (_sessionStartingPipelineEntered)
            {
                DisposeStep(CleanupStep.StoppingPipeline, _host.InvokeSessionStoppingPipeline, "session stopping pipeline");
            }
            else
            {
                _completedCleanupSteps |= CleanupStep.StoppingPipeline;
            }

            DisposeStep(CleanupStep.SnapshotRouting, _host.DisposeSnapshotRouting, "snapshot routing");
            DisposeStep(CleanupStep.ConfirmedView, _host.DisposeConfirmedView, "confirmed view");
            DisposeStep(CleanupStep.BattleWorlds, _host.TryDestroyBattleWorlds, "battle worlds");
            DisposeStep(CleanupStep.ConfirmedWorld, _host.DisposeConfirmedWorld, "confirmed world");
            DisposeStep(CleanupStep.RemoteDrivenWorld, _host.DisposeRemoteDrivenWorld, "remote-driven world");
            DisposeStep(CleanupStep.RemoteInterpolation, _host.DisposeRemoteInterpolation, "remote interpolation");
            DisposeStep(CleanupStep.FrameSubscription, _host.UnsubscribeFrameReceived, "unsubscribe frame receiver");
            DisposeStep(CleanupStep.LogicSession, _host.StopBattleLogicSession, "battle logic session");

            if (failures.Count == 0)
            {
                DisposeStep(CleanupStep.SessionHandles, _host.ResetSessionHandles, "session handles");
            }

            if (failures.Count > 0)
            {
                throw new AggregateException("Battle session cleanup completed with one or more failures.", failures);
            }

            _cleanupRequired = false;
            _sessionStartingPipelineEntered = false;
        }

        private void DisposeContextRecordWriter()
        {
            var ctx = _host.Context;
            if (ctx == null) return;

            var writer = ctx.InputRecordWriter;
            ctx.InputRecordWriter = null;
            writer?.Dispose();
        }

        private void ClearBattleContext()
        {
            SessionContextBinder.ClearSession(_host.Context);
        }

        private static int ResolveTickRate(BattleStartPlan plan)
        {
            if (IsGatewayRemoteTransport(plan)) return 30;

            var tickRate = plan.World.TickRate;
            return tickRate > 0 ? tickRate : 30;
        }

        private static bool IsGatewayRemoteTransport(BattleStartPlan plan)
        {
            return plan.HostMode == BattleHostMode.GatewayRemote && plan.Gateway.UseGatewayTransport;
        }
    }
}
