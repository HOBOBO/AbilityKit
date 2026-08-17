using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Demo.Common.Composition;
using AbilityKit.Demo.Common.Gameplay;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    public static class MobaDemoBuild
    {
        private const string MultiplayerStarterScenePath = "Assets/Scenes/" + DemoSceneRoutes.Starter + ".unity";
        private const string GameplayBootstrapScenePath = "Assets/Scenes/" + DemoSceneRoutes.Gameplay + ".unity";
        private const string GameplayCatalogPath = "Assets/DemoComposition/Profiles/DemoGameplayCatalog.asset";
        private const string MobaMenuPath = "Tools/AbilityKit/Demos/Moba/Builds/Build MOBA Windows IL2CPP";
        private const string ShooterMenuPath = "Tools/AbilityKit/Demos/Moba/Builds/Build Shooter Windows IL2CPP";
        private const string MultiplayerMenuPath = "Tools/AbilityKit/Demos/Moba/Builds/Build Multiplayer Starter Windows IL2CPP";
        private const string AllMenuPath = "Tools/AbilityKit/Demos/Moba/Builds/Build All Windows IL2CPP";
        private const string MobaExecutableName = "AbilityKitMobaDemo.exe";
        private const string ShooterExecutableName = "AbilityKitShooterDemo.exe";
        private const string MultiplayerExecutableName = "AbilityKitMultiplayerDemo.exe";
        private const string MobaLocalBuildDefine = "ABILITYKIT_DEMO_MOBA_LOCAL";
        private const string ShooterLocalBuildDefine = "ABILITYKIT_DEMO_SHOOTER_LOCAL";

        [MenuItem(MobaMenuPath, priority = 30)]
        public static void BuildWindowsIl2Cpp()
        {
            ConfigurePlayerSettings();
            BuildDemo(
                new[] { GameplayBootstrapScenePath },
                "Moba",
                MobaExecutableName,
                "MOBA Local",
                new[] { MobaLocalBuildDefine });
        }

        [MenuItem(ShooterMenuPath, priority = 31)]
        public static void BuildShooterWindowsIl2Cpp()
        {
            ConfigurePlayerSettings();
            BuildDemo(
                new[] { GameplayBootstrapScenePath },
                "Shooter",
                ShooterExecutableName,
                "Shooter Local",
                new[] { ShooterLocalBuildDefine });
        }

        [MenuItem(MultiplayerMenuPath, priority = 32)]
        public static void BuildMultiplayerWindowsIl2Cpp()
        {
            ConfigurePlayerSettings();
            BuildDemo(
                new[] { MultiplayerStarterScenePath, GameplayBootstrapScenePath },
                "Multiplayer",
                MultiplayerExecutableName,
                "Multiplayer Starter",
                Array.Empty<string>());
        }

        [MenuItem(AllMenuPath, priority = 33)]
        public static void BuildAllWindowsIl2Cpp()
        {
            ConfigurePlayerSettings();
            BuildDemo(
                new[] { GameplayBootstrapScenePath },
                "Moba",
                MobaExecutableName,
                "MOBA Local",
                new[] { MobaLocalBuildDefine });
            BuildDemo(
                new[] { GameplayBootstrapScenePath },
                "Shooter",
                ShooterExecutableName,
                "Shooter Local",
                new[] { ShooterLocalBuildDefine });
            BuildDemo(
                new[] { MultiplayerStarterScenePath, GameplayBootstrapScenePath },
                "Multiplayer",
                MultiplayerExecutableName,
                "Multiplayer Starter",
                Array.Empty<string>());
        }

        public static void ValidateMultiplayerSceneTopology()
        {
            var expectedScenes = new[]
            {
                MultiplayerStarterScenePath,
                GameplayBootstrapScenePath
            };
            ValidateBuildInput(expectedScenes, "Multiplayer Starter");

            var buildScenes = EditorBuildSettings.scenes;
            var enabledScenePaths = new List<string>(buildScenes.Length);
            for (var i = 0; i < buildScenes.Length; i++)
            {
                if (buildScenes[i].enabled)
                {
                    enabledScenePaths.Add(buildScenes[i].path);
                }
            }

            if (enabledScenePaths.Count != expectedScenes.Length)
            {
                throw new InvalidOperationException(
                    $"Build Settings must contain exactly {expectedScenes.Length} enabled demo scenes: " +
                    $"'{MultiplayerStarterScenePath}' and '{GameplayBootstrapScenePath}'.");
            }
            for (var i = 0; i < expectedScenes.Length; i++)
            {
                if (!string.Equals(enabledScenePaths[i], expectedScenes[i], StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Enabled demo build scene {i} must equal '{expectedScenes[i]}'.");
                }
            }

            ValidateStarterScene();
            ValidateGameplayComposition();

            Debug.Log("[MobaDemoBuild] Unified demo gameplay topology validation passed.");
        }

        private static void ValidateStarterScene()
        {
            var scene = EditorSceneManager.OpenScene(MultiplayerStarterScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            GameObject starterRoot = null;
            for (var i = 0; i < roots.Length; i++)
            {
                if (string.Equals(roots[i].name, "MultiplayerStarter", StringComparison.Ordinal))
                {
                    starterRoot = roots[i];
                    break;
                }
            }

            if (starterRoot == null)
            {
                throw new InvalidOperationException("MultiplayerStarterScene must contain a MultiplayerStarter root object.");
            }

            var behaviours = starterRoot.GetComponents<MonoBehaviour>();
            if (behaviours.Length != 1 || behaviours[0] == null ||
                !string.Equals(behaviours[0].GetType().FullName, "AbilityKit.Starter.MultiplayerStarterController", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "MultiplayerStarter root must contain exactly one MultiplayerStarterController.");
            }
        }

        private static void ValidateGameplayComposition()
        {
            var scene = EditorSceneManager.OpenScene(GameplayBootstrapScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            if (roots.Length != 1)
            {
                throw new InvalidOperationException(
                    $"{DemoSceneRoutes.Gameplay} must contain exactly one root object, but found {roots.Length}.");
            }

            var bootstraps = roots[0].GetComponentsInChildren<DemoGameplayBootstrap>(includeInactive: true);
            if (bootstraps.Length != 1)
            {
                throw new InvalidOperationException(
                    $"{DemoSceneRoutes.Gameplay} must contain exactly one {nameof(DemoGameplayBootstrap)}.");
            }

            var serializedBootstrap = new SerializedObject(bootstraps[0]);
            var catalogProperty = serializedBootstrap.FindProperty("catalog");
            var catalog = catalogProperty?.objectReferenceValue as DemoGameplayCatalogSO;
            if (catalog == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(DemoGameplayBootstrap)} must reference a {nameof(DemoGameplayCatalogSO)}.");
            }

            var catalogPath = AssetDatabase.GetAssetPath(catalog);
            if (!string.Equals(catalogPath, GameplayCatalogPath, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Gameplay bootstrap catalog must be '{GameplayCatalogPath}', but was '{catalogPath}'.");
            }

            ValidateGameplayCatalog(catalog);
        }

        private static void ValidateGameplayCatalog(DemoGameplayCatalogSO catalog)
        {
            var profiles = catalog.Profiles;
            if (profiles.Count != 4)
            {
                throw new InvalidOperationException(
                    $"Gameplay catalog must contain exactly four launch profiles, but found {profiles.Count}.");
            }

            var profileIds = new HashSet<string>(StringComparer.Ordinal);
            var launchKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < profiles.Count; i++)
            {
                var profile = profiles[i];
                if (profile == null)
                {
                    throw new InvalidOperationException($"Gameplay catalog profile {i} is null.");
                }
                if (!profile.TryValidate(out var error))
                {
                    throw new InvalidOperationException(error);
                }
                if (!profileIds.Add(profile.ProfileId))
                {
                    throw new InvalidOperationException($"Duplicate gameplay profile id '{profile.ProfileId}'.");
                }

                var launchKey = profile.Gameplay + "/" + profile.Mode;
                if (!launchKeys.Add(launchKey))
                {
                    throw new InvalidOperationException($"Duplicate gameplay launch profile '{launchKey}'.");
                }

                ValidateGameplayRoot(profile);
            }

            foreach (DemoGameplayId gameplay in Enum.GetValues(typeof(DemoGameplayId)))
            {
                foreach (DemoLaunchMode mode in Enum.GetValues(typeof(DemoLaunchMode)))
                {
                    var launchKey = gameplay + "/" + mode;
                    if (!launchKeys.Contains(launchKey))
                    {
                        throw new InvalidOperationException($"Gameplay catalog is missing launch profile '{launchKey}'.");
                    }
                }
            }
        }

        private static void ValidateGameplayRoot(DemoGameplayProfileSO profile)
        {
            var rootPrefab = profile.RootPrefab;
            var prefabPath = AssetDatabase.GetAssetPath(rootPrefab);
            if (rootPrefab == null || string.IsNullOrWhiteSpace(prefabPath) ||
                PrefabUtility.GetPrefabAssetType(rootPrefab) == PrefabAssetType.NotAPrefab)
            {
                throw new InvalidOperationException(
                    $"Gameplay profile '{profile.ProfileId}' root must reference a prefab asset.");
            }

            var cameras = rootPrefab.GetComponentsInChildren<Camera>(includeInactive: true);
            if (cameras.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Gameplay profile '{profile.ProfileId}' root must contain exactly one Camera, but found {cameras.Length}.");
            }

            var listeners = rootPrefab.GetComponentsInChildren<AudioListener>(includeInactive: true);
            if (listeners.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Gameplay profile '{profile.ProfileId}' root contains {listeners.Length} AudioListeners.");
            }

            var expectedEntryType = GetExpectedEntryType(profile.Gameplay, profile.Mode);
            var entryCount = 0;
            var behaviours = rootPrefab.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null && behaviours[i].GetType() == expectedEntryType)
                {
                    entryCount++;
                }
            }
            if (entryCount != 1)
            {
                throw new InvalidOperationException(
                    $"Gameplay profile '{profile.ProfileId}' root must contain exactly one " +
                    $"'{expectedEntryType.FullName}', but found {entryCount}.");
            }
        }

        private static Type GetExpectedEntryType(DemoGameplayId gameplay, DemoLaunchMode mode)
        {
            var assemblyQualifiedName = gameplay == DemoGameplayId.Moba
                ? "AbilityKit.Game.GameEntry, AbilityKit.Demo.Moba.View.Runtime"
                : mode == DemoLaunchMode.Local
                    ? "AbilityKit.Demo.Shooter.View.PlayMode.ShooterPlayModeMenu, AbilityKit.Demo.Shooter.View.Runtime"
                    : "AbilityKit.Demo.Shooter.View.PlayMode.ShooterFormalMultiplayerController, AbilityKit.Demo.Shooter.View.Runtime";
            return Type.GetType(assemblyQualifiedName) ?? throw new InvalidOperationException(
                $"Gameplay entry type '{assemblyQualifiedName}' is unavailable.");
        }

        private static void BuildDemo(
            string[] scenePaths,
            string outputDirectoryName,
            string executableName,
            string demoName,
            string[] extraScriptingDefines)
        {
            ValidateBuildInput(scenePaths, demoName);
            ValidateGameplayComposition();
            for (var i = 0; i < scenePaths.Length; i++)
            {
                if (string.Equals(scenePaths[i], MultiplayerStarterScenePath, StringComparison.Ordinal))
                {
                    ValidateStarterScene();
                    break;
                }
            }

            var outputPath = GetOutputPath(outputDirectoryName, executableName);
            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrEmpty(outputDirectory))
            {
                throw new InvalidOperationException($"Unable to resolve build output directory from '{outputPath}'.");
            }

            Directory.CreateDirectory(outputDirectory);

            Debug.Log($"[MobaDemoBuild] Starting {demoName} Windows IL2CPP build. Scenes='{string.Join(", ", scenePaths)}', Output='{outputPath}'.");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenePaths,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.StrictMode,
                extraScriptingDefines = extraScriptingDefines
            });

            var summary = report.summary;
            var resultMessage =
                $"Result={summary.result}, Errors={summary.totalErrors}, Warnings={summary.totalWarnings}, " +
                $"Duration={summary.totalTime}, Size={summary.totalSize}, Output='{outputPath}'.";

            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"[MobaDemoBuild] {demoName} build failed. {resultMessage}");
            }

            Debug.Log($"[MobaDemoBuild] {demoName} build succeeded. {resultMessage}");
        }

        private static void ValidateBuildInput(string[] scenePaths, string demoName)
        {
            if (scenePaths == null || scenePaths.Length == 0)
            {
                throw new InvalidOperationException($"{demoName} build requires at least one scene.");
            }

            for (var i = 0; i < scenePaths.Length; i++)
            {
                var scenePath = scenePaths[i];
                if (!File.Exists(Path.GetFullPath(scenePath)))
                {
                    throw new FileNotFoundException($"{demoName} demo scene was not found.", scenePath);
                }

                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
                if (sceneAsset == null)
                {
                    throw new InvalidOperationException($"{demoName} demo scene cannot be loaded as a SceneAsset: '{scenePath}'.");
                }
            }
        }

        private static void ConfigurePlayerSettings()
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone, ApiCompatibilityLevel.NET_Standard_2_0);
            PlayerSettings.SetArchitecture(BuildTargetGroup.Standalone, 1);
        }

        private static string GetOutputPath(string outputDirectoryName, string executableName)
        {
            var unityProjectDirectory = Directory.GetParent(Application.dataPath)?.FullName;
            var repositoryRoot = unityProjectDirectory == null
                ? null
                : Directory.GetParent(unityProjectDirectory)?.FullName;

            if (string.IsNullOrEmpty(repositoryRoot))
            {
                throw new InvalidOperationException($"Unable to resolve repository root from Application.dataPath '{Application.dataPath}'.");
            }

            return Path.GetFullPath(Path.Combine(repositoryRoot, "build", outputDirectoryName, executableName));
        }
    }
}
