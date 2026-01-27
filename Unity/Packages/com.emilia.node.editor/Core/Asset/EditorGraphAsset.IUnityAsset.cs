using System.Collections.Generic;
using Emilia.Kit.Editor;
using UnityEngine;

namespace Emilia.Node.Editor
{
    public partial class EditorGraphAsset : IUnityAsset
    {
        /// <summary>
        /// 设置Unity的子资产
        /// </summary>
        public virtual void SetChildren(List<Object> childAssets)
        {
            _nodes.Clear();
            _edges.Clear();
            _items.Clear();

            this._nodeMap.Clear();
            this._edgeMap.Clear();
            this._itemMap.Clear();

            for (var i = 0; i < childAssets.Count; i++)
            {
                Object childAsset = childAssets[i];

                switch (childAsset)
                {
                    case EditorNodeAsset node:
                        AddNode(node);
                        break;
                    case EditorEdgeAsset edge:
                        AddEdge(edge);
                        break;
                    case EditorItemAsset item:
                        AddItem(item);
                        break;
                }
            }
        }

        /// <summary>
        /// 获取Unity的子资产
        /// </summary>
        public virtual List<Object> GetChildren()
        {
            List<Object> assets = new();

            for (var i = 0; i < this._nodes.Count; i++)
            {
                EditorNodeAsset node = this._nodes[i];
                assets.Add(node);
            }

            for (var i = 0; i < this._edges.Count; i++)
            {
                EditorEdgeAsset edge = this._edges[i];
                assets.Add(edge);
            }

            for (var i = 0; i < this._items.Count; i++)
            {
                EditorItemAsset item = this._items[i];
                assets.Add(item);
            }

            return assets;
        }
    }
}