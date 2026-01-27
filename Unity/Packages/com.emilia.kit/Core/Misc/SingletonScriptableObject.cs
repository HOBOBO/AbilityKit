#if UNITY_EDITOR
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Emilia.Kit
{
    public class SingletonScriptableObject<T> : SerializedScriptableObject where T : SingletonScriptableObject<T>
    {
        private static bool isGet;

        private static T _instance;

        public static T instance
        {
            get
            {
                if (_instance != null) return _instance;
                if (isGet) return _instance;

                T[] preferences = EditorAssetKit.GetEditorResources<T>();
                isGet = true;

                if (preferences.Length == 0) return _instance;
                if (preferences.Length > 1) Debug.LogWarning("单例资源文件存在多个");
                _instance = preferences.FirstOrDefault();
                return _instance;
            }
        }
    }
}
#endif