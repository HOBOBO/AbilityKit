#if UNITY_EDITOR
using UnityEditor;

namespace Emilia.Kit
{
    public static class TempFolderKit
    {
        public const string TempFolderPath = "Assets/Temp";

        public static void CreateTempFolder()
        {
            if (AssetDatabase.IsValidFolder(TempFolderPath) == false) AssetDatabase.CreateFolder("Assets", "Temp");
        }

        [InitializeOnLoadMethod]
        static void QuitEventRegistration()
        {
            EditorApplication.wantsToQuit -= OnQuit;
            EditorApplication.wantsToQuit += OnQuit;
        }

        private static bool OnQuit()
        {
            AssetDatabase.DeleteAsset(TempFolderPath);
            return true;
        }
    }
}
#endif