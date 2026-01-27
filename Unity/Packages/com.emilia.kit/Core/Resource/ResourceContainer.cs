#if UNITY_EDITOR
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace Emilia.Kit
{
    [CreateAssetMenu(fileName = "ResourceContainer", menuName = "Emilia/Kit/ResourceContainer")]
    public class ResourceContainer : ScriptableObject
    {
        [LabelText("资源文件夹列表")]
        public List<ResourceFolder> resourceFolders = new List<ResourceFolder>();

        private static ResourceContainer[] _instances;

        public static ResourceContainer[] instances
        {
            get
            {
                if (_instances != default) return _instances;

                string typeSearchString = $"t:{nameof(ResourceContainer)}";
                string[] guids = AssetDatabase.FindAssets(typeSearchString);

                ResourceContainer[] findInstances = new ResourceContainer[guids.Length];
                for (int i = 0; i < guids.Length; i++)
                {
                    string guid = guids[i];
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    findInstances[i] = AssetDatabase.LoadAssetAtPath<ResourceContainer>(path);
                }

                _instances = findInstances;
                return _instances;
            }
        }
    }
}
#endif