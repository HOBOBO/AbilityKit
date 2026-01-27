#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Emilia.Kit
{
    public static class ResourceUtility
    {
        private static Dictionary<string, Object> loadCache = new Dictionary<string, Object>();

        /// <summary>
        /// 加载资源
        /// </summary>
        public static T LoadResource<T>(string path) where T : Object
        {
            if (loadCache.TryGetValue(path, out Object value)) return value as T;

            int containerAmount = ResourceContainer.instances.Length;
            for (int i = 0; i < containerAmount; i++)
            {
                ResourceContainer resourceContainer = ResourceContainer.instances[i];
                int amount = resourceContainer.resourceFolders.Count;
                for (int j = 0; j < amount; j++)
                {
                    ResourceFolder resourceFolder = resourceContainer.resourceFolders[j];

                    string filterPath = path;
                    if (string.IsNullOrEmpty(resourceFolder.pathFilter) == false) filterPath = path.Replace($"{resourceFolder.pathFilter}/", "");
                   
                    string fullPath = $"{resourceFolder.folderAsset.unityPath}/{filterPath}";
                    T resource = AssetDatabase.LoadAssetAtPath<T>(fullPath);
                    if (resource == default) continue;
                    loadCache[path] = resource;
                    return resource;
                }
            }

            return null;
        }
    }
}
#endif