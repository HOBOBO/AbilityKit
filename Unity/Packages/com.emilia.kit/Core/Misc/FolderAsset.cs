#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Emilia.Kit.Editor
{
    [Serializable]
    public class FolderAsset
    {
        [SerializeField]
        private DefaultAsset _folder;

        public DefaultAsset folder
        {
            get
            {
                if (this._folder == null)
                {
                    if (string.IsNullOrEmpty(this.folderPath)) return null;
                    this._folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(this.folderPath);
                }
                if (this._folder != null) this.folderPath = AssetDatabase.GetAssetPath(this._folder);
                return _folder;
            }

            set
            {
                _folder = value;
                if (this._folder != null) this.folderPath = AssetDatabase.GetAssetPath(this._folder);
            }
        }

        [SerializeField]
        private string folderPath;

        public string unityPath
        {
            get
            {
                if (folder == null)
                {
                    Debug.LogError("FolderAsset is null");
                    return string.Empty;
                }

                return AssetDatabase.GetAssetPath(folder);
            }
        }

        public string fullPath => Path.GetFullPath(unityPath);

        public override string ToString()
        {
            return unityPath;
        }

        public string[] GetFileFullPaths(string searchPattern, SearchOption searchOption = SearchOption.AllDirectories)
        {
            return Directory.GetFiles(fullPath, searchPattern, searchOption);
        }

        public string[] GetFileUnityPaths(string searchPattern, SearchOption searchOption = SearchOption.AllDirectories)
        {
            string[] fullPaths = GetFileFullPaths(searchPattern, searchOption);
            string parentPath = Application.dataPath;
            string[] unityPaths = new string[fullPaths.Length];
            for (int i = 0; i < fullPaths.Length; i++)
            {
                string full = fullPaths[i];
                string unity = full.Replace("\\", "/");
                unity = unity.Replace(parentPath, "Assets");
                unityPaths[i] = unity;
            }
            return unityPaths;
        }

        public T[] GetAssets<T>(string searchPattern, SearchOption searchOption = SearchOption.AllDirectories) where T : Object
        {
            string[] unityPaths = GetFileUnityPaths(searchPattern, searchOption);
            T[] assets = new T[unityPaths.Length];
            for (int i = 0; i < unityPaths.Length; i++)
            {
                string path = unityPaths[i];
                assets[i] = AssetDatabase.LoadAssetAtPath<T>(path);
            }
            return assets;
        }
    }
}
#endif