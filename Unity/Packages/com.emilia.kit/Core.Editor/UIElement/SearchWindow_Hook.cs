using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Emilia.Reflection.Editor;
using MonoHook;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Emilia.Kit.Editor
{
    public class SearchWindow_Hook : SearchWindow_Internals
    {
        [InitializeOnLoadMethod]
        static void InstallationHook()
        {
            Type searchWindowViewType = typeof(SearchWindow);
            Type graphViewHookType = typeof(SearchWindow_Hook);

            HookRebuildSearch(searchWindowViewType, graphViewHookType);
        }

        private static void HookRebuildSearch(Type searchWindowViewType, Type graphViewHookType)
        {
            MethodInfo methodInfo = searchWindowViewType.GetMethod("RebuildSearch", BindingFlags.Instance | BindingFlags.NonPublic);

            MethodInfo hookInfo = graphViewHookType.GetMethod(nameof(RebuildSearch_Hook), BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo proxyInfo = graphViewHookType.GetMethod(nameof(RebuildSearch_Proxy), BindingFlags.Instance | BindingFlags.NonPublic);

            MethodHook hook = new(methodInfo, hookInfo, proxyInfo);
            hook.Install();
        }

        public static bool Open<P, W>(SearchWindowContext context, P provider) where P : ScriptableObject, ISearchWindowProvider where W : SearchWindow
        {
            Object[] objectsOfTypeAll = Resources.FindObjectsOfTypeAll(typeof(SearchWindow));
            if (objectsOfTypeAll.Length != 0)
            {
                try
                {
                    ((EditorWindow) objectsOfTypeAll[0]).Close();
                    return false;
                }
                catch
                {
                    filterWindow_Internal = null;
                }
            }
            if (DateTime.Now.Ticks / 10000L < lastClosedTime_Internal + 50L) return false;
            if (filterWindow_Internal == null)
            {
                filterWindow_Internal = CreateInstance<W>();
                filterWindow_Internal.hideFlags = HideFlags.HideAndDontSave;
            }

            filterWindow_Internal.Init_Internals(context, provider);
            return true;
        }

        protected virtual bool CanSearchPro() => true;

        private void RebuildSearch_Hook()
        {
            if (ReflectUtility.Invoke(this, nameof(CanSearchPro), new object[] { }) is bool result && result) OverrideRebuildSearch();
            else RebuildSearch_Proxy();
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private void RebuildSearch_Proxy()
        {
            Debug.Log(nameof(RebuildSearch_Proxy));
        }

        protected virtual void OverrideRebuildSearch()
        {
            if (hasSearch_Internals == false)
            {
                searchResultTree_Internals = null;
                if (selectionStack_Internals[selectionStack_Internals.Count - 1].name == "Search")
                {
                    selectionStack_Internals.Clear();
                    selectionStack_Internals.Add(tree_Internals[0] as SearchTreeGroupEntry);
                }
                animTarget_Internals = 1;
                lastTime_Internals = DateTime.Now.Ticks;
            }
            else
            {
                List<(SearchTreeEntry, int)> collection = new();

                foreach (SearchTreeEntry searchTreeEntry in tree_Internals)
                {
                    if (searchTreeEntry is SearchTreeGroupEntry) continue;
                    string entryName = searchTreeEntry.name;
                    int source = SearchUtility.SmartSearch(entryName, search_Internals);
                    if (source != 0) collection.Add((searchTreeEntry, source));
                }

                collection.Sort((a, b) => b.Item2.CompareTo(a.Item2));

                List<SearchTreeEntry> searchTreeEntryList = new();
                searchTreeEntryList.Add(new SearchTreeGroupEntry(new GUIContent("Search")));
                searchTreeEntryList.AddRange(collection.Select((i) => i.Item1));
                searchResultTree_Internals = searchTreeEntryList.ToArray();
                selectionStack_Internals.Clear();
                selectionStack_Internals.Add(searchResultTree_Internals[0] as SearchTreeGroupEntry);
                if (GetChildren_Internals(activeTree_Internals, activeParent_Internals).Count >= 1) activeParent_Internals.SetSelectedIndex(0);
                else activeParent_Internals.SetSelectedIndex(-1);
            }
        }
    }
}