using System;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.ExcelSync.Editor.DataBrowser
{
    public interface IDefaultListAdapter
    {
        bool TryBind(ScriptableObject asset, out SerializedObject serializedObject, out SerializedProperty listProperty, out Type elementType);
    }
}
