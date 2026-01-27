#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Emilia.Kit
{
    public static class ValueDropdownListUtility
    {
        public static ValueDropdownBuilder<T, T> GetScriptableObject<T>() where T : ScriptableObject
            => ValueDropdownListFactory.ScriptableObject<T, T>().WithSelector(x => x);

        public static ValueDropdownBuilder<T, T> GetScriptableObject<T>(string path) where T : ScriptableObject
            => ValueDropdownListFactory.ScriptableObjectAtPath<T, T>(path).WithSelector(x => x);

        public static ValueDropdownBuilder<T, string> GetScriptableObjectName<T>() where T : ScriptableObject
            => ValueDropdownListFactory.ScriptableObject<T, string>().WithSelector(x => x.name);

        public static ValueDropdownBuilder<T, string> GetScriptableObjectName<T>(string path) where T : ScriptableObject
            => ValueDropdownListFactory.ScriptableObjectAtPath<T, string>(path).WithSelector(x => x.name);

        public static ValueDropdownBuilder<T, string> GetScriptableObjectPath<T>() where T : ScriptableObject
            => ValueDropdownListFactory.ScriptableObject<T, string>().WithSelector(AssetDatabase.GetAssetPath);

        public static ValueDropdownBuilder<T, string> GetScriptableObjectPathFilter<T>(string parent) where T : ScriptableObject
            => ValueDropdownListFactory.ScriptableObject<T, string>().WithSelector(x => EditorKit.RemovePathPrefix(AssetDatabase.GetAssetPath(x), parent));

        public static ValueDropdownBuilder<T, string> GetScriptableObjectPath<T>(string path) where T : ScriptableObject
            => ValueDropdownListFactory.ScriptableObjectAtPath<T, string>(path).WithSelector(AssetDatabase.GetAssetPath);

        public static ValueDropdownBuilder<T, string> GetScriptableObjectPathFilter<T>(string path, string parent) where T : ScriptableObject
            => ValueDropdownListFactory.ScriptableObjectAtPath<T, string>(path).WithSelector(x => EditorKit.RemovePathPrefix(AssetDatabase.GetAssetPath(x), parent));

        public static ValueDropdownBuilder<GameObject, GameObject> GetPrefab(string path)
            => ValueDropdownListFactory.Prefab<GameObject>(path).WithSelector(x => x);

        public static ValueDropdownBuilder<GameObject, string> GetPrefabName(string path)
            => ValueDropdownListFactory.Prefab<string>(path).WithSelector(x => x.name);

        public static ValueDropdownBuilder<GameObject, string> GetPrefabPath(string path)
            => ValueDropdownListFactory.Prefab<string>(path).WithSelector(AssetDatabase.GetAssetPath);

        public static ValueDropdownBuilder<GameObject, string> GetPrefabPath(string path, string parent)
            => ValueDropdownListFactory.Prefab<string>(path).WithSelector(x => EditorKit.RemovePathPrefix(AssetDatabase.GetAssetPath(x), parent));

        public static ValueDropdownBuilder<T, T> GetAsset<T>(string path, string searchPattern) where T : Object
            => ValueDropdownListFactory.Asset<T, T>(path, searchPattern).WithSelector(x => x);

        public static ValueDropdownBuilder<T, string> GetAssetName<T>(string path, string searchPattern) where T : Object
            => ValueDropdownListFactory.Asset<T, string>(path, searchPattern).WithSelector(x => x.name);

        public static ValueDropdownBuilder<T, string> GetAssetPath<T>(string path, string searchPattern) where T : Object
            => ValueDropdownListFactory.Asset<T, string>(path, searchPattern).WithSelector(AssetDatabase.GetAssetPath);

        public static ValueDropdownBuilder<T, string> GetAssetPathFilter<T>(string path, string searchPattern, string parent) where T : Object
            => ValueDropdownListFactory.Asset<T, string>(path, searchPattern).WithSelector(x => EditorKit.RemovePathPrefix(AssetDatabase.GetAssetPath(x), parent));
    }
}
#endif