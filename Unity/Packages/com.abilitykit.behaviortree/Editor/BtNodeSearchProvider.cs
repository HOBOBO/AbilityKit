#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace AbilityKit.BehaviorTree.Editor
{
    /// <summary>节点创建菜单：从描述符目录拉取分组与类型。</summary>
    internal sealed class BtNodeSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        private IBtAuthoringGraphHost? _host;

        public void Init(IBtAuthoringGraphHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var entries = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Create Node")),
            };

            foreach (var group in BtEditorNodeCatalog.Registry.Descriptors
                         .Select(d => d.Category)
                         .Distinct()
                         .OrderBy(c => c, StringComparer.Ordinal))
            {
                entries.Add(new SearchTreeGroupEntry(new GUIContent(group), 1));
                foreach (var descriptor in BtEditorNodeCatalog.Registry.Descriptors
                             .Where(d => d.Category == group)
                             .OrderBy(d => d.MenuOrder)
                             .ThenBy(d => d.DisplayName, StringComparer.Ordinal))
                {
                    entries.Add(new SearchTreeEntry(new GUIContent(descriptor.DisplayName))
                    {
                        level = 2,
                        userData = descriptor,
                    });
                }
            }
            return entries;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            if (entry.userData is not BtNodeDescriptor descriptor || _host == null) return false;
            if (_host.IsReadOnly) return false;

            var graphPosition = _host.ScreenToGraphPosition(context.screenMousePosition);
            _host.AddNode(descriptor, graphPosition);
            return true;
        }
    }
}
