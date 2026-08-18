using System;
using UnityEngine.SceneManagement;

namespace AbilityKit.Game.Flow
{
    internal sealed class LobbySceneExitLifecycle
    {
        private readonly Action _destroyGameEntry;
        private readonly Action<string> _loadScene;

        public LobbySceneExitLifecycle()
            : this(DestroyGameEntry, LoadScene)
        {
        }

        internal LobbySceneExitLifecycle(
            Action destroyGameEntry,
            Action<string> loadScene)
        {
            _destroyGameEntry = destroyGameEntry ?? throw new ArgumentNullException(nameof(destroyGameEntry));
            _loadScene = loadScene ?? throw new ArgumentNullException(nameof(loadScene));
        }

        public void Exit(
            Action cancelLifetime,
            Action cancelController,
            Action clearSelection,
            string starterScene)
        {
            if (cancelLifetime == null) throw new ArgumentNullException(nameof(cancelLifetime));

            cancelLifetime();
            cancelController?.Invoke();
            clearSelection?.Invoke();
            _destroyGameEntry();
            _loadScene(string.IsNullOrWhiteSpace(starterScene)
                ? "StarterScene"
                : starterScene.Trim());
        }

        private static void DestroyGameEntry()
        {
            if (GameEntry.IsInitialized)
            {
                UnityEngine.Object.Destroy(GameEntry.Instance.gameObject);
            }
        }

        private static void LoadScene(string sceneName)
        {
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
    }
}
