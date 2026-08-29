using System;
using AbilityKit.Core.Logging;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private void OnStartSessionRequested()
        {
            try
            {
                if (_plan.RunModeOptions.RunMode == BattleRunMode.Replay)
                {
                    StartIsolatedReplaySession();
                    return;
                }

                Log.Info("[BattleSessionFeature] Starting session");
                StartSession();
                _eventsCtrl.NotifySessionStarted(this, _plan);
                ApplyAutoPlanActions();
                SessionContextBinder.BindSession(_ctx, _state, _handles, Hooks, _plan);

                BeginColdStartRecoveryIfReady(requireStartedSession: true);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[BattleSessionFeature] StartSession failed after gateway room preparation");
                _runtime.Replay.Stop();
                StopSession();
                _eventsCtrl.NotifySessionFailed(this, ex);
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

            if (!MobaBattlePauseController.BeginColdStartRecovery(_ctx))
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
            _eventsCtrl.NotifySessionStarted(this, _plan);
        }
    }
}
