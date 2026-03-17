using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.ExcelSync.Editor.DataBrowser
{
    public sealed class DefaultListAdapter : IDefaultListAdapter
    {
        private static readonly string[] CandidateListNames = { "DataList", "Items", "Rows" };

        public bool TryBind(ScriptableObject asset, out SerializedObject serializedObject, out SerializedProperty listProperty, out Type elementType)
        {
            serializedObject = null;
            listProperty = null;
            elementType = null;

            if (asset == null)
            {
                return false;
            }

            var t = asset.GetType();
            FieldInfo field = null;
            string listName = null;
            for (int i = 0; i < CandidateListNames.Length; i++)
            {
                var n = CandidateListNames[i];
                field = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    listName = n;
                    break;
                }
            }

            if (field == null || string.IsNullOrEmpty(listName))
            {
                return false;
            }

            try
            {
                serializedObject = new SerializedObject(asset);
                listProperty = serializedObject.FindProperty(listName);
            }
            catch
            {
                serializedObject = null;
                listProperty = null;
            }

            elementType = ResolveElementType(field, asset);
            return elementType != null && listProperty != null;
        }

        private static Type ResolveElementType(FieldInfo field, ScriptableObject asset)
        {
            if (field == null)
            {
                return null;
            }

            var fieldType = field.FieldType;
            if (fieldType.IsArray)
            {
                return fieldType.GetElementType();
            }

            if (fieldType.IsGenericType)
            {
                var args = fieldType.GetGenericArguments();
                if (args != null && args.Length == 1)
                {
                    return args[0];
                }
            }

            if (asset == null)
            {
                return null;
            }

            object value;
            try
            {
                value = field.GetValue(asset);
            }
            catch
            {
                return null;
            }

            if (value is IList list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] != null)
                    {
                        return list[i].GetType();
                    }
                }
            }
            else if (value is IEnumerable enumerable)
            {
                foreach (var e in enumerable)
                {
                    if (e != null)
                    {
                        return e.GetType();
                    }
                }
            }

            return null;
        }
    }
}
