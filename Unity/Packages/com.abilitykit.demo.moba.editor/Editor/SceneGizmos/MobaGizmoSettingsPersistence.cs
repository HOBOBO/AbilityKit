using UnityEditor;

namespace AbilityKit.Game.Editor.Gizmos
{
    /// <summary>
    /// 用 EditorPrefs 持久化 MOBA Gizmo 设置，避免 Domain Reload 后丢失。
    /// 仅做最小数据序列化（int mask），不做版本号管理——后续若需要兼容旧值，
    /// 再扩展为版本化结构即可。
    /// </summary>
    internal static class MobaGizmoSettingsPersistence
    {
        private const string EnabledMaskKey = "AbilityKit.Game.Editor.Gizmos.EnabledMask.v1";
        private const string CurrentEnabledMaskKey = "AbilityKit.Game.Editor.Gizmos.EnabledMask.v2";

        public static int LoadEnabledMask()
        {
            return EditorPrefs.GetInt(CurrentEnabledMaskKey, EditorPrefs.GetInt(EnabledMaskKey, 0));
        }

        public static bool HasEnabledMask() =>
            EditorPrefs.HasKey(CurrentEnabledMaskKey) || EditorPrefs.HasKey(EnabledMaskKey);

        public static void SaveEnabledMask(int mask)
        {
            EditorPrefs.SetInt(CurrentEnabledMaskKey, mask);
        }

        public static void EnsureDefaults(int defaultMask, int addedDefaultMask)
        {
            if (!EditorPrefs.HasKey(CurrentEnabledMaskKey))
            {
                var legacy = EditorPrefs.GetInt(EnabledMaskKey, 0);
                EditorPrefs.SetInt(CurrentEnabledMaskKey, legacy == 0 ? defaultMask : legacy | addedDefaultMask);
            }
        }

        public static void Clear()
        {
            EditorPrefs.DeleteKey(EnabledMaskKey);
            EditorPrefs.DeleteKey(CurrentEnabledMaskKey);
        }
    }
}
