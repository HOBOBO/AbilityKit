using System;
using System.Reflection;
using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM.Runtime;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.HFSM.Editor
{
    /// <summary>Editor-owned pull catalog. Runtime simulation never depends on this scan.</summary>
    public static class EditorBindingCatalog
    {
        private const string LegacyCatalogAssetGuidPreference = "AbilityKit.HFSM.BindingCatalogGuid";
        private static BindingCatalog _catalog;
        private static BindingCatalogAsset _catalogAsset;

        public static BindingCatalogAsset ConfiguredAsset
        {
            get
            {
                if (_catalogAsset != null)
                    return _catalogAsset;

                var guid = EditorProjectSettings.instance.CatalogAssetGuid;
                if (string.IsNullOrEmpty(guid) && EditorPrefs.HasKey(LegacyCatalogAssetGuidPreference))
                {
                    guid = EditorPrefs.GetString(LegacyCatalogAssetGuidPreference, string.Empty);
                    EditorProjectSettings.instance.SetCatalogAssetGuid(guid);
                    EditorPrefs.DeleteKey(LegacyCatalogAssetGuidPreference);
                }
                if (string.IsNullOrEmpty(guid))
                    return null;

                var path = AssetDatabase.GUIDToAssetPath(guid);
                _catalogAsset = string.IsNullOrEmpty(path)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<BindingCatalogAsset>(path);
                return _catalogAsset;
            }
        }

        public static BindingCatalog Catalog
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

        public static void SetConfiguredAsset(BindingCatalogAsset asset)
        {
            _catalogAsset = asset;
            _catalog = null;
            if (asset == null)
            {
                EditorProjectSettings.instance.SetCatalogAssetGuid(string.Empty);
                return;
            }

            var path = AssetDatabase.GetAssetPath(asset);
            var guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
                EditorProjectSettings.instance.SetCatalogAssetGuid(string.Empty);
            else
                EditorProjectSettings.instance.SetCatalogAssetGuid(guid);
        }

        [MenuItem("Assets/AbilityKit/HFSM/Use Selected Binding Catalog", true)]
        private static bool ValidateUseSelectedAsset()
        {
            return Selection.activeObject is BindingCatalogAsset;
        }

        [MenuItem("Assets/AbilityKit/HFSM/Use Selected Binding Catalog")]
        private static void UseSelectedAsset()
        {
            SetConfiguredAsset(Selection.activeObject as BindingCatalogAsset);
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

        private static BindingCatalog BuildReflectionCatalog()
        {
            var catalog = new BindingCatalog();
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
}
