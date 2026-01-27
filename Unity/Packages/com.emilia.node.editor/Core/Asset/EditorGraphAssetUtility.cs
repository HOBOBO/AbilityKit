using Emilia.Kit;
using UnityEditor;
using UnityEngine;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 编辑器Graph资产的实用函数
    /// </summary>
    public static class EditorGraphAssetUtility
    {
        /// <summary>
        /// 为指定的资源绑定EditorGraphAsset
        /// </summary>
        public static T CreateAsAttached<T>(string assetPath) where T : EditorGraphAsset
        {
            Object masterAsset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (masterAsset == null)
            {
                Debug.LogError($"Master asset not found at path: {assetPath}");
                return null;
            }

            T asset = ScriptableObject.CreateInstance<T>();
            EditorAssetKit.SaveAssetIntoObject(asset, masterAsset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        /// <summary>
        /// 创建EditorGraphAsset
        /// </summary>
        public static T Create<T>(string savePath) where T : EditorGraphAsset
        {
            T asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, savePath);
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}