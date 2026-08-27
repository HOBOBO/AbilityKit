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

namespace AbilityKit.Game.Editor.Automation
{
    /// <summary>
    /// Headless MOBA 启动 + 游戏视图截图：复用 Starter 场景正确链路
    /// （StarterController.LaunchLocalAutomated），战斗 boot 后截一帧游戏视图存 PNG。
    /// 用法：Unity.exe -batchmode -projectPath Unity -executeMethod AbilityKit.Game.Editor.Automation.StarterLocalScreenshotCommand.Run
    /// 产物：local/Logs/moba-battle.png
    /// </summary>
    [InitializeOnLoad]
    public static class StarterLocalScreenshotCommand
    {
        private const string RunningKey = "AbilityKit.StarterScreenshot.Running";
        private const string LaunchIssuedKey = "AbilityKit.StarterScreenshot.LaunchIssued";
        private const string TickKey = "AbilityKit.StarterScreenshot.Tick";
        private const string ScreenshotTickKey = "AbilityKit.StarterScreenshot.ScreenshotTick";
        private const int MaxTicks = 1800;
        private const string StarterScenePath = "Assets/Scenes/" + DemoSceneRoutes.Starter + ".unity";

        static StarterLocalScreenshotCommand()
        {
            EditorApplication.update -= Continue;
            EditorApplication.update += Continue;
        }

        public static void Run()
        {
            try
            {
                SessionState.SetBool(RunningKey, true);
                SessionState.SetBool(LaunchIssuedKey, false);
                SessionState.SetInt(TickKey, 0);
                SessionState.SetInt(ScreenshotTickKey, 0);
                DemoLaunchIntent.Clear();
                DemoMultiplayerLaunchIntent.Clear();

                var scene = EditorSceneManager.OpenScene(StarterScenePath, OpenSceneMode.Single);
                if (!scene.IsValid())
                {
                    throw new InvalidOperationException($"Starter scene could not be opened from '{StarterScenePath}'.");
                }

                Debug.Log("[StarterScreenshot] starter scene opened, entering play");
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception)
            {
                Debug.LogError("[StarterScreenshot] " + exception);
                EditorApplication.Exit(1);
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
                    throw new TimeoutException("Starter screenshot timed out. " + BuildDiagnostic());
                }

                if (!SessionState.GetBool(LaunchIssuedKey, false))
                {
                    var starter = UnityEngine.Object.FindObjectOfType<StarterController>();
                    if (starter == null) return;
                    starter.LaunchLocalAutomated(DemoGameplayId.Moba);
                    SessionState.SetBool(LaunchIssuedKey, true);
                    Debug.Log("[StarterScreenshot] local MOBA launch issued");
                    return;
                }

                if (!string.Equals(SceneManager.GetActiveScene().name, DemoSceneRoutes.Moba, StringComparison.Ordinal))
                {
                    return;
                }

                var bootstrap = UnityEngine.Object.FindObjectOfType<DemoGameplayBootstrap>();
                if (bootstrap == null || bootstrap.ActiveRoot == null)
                {
                    return;
                }

                var screenshotTick = SessionState.GetInt(ScreenshotTickKey, 0) + 1;
                SessionState.SetInt(ScreenshotTickKey, screenshotTick);
                if (screenshotTick == 30)
                {
                    var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "local", "Logs", "moba-battle.png"));
                    if (CaptureCameraScreenshot(path, 1280, 720))
                    {
                        Debug.Log("[StarterScreenshot] screenshot saved: " + path);
                    }
                    else
                    {
                        Debug.LogWarning("[StarterScreenshot] no camera found to capture.");
                    }
                }
                if (screenshotTick >= 60)
                {
                    Debug.Log("[StarterScreenshot] done, profile=" + bootstrap.ActiveProfile?.ProfileId);
                    Finish();
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("[StarterScreenshot] " + exception + " | " + BuildDiagnostic());
                Finish();
            }
        }

        /// <summary>headless 可靠截图：相机渲染到 RenderTexture -> ReadPixels -> PNG。</summary>
        private static bool CaptureCameraScreenshot(string absolutePath, int width, int height)
        {
            var camera = Camera.main ?? UnityEngine.Object.FindObjectOfType<Camera>();
            if (camera == null) return false;

            var directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var rt = new RenderTexture(width, height, 24);
            var previousTarget = camera.targetTexture;
            camera.targetTexture = rt;
            camera.Render();

            RenderTexture.active = rt;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());

            RenderTexture.active = null;
            camera.targetTexture = previousTarget;
            UnityEngine.Object.DestroyImmediate(texture);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            return true;
        }

        private static string BuildDiagnostic()
        {
            var scene = SceneManager.GetActiveScene();
            var bootstrap = UnityEngine.Object.FindObjectOfType<DemoGameplayBootstrap>();
            return $"tick={SessionState.GetInt(TickKey, 0)}, " +
                   $"launchIssued={SessionState.GetBool(LaunchIssuedKey, false)}, " +
                   $"activeScene='{scene.name}', " +
                   $"bootstrap={(bootstrap != null)}, " +
                   $"activeRoot={(bootstrap?.ActiveRoot != null)}, " +
                   $"lastError='{bootstrap?.LastError ?? string.Empty}'.";
        }

        private static void Finish()
        {
            SessionState.EraseBool(RunningKey);
            SessionState.EraseBool(LaunchIssuedKey);
            SessionState.EraseInt(TickKey);
            SessionState.EraseInt(ScreenshotTickKey);
            DemoLaunchIntent.Clear();
            DemoMultiplayerLaunchIntent.Clear();
            EditorApplication.update -= Continue;
            if (EditorApplication.isPlaying)
            {
                EditorApplication.ExitPlaymode();
            }
            EditorApplication.Exit(0);
        }
    }
}
