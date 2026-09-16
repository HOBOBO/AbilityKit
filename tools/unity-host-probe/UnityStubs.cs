// Minimal Unity API stubs so the C# compiler can FULLY bind hand-written Unity
// host code offline.
//
// Why stubs instead of "compile without Unity": when the Unity types are simply
// missing, every use of them is an error and the compiler gives up on the
// surrounding expressions - a real defect in resolvable code (`string[].Count`
// is CS0019) is silently swallowed. Giving the compiler just enough shape to
// finish binding makes those defects surface.
//
// These stubs exist to satisfy binding, NOT to validate Unity's API. A wrong
// Unity member name will still pass here; only a real Unity build catches that
// (see tools/check_unity_usings.js for the missing-using half of it).
// ReSharper disable All
#pragma warning disable CS0067, CS0649, CS0169

namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0f, 0f);
    }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height)
        { this.x = x; this.y = y; this.width = width; this.height = height; }
        public float xMax => x + width;
        public float yMax => y + height;
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; a = 1f; }
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
    }

    public class Object { }
    public class ScriptableObject : Object { }
    public class TextAsset : Object { public string text => string.Empty; }
    public class MonoScript : Object { }
    public static class Application { public static string dataPath => string.Empty; }
    public static class Debug
    {
        public static void Log(object message) { }
        public static void LogWarning(object message) { }
        public static void LogError(object message) { }
    }

    [System.AttributeUsage(System.AttributeTargets.Field)]
    public sealed class SerializeFieldAttribute : System.Attribute { }

    public class GUIContent
    {
        public static readonly GUIContent none = new GUIContent();
        public GUIContent() { }
        public GUIContent(string text) { }
    }

    public class GUIStyle { }

    public class GUISkin
    {
        public GUIStyle box => null;
        public GUIStyle horizontalSlider => null;
        public GUIStyle label => null;
        public GUIStyle miniLabel => null;
    }

    public class GUILayoutOption { }

    public static class GUILayout
    {
        public static GUILayoutOption Width(float v) => null;
        public static GUILayoutOption Height(float v) => null;
        public static GUILayoutOption MinHeight(float v) => null;
        public static GUILayoutOption MaxHeight(float v) => null;
        public static GUILayoutOption ExpandWidth(bool v) => null;
        public static bool Button(string text, params GUILayoutOption[] options) => false;
        public static bool Button(string text, GUIStyle style, params GUILayoutOption[] options) => false;
        public static void Space(float px) { }
        public static void Label(string text) { }
        public static void Label(string text, GUIStyle style) { }
        public static void BeginArea(Rect screenRect) { }
        public static void EndArea() { }
        public static void BeginHorizontal(params GUILayoutOption[] options) { }
        public static void EndHorizontal() { }
        public static void BeginVertical(params GUILayoutOption[] options) { }
        public static void EndVertical() { }
    }

    public static class GUILayoutUtility
    {
        public static Rect GetRect(float width, float height, params GUILayoutOption[] options) => default;
        public static Rect GetRect(float width, float height) => default;
    }

    public static class GUI
    {
        public static Color color { get; set; }
        public static bool enabled { get; set; }
        public static Color backgroundColor { get; set; }
        public static GUISkin skin => null;
        public static void Box(Rect position, string text) { }
        public static void Box(Rect position, GUIContent content, GUIStyle style) { }
        public static void Label(Rect position, string text, GUIStyle style) { }
        public static void Label(Rect position, string text) { }
        public static void BeginClip(Rect position) { }
        public static void EndClip() { }
        public static void FocusControl(string name) { }
    }

    public static class Mathf
    {
        public static int Clamp(int v, int min, int max) => v;
        public static float Clamp(float v, float min, float max) => v;
        public static float Max(float a, float b) => a;
        public static int Max(int a, int b) => a;
        public static float Min(float a, float b) => a;
        public static float Lerp(float a, float b, float t) => a;
        public static float Abs(float v) => v;
        public static int Abs(int v) => v;
    }
}

namespace UnityEngine.UIElements
{
    public class VisualElement { }
}

namespace UnityEditor
{
    using System;
    using UnityEngine;

    public enum MessageType { None, Info, Warning, Error }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MenuItemAttribute : Attribute
    {
        public MenuItemAttribute(string itemName) { }
        public MenuItemAttribute(string itemName, bool isValidateFunction) { }
        public MenuItemAttribute(string itemName, bool isValidateFunction, int priority) { }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class InitializeOnLoadAttribute : Attribute { }

    // Unity applies this to the ScriptableSingleton class, not a field.
    [AttributeUsage(AttributeTargets.All)]
    public sealed class FilePathAttribute : Attribute
    {
        public enum Location { Project, Preferences, ProjectFolder }

        public FilePathAttribute(string path, Location location) { }

        public Location location { get; set; }
    }

    public abstract class ScriptableSingleton<T> : UnityEngine.ScriptableObject
        where T : ScriptableSingleton<T>
    {
        public static T instance => null;
    }

    public class EditorWindow
    {
        public GUIContent titleContent { get; set; }
        public Vector2 minSize { get; set; }
        public static T GetWindow<T>() where T : EditorWindow => null;
        public void Show() { }
        public void Repaint() { }
    }

    public static class EditorStyles
    {
        public static GUIStyle boldLabel => null;
        public static GUIStyle miniLabel => null;
        public static GUIStyle miniButton => null;
        public static GUIStyle miniButtonLeft => null;
        public static GUIStyle miniButtonRight => null;
        public static GUIStyle toolbarSearchField => null;
        public static GUIStyle wordWrappedLabel => null;
        public static GUIStyle wordWrappedMiniLabel => null;
        public static GUIStyle centeredGreyMiniLabel => null;
        public static GUIStyle helpBox => null;
        public static GUIStyle textArea => null;
    }

    public static class EditorGUI
    {
        public static int indentLevel { get; set; }
        public static void DrawRect(Rect rect, Color color) { }
    }

    public static class EditorGUIUtility
    {
        public static bool isProSkin => true;
    }

    public static class EditorGUILayout
    {
        public sealed class ScrollViewScope : IDisposable
        {
            public Vector2 scrollPosition;
            public ScrollViewScope(Vector2 scrollPosition, params GUILayoutOption[] options)
            { this.scrollPosition = scrollPosition; }
            public void Dispose() { }
        }

        public static void LabelField(string label) { }
        public static void LabelField(string label, GUIStyle style) { }
        public static void LabelField(string label, GUIStyle style, params GUILayoutOption[] options) { }
        public static void LabelField(string label, params GUILayoutOption[] options) { }
        public static void LabelField(string label, string value) { }
        public static void LabelField(string label, string value, GUIStyle style) { }
        public static void LabelField(string label, string value, GUIStyle style, params GUILayoutOption[] options) { }
        public static void Space() { }
        public static void Space(float px) { }
        public static void HelpBox(string message, MessageType type) { }
        public static bool Foldout(bool foldout, string content, bool toggleOnLabelClick) => foldout;
        public static int IntSlider(int value, int left, int right) => value;
        public static string TextField(string text, GUIStyle style) => text;
        public static string TextArea(string text, GUIStyle style, params GUILayoutOption[] options) => text;
        public static void BeginHorizontal(params GUILayoutOption[] options) { }
        public static void BeginHorizontal(GUIStyle style, params GUILayoutOption[] options) { }
        public static void EndHorizontal() { }
        public static void BeginVertical(params GUILayoutOption[] options) { }
        public static void BeginVertical(GUIStyle style, params GUILayoutOption[] options) { }
        public static void EndVertical() { }
    }

    public static class AssetDatabase
    {
        public static string[] FindAssets(string filter, string[] searchInFolders) => Array.Empty<string>();
        public static string GUIDToAssetPath(string guid) => string.Empty;
        public static T LoadAssetAtPath<T>(string assetPath) where T : UnityEngine.Object => null;
        public static bool OpenAsset(UnityEngine.Object target, int lineNumber) => false;
    }
}
