#nullable enable

using System;
using AbilityKit.Demo.Common.Gameplay;
using AbilityKit.Demo.Common.Rooms;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbilityKit.Demo.Common.Composition
{
    [DisallowMultipleComponent]
    public sealed class DemoGameplayBootstrap : MonoBehaviour
    {
        [SerializeField] private DemoGameplayCatalogSO? catalog;
        [SerializeField] private string starterSceneName = DemoSceneRoutes.Starter;

        private GameObject? _activeRoot;
        private DemoGameplayProfileSO? _activeProfile;
        private string _lastError = string.Empty;

        public GameObject? ActiveRoot => _activeRoot;
        public DemoGameplayProfileSO? ActiveProfile => _activeProfile;
        public string LastError => _lastError;

        private void Start()
        {
            TryLaunch(out _);
        }

        public bool TryLaunch(out string error)
        {
            if (_activeRoot != null)
            {
                error = "A gameplay root is already active.";
                return false;
            }

            if (catalog == null)
            {
                return FailLaunch("Demo gameplay catalog is not assigned.", out error);
            }

            if (!DemoLaunchIntent.TryConsume(out var request))
            {
                return FailLaunch("No demo launch request is pending.", out error);
            }

            if (!catalog.TryFind(in request, out var profile, out error) || profile == null)
            {
                return FailLaunch(error, out error);
            }

            if (!ValidateMultiplayerIntent(in request, out error))
            {
                return FailLaunch(error, out error);
            }

            var rootPrefab = profile.RootPrefab;
            if (rootPrefab == null)
            {
                return FailLaunch($"Gameplay profile '{profile.ProfileId}' has no root prefab.", out error);
            }

            GameObject? instance = null;
            try
            {
                instance = Instantiate(rootPrefab);
                SceneManager.MoveGameObjectToScene(instance, gameObject.scene);
                _activeRoot = instance;
                _activeProfile = profile;
                _lastError = string.Empty;
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                if (instance != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(instance);
                    }
                    else
                    {
                        DestroyImmediate(instance);
                    }
                }

                return FailLaunch(
                    $"Failed to instantiate gameplay profile '{profile.ProfileId}': {exception.Message}",
                    out error);
            }
        }

        public void Shutdown()
        {
            if (_activeRoot != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_activeRoot);
                }
                else
                {
                    DestroyImmediate(_activeRoot);
                }
            }

            _activeRoot = null;
            _activeProfile = null;
        }

        public void ReturnToStarter()
        {
            Shutdown();
            var sceneName = string.IsNullOrWhiteSpace(starterSceneName)
                ? DemoSceneRoutes.Starter
                : starterSceneName.Trim();
            SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private static bool ValidateMultiplayerIntent(
            in DemoLaunchRequest request,
            out string error)
        {
            var hasMultiplayerIntent = DemoMultiplayerLaunchIntent.TryPeek(
                out var multiplayerGameplay,
                out _);

            if (request.Mode == DemoLaunchMode.Local)
            {
                if (hasMultiplayerIntent)
                {
                    error = "A local demo launch cannot carry a multiplayer launch intent.";
                    return false;
                }

                error = string.Empty;
                return true;
            }

            if (!hasMultiplayerIntent)
            {
                error = "A multiplayer demo launch requires a multiplayer launch intent.";
                return false;
            }

            var expectedGameplay = request.Gameplay == DemoGameplayId.Moba
                ? DemoMultiplayerGameplay.Moba
                : DemoMultiplayerGameplay.Shooter;
            if (multiplayerGameplay != expectedGameplay)
            {
                error = $"Gameplay mismatch: composition requested {request.Gameplay}, "
                        + $"but multiplayer intent requested {multiplayerGameplay}.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool FailLaunch(string message, out string error)
        {
            DemoLaunchIntent.Clear();
            DemoMultiplayerLaunchIntent.Clear();
            _lastError = string.IsNullOrWhiteSpace(message)
                ? "Demo gameplay launch failed."
                : message;
            error = _lastError;
            Debug.LogError($"[{nameof(DemoGameplayBootstrap)}] {_lastError}", this);
            return false;
        }
    }
}
