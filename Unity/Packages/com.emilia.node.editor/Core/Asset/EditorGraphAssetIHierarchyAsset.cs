using System;
using System.Collections.Generic;
using Emilia.Kit;
using Sirenix.Serialization;
using UnityEngine;

namespace Emilia.Node.Editor
{
    public partial class EditorGraphAsset : IHierarchyAsset
    {
        [NonSerialized, OdinSerialize, HideInInspector]
        private EditorGraphAsset _parent;

        [NonSerialized, OdinSerialize, HideInInspector]
        private List<EditorGraphAsset> _children = new();

        /// <summary>
        /// 父级
        /// </summary>
        public IHierarchyAsset parent
        {
            get => _parent;
            set => _parent = value as EditorGraphAsset;
        }

        /// <summary>
        /// 子级
        /// </summary>
        public IReadOnlyList<IHierarchyAsset> children => _children;

        /// <summary>
        /// 添加子级
        /// </summary>
        public void AddChild(IHierarchyAsset child)
        {
            EditorGraphAsset editorGraphAsset = child as EditorGraphAsset;
            if (this._children.Contains(editorGraphAsset)) return;

            this._children.Add(editorGraphAsset);
            child.parent = this;

            EditorAssetKit.SaveAssetIntoObject(editorGraphAsset, this);
        }

        /// <summary>
        /// 移除子级
        /// </summary>
        public void RemoveChild(IHierarchyAsset child)
        {
            EditorGraphAsset editorGraphAsset = child as EditorGraphAsset;
            if (this._children.Contains(editorGraphAsset) == false) return;

            this._children.Remove(editorGraphAsset);
            child.parent = null;
        }
    }
}