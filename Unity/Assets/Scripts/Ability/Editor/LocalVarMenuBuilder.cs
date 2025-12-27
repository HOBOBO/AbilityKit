using System;
using System.Collections.Generic;
using AbilityKit.Configs;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    internal static class LocalVarMenuBuilder
    {
        public static void ShowAddMenu(AbilityModuleSO owner, List<LocalVarEntry> localVars, Action<List<LocalVarEntry>> onListChanged)
        {
            var menu = new GenericMenu();

            Add(menu, owner, localVars, onListChanged, ArgValueKind.Int, "整数");
            Add(menu, owner, localVars, onListChanged, ArgValueKind.Float, "浮点");
            Add(menu, owner, localVars, onListChanged, ArgValueKind.Bool, "布尔");
            Add(menu, owner, localVars, onListChanged, ArgValueKind.String, "字符串");
            Add(menu, owner, localVars, onListChanged, ArgValueKind.Object, "对象");

            menu.ShowAsContext();
        }

        private static void Add(GenericMenu menu, AbilityModuleSO owner, List<LocalVarEntry> localVars, Action<List<LocalVarEntry>> onListChanged, ArgValueKind kind, string label)
        {
            menu.AddItem(new GUIContent(label), false, () => AddInternal(owner, localVars, onListChanged, kind));
        }

        private static void AddInternal(AbilityModuleSO owner, List<LocalVarEntry> localVars, Action<List<LocalVarEntry>> onListChanged, ArgValueKind kind)
        {
            if (owner != null)
            {
                Undo.RecordObject(owner, "Add Local Var");
            }

            if (localVars == null)
            {
                localVars = new List<LocalVarEntry>();
                onListChanged?.Invoke(localVars);
            }

            localVars.Add(new LocalVarEntry { Kind = kind });

            if (owner != null)
            {
                EditorUtility.SetDirty(owner);
            }
        }
    }
}
