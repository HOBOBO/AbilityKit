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
        private const string GameplayScenePath = DemoGameplayCompositionBuilder.MobaScenePath;
        private const string LocalProfileId = "moba-local";
        private const string PendingLaunchKey = "AbilityKit.MobaDemo.PendingUnifiedLaunch";
        private const string MenuRoot = "Tools/AbilityKit/Demos/Moba/";
        private const string OpenMenuPath = MenuRoot + "Open Demo Scene";
        private const string PlayMenuPath = MenuRoot + "Play Existing Demo Scene";

        static MobaDemoSceneMenu()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            if (SessionState.GetBool(PendingLaunchKey, false))
                IssueLocalMobaRequest();
        }

        [MenuItem(OpenMenuPath, priority = 10)]
        private static void OpenDemoScene()
        {
            TryOpenExistingDemoScene();
        }

        /// <summary>
        /// 非交互批处理兼容入口：重建两套示例装配并确认 MOBA 场景存在。
        /// </summary>
        public static void CreateOrRefreshDemoSceneBatch()
        {
            DemoGameplayCompositionBuilder.GenerateAll();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameplayScenePath) == null)
            {
                throw new InvalidOperationException("Unable to create or refresh the MOBA gameplay scene.");
            }
        }

        [MenuItem(PlayMenuPath, priority = 13)]
        private static void PlayExistingDemoScene()
        {
            if (TryOpenExistingDemoScene()) LaunchLocalDemo();
        }

        [MenuItem(OpenMenuPath, validate = true)]
        [MenuItem(PlayMenuPath, validate = true)]
        private static bool CanOpenDemoScene() => !EditorApplication.isPlayingOrWillChangePlaymode;

        internal static bool TryOpenExistingDemoScene()
        {
            if (!CanOpenDemoScene())
            {
                return false;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameplayScenePath) == null)
            {
                Debug.LogError($"MOBA 场景不存在：{GameplayScenePath}。如需生成，请显式执行 {DemoGameplayCompositionBuilder.MenuPath}。");
                return false;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return false;
            }

            var scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                throw new InvalidOperationException($"Unable to open MOBA gameplay scene '{GameplayScenePath}'.");
            }

            return true;
        }

        internal static void LaunchLocalDemo()
        {
            ScheduleLocalDemoLaunch();
            EditorApplication.EnterPlaymode();
        }

        internal static void ScheduleLocalDemoLaunch()
        {
            SessionState.SetBool(PendingLaunchKey, true);
            IssueLocalMobaRequest();
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

    }
}
