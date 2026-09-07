using System;
using System.Reflection;
using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM.Runtime;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.HFSM.Editor
{

    [FilePath("ProjectSettings/AbilityKitHfsmSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class EditorProjectSettings : ScriptableSingleton<EditorProjectSettings>
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
