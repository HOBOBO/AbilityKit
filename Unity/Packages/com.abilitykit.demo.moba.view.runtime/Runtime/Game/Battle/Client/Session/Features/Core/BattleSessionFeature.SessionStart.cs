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
                if (_plan.RunModeOptions.RunMode == BattleStartConfig.BattleRunMode.Replay)
                {
                    StartIsolatedReplaySession();
                    return;
                }

                Log.Info("[BattleSessionFeature] Starting session");
                StartSession();
                _eventsCtrl.NotifySessionStarted(this, _plan);
                ApplyAutoPlanActions();
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[BattleSessionFeature] StartSession failed after gateway room preparation");
                _replayOwner.Stop();
                StopSession();
                _eventsCtrl.NotifySessionFailed(this, ex);
                return;
            }

            SessionContextBinder.BindSession(_ctx, _state, _handles, Hooks, _plan);
        }

        private void StartIsolatedReplaySession()
        {
            var path = _plan.RunModeOptions.InputReplayPath;
            if (!_replayOwner.TryStart(_plan, path, out var error))
            {
                throw new InvalidOperationException(error ?? "无法启动独立 Replay Session。");
            }

            Log.Info($"[BattleSessionFeature] Started isolated replay session: {path}");
            _eventsCtrl.NotifySessionStarted(this, _plan);
        }
    }
}
