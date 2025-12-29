using NBC.ActionEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace ActionEditorExample.Editor
{
    public class Initializer
    {
        [InitializeOnLoadMethod]
        private static void OnInitialize()
        {
            App.OnInitialize -= ActionSequencerInit;
            App.OnInitialize += ActionSequencerInit;

            App.OnOpenAsset -= OpenAssetData;
            App.OnOpenAsset += OpenAssetData;

            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void ActionSequencerInit()
        {
            TryChangeScene();
        }

        static void OnEditorUpdate()
        {
        }

        private static void OpenAssetData(Asset asset)
        {
            //打开资源做的处理
        }

        private static string EditScenePath = "Assets/Scenes/SampleScene.unity";

        /// <summary>
        /// 显示界面时，尝试切换场景
        /// </summary>
        static void TryChangeScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditScenePath != scene.path)
            {
                EditorSceneManager.OpenScene(EditScenePath);
            }
        }
    }
}