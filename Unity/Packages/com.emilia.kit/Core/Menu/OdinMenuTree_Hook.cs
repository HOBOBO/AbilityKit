#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Emilia.Reflection.Editor;
using MonoHook;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace Emilia.Kit
{
    public class OdinMenuTree_Hook : OdinMenuTree
    {
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            Type type = typeof(OdinMenuTree);
            Type hookType = typeof(OdinMenuTree_Hook);

            HookDrawSearchToolbar(type, hookType);
        }

        private static void HookDrawSearchToolbar(Type odinMenuTreeType, Type odinMenuTreeHookType)
        {
            MethodInfo methodInfo = odinMenuTreeType.GetMethod(nameof(DrawSearchToolbar), BindingFlags.Instance | BindingFlags.Public);

            MethodInfo hookInfo = odinMenuTreeHookType.GetMethod(nameof(DrawSearchToolbar_Hook), BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo proxyInfo = odinMenuTreeHookType.GetMethod(nameof(DrawSearchToolbar_Proxy), BindingFlags.Instance | BindingFlags.NonPublic);

            MethodHook hook = new(methodInfo, hookInfo, proxyInfo);
            hook.Install();
        }

        private void DrawSearchToolbar_Hook(GUIStyle toolbarStyle = null)
        {
            Func<OdinMenuItem, int> searchFunction = this.GetSearchFunction();

            if (searchFunction == null)
            {
                DrawSearchToolbar_Proxy(toolbarStyle);
                return;
            }

            var config = this.Config;

            var searchFieldRect = GUILayoutUtility.GetRect(0, config.SearchToolbarHeight, GUILayoutOptions.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint)
            {
                (toolbarStyle ?? SirenixGUIStyles.ToolbarBackground).Draw(searchFieldRect, GUIContent.none, 0);
            }

            searchFieldRect = searchFieldRect.Padding(4);
            searchFieldRect.yMax += 1;

            EditorGUI.BeginChangeCheck();
            config.SearchTerm = this.DrawSearchField_Internal(searchFieldRect, config.SearchTerm, config.AutoFocusSearchBar);

            var changed = EditorGUI.EndChangeCheck();

            if ((changed || this.GetUpdateSearchResults_Internal()) && this.GetHasRepaintedCurrentSearchResult_Internal())
            {
                this.SetLayoutRequiresUpdate_Internal(true);

                this.SetUpdateSearchResults_Internal(false);

                // We want fast visual search feedback. If the user is typing faster than the window can repaint,
                // then no results will be visible while he's typing. this.hasRepaintedCurrentSearchResult fixes that.

                this.SetHasRepaintedCurrentSearchResult_Internal(false);
                ;

                bool doSearch = ! string.IsNullOrEmpty(config.SearchTerm);
                if (doSearch)
                {
                    if (! this.DrawInSearchMode)
                    {
                        config.ScrollPos = new Vector2();
                    }

                    this.SetDrawInSearchMode_Internal(true);

                    this.FlatMenuTree.Clear();
                    this.FlatMenuTree.AddRange(
                        this.EnumerateTree()
                            .Where(x => x.Value != null)
                            .Select(x => {
                                int score = searchFunction(x);
                                bool include = score > 0;
                                return new {score, item = x, include};
                            })
                            .Where(x => x.include)
                            .OrderByDescending(x => x.score)
                            .Select(x => x.item));

                    RootMenuItem.UpdateFlatMenuItemNavigation_Internal();
                }
                else
                {
                    this.SetDrawInSearchMode_Internal(false);

                    // Ensure all selected elements are visible, and scroll to the last one.
                    this.FlatMenuTree.Clear();
                    var last = this.Selection.LastOrDefault();
                    this.UpdateMenuTree();
                    this.Selection.SelectMany(x => x.GetParentMenuItemsRecursive(false)).ForEach(x => x.Toggled = true);
                    if (last != null)
                    {
                        this.ScrollToMenuItem(last);
                    }

                    RootMenuItem.UpdateFlatMenuItemNavigation_Internal();
                }
            }

            if (Event.current.type == EventType.Repaint)
            {
                this.SetHasRepaintedCurrentSearchResult_Internal(true);
            }
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private void DrawSearchToolbar_Proxy(GUIStyle toolbarStyle = null)
        {
            Debug.Log(nameof(DrawSearchToolbar_Proxy));
        }
    }

    public static class OdinMenuTreeExtensions
    {
        private static readonly ConditionalWeakTable<OdinMenuTree, Func<OdinMenuItem, int>> _additionalData = new();

        public static Func<OdinMenuItem, int> GetSearchFunction(this OdinMenuTree self)
        {
            if (_additionalData.TryGetValue(self, out var func)) return func;
            return null;
        }

        public static void SetSearchFunction(this OdinMenuTree obj, Func<OdinMenuItem, int> value)
        {
            _additionalData.AddOrUpdate(obj, value);
        }
    }
}
#endif