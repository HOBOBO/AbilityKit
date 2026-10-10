using System;
using System.Threading.Tasks;
using AbilityKit.Core.Logging;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private void OnStartSessionRequested()
        {
            _ = _startupTasks.RequestStart(StartSessionAfterGatewayPreparationAsync);
        }

        private Task StartPlannedSessionAsync()
        {
            if (_plan.RunModeOptions.RunMode != BattleRunMode.Replay)
                return StartSessionAsync();
            StartIsolatedReplaySession();
            return Task.CompletedTask;
        }

        private async Task StartSessionAfterGatewayPreparationAsync(int generation)
        {
            try
            {
                if (!_startupTasks.IsCurrent(generation)) return;
                if (_plan.RunModeOptions.RunMode == BattleRunMode.Replay)
                {
                    StartIsolatedReplaySession();
                    _eventsCtrl.NotifySessionStarted(this, _plan);
                    return;
                }

                Log.Info("[BattleSessionFeature] Starting session");
                await StartSessionAsync();
                if (!_startupTasks.IsCurrent(generation)) return;
                _eventsCtrl.NotifySessionStarted(this, _plan);
                ApplyAutoPlanActions();
                SessionContextBinder.BindSession(_ctx, _state, _handles, Hooks, _plan);

                BeginColdStartRecoveryIfReady(requireStartedSession: true);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[BattleSessionFeature] StartSession failed after gateway room preparation");
                _runtime.Replay.Stop();
                var failure = ex;
                try
                {
                    await StopSessionAsync();
                }
                catch (Exception cleanupFailure)
                {
                    failure = new AggregateException(
                        "Gateway session startup failed and cleanup also reported failures.",
                        ex,
                        cleanupFailure);
                }

                if (_startupTasks.IsCurrent(generation))
                    _eventsCtrl.NotifySessionFailed(this, failure);
            }
        }

        internal void BeginColdStartRecoveryAfterImmediateSessionStart()
        {
            BeginColdStartRecoveryIfReady(requireStartedSession: true);
        }

        private void BeginColdStartRecoveryIfReady(bool requireStartedSession)
        {
            if (!(_bootstrapper is ExistingGatewayRoomBattleBootstrapper existing) ||
                !existing.ColdStartReconnect)
            {
                return;
            }

            if (_session == null)
            {
                if (requireStartedSession)
                {
                    throw new InvalidOperationException(
                        "Unable to start lockstep cold-start recovery before the battle session exists.");
                }

                return;
            }

            if (!_runtime.LockstepRecovery.BeginColdStartRecovery(_ctx))
            {
                throw new InvalidOperationException(
                    "Unable to start lockstep cold-start recovery for the restored battle.");
            }

            Log.Info("[BattleSessionFeature] Started frame-0 lockstep cold-start recovery");
        }

        private void StartIsolatedReplaySession()
        {
            var path = _plan.RunModeOptions.InputReplayPath;
            if (!_runtime.Replay.TryStart(_plan, path, out var error))
            {
                throw new InvalidOperationException(error ?? "无法启动独立 Replay Session。");
            }

            Log.Info($"[BattleSessionFeature] Started isolated replay session: {path}");
        }
    }
}
