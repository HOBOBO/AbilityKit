using System;
using System.Threading.Tasks;
using AbilityKit.Core.Logging;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
#if UNITY_EDITOR
        private void TryInstallEditorPlayModeStopHook()
        {
            if (_editorPlayModeHookActive) return;

            if (!_editorPlayModeHookInstalled)
            {
                UnityEditor.EditorApplication.playModeStateChanged += OnEditorPlayModeStateChanged;
                _editorPlayModeHookInstalled = true;
            }

            _editorPlayModeHookActive = true;
        }

        private void TryUninstallEditorPlayModeStopHook()
        {
            _editorPlayModeHookActive = false;
        }

        private void OnEditorPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (!_editorPlayModeHookActive) return;

            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                try
                {
                    _ = ObserveEditorStopAsync(Task.WhenAll(
                        StopGatewayRoomPreparationAsync(),
                        StopSessionAsync()));
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, "[BattleSessionFeature] Stop on play mode exit failed");
                }
            }
        }

        private static async Task ObserveEditorStopAsync(Task stopTask)
        {
            try
            {
                await (stopTask ?? Task.CompletedTask);
            }
            catch (Exception exception)
            {
                Log.Exception(exception, "[BattleSessionFeature] Stop on play mode exit failed");
            }
        }
#endif
    }
}
