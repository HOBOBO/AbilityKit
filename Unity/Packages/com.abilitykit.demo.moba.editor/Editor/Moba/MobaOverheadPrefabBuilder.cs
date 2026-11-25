using System.IO;
using AbilityKit.Game.Flow;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Demo.Moba.Editor
{
    /// <summary>
    /// 头顶面板正式 prefab 的唯一结构生成入口，消费运行时强类型绑定。
    /// </summary>
    public static class MobaOverheadPrefabBuilder
    {
        private const string Path = "Packages/com.abilitykit.demo.moba.view.runtime/Resources/ui/battle/hud_overhead.prefab";

        /// <summary>
        /// 可在菜单或 Unity executeMethod 中调用，生成后核对必要引用。
        /// </summary>
        [MenuItem("AbilityKit/MOBA/Build Overhead HUD Prefab")]
        public static void Build()
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            var view = BattleHudOverheadView.CreateDefault(null);
            try
            {
                if (!view.IsValid) throw new System.InvalidOperationException("Invalid overhead view structure.");
                PrefabUtility.SaveAsPrefabAsset(view.gameObject, Path);
                AssetDatabase.SaveAssets();
                Debug.Log("[OverheadHud] Published typed prefab: " + Path);
            }
            finally { Object.DestroyImmediate(view.gameObject); }
        }
    }
}
