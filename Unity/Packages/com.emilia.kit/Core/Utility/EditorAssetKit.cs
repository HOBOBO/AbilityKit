#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Emilia.Kit
{
    public static class EditorAssetKit
    {
        public static string dataParentPath => Directory.GetParent(Application.dataPath).ToString();

        public static T[] GetEditorResources<T>() where T : Object
        {
            string typeSearchString = $" t:{typeof(T).Name}";
            string[] guids = AssetDatabase.FindAssets(typeSearchString);
            T[] preferences = new T[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                preferences[i] = AssetDatabase.LoadAssetAtPath<T>(path);
            }
            return preferences;
        }

        public static Object[] GetEditorResources(Type type)
        {
            string typeSearchString = $" t:{type.Name}";
            string[] guids = AssetDatabase.FindAssets(typeSearchString);
            Object[] preferences = new Object[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                preferences[i] = AssetDatabase.LoadAssetAtPath<Object>(path);
            }
            return preferences;
        }

        public static List<T> GetEditorResources<T>(string pathFilter) where T : Object
        {
            string typeSearchString = $" t:{typeof(T).Name}";
            string[] guids = AssetDatabase.FindAssets(typeSearchString);
            List<T> preferences = new List<T>();
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.Contains(pathFilter) == false) continue;
                preferences.Add(AssetDatabase.LoadAssetAtPath<T>(path));
            }
            return preferences;
        }
        
        public static List<T> LoadAssetAtPath<T>(string path) where T : ScriptableObject
        {
            List<T> loads = new List<T>();

            string dataPath = $"{dataParentPath}/";
            string fullPath = $"{dataPath}{path}";
            if (Directory.Exists(fullPath) == false) return loads;
            string[] files = Directory.GetFiles(fullPath, "*.asset", SearchOption.AllDirectories);
            int lenght = files.Length;
            for (int i = 0; i < lenght; i++)
            {
                string filePath = files[i];
                string unityPath = filePath.Replace(dataPath, "");
                T asset = AssetDatabase.LoadAssetAtPath<T>(unityPath);
                loads.Add(asset);
            }

            return loads;
        }
        
        public static List<T> LoadAtPath<T>(string path, string searchPattern) where T : Object
        {
            List<T> loads = new List<T>();

            string dataPath = $"{dataParentPath}/";
            string fullPath = $"{dataPath}{path}";
            if (Directory.Exists(fullPath) == false) return loads;
            string[] files = Directory.GetFiles(fullPath, searchPattern, SearchOption.AllDirectories);
            int lenght = files.Length;
            for (int i = 0; i < lenght; i++)
            {
                string filePath = files[i];
                string unityPath = filePath.Replace(dataPath, "");
                T asset = AssetDatabase.LoadAssetAtPath<T>(unityPath);
                loads.Add(asset);
            }

            return loads;
        }

        public static void SaveAssetIntoObject(Object childAsset, Object masterAsset)
        {
            if (childAsset == null || masterAsset == null) return;

            if ((masterAsset.hideFlags & HideFlags.DontSave) != 0) childAsset.hideFlags |= HideFlags.DontSave;
            else
            {
                childAsset.hideFlags |= HideFlags.HideInHierarchy;
                if (! AssetDatabase.Contains(childAsset) && AssetDatabase.Contains(masterAsset)) AssetDatabase.AddObjectToAsset(childAsset, masterAsset);
            }
        }

        public static T CreateAsset<T>(string path) where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static bool DeleteAsset(Object target)
        {
            if (target == null) return false;
            string path = AssetDatabase.GetAssetPath(target);
            return AssetDatabase.DeleteAsset(path);
        }

        public static GameObject GetPrefabAsset(GameObject gameObject)
        {
            if (gameObject == null) return null;
            if (PrefabUtility.IsPartOfPrefabAsset(gameObject)) return gameObject;
            if (PrefabUtility.IsPartOfPrefabInstance(gameObject)) return PrefabUtility.GetCorrespondingObjectFromOriginalSource(gameObject);
            PrefabStage prefabStage = PrefabStageUtility.GetPrefabStage(gameObject);
            if (prefabStage != null) return AssetDatabase.LoadAssetAtPath<GameObject>(prefabStage.assetPath);
            return null;
        }
        
        public static void Save(this Object target, bool isRefresh = false)
        {
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
            if (isRefresh) AssetDatabase.Refresh();
        }
    }
}
#endif