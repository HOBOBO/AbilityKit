using System.IO;
using AbilityKit.Game.Flow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbilityKit.Demo.Moba.Editor
{
    /// <summary>
    /// 头顶面板正式 prefab 的唯一结构生成入口，消费运行时强类型绑定。
    /// </summary>
    public static class MobaOverheadPrefabBuilder
    {
        private const string Path = "Packages/com.abilitykit.demo.moba.view.runtime/Resources/ui/battle/hud_overhead.prefab";
        private const string MenuPath = "Tools/AbilityKit/Demos/Moba/HUD/预检并重建头顶 HUD Prefab";

        [MenuItem(MenuPath, true)]
        private static bool CanPreviewAndBuild() =>
            !EditorApplication.isPlayingOrWillChangePlaymode &&
            !EditorApplication.isCompiling && !EditorApplication.isUpdating &&
            PrefabStageUtility.GetCurrentPrefabStage() == null;

        [MenuItem(MenuPath)]
        private static void PreviewAndBuild()
        {
            if (!CanPreviewAndBuild()) return;

            var existing = AssetDatabase.LoadMainAssetAtPath(Path);
            if ((existing != null && (existing is not GameObject || EditorUtility.IsDirty(existing))) ||
                (existing == null && (File.Exists(Path) || Directory.Exists(Path))))
            {
                EditorUtility.DisplayDialog("无法重建头顶 HUD Prefab",
                    "目标有未保存修改，或不是可覆盖的 Prefab。请先处理目标：\n" + Path, "关闭");
                return;
            }

            var action = existing == null ? "新建" : "覆盖";
            if (EditorUtility.DisplayDialog("重建头顶 HUD Prefab",
                    $"将按默认结构{action}：\n{Path}\n\n不会保留目标上的手工结构修改。保存可能同时写入其他未保存资产；不会发布 Luban 配置。",
                    "确认重建", "取消"))
                Build();
        }

        /// <summary>
        /// 从确认后的编辑器菜单或 Unity executeMethod 直接生成正式 Prefab；批处理不弹窗。
        /// </summary>
        public static void Build()
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            var view = BattleHudOverheadView.CreateDefault(null);
            try
            {
                if (!view.IsValid) throw new System.InvalidOperationException("Invalid overhead view structure.");
                PrefabUtility.SaveAsPrefabAsset(view.gameObject, Path, out var saved);
                if (!saved) throw new IOException("无法保存头顶 HUD Prefab：" + Path);
                AssetDatabase.SaveAssets();
                Debug.Log("[OverheadHud] Published typed prefab: " + Path);
            }
            finally { Object.DestroyImmediate(view.gameObject); }
        }
    }
}
