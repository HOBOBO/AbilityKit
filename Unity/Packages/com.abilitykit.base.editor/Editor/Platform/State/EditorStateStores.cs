#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Editor.Platform.State
{
    public interface IEditorUserStateStore
    {
        bool HasKey(string key);
        string GetString(string key, string defaultValue = "");
        void SetString(string key, string value);
        int GetInt(string key, int defaultValue = 0);
        void SetInt(string key, int value);
        float GetFloat(string key, float defaultValue = 0f);
        void SetFloat(string key, float value);
        bool GetBool(string key, bool defaultValue = false);
        void SetBool(string key, bool value);
        void DeleteKey(string key);
    }

    /// <summary>
    /// Stores machine-local editor preferences under a mandatory module namespace.
    /// Shared team settings must use project settings instead.
    /// </summary>
    public sealed class EditorPrefsUserStateStore : IEditorUserStateStore
    {
        private const string Prefix = "AbilityKit.Editor.Platform.";
        private readonly string _scope;

        public EditorPrefsUserStateStore(string moduleId, string scope = "default")
        {
            _scope = Sanitize(moduleId, nameof(moduleId)) + "." + Sanitize(scope, nameof(scope)) + ".";
        }

        public bool HasKey(string key) => EditorPrefs.HasKey(Resolve(key));
        public string GetString(string key, string defaultValue = "") => EditorPrefs.GetString(Resolve(key), defaultValue);
        public void SetString(string key, string value) => EditorPrefs.SetString(Resolve(key), value ?? string.Empty);
        public int GetInt(string key, int defaultValue = 0) => EditorPrefs.GetInt(Resolve(key), defaultValue);
        public void SetInt(string key, int value) => EditorPrefs.SetInt(Resolve(key), value);
        public float GetFloat(string key, float defaultValue = 0f) => EditorPrefs.GetFloat(Resolve(key), defaultValue);
        public void SetFloat(string key, float value) => EditorPrefs.SetFloat(Resolve(key), value);
        public bool GetBool(string key, bool defaultValue = false) => EditorPrefs.GetBool(Resolve(key), defaultValue);
        public void SetBool(string key, bool value) => EditorPrefs.SetBool(Resolve(key), value);
        public void DeleteKey(string key) => EditorPrefs.DeleteKey(Resolve(key));

        private string Resolve(string key)
        {
            return Prefix + _scope + Sanitize(key, nameof(key));
        }

        private static string Sanitize(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A non-empty value is required.", parameterName);
            }

            return value.Trim();
        }
    }

    [FilePath("ProjectSettings/AbilityKitEditorPlatformSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class EditorPlatformProjectSettings : ScriptableSingleton<EditorPlatformProjectSettings>
    {
        [SerializeField] private string defaultLanguage = "en";

        public string DefaultLanguage
        {
            get => string.IsNullOrWhiteSpace(defaultLanguage) ? "en" : defaultLanguage;
            set
            {
                var next = string.IsNullOrWhiteSpace(value) ? "en" : value.Trim();
                if (string.Equals(defaultLanguage, next, StringComparison.OrdinalIgnoreCase)) return;
                defaultLanguage = next;
                Save(true);
            }
        }
    }
}
#endif
