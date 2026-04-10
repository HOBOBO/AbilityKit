using System;
using AbilityKit.Core.Common.Log;

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
                    StopGatewayRoomPreparation();
                    StopSession();
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, "[BattleSessionFeature] Stop on play mode exit failed");
                }
            }
        }
#endif
    }
}
