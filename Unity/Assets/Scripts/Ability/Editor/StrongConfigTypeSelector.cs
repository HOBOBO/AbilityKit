using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    public static class StrongConfigTypeSelector
    {
        public static void ShowAddConditionSelector(List<ConditionEditorConfigBase> target, UnityEngine.Object owner)
        {
            if (target == null) return;

            var items = StrongTypeRegistry.GetConditionItems();
            ShowSelector(items, t =>
            {
                if (owner != null)
                {
                    Undo.RecordObject(owner, "Add Condition");
                }
                var inst = (ConditionEditorConfigBase)Activator.CreateInstance(t);
                target.Add(inst);
                if (owner != null)
                {
                    EditorUtility.SetDirty(owner);
                }
                GUIHelper.RequestRepaint();
            });
        }

        public static void ShowAddActionSelector(List<ActionEditorConfigBase> target, UnityEngine.Object owner)
        {
            if (target == null) return;

            var items = StrongTypeRegistry.GetActionItems();
            ShowSelector(items, t =>
            {
                if (owner != null)
                {
                    Undo.RecordObject(owner, "Add Action");
                }
                var inst = (ActionEditorConfigBase)Activator.CreateInstance(t);
                target.Add(inst);
                if (owner != null)
                {
                    EditorUtility.SetDirty(owner);
                }
                GUIHelper.RequestRepaint();
            });
        }

        private static void ShowSelector(IReadOnlyList<ValueDropdownItem<Type>> items, Action<Type> onSelected)
        {
            TypePickPopup.Show(items, onSelected);
        }

        private sealed class TypePickPopup : EditorWindow
        {
            private IReadOnlyList<ValueDropdownItem<Type>> _items;
            private Action<Type> _onSelected;
            private string _search;
            private Vector2 _scroll;

            public static void Show(IReadOnlyList<ValueDropdownItem<Type>> items, Action<Type> onSelected)
            {
                var wnd = CreateInstance<TypePickPopup>();
                wnd._items = items ?? Array.Empty<ValueDropdownItem<Type>>();
                wnd._onSelected = onSelected;

                var size = new Vector2(420, 520);

                Rect rect;
                try
                {
                    var mouse = GUIUtility.GUIToScreenPoint(Event.current != null ? Event.current.mousePosition : Vector2.zero);
                    rect = new Rect(mouse, Vector2.zero);
                }
                catch
                {
                    rect = new Rect(new Vector2(100, 100), Vector2.zero);
                }

                wnd.ShowAsDropDown(rect, size);
                wnd.Focus();
            }

            private void OnGUI()
            {
                if (_items == null) return;

                SirenixEditorGUI.BeginHorizontalToolbar();
                GUILayout.Label("Search", GUILayout.Width(45));
                _search = SirenixEditorGUI.ToolbarSearchField(_search);
                SirenixEditorGUI.EndHorizontalToolbar();

                _scroll = EditorGUILayout.BeginScrollView(_scroll);

                var query = string.IsNullOrEmpty(_search) ? null : _search.Trim();
                for (int i = 0; i < _items.Count; i++)
                {
                    var it = _items[i];
                    if (it.Value == null) continue;

                    if (!string.IsNullOrEmpty(query))
                    {
                        if (it.Text == null) continue;
                        if (it.Text.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    }

                    if (GUILayout.Button(it.Text, EditorStyles.miniButton))
                    {
                        _onSelected?.Invoke(it.Value);
                        Close();
                        GUIUtility.ExitGUI();
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private static class StrongTypeRegistry
        {
            private static List<ValueDropdownItem<Type>> _conditionItems;
            private static List<ValueDropdownItem<Type>> _actionItems;

            public static IReadOnlyList<ValueDropdownItem<Type>> GetConditionItems()
            {
                if (_conditionItems == null) Refresh();
                return _conditionItems;
            }

            public static IReadOnlyList<ValueDropdownItem<Type>> GetActionItems()
            {
                if (_actionItems == null) Refresh();
                return _actionItems;
            }

            public static void Refresh()
            {
                _conditionItems = TriggerTypeScanUtil.CollectStrongConfigTypes(typeof(ConditionEditorConfigBase));
                _actionItems = TriggerTypeScanUtil.CollectStrongConfigTypes(typeof(ActionEditorConfigBase));
            }
        }

        [InitializeOnLoadMethod]
        private static void AutoRefreshOnLoad()
        {
            StrongTypeRegistry.Refresh();
        }
    }
}
