#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Emilia.Kit
{
    public static class OdinMenuUtility
    {
        public static OdinMenuBuilder<T, T> GetScriptableObject<T>() where T : ScriptableObject
            => OdinMenuFactory.ScriptableObject<T, T>().WithSelector(x => x);

        public static OdinMenuBuilder<T, T> GetScriptableObject<T>(string path) where T : ScriptableObject
            => OdinMenuFactory.ScriptableObjectAtPath<T, T>(path).WithSelector(x => x);

        public static OdinMenuBuilder<T, string> GetScriptableObjectName<T>() where T : ScriptableObject
            => OdinMenuFactory.ScriptableObject<T, string>().WithSelector(x => x.name);

        public static OdinMenuBuilder<T, string> GetScriptableObjectName<T>(string path) where T : ScriptableObject
            => OdinMenuFactory.ScriptableObjectAtPath<T, string>(path).WithSelector(x => x.name);

        public static OdinMenuBuilder<T, string> GetScriptableObjectPath<T>() where T : ScriptableObject
            => OdinMenuFactory.ScriptableObject<T, string>().WithSelector(AssetDatabase.GetAssetPath);

        public static OdinMenuBuilder<T, string> GetScriptableObjectPathFilter<T>(string parent) where T : ScriptableObject
            => OdinMenuFactory.ScriptableObject<T, string>().WithSelector(x => EditorKit.RemovePathPrefix(AssetDatabase.GetAssetPath(x), parent));

        public static OdinMenuBuilder<T, string> GetScriptableObjectPath<T>(string path) where T : ScriptableObject
            => OdinMenuFactory.ScriptableObjectAtPath<T, string>(path).WithSelector(AssetDatabase.GetAssetPath);

        public static OdinMenuBuilder<T, string> GetScriptableObjectPath<T>(string path, string parent) where T : ScriptableObject
            => OdinMenuFactory.ScriptableObjectAtPath<T, string>(path).WithSelector(x => EditorKit.RemovePathPrefix(AssetDatabase.GetAssetPath(x), parent));

        public static OdinMenuBuilder<GameObject, GameObject> GetPrefab(string path)
            => OdinMenuFactory.Prefab<GameObject>(path).WithSelector(x => x);

        public static OdinMenuBuilder<GameObject, string> GetPrefabName(string path)
            => OdinMenuFactory.Prefab<string>(path).WithSelector(x => x.name);

        public static OdinMenuBuilder<GameObject, string> GetPrefabPath(string path)
            => OdinMenuFactory.Prefab<string>(path).WithSelector(AssetDatabase.GetAssetPath);

        public static OdinMenuBuilder<GameObject, string> GetPrefabPath(string path, string parent)
            => OdinMenuFactory.Prefab<string>(path).WithSelector(x => EditorKit.RemovePathPrefix(AssetDatabase.GetAssetPath(x), parent));

        public static OdinMenuBuilder<T, T> GetAsset<T>(string path, string searchPattern) where T : Object
            => OdinMenuFactory.Asset<T, T>(path, searchPattern).WithSelector(x => x);

        public static OdinMenuBuilder<T, string> GetAssetName<T>(string path, string searchPattern) where T : Object
            => OdinMenuFactory.Asset<T, string>(path, searchPattern).WithSelector(x => x.name);

        public static OdinMenuBuilder<T, string> GetAssetPath<T>(string path, string searchPattern) where T : Object
            => OdinMenuFactory.Asset<T, string>(path, searchPattern).WithSelector(AssetDatabase.GetAssetPath);

        public static OdinMenuBuilder<T, string> GetAssetPathFilter<T>(string path, string searchPattern, string parent) where T : Object
            => OdinMenuFactory.Asset<T, string>(path, searchPattern).WithSelector(x => EditorKit.RemovePathPrefix(AssetDatabase.GetAssetPath(x), parent));
    }
}
#endif