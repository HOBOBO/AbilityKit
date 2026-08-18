#nullable enable

using System;
using AbilityKit.Demo.Common.Gameplay;
using AbilityKit.Demo.Common.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    [InitializeOnLoad]
    public static class MobaDemoSceneMenu
    {
        private const string GameplayScenePath =
            "Packages/com.abilitykit.demo.moba.view.runtime/Scenes/" + DemoSceneRoutes.Moba + ".unity";
        private const string LocalProfileId = "moba-local";
        private const string PendingLaunchKey = "AbilityKit.MobaDemo.PendingUnifiedLaunch";
        private const string MenuRoot = "Tools/AbilityKit/Demos/Moba/";

        static MobaDemoSceneMenu()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem(MenuRoot + "Open Demo Scene", priority = 10)]
        private static void OpenDemoScene()
        {
            PrepareAndOpenGameplayScene();
        }

        [MenuItem(MenuRoot + "Create Or Refresh Demo Scene", priority = 11)]
        private static void CreateOrRefreshDemoScene()
        {
            if (!PrepareAndOpenGameplayScene())
            {
                return;
            }

            EditorUtility.DisplayDialog("MOBA Demo", $"MOBA package composition is ready:\n{GameplayScenePath}", "OK");
            PingSceneAsset();
        }

        public static void CreateOrRefreshDemoSceneBatch()
        {
            DemoGameplayCompositionBuilder.GenerateAll();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameplayScenePath) == null)
            {
                throw new InvalidOperationException("Unable to create or refresh the MOBA gameplay scene.");
            }
        }

        [MenuItem(MenuRoot + "Play Demo Scene", priority = 12)]
        private static void PlayDemoScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!PrepareAndOpenGameplayScene())
            {
                return;
            }

            SessionState.SetBool(PendingLaunchKey, true);
            IssueLocalMobaRequest();
            EditorApplication.EnterPlaymode();
        }

        private static bool PrepareAndOpenGameplayScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return false;
            }

            DemoGameplayCompositionBuilder.GenerateAll();
            var scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                throw new InvalidOperationException($"Unable to open MOBA gameplay scene '{GameplayScenePath}'.");
            }

            return true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.EraseBool(PendingLaunchKey);
                DemoLaunchIntent.Clear();
                DemoMultiplayerLaunchIntent.Clear();
                return;
            }

            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingLaunchKey, false))
            {
                SessionState.EraseBool(PendingLaunchKey);
                IssueLocalMobaRequest();
            }
        }

        private static void IssueLocalMobaRequest()
        {
            DemoMultiplayerLaunchIntent.Clear();
            var request = new DemoLaunchRequest(
                DemoGameplayId.Moba,
                DemoLaunchMode.Local,
                LocalProfileId);
            DemoLaunchIntent.Request(in request);
        }

        private static void PingSceneAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(GameplayScenePath);
            if (asset == null)
            {
                return;
            }

            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
        }
    }
}
