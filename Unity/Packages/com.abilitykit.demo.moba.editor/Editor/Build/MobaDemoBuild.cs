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
        private const string StarterScenePath = "Assets/Scenes/" + DemoSceneRoutes.Starter + ".unity";
        private const string MobaGameplayScenePath =
            "Packages/com.abilitykit.demo.moba.view.runtime/Scenes/" + DemoSceneRoutes.Moba + ".unity";
        private const string ShooterGameplayScenePath =
            "Packages/com.abilitykit.demo.shooter.view.runtime/Scenes/" + DemoSceneRoutes.Shooter + ".unity";
        private const string MobaGameplayCatalogPath =
            "Packages/com.abilitykit.demo.moba.view.runtime/Composition/Profiles/MobaGameplayCatalog.asset";
        private const string ShooterGameplayCatalogPath =
            "Packages/com.abilitykit.demo.shooter.view.runtime/Composition/Profiles/ShooterGameplayCatalog.asset";
        private const string MobaMenuPath = "Tools/AbilityKit/Demos/Moba/Builds/Build MOBA Windows IL2CPP";
        private const string ShooterMenuPath = "Tools/AbilityKit/Demos/Moba/Builds/Build Shooter Windows IL2CPP";
        private const string MultiplayerMenuPath = "Tools/AbilityKit/Demos/Moba/Builds/Build Starter Windows IL2CPP";
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
                new[] { MobaGameplayScenePath },
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
                new[] { ShooterGameplayScenePath },
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
                new[] { StarterScenePath, MobaGameplayScenePath, ShooterGameplayScenePath },
                "Multiplayer",
                MultiplayerExecutableName,
                "Starter",
                Array.Empty<string>());
        }

        [MenuItem(AllMenuPath, priority = 33)]
        public static void BuildAllWindowsIl2Cpp()
        {
            ConfigurePlayerSettings();
            BuildDemo(
                new[] { MobaGameplayScenePath },
                "Moba",
                MobaExecutableName,
                "MOBA Local",
                new[] { MobaLocalBuildDefine });
            BuildDemo(
                new[] { ShooterGameplayScenePath },
                "Shooter",
                ShooterExecutableName,
                "Shooter Local",
                new[] { ShooterLocalBuildDefine });
            BuildDemo(
                new[] { StarterScenePath, MobaGameplayScenePath, ShooterGameplayScenePath },
                "Multiplayer",
                MultiplayerExecutableName,
                "Starter",
                Array.Empty<string>());
        }

        public static void ValidateMultiplayerSceneTopology()
        {
            var expectedScenes = new[]
            {
                StarterScenePath,
                MobaGameplayScenePath,
                ShooterGameplayScenePath
            };
            ValidateBuildInput(expectedScenes, "Starter");

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
                    $"'{StarterScenePath}', '{MobaGameplayScenePath}', and '{ShooterGameplayScenePath}'.");
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
            ValidateGameplayComposition(
                MobaGameplayScenePath,
                MobaGameplayCatalogPath,
                DemoGameplayId.Moba);
            ValidateGameplayComposition(
                ShooterGameplayScenePath,
                ShooterGameplayCatalogPath,
                DemoGameplayId.Shooter);

            Debug.Log("[MobaDemoBuild] Unified demo gameplay topology validation passed.");
        }

        private static void ValidateStarterScene()
        {
            var scene = EditorSceneManager.OpenScene(StarterScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            GameObject starterRoot = null;
            for (var i = 0; i < roots.Length; i++)
            {
                if (string.Equals(roots[i].name, "Starter", StringComparison.Ordinal))
                {
                    starterRoot = roots[i];
                    break;
                }
            }

            if (starterRoot == null)
            {
                throw new InvalidOperationException("StarterScene must contain a Starter root object.");
            }

            var behaviours = starterRoot.GetComponents<MonoBehaviour>();
            if (behaviours.Length != 1 || behaviours[0] == null ||
                !string.Equals(behaviours[0].GetType().FullName, "AbilityKit.Starter.StarterController", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Starter root must contain exactly one StarterController.");
            }
        }

        private static void ValidateGameplayComposition(
            string scenePath,
            string expectedCatalogPath,
            DemoGameplayId expectedGameplay)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            if (roots.Length != 1)
            {
                throw new InvalidOperationException(
                    $"'{scenePath}' must contain exactly one root object, but found {roots.Length}.");
            }

            var bootstraps = roots[0].GetComponentsInChildren<DemoGameplayBootstrap>(includeInactive: true);
            if (bootstraps.Length != 1)
            {
                throw new InvalidOperationException(
                    $"'{scenePath}' must contain exactly one {nameof(DemoGameplayBootstrap)}.");
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
            if (!string.Equals(catalogPath, expectedCatalogPath, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Gameplay bootstrap catalog must be '{expectedCatalogPath}', but was '{catalogPath}'.");
            }

            ValidateGameplayCatalog(catalog, expectedGameplay);
        }

        private static void ValidateGameplayCatalog(
            DemoGameplayCatalogSO catalog,
            DemoGameplayId expectedGameplay)
        {
            var profiles = catalog.Profiles;
            if (profiles.Count != 2)
            {
                throw new InvalidOperationException(
                    $"{expectedGameplay} catalog must contain exactly two launch profiles, but found {profiles.Count}.");
            }

            var profileIds = new HashSet<string>(StringComparer.Ordinal);
            var modes = new HashSet<DemoLaunchMode>();
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
                if (profile.Gameplay != expectedGameplay)
                {
                    throw new InvalidOperationException(
                        $"Catalog for {expectedGameplay} contains profile '{profile.ProfileId}' for {profile.Gameplay}.");
                }
                if (!profileIds.Add(profile.ProfileId))
                {
                    throw new InvalidOperationException($"Duplicate gameplay profile id '{profile.ProfileId}'.");
                }
                if (!modes.Add(profile.Mode))
                {
                    throw new InvalidOperationException(
                        $"Duplicate {expectedGameplay} launch mode '{profile.Mode}'.");
                }

                ValidateGameplayRoot(profile);
            }

            foreach (DemoLaunchMode mode in Enum.GetValues(typeof(DemoLaunchMode)))
            {
                if (!modes.Contains(mode))
                {
                    throw new InvalidOperationException(
                        $"{expectedGameplay} catalog is missing launch mode '{mode}'.");
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
            for (var i = 0; i < scenePaths.Length; i++)
            {
                if (string.Equals(scenePaths[i], StarterScenePath, StringComparison.Ordinal))
                {
                    ValidateStarterScene();
                }
                else if (string.Equals(scenePaths[i], MobaGameplayScenePath, StringComparison.Ordinal))
                {
                    ValidateGameplayComposition(
                        MobaGameplayScenePath,
                        MobaGameplayCatalogPath,
                        DemoGameplayId.Moba);
                }
                else if (string.Equals(scenePaths[i], ShooterGameplayScenePath, StringComparison.Ordinal))
                {
                    ValidateGameplayComposition(
                        ShooterGameplayScenePath,
                        ShooterGameplayCatalogPath,
                        DemoGameplayId.Shooter);
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
