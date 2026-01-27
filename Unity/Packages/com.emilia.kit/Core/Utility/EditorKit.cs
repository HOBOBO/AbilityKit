#if UNITY_EDITOR
using System;
using System.Text.RegularExpressions;
using Emilia.Reflection.Editor;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Emilia.Kit
{
    public static class EditorKit
    {
        public static void UnityInvoke(Action action)
        {
            bool waitFrameEnd = false;

            EditorApplication.update += Invoke;

            void Invoke()
            {
                if (waitFrameEnd == false)
                {
                    waitFrameEnd = true;
                    return;
                }

                action?.Invoke();
                EditorApplication.update -= Invoke;
            }
        }

        public static void SetSelection(object target, string disposeName = null)
        {
            if (string.IsNullOrEmpty(disposeName)) disposeName = target.ToString();
            SelectionContainer selectionContainer = ScriptableObject.CreateInstance<SelectionContainer>();
            selectionContainer.target = target;
            selectionContainer.displayName = disposeName;
            Selection.activeObject = selectionContainer;
        }

        public static string GetNumberAlpha(string source)
        {
            if (string.IsNullOrEmpty(source)) return null;
            string pattern = "[A-Za-z0-9_]";
            string strRet = "";
            MatchCollection results = Regex.Matches(source, pattern);
            foreach (Match v in results) strRet += v.ToString();
            return strRet;
        }

        public static string GetWholePath(Transform current, Transform target)
        {
            if (current.parent == null || current.parent == target) return current.name;
            return GetWholePath(current.parent, target) + "/" + current.name;
        }

        public static void ClearScene(Scene scene)
        {
            GameObject[] previewGameObjects = scene.GetRootGameObjects();
            int previewAmount = previewGameObjects.Length;
            for (int i = 0; i < previewAmount; i++)
            {
                GameObject previewGameObject = previewGameObjects[i];
                Object.DestroyImmediate(previewGameObject);
            }
        }

        public static void SetSceneDisplay(GameObject[] gameObjects, bool isDisplay, bool includeDescendants)
        {
            if (isDisplay) SceneVisibilityManager.instance.Show(gameObjects, includeDescendants);
            else SceneVisibilityManager.instance.Hide(gameObjects, includeDescendants);
        }

        public static void SetScenePicking(GameObject[] gameObjects, bool isEnable, bool includeDescendants)
        {
            if (isEnable) SceneVisibilityManager.instance.EnablePicking(gameObjects, includeDescendants);
            else SceneVisibilityManager.instance.DisablePicking(gameObjects, includeDescendants);
        }

        public static void SetSceneExpanded(GameObject[] gameObjects, bool expand, bool includeDescendants)
        {
            int count = gameObjects.Length;

            for (int i = 0; i < count; i++)
            {
                GameObject gameObject = gameObjects[i];
                if (gameObject == null) continue;

                int id = gameObject.GetInstanceID();

                if (includeDescendants) SceneHierarchyWindow_Internals.SetExpandedRecursive_Internals(id, expand);
                else SceneHierarchyWindow_Internals.SetExpanded_Internals(id, expand);
            }
        }

        public static Texture2D CaptureScreen(Rect rect)
        {
            int width = (int) rect.width;
            int height = (int) rect.height;

            Texture2D captureTexture = new Texture2D(width, height, TextureFormat.RGB24, false);

            Color[] pixels = InternalEditorUtility.ReadScreenPixel(rect.position, width, height);
            captureTexture.SetPixels(pixels);
            captureTexture.Apply();
            return captureTexture;
        }

        public static string PathToName(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            path = path.Replace("\\", "/");
            int index = path.LastIndexOf('/');
            if (index >= 0 && index < path.Length - 1) return path.Substring(index + 1);
            return path;
        }

        public static string PathToCategory(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            path = path.Replace("\\", "/");
            int index = path.LastIndexOf('/');
            if (index > 0) return path.Substring(0, index);
            return string.Empty;
        }

        public static void PathToNameAndCategory(string path, out string name, out string category)
        {
            if (path == null) path = string.Empty;
            path = path.Replace("\\", "/");
            int index = path.LastIndexOf('/');
            if (index >= 0)
            {
                name = index == path.Length - 1 ? string.Empty : path.Substring(index + 1);
                category = path.Substring(0, index + 1);
            }
            else
            {
                name = path;
                category = string.Empty;
            }
        }
        
        public static string RemovePathPrefix(string fullPath, string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) return fullPath;
            if (fullPath.StartsWith(prefix)) return fullPath.Substring(prefix.Length);
            return fullPath;
        }
        
        [HideMonoScript]
        private class SelectionContainer : TitleAsset
        {
            [HideInInspector, SerializeField]
            public string displayName;

            [NonSerialized, OdinSerialize, HideReferenceObjectPicker, HideLabel]
            public object target;

            public override string title => displayName;
        }
    }
}
#endif