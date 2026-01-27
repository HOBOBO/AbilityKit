#if UNITY_EDITOR
using System;

namespace Emilia.Kit
{
    [Serializable]
    public abstract class LocalSetting<T> where T : LocalSetting<T>, new()
    {
        private const string SettingKey = "##Emilia.Kit.LocalSetting";
        public static string key => $"{SettingKey}.{typeof(T).FullName}";

        private static T _instance;

        public static T instance
        {
            get
            {
                if (_instance != null) return _instance;

                if (OdinEditorPrefs.HasValue(key)) _instance = OdinEditorPrefs.GetValue<T>(key);
                
                if (_instance == null)
                {
                    _instance = new T();
                    OdinEditorPrefs.SetValue(key, _instance);
                }

                return _instance;
            }
        }

        public static void Save()
        {
            OdinEditorPrefs.SetValue(key, instance);
        }
    }
}
#endif