using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    public static class MobaDemoBuild
    {
        private const string MobaScenePath = "Assets/Scenes/MobaDemoScene.unity";
        private const string ShooterScenePath = "Assets/Scenes/ShooterDemoScene.unity";
        private const string MobaMenuPath = "Tools/AbilityKit/Demo Builds/Build MOBA Windows IL2CPP";
        private const string ShooterMenuPath = "Tools/AbilityKit/Demo Builds/Build Shooter Windows IL2CPP";
        private const string AllMenuPath = "Tools/AbilityKit/Demo Builds/Build All Windows IL2CPP";
        private const string MobaExecutableName = "AbilityKitMobaDemo.exe";
        private const string ShooterExecutableName = "AbilityKitShooterDemo.exe";

        [MenuItem(MobaMenuPath, priority = 30)]
        public static void BuildWindowsIl2Cpp()
        {
            ConfigurePlayerSettings();
            BuildDemo(MobaScenePath, "Moba", MobaExecutableName, "MOBA");
        }

        [MenuItem(ShooterMenuPath, priority = 31)]
        public static void BuildShooterWindowsIl2Cpp()
        {
            ConfigurePlayerSettings();
            BuildDemo(ShooterScenePath, "Shooter", ShooterExecutableName, "Shooter");
        }

        [MenuItem(AllMenuPath, priority = 32)]
        public static void BuildAllWindowsIl2Cpp()
        {
            ConfigurePlayerSettings();
            BuildDemo(MobaScenePath, "Moba", MobaExecutableName, "MOBA");
            BuildDemo(ShooterScenePath, "Shooter", ShooterExecutableName, "Shooter");
        }

        private static void BuildDemo(string scenePath, string outputDirectoryName, string executableName, string demoName)
        {
            ValidateBuildInput(scenePath, demoName);
            var outputPath = GetOutputPath(outputDirectoryName, executableName);
            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrEmpty(outputDirectory))
            {
                throw new InvalidOperationException($"Unable to resolve build output directory from '{outputPath}'.");
            }

            Directory.CreateDirectory(outputDirectory);

            Debug.Log($"[MobaDemoBuild] Starting {demoName} Windows IL2CPP build. Scene='{scenePath}', Output='{outputPath}'.");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
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

        private static void ValidateBuildInput(string scenePath, string demoName)
        {
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
