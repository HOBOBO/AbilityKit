using AbilityKit.Demo.Shooter.View.PlayMode;
using AbilityKit.Game.Flow;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbilityKit.Starter
{
    [DisallowMultipleComponent]
    public sealed class MultiplayerStarterController : MonoBehaviour
    {
        private const string MobaSceneName = "MobaMultiplayerScene";
        private const string ShooterSceneName = "ShooterMultiplayerScene";
        private const float WindowWidth = 420f;
        private const float WindowHeight = 250f;

        private bool _loading;
        private string _status = "Select a multiplayer game.";

        private void OnGUI()
        {
            var rect = new Rect(
                Mathf.Max(16f, (Screen.width - WindowWidth) * 0.5f),
                Mathf.Max(16f, (Screen.height - WindowHeight) * 0.5f),
                WindowWidth,
                WindowHeight);
            GUILayout.BeginArea(rect, "AbilityKit Multiplayer", GUI.skin.window);
            GUILayout.Space(8f);
            GUILayout.Label("Game");

            var previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !_loading;
            if (GUILayout.Button("MOBA", GUILayout.Height(52f)))
            {
                LaunchMoba();
            }

            if (GUILayout.Button("Shooter", GUILayout.Height(52f)))
            {
                LaunchShooter();
            }

            GUI.enabled = previousEnabled;
            GUILayout.Space(10f);
            GUILayout.Label(_status);
            GUILayout.EndArea();
        }

        private void LaunchMoba()
        {
            MobaMultiplayerLaunchContext.Request();
            LoadGame(MobaSceneName, "Opening MOBA login and room flow...");
        }

        private void LaunchShooter()
        {
            ShooterMultiplayerLaunchContext.Request();
            LoadGame(ShooterSceneName, "Opening Shooter login and room flow...");
        }

        private void LoadGame(string sceneName, string status)
        {
            _loading = true;
            _status = status;
            SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }
    }
}
