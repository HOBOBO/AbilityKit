using System;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.ExcelSync.Editor.DataBrowser
{
    [Serializable]
    public sealed class DataBrowserBindingContext
    {
        public ScriptableObject TargetAsset { get; private set; }
        public SerializedObject SerializedTarget { get; private set; }
        public SerializedProperty ListProperty { get; private set; }
        public Type ElementType { get; private set; }
        public string ListPropertyName => ListProperty != null ? ListProperty.name : string.Empty;

        public bool IsBound => TargetAsset != null && SerializedTarget != null && ListProperty != null && ElementType != null;

        public void Clear()
        {
            TargetAsset = null;
            SerializedTarget = null;
            ListProperty = null;
            ElementType = null;
        }

        public bool TryBind(IDefaultListAdapter adapter, ScriptableObject asset)
        {
            Clear();

            if (adapter == null || asset == null)
            {
                return false;
            }

            if (!adapter.TryBind(asset, out var serializedObject, out var listProperty, out var elementType))
            {
                return false;
            }

            TargetAsset = asset;
            SerializedTarget = serializedObject;
            ListProperty = listProperty;
            ElementType = elementType;
            return true;
        }

        public int GetItemCount()
        {
            return ListProperty != null ? ListProperty.arraySize : 0;
        }
    }
}
