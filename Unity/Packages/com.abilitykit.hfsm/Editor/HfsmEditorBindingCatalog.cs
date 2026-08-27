using System;
using System.Reflection;
using AbilityKit.HFSM;
using UnityEditor;
using UnityEngine;

namespace UnityHFSM.Editor
{
    /// <summary>Editor-owned pull catalog. Runtime simulation never depends on this scan.</summary>
    public static class HfsmEditorBindingCatalog
    {
        private const string LegacyCatalogAssetGuidPreference = "AbilityKit.HFSM.BindingCatalogGuid";
        private static HfsmBindingCatalog _catalog;
        private static HfsmBindingCatalogAsset _catalogAsset;

        public static HfsmBindingCatalogAsset ConfiguredAsset
        {
            get
            {
                if (_catalogAsset != null)
                    return _catalogAsset;

                var guid = HfsmEditorProjectSettings.instance.CatalogAssetGuid;
                if (string.IsNullOrEmpty(guid) && EditorPrefs.HasKey(LegacyCatalogAssetGuidPreference))
                {
                    guid = EditorPrefs.GetString(LegacyCatalogAssetGuidPreference, string.Empty);
                    HfsmEditorProjectSettings.instance.SetCatalogAssetGuid(guid);
                    EditorPrefs.DeleteKey(LegacyCatalogAssetGuidPreference);
                }
                if (string.IsNullOrEmpty(guid))
                    return null;

                var path = AssetDatabase.GUIDToAssetPath(guid);
                _catalogAsset = string.IsNullOrEmpty(path)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<HfsmBindingCatalogAsset>(path);
                return _catalogAsset;
            }
        }

        public static HfsmBindingCatalog Catalog
        {
            get
            {
                if (_catalog == null)
                    _catalog = ConfiguredAsset == null
                        ? BuildReflectionCatalog()
                        : ConfiguredAsset.BuildCatalog();
                return _catalog;
            }
        }

        public static void SetConfiguredAsset(HfsmBindingCatalogAsset asset)
        {
            _catalogAsset = asset;
            _catalog = null;
            if (asset == null)
            {
                HfsmEditorProjectSettings.instance.SetCatalogAssetGuid(string.Empty);
                return;
            }

            var path = AssetDatabase.GetAssetPath(asset);
            var guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
                HfsmEditorProjectSettings.instance.SetCatalogAssetGuid(string.Empty);
            else
                HfsmEditorProjectSettings.instance.SetCatalogAssetGuid(guid);
        }

        [MenuItem("Assets/AbilityKit/HFSM/Use Selected Binding Catalog", true)]
        private static bool ValidateUseSelectedAsset()
        {
            return Selection.activeObject is HfsmBindingCatalogAsset;
        }

        [MenuItem("Assets/AbilityKit/HFSM/Use Selected Binding Catalog")]
        private static void UseSelectedAsset()
        {
            SetConfiguredAsset(Selection.activeObject as HfsmBindingCatalogAsset);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Assets/AbilityKit/HFSM/Clear Configured Binding Catalog")]
        private static void ClearConfiguredAsset()
        {
            SetConfiguredAsset(null);
        }

        public static void Reset()
        {
            _catalog = null;
        }

        private static HfsmBindingCatalog BuildReflectionCatalog()
        {
            var catalog = new HfsmBindingCatalog();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    catalog.ScanAssembly(assembly);
                }
                catch (ReflectionTypeLoadException)
                {
                    // Optional editor assemblies with unavailable types do not invalidate other catalogs.
                }
            }

            return catalog;
        }
    }

    [FilePath("ProjectSettings/AbilityKitHfsmSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class HfsmEditorProjectSettings : ScriptableSingleton<HfsmEditorProjectSettings>
    {
        [SerializeField] private string catalogAssetGuid = string.Empty;

        public string CatalogAssetGuid => catalogAssetGuid;

        public void SetCatalogAssetGuid(string value)
        {
            value = value ?? string.Empty;
            if (catalogAssetGuid == value) return;
            catalogAssetGuid = value;
            Save(true);
        }
    }
}
