using System;
using System.Reflection;
using AbilityKit.HFSM;
using UnityEditor;

namespace UnityHFSM.Editor
{
    /// <summary>Editor-owned pull catalog. Runtime simulation never depends on this scan.</summary>
    public static class HfsmEditorBindingCatalog
    {
        private const string CatalogAssetGuidPreference = "AbilityKit.HFSM.BindingCatalogGuid";
        private static HfsmBindingCatalog _catalog;
        private static HfsmBindingCatalogAsset _catalogAsset;

        public static HfsmBindingCatalogAsset ConfiguredAsset
        {
            get
            {
                if (_catalogAsset != null)
                    return _catalogAsset;

                var guid = EditorPrefs.GetString(CatalogAssetGuidPreference, string.Empty);
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
                EditorPrefs.DeleteKey(CatalogAssetGuidPreference);
                return;
            }

            var path = AssetDatabase.GetAssetPath(asset);
            var guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
                EditorPrefs.DeleteKey(CatalogAssetGuidPreference);
            else
                EditorPrefs.SetString(CatalogAssetGuidPreference, guid);
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
}
