#nullable enable

using System;
using System.IO;
using AbilityKit.Demo.Common.Composition;
using AbilityKit.Demo.Common.Gameplay;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Starter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AbilityKit.Game.Editor.Automation
{
    [InitializeOnLoad]
    public static class StarterLocalLaunchHeadlessCommand
    {
        private const string RunningKey = "AbilityKit.StarterLocalLaunch.Running";
        private const string GameplayKey = "AbilityKit.StarterLocalLaunch.Gameplay";
        private const string ResultPathKey = "AbilityKit.StarterLocalLaunch.ResultPath";
        private const string LaunchIssuedKey = "AbilityKit.StarterLocalLaunch.LaunchIssued";
        private const string TickKey = "AbilityKit.StarterLocalLaunch.Tick";
        private const int MaxTicks = 1800;
        private const string StarterScenePath =
            "Assets/Scenes/" + DemoSceneRoutes.Starter + ".unity";
        private const string MobaPackageRoot =
            "Packages/com.abilitykit.demo.moba.view.runtime/";
        private const string ShooterPackageRoot =
            "Packages/com.abilitykit.demo.shooter.view.runtime/";

        static StarterLocalLaunchHeadlessCommand()
        {
            EditorApplication.update -= Continue;
            EditorApplication.update += Continue;
        }

        public static void Run()
        {
            try
            {
                var gameplay = ParseGameplay(GetArgValue("-starterGameplay"));
                var resultPath = GetArgValue("-starterResult");
                if (string.IsNullOrWhiteSpace(resultPath))
                {
                    resultPath = Path.GetFullPath(
                        $"../starter-local-{gameplay.ToString().ToLowerInvariant()}.json");
                }

                var resultDirectory = Path.GetDirectoryName(resultPath);
                if (!string.IsNullOrWhiteSpace(resultDirectory))
                {
                    Directory.CreateDirectory(resultDirectory);
                }

                SessionState.SetBool(RunningKey, true);
                SessionState.SetInt(GameplayKey, (int)gameplay);
                SessionState.SetString(ResultPathKey, resultPath);
                SessionState.SetBool(LaunchIssuedKey, false);
                SessionState.SetInt(TickKey, 0);
                DemoLaunchIntent.Clear();
                DemoMultiplayerLaunchIntent.Clear();

                var scene = EditorSceneManager.OpenScene(StarterScenePath, OpenSceneMode.Single);
                if (!scene.IsValid())
                {
                    throw new InvalidOperationException(
                        $"Starter scene could not be opened from '{StarterScenePath}'.");
                }

                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception)
            {
                Finish(false, "Failed to start Starter local launch validation: " + exception);
            }
        }

        private static void Continue()
        {
            if (!SessionState.GetBool(RunningKey, false) ||
                !EditorApplication.isPlaying ||
                EditorApplication.isPaused)
            {
                return;
            }

            try
            {
                var tick = SessionState.GetInt(TickKey, 0) + 1;
                SessionState.SetInt(TickKey, tick);
                if (tick > MaxTicks)
                {
                    throw new TimeoutException(
                        "Starter local launch validation timed out. " + BuildDiagnostic());
                }

                var gameplay = (DemoGameplayId)SessionState.GetInt(
                    GameplayKey,
                    (int)DemoGameplayId.Moba);
                if (!SessionState.GetBool(LaunchIssuedKey, false))
                {
                    var starter = Object.FindObjectOfType<StarterController>();
                    if (starter == null)
                    {
                        return;
                    }

                    starter.LaunchLocalAutomated(gameplay);
                    SessionState.SetBool(LaunchIssuedKey, true);
                    return;
                }

                var expectedSceneName = DemoSceneRoutes.GetGameplaySceneName(gameplay);
                var activeScene = SceneManager.GetActiveScene();
                if (!string.Equals(activeScene.name, expectedSceneName, StringComparison.Ordinal))
                {
                    return;
                }

                var bootstrap = Object.FindObjectOfType<DemoGameplayBootstrap>();
                if (bootstrap == null || bootstrap.ActiveProfile == null || bootstrap.ActiveRoot == null)
                {
                    return;
                }

                ValidateResult(gameplay, activeScene, bootstrap);
                Finish(
                    true,
                    $"Starter launched {gameplay} Local through '{activeScene.path}' " +
                    $"with profile '{bootstrap.ActiveProfile.ProfileId}'.",
                    gameplay,
                    activeScene.path,
                    bootstrap.ActiveProfile.ProfileId,
                    AssetDatabase.GetAssetPath(bootstrap.ActiveProfile),
                    AssetDatabase.GetAssetPath(bootstrap.ActiveProfile.RootPrefab));
            }
            catch (Exception exception)
            {
                Finish(false, exception + " | " + BuildDiagnostic());
            }
        }

        private static void ValidateResult(
            DemoGameplayId gameplay,
            Scene activeScene,
            DemoGameplayBootstrap bootstrap)
        {
            var profile = bootstrap.ActiveProfile
                          ?? throw new InvalidOperationException("Gameplay profile is unavailable.");
            if (profile.Gameplay != gameplay || profile.Mode != DemoLaunchMode.Local)
            {
                throw new InvalidOperationException(
                    $"Expected {gameplay}/Local, got {profile.Gameplay}/{profile.Mode}.");
            }

            if (DemoMultiplayerLaunchIntent.TryPeek(out _, out _))
            {
                throw new InvalidOperationException(
                    "Starter Local mode left a multiplayer launch intent pending.");
            }

            var expectedPackageRoot = gameplay == DemoGameplayId.Moba
                ? MobaPackageRoot
                : ShooterPackageRoot;
            AssertOwnedByPackage("scene", activeScene.path, expectedPackageRoot);
            AssertOwnedByPackage(
                "profile",
                AssetDatabase.GetAssetPath(profile),
                expectedPackageRoot);
            AssertOwnedByPackage(
                "root prefab",
                AssetDatabase.GetAssetPath(profile.RootPrefab),
                expectedPackageRoot);
        }

        private static void AssertOwnedByPackage(
            string assetKind,
            string assetPath,
            string expectedPackageRoot)
        {
            if (string.IsNullOrWhiteSpace(assetPath) ||
                !assetPath.StartsWith(expectedPackageRoot, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{assetKind} must be owned by '{expectedPackageRoot}', but was '{assetPath}'.");
            }
        }

        private static DemoGameplayId ParseGameplay(string value)
        {
            if (Enum.TryParse(value, ignoreCase: true, out DemoGameplayId gameplay) &&
                Enum.IsDefined(typeof(DemoGameplayId), gameplay))
            {
                return gameplay;
            }

            throw new ArgumentException(
                $"-starterGameplay must be Moba or Shooter, but was '{value}'.");
        }

        private static string BuildDiagnostic()
        {
            var scene = SceneManager.GetActiveScene();
            var bootstrap = Object.FindObjectOfType<DemoGameplayBootstrap>();
            return $"tick={SessionState.GetInt(TickKey, 0)}, " +
                   $"launchIssued={SessionState.GetBool(LaunchIssuedKey, false)}, " +
                   $"activeScene='{scene.name}', " +
                   $"bootstrap={(bootstrap != null)}, " +
                   $"profile='{bootstrap?.ActiveProfile?.ProfileId ?? string.Empty}', " +
                   $"lastError='{bootstrap?.LastError ?? string.Empty}'.";
        }

        private static string GetArgValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return string.Empty;
        }

        private static void Finish(
            bool success,
            string message,
            DemoGameplayId gameplay = DemoGameplayId.Moba,
            string scenePath = "",
            string profileId = "",
            string profilePath = "",
            string rootPrefabPath = "")
        {
            var resultPath = SessionState.GetString(ResultPathKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(resultPath))
            {
                var escapedMessage = EscapeJson(message);
                var json = "{\n" +
                           $"  \"success\": {(success ? "true" : "false")},\n" +
                           $"  \"gameplay\": \"{gameplay}\",\n" +
                           "  \"mode\": \"Local\",\n" +
                           $"  \"scenePath\": \"{EscapeJson(scenePath)}\",\n" +
                           $"  \"profileId\": \"{EscapeJson(profileId)}\",\n" +
                           $"  \"profilePath\": \"{EscapeJson(profilePath)}\",\n" +
                           $"  \"rootPrefabPath\": \"{EscapeJson(rootPrefabPath)}\",\n" +
                           $"  \"message\": \"{escapedMessage}\"\n" +
                           "}\n";
                File.WriteAllText(resultPath, json);
            }

            if (success)
            {
                Debug.Log("[StarterLocalLaunchHeadless] " + message);
            }
            else
            {
                Debug.LogError("[StarterLocalLaunchHeadless] " + message);
            }

            SessionState.EraseBool(RunningKey);
            SessionState.EraseInt(GameplayKey);
            SessionState.EraseString(ResultPathKey);
            SessionState.EraseBool(LaunchIssuedKey);
            SessionState.EraseInt(TickKey);
            DemoLaunchIntent.Clear();
            DemoMultiplayerLaunchIntent.Clear();
            EditorApplication.update -= Continue;
            if (EditorApplication.isPlaying)
            {
                EditorApplication.ExitPlaymode();
            }
            EditorApplication.Exit(success ? 0 : 1);
        }

        private static string EscapeJson(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }
}
