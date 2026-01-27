#if UNITY_EDITOR
using UnityEditor;

namespace Emilia.Kit
{
    public static class OdinEditorPrefs
    {
        public static T GetValue<T>(string key, T defaultValue = default)
        {
            string byteString = EditorPrefs.GetString(key);
            if (string.IsNullOrEmpty(byteString)) return defaultValue;
            return OdinSerializableUtility.FromByteString<T>(byteString);
        }

        public static void SetValue<T>(string key, T value)
        {
            string byteString = OdinSerializableUtility.ToByteString(value);
            EditorPrefs.SetString(key, byteString);
        }

        public static bool HasValue(string key) => EditorPrefs.HasKey(key);
    }
}
#endif