using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    public static class MobaDemoBuild
    {
        private const string MobaScenePath = "Assets/Scenes/MobaDemoScene.unity";
        private const string ShooterScenePath = "Assets/Scenes/ShooterDemoScene.unity";
        private const string MultiplayerStarterScenePath = "Assets/Scenes/MultiplayerStarterScene.unity";
        private const string MobaMultiplayerScenePath = "Assets/Scenes/MobaMultiplayerScene.unity";
        private const string ShooterMultiplayerScenePath = "Assets/Scenes/ShooterMultiplayerScene.unity";
        private const string MobaMenuPath = "Tools/AbilityKit/Demo Builds/Build MOBA Windows IL2CPP";
        private const string ShooterMenuPath = "Tools/AbilityKit/Demo Builds/Build Shooter Windows IL2CPP";
        private const string MultiplayerMenuPath = "Tools/AbilityKit/Demo Builds/Build Multiplayer Starter Windows IL2CPP";
        private const string AllMenuPath = "Tools/AbilityKit/Demo Builds/Build All Windows IL2CPP";
        private const string MobaExecutableName = "AbilityKitMobaDemo.exe";
        private const string ShooterExecutableName = "AbilityKitShooterDemo.exe";
        private const string MultiplayerExecutableName = "AbilityKitMultiplayerDemo.exe";

        [MenuItem(MobaMenuPath, priority = 30)]
        public static void BuildWindowsIl2Cpp()
        {
            ConfigurePlayerSettings();
            BuildDemo(new[] { MobaScenePath }, "Moba", MobaExecutableName, "MOBA Local");
        }

        [MenuItem(ShooterMenuPath, priority = 31)]
        public static void BuildShooterWindowsIl2Cpp()
        {
            ConfigurePlayerSettings();
            BuildDemo(new[] { ShooterScenePath }, "Shooter", ShooterExecutableName, "Shooter Local");
        }

        [MenuItem(MultiplayerMenuPath, priority = 32)]
        public static void BuildMultiplayerWindowsIl2Cpp()
        {
            ConfigurePlayerSettings();
            BuildDemo(
                new[] { MultiplayerStarterScenePath, MobaMultiplayerScenePath, ShooterMultiplayerScenePath },
                "Multiplayer",
                MultiplayerExecutableName,
                "Multiplayer Starter");
        }

        [MenuItem(AllMenuPath, priority = 33)]
        public static void BuildAllWindowsIl2Cpp()
        {
            ConfigurePlayerSettings();
            BuildDemo(new[] { MobaScenePath }, "Moba", MobaExecutableName, "MOBA Local");
            BuildDemo(new[] { ShooterScenePath }, "Shooter", ShooterExecutableName, "Shooter Local");
            BuildDemo(
                new[] { MultiplayerStarterScenePath, MobaMultiplayerScenePath, ShooterMultiplayerScenePath },
                "Multiplayer",
                MultiplayerExecutableName,
                "Multiplayer Starter");
        }

        public static void ValidateMultiplayerSceneTopology()
        {
            var expectedScenes = new[]
            {
                MultiplayerStarterScenePath,
                MobaMultiplayerScenePath,
                ShooterMultiplayerScenePath,
                MobaScenePath,
                ShooterScenePath
            };
            ValidateBuildInput(expectedScenes, "Multiplayer Starter");

            var buildScenes = EditorBuildSettings.scenes;
            if (buildScenes.Length < expectedScenes.Length)
            {
                throw new InvalidOperationException("Multiplayer build settings must contain Starter, MOBA, and Shooter scenes.");
            }

            for (var i = 0; i < expectedScenes.Length; i++)
            {
                if (!buildScenes[i].enabled || !string.Equals(buildScenes[i].path, expectedScenes[i], StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Multiplayer build scene {i} must be enabled and equal to '{expectedScenes[i]}'.");
                }
            }

            var starterScene = EditorSceneManager.OpenScene(MultiplayerStarterScenePath, OpenSceneMode.Single);
            var roots = starterScene.GetRootGameObjects();
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

            ValidateMultiplayerEntryMarker(MobaMultiplayerScenePath, expectedGameplay: 0);
            ValidateMultiplayerEntryMarker(ShooterMultiplayerScenePath, expectedGameplay: 1);
            ValidateLocalSceneHasNoMultiplayerEntryMarker(MobaScenePath);
            ValidateLocalSceneHasNoMultiplayerEntryMarker(ShooterScenePath);

            Debug.Log("[MobaDemoBuild] Multiplayer scene topology validation passed.");
        }

        public static void CreateMultiplayerClientScenes()
        {
            CopySceneIfMissing(MobaScenePath, MobaMultiplayerScenePath);
            CopySceneIfMissing(ShooterScenePath, ShooterMultiplayerScenePath);
            EnsureMultiplayerEntryMarker(MobaMultiplayerScenePath, gameplay: 0);
            EnsureMultiplayerEntryMarker(ShooterMultiplayerScenePath, gameplay: 1);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CopySceneIfMissing(string sourcePath, string destinationPath)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(destinationPath) != null)
            {
                return;
            }

            if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
            {
                throw new InvalidOperationException(
                    $"Failed to create multiplayer scene '{destinationPath}' from '{sourcePath}'.");
            }
        }

        private static void EnsureMultiplayerEntryMarker(string scenePath, int gameplay)
        {
            var markerType = Type.GetType("AbilityKit.Starter.MultiplayerSceneEntryMarker, Assembly-CSharp");
            if (markerType == null)
            {
                throw new InvalidOperationException("MultiplayerSceneEntryMarker type is unavailable.");
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            GameObject markerRoot = null;
            for (var i = 0; i < roots.Length; i++)
            {
                if (string.Equals(roots[i].name, "MultiplayerSceneEntry", StringComparison.Ordinal))
                {
                    markerRoot = roots[i];
                    break;
                }
            }

            if (markerRoot == null)
            {
                markerRoot = new GameObject("MultiplayerSceneEntry");
            }

            var marker = markerRoot.GetComponent(markerType) ?? markerRoot.AddComponent(markerType);
            var serializedMarker = new SerializedObject(marker);
            serializedMarker.FindProperty("gameplay").enumValueIndex = gameplay;
            serializedMarker.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
        }

        private static void ValidateMultiplayerEntryMarker(string scenePath, int expectedGameplay)
        {
            var marker = FindMultiplayerEntryMarker(scenePath);
            if (marker == null)
            {
                throw new InvalidOperationException($"Multiplayer scene '{scenePath}' is missing its entry marker.");
            }

            var serializedMarker = new SerializedObject(marker);
            if (serializedMarker.FindProperty("gameplay").enumValueIndex != expectedGameplay)
            {
                throw new InvalidOperationException($"Multiplayer scene '{scenePath}' has the wrong gameplay marker.");
            }
        }

        private static void ValidateLocalSceneHasNoMultiplayerEntryMarker(string scenePath)
        {
            if (FindMultiplayerEntryMarker(scenePath) != null)
            {
                throw new InvalidOperationException($"Local demo scene '{scenePath}' must not contain a multiplayer entry marker.");
            }
        }

        private static Component FindMultiplayerEntryMarker(string scenePath)
        {
            var markerType = Type.GetType("AbilityKit.Starter.MultiplayerSceneEntryMarker, Assembly-CSharp");
            if (markerType == null)
            {
                throw new InvalidOperationException("MultiplayerSceneEntryMarker type is unavailable.");
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var marker = roots[i].GetComponent(markerType);
                if (marker != null)
                {
                    return marker;
                }
            }

            return null;
        }

        private static void BuildDemo(string[] scenePaths, string outputDirectoryName, string executableName, string demoName)
        {
            ValidateBuildInput(scenePaths, demoName);
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
                options = BuildOptions.StrictMode
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
